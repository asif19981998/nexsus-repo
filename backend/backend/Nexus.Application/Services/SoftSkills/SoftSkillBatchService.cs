using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.Internships;
using Nexus.Application.Dtos.SoftSkills;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.SoftSkills;

/// <summary>Batches (cl.6 খ, cl.8) and the university group of each taking part university (cl.6 ক, cl.12 গ).</summary>
public interface ISoftSkillBatchService
{
    Task<List<SoftSkillBatchDto>> ListAsync();
    Task<SoftSkillBatchDto> GetAsync(long id);
    Task<SoftSkillBatchDto> CreateAsync(CreateSoftSkillBatchRequest request);
    Task<SoftSkillBatchDto> UpdateAsync(long id, UpdateSoftSkillBatchRequest request);
    Task<SoftSkillBatchDto> SetStatusAsync(long id, SetBatchStatusRequest request);
    Task<SoftSkillBatchDto> AddUniversityGroupAsync(long batchId, UniversityGroupRequest request);
    Task<SoftSkillBatchDto> UpdateUniversityGroupAsync(long universityGroupId, UniversityGroupRequest request);
    Task<SoftSkillBatchDto> RemoveUniversityGroupAsync(long universityGroupId);
    Task<SoftSkillBatchDto> SetCoordinatorAsync(long universityGroupId, SetCoordinatorRequest request);
}

