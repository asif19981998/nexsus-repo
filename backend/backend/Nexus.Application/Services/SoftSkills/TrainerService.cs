using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.Documents;
using Nexus.Application.Dtos.SoftSkills;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.Application.Services.Documents;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.SoftSkills;

/// <summary>
/// cl.12: university teachers register as trainers; the Commission approves them and records the Training of Trainers.
/// Only an approved trainer with the ToT can be given sessions or nominated as coordinator.
/// </summary>
public interface ITrainerService
{
    /// <summary>The signed-in teacher's profile, eligibility, registration, university groups and coming sessions.</summary>
    Task<MyTrainerDto> GetMineAsync();
    Task<MyTrainerDto> ApplyAsync(TrainerApplyRequest request);

    Task<List<TrainerDto>> ListAsync(string? status, long? universityId);
    Task<TrainerDto> GetAsync(long id);
    Task<TrainerDto> ApproveAsync(long id, TrainerDecisionRequest request);
    Task<TrainerDto> RejectAsync(long id, TrainerDecisionRequest request);
    Task<TrainerDto> SuspendAsync(long id, TrainerDecisionRequest request);
    Task<TrainerDto> RecordTotAsync(long id, TotRequest request);
}

public sealed class TrainerService(
    IUnitOfWork uow, ICurrentUserService me, IClock clock, IAuditService audit, INotificationService notify,
    IRequirementService requirements) : ITrainerService
{
    private const int MinStatementLength = 30;

    public async Task<MyTrainerDto> GetMineAsync()
    {
        var userId = me.RequireUserId();
        var found = await uow.Users.GetAsync(userId) ?? throw new NotFoundException("User", userId);
        var profile = await uow.Users.GetTrainerProfileAsync(userId);
        var registration = await uow.SoftSkillTrainers.GetForUserAsync(userId);
        var eligibility = await EligibilityAsync(userId, profile, found.User.Designation, registration);

        TrainerDto? reg = null;
        if (registration is not null)
            reg = SoftSkillMapper.Trainer(registration, found.User, await SoftSkillMapper.AttachedAsync(uow, Modules.SoftSkillTrainer, registration.Id));

        var universityGroups = await uow.SoftSkillUniversityGroups.ListAsync(null, userId);
        var taken = await uow.SoftSkillBatches.TakenSeatsByUniversityGroupAsync();
        var today = clock.Today;
        var sessions = new List<SessionDto>();
        foreach (var c in universityGroups.Where(c => c.Batch.Status is BatchStatus.Selection or BatchStatus.Running))
        {
            var detail = await uow.SoftSkillUniversityGroups.GetDetailAsync(c.Id);
            if (detail is null) continue;
            sessions.AddRange(detail.Sessions
                .Where(s => s.TrainerId == userId && s.Date >= today && s.Status == SessionStatus.Scheduled)
                .Select(SoftSkillMapper.Session));
        }

        return new MyTrainerDto(
            ProfileDto(profile, found.User.Designation),
            eligibility,
            reg,
            universityGroups.Select(c => SoftSkillMapper.UniversityGroup(c, taken.GetValueOrDefault(c.Id))).ToList(),
            sessions.OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToList());
    }

    public async Task<MyTrainerDto> ApplyAsync(TrainerApplyRequest r)
    {
        var userId = me.RequireUserId();
        var found = await uow.Users.GetAsync(userId) ?? throw new NotFoundException("User", userId);
        var profile = await uow.Users.GetTrainerProfileAsync(userId);
        var registration = await uow.SoftSkillTrainers.GetForUserAsync(userId);
        var eligibility = await EligibilityAsync(userId, profile, found.User.Designation, registration);
        if (!eligibility.Eligible) throw new BadRequestException($"Not yet: {eligibility.Rules.First(x => !x.Met).Label}.");

        var statement = r.Statement?.Trim() ?? "";
        if (statement.Length < MinStatementLength) throw new BadRequestException($"Write at least {MinStatementLength} characters on your teaching experience.");
        if (statement.Length > SoftSkillTrainer.MaxStatementLength) throw new BadRequestException($"At most {SoftSkillTrainer.MaxStatementLength} characters.");

        if (registration is null)
        {
            registration = new SoftSkillTrainer { UserId = userId, UniversityId = profile!.UniversityId!.Value, Statement = statement, AppliedAt = clock.UtcNow };
            await uow.SoftSkillTrainers.AddAsync(registration);
        }
        else
        {
            // Applying again after a rejection; a ToT already recorded stays.
            registration.UniversityId = profile!.UniversityId!.Value;
            registration.Statement = statement;
            registration.Status = TrainerStatus.Submitted;
            registration.AppliedAt = clock.UtcNow;
            registration.DecidedAt = null;
            registration.DecidedById = null;
            registration.DecisionNote = null;
        }
        await uow.SaveChangesAsync();

        var checklist = await requirements.GetChecklistAsync(Modules.SoftSkillTrainer, userId);
        var already = (await uow.Documents.ListAttachedAsync(Modules.SoftSkillTrainer, registration.Id)).Select(d => d.Id).ToHashSet();
        foreach (var d in checklist.Rows.Where(x => x.Document is not null && !already.Contains(x.Document.Id)).Select(x => x.Document!))
            await uow.Documents.AttachAsync(new ApplicationDocument { Module = Modules.SoftSkillTrainer, ApplicationId = registration.Id, UserDocumentId = d.Id });

        await audit.LogAsync("softskill.trainer.apply", "SoftSkillTrainer", registration.Id, $"{me.FullName} registered as a trainer");
        await notify.NotifyHoldersAsync(Permissions.SoftSkill.TrainerReview, "New Soft Skill trainer registration",
            $"{me.FullName} ({profile.University?.Name}) registered as a trainer.", Links.SoftSkillTrainerRegistration(registration.Id));
        await uow.SaveChangesAsync();
        return await GetMineAsync();
    }

    public async Task<List<TrainerDto>> ListAsync(string? status, long? universityId)
    {
        TrainerStatus? s = null;
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            if (!Enum.TryParse<TrainerStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed)) throw new BadRequestException("Unknown trainer status.");
            s = parsed;
        }
        return (await uow.SoftSkillTrainers.ListAsync(s, universityId)).Select(t => SoftSkillMapper.Trainer(t, t.User)).ToList();
    }

    public async Task<TrainerDto> GetAsync(long id) => await DtoAsync(await LoadAsync(id));

    public async Task<TrainerDto> ApproveAsync(long id, TrainerDecisionRequest r)
    {
        var t = await LoadAsync(id);
        if (t.Status is not (TrainerStatus.Submitted or TrainerStatus.Suspended)) throw new BadRequestException("Only a registration waiting for a decision, or a suspended one, can be approved.");
        Decide(t, TrainerStatus.Approved, r.Note);
        await audit.LogAsync("softskill.trainer.approve", "SoftSkillTrainer", id, r.Note);
        await notify.NotifyAsync(t.UserId, "Trainer registration approved",
            t.TotCompleted ? "You can now be given Soft Skill sessions." : "Complete the UGC Training of Trainers before you are given sessions (cl.12 খ).", Links.SoftSkillTrainer);
        await uow.SaveChangesAsync();
        return await DtoAsync(t);
    }

    public async Task<TrainerDto> RejectAsync(long id, TrainerDecisionRequest r)
    {
        var t = await LoadAsync(id);
        if (t.Status != TrainerStatus.Submitted) throw new BadRequestException("Only a registration waiting for a decision can be rejected.");
        if (string.IsNullOrWhiteSpace(r.Note)) throw new BadRequestException("Give a reason.");
        Decide(t, TrainerStatus.Rejected, r.Note);
        await audit.LogAsync("softskill.trainer.reject", "SoftSkillTrainer", id, r.Note);
        await notify.NotifyAsync(t.UserId, "Trainer registration not approved", r.Note.Trim(), Links.SoftSkillTrainer);
        await uow.SaveChangesAsync();
        return await DtoAsync(t);
    }

    public async Task<TrainerDto> SuspendAsync(long id, TrainerDecisionRequest r)
    {
        var t = await LoadAsync(id);
        if (t.Status != TrainerStatus.Approved) throw new BadRequestException("Only an approved trainer can be suspended.");
        if (string.IsNullOrWhiteSpace(r.Note)) throw new BadRequestException("Give a reason.");
        Decide(t, TrainerStatus.Suspended, r.Note);
        await audit.LogAsync("softskill.trainer.suspend", "SoftSkillTrainer", id, r.Note);
        await notify.NotifyAsync(t.UserId, "Trainer registration suspended", r.Note.Trim(), Links.SoftSkillTrainer);
        await uow.SaveChangesAsync();
        return await DtoAsync(t);
    }

    public async Task<TrainerDto> RecordTotAsync(long id, TotRequest r)
    {
        var t = await LoadAsync(id);
        if (r.CompletedOn is not { } on) throw new BadRequestException("Enter the date the ToT was completed.");
        if (on > clock.Today) throw new BadRequestException("The ToT date cannot be in the future.");
        var reference = r.Reference?.Trim();
        if (reference is { Length: > 200 }) throw new BadRequestException("The reference can have at most 200 characters.");
        t.TotCompleted = true;
        t.TotCompletedOn = on;
        t.TotReference = string.IsNullOrEmpty(reference) ? null : reference;
        await audit.LogAsync("softskill.trainer.tot", "SoftSkillTrainer", id, $"ToT {on:yyyy-MM-dd} {t.TotReference}");
        await notify.NotifyAsync(t.UserId, "ToT recorded", "Your Training of Trainers is recorded (cl.12 খ).", Links.SoftSkillTrainer);
        await uow.SaveChangesAsync();
        return await DtoAsync(t);
    }

    // ---------- helpers ----------

    public static TrainerProfileDto ProfileDto(Domain.Entities.Identity.TrainerProfile? p, string? designation) =>
        new(p?.UniversityId, p?.University?.Name, p?.University?.IsPublic == true, designation, p?.AcademicDepartment, p?.EmployeeId);

    private async Task<TrainerEligibilityDto> EligibilityAsync(long userId, Domain.Entities.Identity.TrainerProfile? profile, string? designation, SoftSkillTrainer? registration)
    {
        var checklist = await requirements.GetChecklistAsync(Modules.SoftSkillTrainer, userId);
        var u = profile?.University;
        var rules = new List<EligibilityRule>
        {
            new("university", "cl.12 (ক)", "Teaches at a public university", u is { IsPublic: true, IsActive: true },
                u is null ? "Choose your university in My profile." : u.IsPublic ? u.Name : $"{u.Name} is not a public university.", null),
            new("designation", "Nexus rule", "Designation given", !string.IsNullOrWhiteSpace(designation),
                string.IsNullOrWhiteSpace(designation) ? "Add your designation in My profile." : designation!, null),
            new("documents", "Nexus rule", "Required documents uploaded", checklist.Ready,
                checklist.Ready ? "Every required document is in My documents." : $"Missing: {string.Join(", ", checklist.Missing)}.", null),
            new("not-registered", "Nexus rule", "No registration in progress", registration is null or { Status: TrainerStatus.Rejected },
                registration is null ? "Not registered yet." : $"Registration status: {registration.Status}.", null),
        };
        return new TrainerEligibilityDto(rules.All(x => x.Met), rules);
    }

    private void Decide(SoftSkillTrainer t, TrainerStatus status, string? note)
    {
        t.Status = status;
        t.DecidedAt = clock.UtcNow;
        t.DecidedById = me.UserId;
        t.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (t.DecisionNote is { Length: > SoftSkillTrainer.MaxNoteLength }) throw new BadRequestException($"The note can have at most {SoftSkillTrainer.MaxNoteLength} characters.");
    }

    private async Task<SoftSkillTrainer> LoadAsync(long id)
    {
        var t = await uow.SoftSkillTrainers.GetByIdAsync(id) ?? throw new NotFoundException("Trainer", id);
        return await uow.SoftSkillTrainers.GetForUserAsync(t.UserId) ?? t;
    }

    private async Task<TrainerDto> DtoAsync(SoftSkillTrainer t)
    {
        // Not tracked, so attaching the profile here changes nothing in the database.
        var user = (await uow.Users.GetAsync(t.UserId))?.User ?? throw new NotFoundException("User", t.UserId);
        user.TrainerProfile = await uow.Users.GetTrainerProfileAsync(t.UserId);
        return SoftSkillMapper.Trainer(t, user, await SoftSkillMapper.AttachedAsync(uow, Modules.SoftSkillTrainer, t.Id));
    }
}
