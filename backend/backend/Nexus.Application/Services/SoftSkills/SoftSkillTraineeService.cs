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

/// <summary>The Commission's side of trainees: the list, cancellation (cl.11 ঞ) and the certificate (cl.15).</summary>
public interface ISoftSkillTraineeService
{
    Task<List<TraineeListItemDto>> ListAsync(long? batchId, long? universityGroupId);
    Task<TraineeDto> GetAsync(long id);
    Task<TraineeDto> CancelAsync(long id, ReasonRequest request);

    /// <summary>Issues the certificate when the computed outcome allows it, otherwise records "Not certified" with the reasons.</summary>
    Task<TraineeDto> IssueOutcomeAsync(long id);
}

/// <summary>cl.10: stipends per month, paid or withheld by the Commission's finance section.</summary>
public interface ISoftSkillStipendService
{
    Task<List<StipendRowDto>> ListAsync(long? batchId, string? status);
    Task<StipendRowDto> PayAsync(long traineeId, int no);
    Task<StipendRowDto> WithholdAsync(long traineeId, int no, ReasonRequest request);
    Task<StipendRowDto> ReleaseAsync(long traineeId, int no);
    Task<SoftSkillOverviewDto> GetOverviewAsync();
}

public sealed class SoftSkillTraineeService(
    IUnitOfWork uow, ISoftSkillSetupService setup, IClock clock, IAuditService audit, INotificationService notify,
    SoftSkillWorkflow workflow) : ISoftSkillTraineeService
{
    public async Task<List<TraineeListItemDto>> ListAsync(long? batchId, long? universityGroupId)
    {
        var snap = await setup.GetSnapshotAsync();
        var today = clock.Today;
        return (await uow.SoftSkillTrainees.ListAsync(batchId, universityGroupId))
            .Where(t => SoftSkillRules.TraineeStatuses.Contains(t.Application.Status))
            .Select(t =>
            {
                var att = SoftSkillRules.Attendance(t, t.UniversityGroup.Sessions, snap.Policy);
                var outcome = SoftSkillRules.Outcome(t, t.UniversityGroup, today, snap);
                return new TraineeListItemDto(t.Id, SoftSkillMapper.Person(t.Application.Student), t.Application.Status.Label(), t.UniversityGroupId,
                    t.UniversityGroup.University.Name, t.UniversityGroup.Batch.Code, att.Pct, att.BelowMin, t.Results.Count,
                    t.AverageScore ?? outcome.Average, t.CertificateGrade ?? outcome.Grade, t.Report?.Status.Label(),
                    outcome.Ready, outcome.Certifiable, t.CertificateNumber);
            })
            .ToList();
    }

    public async Task<TraineeDto> GetAsync(long id) => SoftSkillMapper.Trainee(await LoadAsync(id), await setup.GetSnapshotAsync(), clock.Today);

    public async Task<TraineeDto> CancelAsync(long id, ReasonRequest r)
    {
        var t = await LoadAsync(id);
        if (t.Application.Status is not (ApplicationStatus.Selected or ApplicationStatus.Active))
            throw new BadRequestException("Only a selected or running trainee's participation can be cancelled.");
        var reason = r.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason)) throw new BadRequestException("Give the reason (cl.11 ঞ).");
        if (reason.Length > 1000) throw new BadRequestException("The reason can have at most 1000 characters.");
        t.CancelReason = reason;
        t.CancelledAt = clock.UtcNow;
        workflow.SetStatus(t.Application, ApplicationStatus.Terminated, reason);
        await audit.LogAsync("softskill.trainee.cancel", "SoftSkillTrainee", id, reason);
        await notify.NotifyAsync(t.Application.StudentId, "Soft Skill participation cancelled", reason, Links.SoftSkillStudent);
        await uow.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<TraineeDto> IssueOutcomeAsync(long id)
    {
        var t = await LoadAsync(id);
        if (t.Application.Status != ApplicationStatus.Completed)
            throw new BadRequestException(t.Application.Status is ApplicationStatus.Certified or ApplicationStatus.NotCertified
                ? "The outcome is already issued."
                : "The outcome is issued once the batch is marked completed.");
        var snap = await setup.GetSnapshotAsync();
        var o = SoftSkillRules.Outcome(t, t.UniversityGroup, clock.Today, snap);
        if (!o.Ready) throw new BadRequestException($"Not ready: {o.Pending[0]}");

        t.AverageScore = o.Average;
        if (o.Certifiable)
        {
            t.CertificateNumber = SoftSkillRules.CertificateNumberFor(t.UniversityGroup.Batch.Year, t.Id);
            t.CertificateGrade = o.Grade;
            t.CertificateIssuedAt = clock.UtcNow;
            t.OutcomeReasons = null;
            workflow.SetStatus(t.Application, ApplicationStatus.Certified, $"Certificate {t.CertificateNumber}, {o.Grade}");
            await notify.NotifyAsync(t.Application.StudentId, "Soft Skill certificate issued",
                $"Congratulations. Your certificate ({o.Grade}) is issued: {t.CertificateNumber}.", Links.SoftSkillStudent);
        }
        else
        {
            t.OutcomeReasons = string.Join('\n', o.Reasons);
            workflow.SetStatus(t.Application, ApplicationStatus.NotCertified, o.Reasons.FirstOrDefault());
            await notify.NotifyAsync(t.Application.StudentId, "Soft Skill outcome", "The training ended without a certificate. See the reasons in your programme page.", Links.SoftSkillStudent);
        }
        await audit.LogAsync("softskill.trainee.outcome", "SoftSkillTrainee", id,
            o.Certifiable ? $"Certified {t.CertificateNumber} {o.Grade} ({o.Average}%)" : $"Not certified: {t.OutcomeReasons}");
        await uow.SaveChangesAsync();
        return await GetAsync(id);
    }

    private async Task<SoftSkillTrainee> LoadAsync(long id) =>
        await uow.SoftSkillTrainees.GetDetailAsync(id) ?? throw new NotFoundException("Trainee", id);
}