public sealed class SoftSkillBatchService(
    IUnitOfWork uow, ISoftSkillSetupService setup, IPermissionService permissions, ICurrentUserService me, IClock clock,
    IAuditService audit, INotificationService notify, SoftSkillWorkflow workflow) : ISoftSkillBatchService
{
    public async Task<List<SoftSkillBatchDto>> ListAsync()
    {
        var batches = await uow.SoftSkillBatches.ListAsync();
        var taken = await uow.SoftSkillBatches.TakenSeatsByUniversityGroupAsync();
        return batches.Select(b => SoftSkillMapper.Batch(b, taken)).ToList();
    }

    public async Task<SoftSkillBatchDto> CreateAsync(CreateSoftSkillBatchRequest r)
    {
        var policy = (await setup.GetSnapshotAsync()).Policy;
        var years = new Dictionary<int, int>();
        if (r.StartDate is { } sd) years[sd.Year] = await uow.SoftSkillBatches.CountInYearAsync(sd.Year, includeCancelled: false);

        var check = SoftSkillRules.ValidateNewBatch(new SoftSkillRules.BatchDraft(r.Name, r.ApplyFrom, r.ApplyTo, r.StartDate), y => years.GetValueOrDefault(y), policy);
        if (check.Errors.Count > 0) throw new BadRequestException(check.Errors[0]);
        await RequireOverrideAsync(check.NeedsOverride, r.OverrideReason);

        var universities = (await uow.Universities.ListAsync(activeOnly: true)).ToDictionary(u => u.Id);
        var ids = r.UniversityIds.Distinct().ToList();
        foreach (var id in ids)
        {
            if (!universities.TryGetValue(id, out var u)) throw new BadRequestException("Choose universities from the list.");
            if (!u.IsPublic) throw new BadRequestException($"{u.Name} is not a public university (cl.3).");
        }

        var year = check.Year!.Value;
        var seq = await uow.SoftSkillBatches.CountInYearAsync(year, includeCancelled: true) + 1;
        var batch = new SoftSkillBatch
        {
            Code = $"UGC-SSK-{year}-B{seq}",
            Name = r.Name!.Trim(),
            Year = year,
            Status = BatchStatus.Draft,
            ApplyFrom = r.ApplyFrom!.Value,
            ApplyTo = r.ApplyTo!.Value,
            StartDate = r.StartDate!.Value,
            EndDate = check.EndDate!.Value,
            Note = Clean(r.Note),
        };
        if (check.NeedsOverride.Count > 0)
        {
            batch.OverrideReason = r.OverrideReason!.Trim();
            batch.OverrideById = me.UserId;
            batch.OverrideAt = clock.UtcNow;
        }
        foreach (var id in ids) batch.UniversityGroups.Add(new SoftSkillUniversityGroup { UniversityId = id, Capacity = policy.SeatsPerUniversity });

        await uow.SoftSkillBatches.AddAsync(batch);
        await uow.SaveChangesAsync();
        await audit.LogAsync(batch.OverrideReason is null ? "softskill.batch.create" : "softskill.batch.create_override", "SoftSkillBatch", batch.Id,
            $"{batch.Code}, {ids.Count} universities{(batch.OverrideReason is null ? "" : $" — override: {batch.OverrideReason}")}");
        await uow.SaveChangesAsync();
        return await DtoAsync(batch.Id);
    }

    public async Task<SoftSkillBatchDto> GetAsync(long id)
    {
        _ = await uow.SoftSkillBatches.GetByIdAsync(id) ?? throw new NotFoundException("Batch", id);
        return await DtoAsync(id);
    }

    public async Task<SoftSkillBatchDto> UpdateAsync(long id, UpdateSoftSkillBatchRequest r)
    {
        var batch = await uow.SoftSkillBatches.GetByIdAsync(id) ?? throw new NotFoundException("Batch", id);
        if (batch.Status is BatchStatus.Completed or BatchStatus.Cancelled)
            throw new BadRequestException($"A batch in \"{batch.Status.Label()}\" can no longer be edited.");

        // Once applications close the window is history; once training runs, its dates are too.
        var windowLocked = batch.Status is BatchStatus.Selection or BatchStatus.Running;
        if (windowLocked && (r.ApplyFrom != batch.ApplyFrom || r.ApplyTo != batch.ApplyTo))
            throw new BadRequestException("The application window cannot change after applications have closed.");
        if (batch.Status == BatchStatus.Running && r.StartDate != batch.StartDate)
            throw new BadRequestException("The start date cannot change while the training is running.");

        var policy = (await setup.GetSnapshotAsync()).Policy;

        // The batch already holds a slot in its own year: the batches-per-year limit is checked
        // again only when the new start date moves it to another year.
        var years = new Dictionary<int, int>();
        if (r.StartDate is { } sd && sd.Year != batch.Year)
            years[sd.Year] = await uow.SoftSkillBatches.CountInYearAsync(sd.Year, includeCancelled: false);
        var check = SoftSkillRules.ValidateNewBatch(new SoftSkillRules.BatchDraft(r.Name, r.ApplyFrom, r.ApplyTo, r.StartDate), y => years.GetValueOrDefault(y), policy);
        if (check.Errors.Count > 0) throw new BadRequestException(check.Errors[0]);

        var start = r.StartDate!.Value;
        var end = check.EndDate!.Value;
        if (start != batch.StartDate || end != batch.EndDate)
        {
            // Sessions and visits must stay inside the training (cl.8).
            var outside = await uow.SoftSkillBatches.CountDatedWorkOutsideAsync(batch.Id, start, end);
            if (outside > 0)
                throw new BadRequestException(
                    $"{outside} planned session{(outside > 1 ? "s or visits fall" : " or visit falls")} outside {start:dd MMM yyyy} – {end:dd MMM yyyy}. Move or cancel them first.");
        }

        await RequireOverrideAsync(check.NeedsOverride, r.OverrideReason);
        string? newOverride = null;
        if (check.NeedsOverride.Count > 0)
        {
            newOverride = r.OverrideReason!.Trim();
            batch.OverrideReason = newOverride;
            batch.OverrideById = me.UserId;
            batch.OverrideAt = clock.UtcNow;
        }
        // An existing override is kept even when the batch is back inside the limit: it records the Commission's decision.

        var changes = new List<string>();
        void Track<T>(string label, T before, T after)
        {
            if (!EqualityComparer<T>.Default.Equals(before, after)) changes.Add($"{label}: {before} → {after}");
        }
        var name = r.Name!.Trim();
        var note = Clean(r.Note);
        Track("name", batch.Name, name);
        Track("apply from", batch.ApplyFrom, r.ApplyFrom!.Value);
        Track("apply to", batch.ApplyTo, r.ApplyTo!.Value);
        Track("start", batch.StartDate, start);
        Track("note", batch.Note, note);

        // The code (UGC-SSK-yyyy-Bn) is a permanent reference and stays as issued, even if the year changes.
        batch.Name = name;
        batch.Year = check.Year!.Value;
        batch.ApplyFrom = r.ApplyFrom.Value;
        batch.ApplyTo = r.ApplyTo.Value;
        batch.StartDate = start;
        batch.EndDate = end;
        batch.Note = note;

        if (changes.Count > 0 || newOverride is not null)
        {
            var detail = changes.Count > 0 ? string.Join("; ", changes) : "no field changes";
            await audit.LogAsync(newOverride is null ? "softskill.batch.update" : "softskill.batch.update_override", "SoftSkillBatch", batch.Id,
                newOverride is null ? $"{batch.Code} — {detail}" : $"{batch.Code} — {detail} — override: {newOverride}");
            await uow.SaveChangesAsync();
        }
        return await DtoAsync(batch.Id);
    }

    public async Task<SoftSkillBatchDto> SetStatusAsync(long id, SetBatchStatusRequest r)
    {
        if (!StatusLabels.TryParseBatch(r.Status, out var status)) throw new BadRequestException("Unknown batch status.");
        var batch = await uow.SoftSkillBatches.GetWithUniversityGroupsAsync(id) ?? throw new NotFoundException("Batch", id);

        var allowed = batch.Status switch
        {
            BatchStatus.Draft => new[] { BatchStatus.ApplicationsOpen, BatchStatus.Cancelled },
            BatchStatus.ApplicationsOpen => [BatchStatus.Selection, BatchStatus.Cancelled],
            BatchStatus.Selection => [BatchStatus.Running, BatchStatus.Cancelled],
            BatchStatus.Running => [BatchStatus.Completed],
            _ => [],
        };
        if (!allowed.Contains(status))
            throw new BadRequestException($"A batch in \"{batch.Status.Label()}\" cannot move to \"{status.Label()}\".");
        if (status == BatchStatus.ApplicationsOpen && batch.UniversityGroups.Count == 0)
            throw new BadRequestException("Add at least one university before opening applications.");

        var from = batch.Status;
        batch.Status = status;

        // What the batch's move means for its applications.
        switch (status)
        {
            case BatchStatus.Running:
                foreach (var app in await uow.SoftSkillApplications.ListTrackedForBatchAsync(id, [ApplicationStatus.Selected]))
                {
                    workflow.SetStatus(app, ApplicationStatus.Active, "Training started");
                    await notify.NotifyAsync(app.StudentId, "Soft Skill training has started", $"{batch.Name} starts on {batch.StartDate:dd MMM yyyy}.", Links.SoftSkillStudent);
                }
                foreach (var app in await uow.SoftSkillApplications.ListTrackedForBatchAsync(id, SoftSkillRules.PendingStatuses))
                    workflow.SetStatus(app, ApplicationStatus.Rejected, "Not selected for this batch");
                break;
            case BatchStatus.Completed:
                foreach (var app in await uow.SoftSkillApplications.ListTrackedForBatchAsync(id, [ApplicationStatus.Active]))
                    workflow.SetStatus(app, ApplicationStatus.Completed, "Training period over");
                break;
            case BatchStatus.Cancelled:
                foreach (var app in await uow.SoftSkillApplications.ListTrackedForBatchAsync(id, [.. SoftSkillRules.PendingStatuses, ApplicationStatus.Selected]))
                {
                    app.RejectionReason = "The batch was cancelled.";
                    workflow.SetStatus(app, ApplicationStatus.Rejected, "Batch cancelled");
                    await notify.NotifyAsync(app.StudentId, "Soft Skill batch cancelled", $"{batch.Name} was cancelled. You may apply to a later batch.", Links.SoftSkillStudent);
                }
                break;
        }

        await audit.LogAsync("softskill.batch.status", "SoftSkillBatch", batch.Id, $"{batch.Code}: {from.Label()} → {status.Label()}");
        await uow.SaveChangesAsync();
        return await DtoAsync(batch.Id);
    }

    public async Task<SoftSkillBatchDto> AddUniversityGroupAsync(long batchId, UniversityGroupRequest r)
    {
        var batch = await uow.SoftSkillBatches.GetWithUniversityGroupsAsync(batchId) ?? throw new NotFoundException("Batch", batchId);
        if (batch.Status is not (BatchStatus.Draft or BatchStatus.ApplicationsOpen))
            throw new BadRequestException("Universities can be added only before selection starts.");
        var university = await uow.Universities.GetByIdAsync(r.UniversityId ?? 0);
        if (university is null || !university.IsActive) throw new BadRequestException("Choose a university from the list.");
        if (!university.IsPublic) throw new BadRequestException($"{university.Name} is not a public university (cl.3).");
        if (batch.UniversityGroups.Any(c => c.UniversityId == university.Id)) throw new ConflictException($"{university.Name} is already in this batch.");

        var policy = (await setup.GetSnapshotAsync()).Policy;
        var universityGroup = new SoftSkillUniversityGroup { UniversityId = university.Id, Venue = Clean(r.Venue) };
        await ApplyCapacityAsync(universityGroup, r.Capacity, r.OverrideReason, policy, taken: 0);
        batch.UniversityGroups.Add(universityGroup);
        await audit.LogAsync("softskill.university_group.add", "SoftSkillBatch", batch.Id, $"{batch.Code}: {university.Name}, {universityGroup.Capacity} seats");
        await uow.SaveChangesAsync();
        return await DtoAsync(batch.Id);
    }

    public async Task<SoftSkillBatchDto> UpdateUniversityGroupAsync(long universityGroupId, UniversityGroupRequest r)
    {
        var universityGroup = await UniversityGroupAsync(universityGroupId);
        var policy = (await setup.GetSnapshotAsync()).Policy;
        if (r.Capacity is not null)
            await ApplyCapacityAsync(universityGroup, r.Capacity, r.OverrideReason, policy, await uow.SoftSkillBatches.TakenSeatsAsync(universityGroup.Id));
        if (r.Venue is not null) universityGroup.Venue = Clean(r.Venue);
        await audit.LogAsync("softskill.university_group.update", "SoftSkillUniversityGroup", universityGroup.Id, $"{universityGroup.Capacity} seats, venue {universityGroup.Venue ?? "—"}");
        await uow.SaveChangesAsync();
        return await DtoAsync(universityGroup.BatchId);
    }

    public async Task<SoftSkillBatchDto> RemoveUniversityGroupAsync(long universityGroupId)
    {
        var universityGroup = await UniversityGroupAsync(universityGroupId);
        var batch = await uow.SoftSkillBatches.GetWithUniversityGroupsAsync(universityGroup.BatchId) ?? throw new NotFoundException("Batch", universityGroup.BatchId);
        if (batch.Status is not (BatchStatus.Draft or BatchStatus.ApplicationsOpen))
            throw new BadRequestException("Universities can be removed only before selection starts.");
        if (await uow.SoftSkillApplications.AnyAsync(a => a.UniversityGroupId == universityGroup.Id))
            throw new ConflictException("Students have applied at this university, so it cannot be removed. Cancel the batch instead.");
        uow.SoftSkillUniversityGroups.Remove(universityGroup);
        await audit.LogAsync("softskill.university_group.remove", "SoftSkillBatch", batch.Id, $"{batch.Code}: university {universityGroup.UniversityId}");
        await uow.SaveChangesAsync();
        return await DtoAsync(batch.Id);
    }

    public async Task<SoftSkillBatchDto> SetCoordinatorAsync(long universityGroupId, SetCoordinatorRequest r)
    {
        var universityGroup = await UniversityGroupAsync(universityGroupId);
        if (r.CoordinatorId is not { } userId)
        {
            universityGroup.CoordinatorId = null;
        }
        else
        {
            // cl.12 (গ): the university nominates one of its own teachers; here an approved trainer of that university.
            var trainer = await uow.SoftSkillTrainers.GetForUserAsync(userId);
            if (trainer is null || trainer.Status != TrainerStatus.Approved)
                throw new BadRequestException("The coordinator must be an approved trainer.");
            if (trainer.UniversityId != universityGroup.UniversityId)
                throw new BadRequestException("The coordinator must teach at this university group's university.");
            universityGroup.CoordinatorId = userId;
            await notify.NotifyAsync(userId, "You coordinate a Soft Skill university group", "Plan the sessions and follow attendance for your university's university group.", Links.SoftSkillUniversityGroup(universityGroup.Id));
        }
        await audit.LogAsync("softskill.university_group.coordinator", "SoftSkillUniversityGroup", universityGroup.Id, $"Coordinator {(r.CoordinatorId?.ToString() ?? "removed")}");
        await uow.SaveChangesAsync();
        return await DtoAsync(universityGroup.BatchId);
    }

    // ---------- helpers ----------

    private async Task<SoftSkillUniversityGroup> UniversityGroupAsync(long id) => await uow.SoftSkillUniversityGroups.GetByIdAsync(id) ?? throw new NotFoundException("University group", id);

    private async Task ApplyCapacityAsync(SoftSkillUniversityGroup universityGroup, int? capacity, string? overrideReason, SoftSkillPolicy policy, int taken)
    {
        var seats = capacity ?? policy.SeatsPerUniversity;
        if (seats < 1) throw new BadRequestException("Seats must be at least 1.");
        if (seats < taken) throw new BadRequestException($"{taken} students are already selected here; seats cannot go below that.");
        if (seats > policy.SeatsPerUniversity)
        {
            await RequireOverrideAsync([$"{seats} seats is above the {policy.SeatsPerUniversity}-seat limit per university (cl.6 ক)."], overrideReason);
            universityGroup.OverrideReason = overrideReason!.Trim();
        }
        else
        {
            universityGroup.OverrideReason = null;
        }
        universityGroup.Capacity = seats;
    }

    /// <summary>Over a cl.6 limit: 400 "needs_override" until a reason is sent, then 403 without the override permission.</summary>
    private async Task RequireOverrideAsync(IReadOnlyList<string> needs, string? reason)
    {
        if (needs.Count == 0) return;
        if (string.IsNullOrWhiteSpace(reason)) throw new BadRequestException(needs[0], "needs_override", needs);
        if (!await permissions.HasAsync(Permissions.SoftSkill.BatchOverride))
            throw new ForbiddenException("You do not have permission to override the cl.6 limits.");
    }

    private async Task<SoftSkillBatchDto> DtoAsync(long batchId)
    {
        var batch = (await uow.SoftSkillBatches.ListAsync()).First(b => b.Id == batchId);
        return SoftSkillMapper.Batch(batch, await uow.SoftSkillBatches.TakenSeatsByUniversityGroupAsync());
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