public sealed class SoftSkillStipendService(
    IUnitOfWork uow, ISoftSkillSetupService setup, ICurrentUserService me, IClock clock, IAuditService audit, INotificationService notify)
    : ISoftSkillStipendService
{
    public async Task<List<StipendRowDto>> ListAsync(long? batchId, string? status)
    {
        var policy = (await setup.GetSnapshotAsync()).Policy;
        var rows = (await uow.SoftSkillTrainees.ListAsync(batchId, null))
            .Where(t => SoftSkillRules.TraineeStatuses.Contains(t.Application.Status) && t.Application.Status != ApplicationStatus.Selected)
            .SelectMany(t => SoftSkillRules.Stipends(t, t.UniversityGroup, policy).Select(v => Row(t, v)));
        if (!string.IsNullOrWhiteSpace(status) && status != "all") rows = rows.Where(r => string.Equals(r.Status, status, StringComparison.OrdinalIgnoreCase));
        return rows.OrderBy(r => r.BatchCode).ThenBy(r => r.University).ThenBy(r => r.Student).ThenBy(r => r.No).ToList();
    }

    public async Task<StipendRowDto> PayAsync(long traineeId, int no)
    {
        var (t, view, policy) = await LoadAsync(traineeId, no);
        if (view.Status != StipendStatus.ReadyToPay) throw new BadRequestException($"This stipend is \"{view.Status}\", not ready to pay.");
        var row = RowFor(t, no);
        row.WithheldReason = null;
        row.WithheldAt = null;
        row.WithheldById = null;
        row.PaidAt = clock.UtcNow;
        row.PaidById = me.UserId;
        row.PaidByName = me.FullName;
        row.PaidAmount = (int)policy.MonthlyStipend;
        row.SlipNumber = SoftSkillStipend.SlipNumberFor(t.UniversityGroup.Batch.Year, t.Id, no);
        await audit.LogAsync("softskill.stipend.pay", "SoftSkillTrainee", t.Id, $"Month {no}: {row.PaidAmount} BDT, slip {row.SlipNumber}");
        await notify.NotifyAsync(t.Application.StudentId, "Stipend paid", $"Your month {no} stipend of {row.PaidAmount:N0} BDT is paid. Slip {row.SlipNumber}.", Links.SoftSkillStudent);
        await uow.SaveChangesAsync();
        return await RowAsync(traineeId, no);
    }

    public async Task<StipendRowDto> WithholdAsync(long traineeId, int no, ReasonRequest r)
    {
        var (t, view, _) = await LoadAsync(traineeId, no);
        if (view.Status != StipendStatus.ReadyToPay) throw new BadRequestException("Only a stipend ready to pay can be withheld.");
        var reason = r.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason)) throw new BadRequestException("Give the reason: attendance, conduct or discipline (cl.10 খ).");
        if (reason.Length > SoftSkillStipend.MaxReasonLength) throw new BadRequestException($"At most {SoftSkillStipend.MaxReasonLength} characters.");
        var row = RowFor(t, no);
        row.WithheldReason = reason;
        row.WithheldAt = clock.UtcNow;
        row.WithheldById = me.UserId;
        await audit.LogAsync("softskill.stipend.withhold", "SoftSkillTrainee", t.Id, $"Month {no}: {reason}");
        await notify.NotifyAsync(t.Application.StudentId, "Stipend withheld", $"Your month {no} stipend is withheld: {reason}", Links.SoftSkillStudent);
        await uow.SaveChangesAsync();
        return await RowAsync(traineeId, no);
    }

    public async Task<StipendRowDto> ReleaseAsync(long traineeId, int no)
    {
        var (t, view, _) = await LoadAsync(traineeId, no);
        if (view.Status != StipendStatus.Withheld) throw new BadRequestException("This stipend is not withheld.");
        var row = RowFor(t, no);
        row.WithheldReason = null;
        row.WithheldAt = null;
        row.WithheldById = null;
        await audit.LogAsync("softskill.stipend.release", "SoftSkillTrainee", t.Id, $"Month {no}");
        await uow.SaveChangesAsync();
        return await RowAsync(traineeId, no);
    }

    public async Task<SoftSkillOverviewDto> GetOverviewAsync()
    {
        var batches = await uow.SoftSkillBatches.ListAsync();
        var taken = await uow.SoftSkillBatches.TakenSeatsByUniversityGroupAsync();
        var open = batches.Where(b => b.Status == BatchStatus.ApplicationsOpen).ToList();
        var live = batches.Where(b => b.Status is BatchStatus.ApplicationsOpen or BatchStatus.Selection or BatchStatus.Running).ToList();
        var apps = await uow.SoftSkillApplications.ListAsync(null, null, null);
        var stipends = await ListAsync(null, StipendStatus.ReadyToPay);
        var pendingTrainers = await uow.SoftSkillTrainers.CountAsync(t => t.Status == TrainerStatus.Submitted);
        var funnel = StatusLabels.ApplicationLabels
            .Select(label => new FunnelDto(label, apps.Count(a => a.Status.Label() == label)))
            .Where(f => f.Count > 0)
            .ToList();
        return new SoftSkillOverviewDto(
            open.Count,
            live.Sum(b => b.UniversityGroups.Count),
            open.SelectMany(b => b.UniversityGroups).Sum(c => Math.Max(0, c.Capacity - taken.GetValueOrDefault(c.Id))),
            apps.Count(a => a.Status is ApplicationStatus.Submitted or ApplicationStatus.Shortlisted),
            apps.Count(a => a.Status == ApplicationStatus.Active),
            pendingTrainers,
            stipends.Count,
            apps.Count(a => a.Status == ApplicationStatus.Certified),
            funnel);
    }

    // ---------- helpers ----------

    private static StipendRowDto Row(SoftSkillTrainee t, SoftSkillStipendView v) =>
        new(t.Id, t.Application.Student.FullName, t.UniversityGroup.University.Name, t.UniversityGroup.Batch.Code, v.No, v.Status, v.Amount, v.Reason,
            v.Marks, v.MaxMarks, v.MonthAttendancePct, v.PaidAt, v.PaidBy, v.SlipNumber);

    private static SoftSkillStipend RowFor(SoftSkillTrainee t, int no)
    {
        var row = t.Stipends.FirstOrDefault(s => s.AssessmentNo == no);
        if (row is null)
        {
            row = new SoftSkillStipend { AssessmentNo = no };
            t.Stipends.Add(row);
        }
        return row;
    }

    private async Task<(SoftSkillTrainee Trainee, SoftSkillStipendView View, SoftSkillPolicy Policy)> LoadAsync(long traineeId, int no)
    {
        if (no is < 1 or > SoftSkillRules.AssessmentCount) throw new BadRequestException($"The month is 1–{SoftSkillRules.AssessmentCount}.");
        var t = await uow.SoftSkillTrainees.GetDetailAsync(traineeId) ?? throw new NotFoundException("Trainee", traineeId);
        var policy = (await setup.GetSnapshotAsync()).Policy;
        var view = SoftSkillRules.Stipends(t, t.UniversityGroup, policy).First(v => v.No == no);
        return (t, view, policy);
    }

    private async Task<StipendRowDto> RowAsync(long traineeId, int no)
    {
        var t = await uow.SoftSkillTrainees.GetDetailAsync(traineeId) ?? throw new NotFoundException("Trainee", traineeId);
        var policy = (await setup.GetSnapshotAsync()).Policy;
        return Row(t, SoftSkillRules.Stipends(t, t.UniversityGroup, policy).First(v => v.No == no));
    }
}
