using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.SoftSkills;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.SoftSkills;

/// <summary>
/// cl.14 (ঘ): the Commission monitors the training regularly and may engage academia/industry experts.
/// The Commission plans visits to university groups; the monitor files a report with ratings, findings and recommendations;
/// the coordinator sees it. A health view flags university groups that need attention.
/// </summary>
public interface ISoftSkillMonitoringService
{
    Task<List<MonitorOptionDto>> ListMonitorsAsync();
    Task<List<VisitDto>> ListAsync(long? batchId, long? universityGroupId, string? status);
    Task<List<VisitDto>> ListMineAsync();
    Task<VisitDto> GetAsync(long id);
    Task<VisitDto> PlanAsync(PlanVisitRequest request);
    Task<VisitDto> CancelAsync(long id);
    Task<VisitDto> SubmitReportAsync(long id, VisitReportRequest request);
    Task<List<UniversityGroupHealthDto>> GetHealthAsync(long? batchId);
}

public sealed class SoftSkillMonitoringService(
    IUnitOfWork uow, ISoftSkillSetupService setup, IPermissionService permissions, ICurrentUserService me, IClock clock,
    IAuditService audit, INotificationService notify) : ISoftSkillMonitoringService
{
    private const int MinFindingsLength = 30;

    public async Task<List<MonitorOptionDto>> ListMonitorsAsync()
    {
        // Experts and Commission officers can both go on a visit.
        var rows = (await uow.Users.ListAsync(Roles.Monitor, null)).Concat(await uow.Users.ListAsync(Roles.Admin, null));
        return rows.Where(r => r.User.IsActive)
            .Select(r => new MonitorOptionDto(r.User.Id, r.User.FullName, r.User.Designation, r.Role ?? ""))
            .OrderBy(m => m.Role == Roles.Monitor ? 0 : 1).ThenBy(m => m.Name)
            .ToList();
    }

    public async Task<List<VisitDto>> ListAsync(long? batchId, long? universityGroupId, string? status) =>
        (await uow.SoftSkillVisits.ListAsync(batchId, universityGroupId, null, ParseStatus(status))).Select(SoftSkillMapper.Visit).ToList();

    public async Task<List<VisitDto>> ListMineAsync() =>
        (await uow.SoftSkillVisits.ListAsync(null, null, me.RequireUserId(), null)).Select(SoftSkillMapper.Visit).ToList();

    public async Task<VisitDto> GetAsync(long id) => SoftSkillMapper.Visit(await LoadForCallerAsync(id));

    public async Task<VisitDto> PlanAsync(PlanVisitRequest r)
    {
        var universityGroup = await uow.SoftSkillUniversityGroups.GetDetailAsync(r.UniversityGroupId ?? 0) ?? throw new BadRequestException("Choose a university group.");
        if (universityGroup.Batch.Status is not (BatchStatus.Selection or BatchStatus.Running))
            throw new BadRequestException("Visits are planned while a batch is in selection or running.");
        var monitors = await ListMonitorsAsync();
        var monitor = monitors.FirstOrDefault(m => m.Id == r.MonitorId) ?? throw new BadRequestException("Choose an expert or a Commission officer.");

        SoftSkillSession? session = null;
        if (r.SessionId is { } sid)
        {
            session = universityGroup.Sessions.FirstOrDefault(s => s.Id == sid) ?? throw new BadRequestException("The session is not in this university group.");
            if (session.Status == SessionStatus.Cancelled) throw new BadRequestException("That session was cancelled.");
        }
        var plannedOn = r.PlannedOn ?? session?.Date ?? throw new BadRequestException("Choose the visit date.");
        if (plannedOn < universityGroup.Batch.StartDate || plannedOn > universityGroup.Batch.EndDate)
            throw new BadRequestException($"Visits fall within the training, {universityGroup.Batch.StartDate:dd MMM} to {universityGroup.Batch.EndDate:dd MMM yyyy}.");
        if (session is not null && session.Date != plannedOn) throw new BadRequestException("The visit date must be the session's date.");
        var purpose = Text(r.Purpose, SoftSkillMonitoringVisit.MaxPurposeLength, "Purpose");

        var visit = new SoftSkillMonitoringVisit { UniversityGroupId = universityGroup.Id, MonitorId = monitor.Id, SessionId = session?.Id, PlannedOn = plannedOn, Purpose = purpose };
        await uow.SoftSkillVisits.AddAsync(visit);
        await uow.SaveChangesAsync();
        await audit.LogAsync("softskill.visit.plan", "SoftSkillUniversityGroup", universityGroup.Id, $"{monitor.Name} on {plannedOn:yyyy-MM-dd}");
        await notify.NotifyAsync(monitor.Id, "Soft Skill monitoring visit",
            $"Visit {universityGroup.University.Name} ({universityGroup.Batch.Name}) on {plannedOn:dd MMM yyyy}.", Links.SoftSkillMonitoring);
        if (universityGroup.CoordinatorId is { } coordinator)
            await notify.NotifyAsync(coordinator, "Monitoring visit planned",
                $"{monitor.Name} will visit your university group on {plannedOn:dd MMM yyyy} (cl.14 ঘ).", Links.SoftSkillUniversityGroup(universityGroup.Id, "monitoring"));
        await uow.SaveChangesAsync();
        return SoftSkillMapper.Visit((await uow.SoftSkillVisits.GetDetailAsync(visit.Id))!);
    }

    public async Task<VisitDto> CancelAsync(long id)
    {
        var v = await uow.SoftSkillVisits.GetDetailAsync(id) ?? throw new NotFoundException("Visit", id);
        if (v.Status != VisitStatus.Planned) throw new BadRequestException("Only a planned visit can be cancelled.");
        v.Status = VisitStatus.Cancelled;
        await audit.LogAsync("softskill.visit.cancel", "SoftSkillUniversityGroup", v.UniversityGroupId, $"Visit {v.Id}");
        await notify.NotifyAsync(v.MonitorId, "Monitoring visit cancelled", $"The visit to {v.UniversityGroup.University.Name} on {v.PlannedOn:dd MMM yyyy} is cancelled.", Links.SoftSkillMonitoring);
        await uow.SaveChangesAsync();
        return SoftSkillMapper.Visit(v);
    }

    public async Task<VisitDto> SubmitReportAsync(long id, VisitReportRequest r)
    {
        var v = await LoadForCallerAsync(id);
        if (v.Status != VisitStatus.Planned) throw new BadRequestException(v.Status == VisitStatus.Submitted ? "The report is already filed." : "This visit was cancelled.");
        if (r.VisitedOn is not { } on) throw new BadRequestException("Enter the date of the visit.");
        if (on > clock.Today) throw new BadRequestException("The visit date cannot be in the future.");
        if (on < v.UniversityGroup.Batch.StartDate) throw new BadRequestException("The visit cannot be before the training starts.");
        int Rating(int? value, string name) =>
            value is >= 1 and <= SoftSkillMonitoringVisit.MaxRating ? value.Value : throw new BadRequestException($"Rate {name} from 1 to {SoftSkillMonitoringVisit.MaxRating}.");
        var findings = r.Findings?.Trim() ?? "";
        if (findings.Length < MinFindingsLength) throw new BadRequestException($"Write at least {MinFindingsLength} characters of findings.");
        if (findings.Length > SoftSkillMonitoringVisit.MaxTextLength) throw new BadRequestException($"Findings can have at most {SoftSkillMonitoringVisit.MaxTextLength} characters.");

        v.DeliveryRating = Rating(r.Delivery, "the trainers' delivery");
        v.EngagementRating = Rating(r.Engagement, "the trainees' participation");
        v.AttendanceRating = Rating(r.Attendance, "punctuality and attendance records");
        v.FacilitiesRating = Rating(r.Facilities, "the venue and equipment");
        v.VisitedOn = on;
        v.Findings = findings;
        v.Recommendations = Text(r.Recommendations, SoftSkillMonitoringVisit.MaxTextLength, "Recommendations");
        v.SubmittedAt = clock.UtcNow;
        v.Status = VisitStatus.Submitted;

        await audit.LogAsync("softskill.visit.report", "SoftSkillUniversityGroup", v.UniversityGroupId, $"Visit {v.Id}: average {v.AverageRating}");
        await notify.NotifyHoldersAsync(Permissions.SoftSkill.MonitoringManage, "Monitoring report filed",
            $"{v.Monitor.FullName} reported on {v.UniversityGroup.University.Name}: average {v.AverageRating}/5.", Links.SoftSkillUniversityGroup(v.UniversityGroupId, "monitoring"));
        if (v.UniversityGroup.CoordinatorId is { } coordinator)
            await notify.NotifyAsync(coordinator, "Monitoring report on your university group", "Read the findings and recommendations in the university group's Monitoring tab.", Links.SoftSkillUniversityGroup(v.UniversityGroupId, "monitoring"));
        await uow.SaveChangesAsync();
        return SoftSkillMapper.Visit(v);
    }

    public async Task<List<UniversityGroupHealthDto>> GetHealthAsync(long? batchId)
    {
        var snap = await setup.GetSnapshotAsync();
        var policy = snap.Policy;
        var today = clock.Today;
        var visits = await uow.SoftSkillVisits.ListAsync(batchId, null, null, VisitStatus.Submitted);
        var universityGroups = (await uow.SoftSkillUniversityGroups.ListAsync(batchId, null))
            .Where(c => c.Batch.Status is BatchStatus.Selection or BatchStatus.Running or BatchStatus.Completed)
            .ToList();

        var list = new List<UniversityGroupHealthDto>();
        foreach (var summary in universityGroups)
        {
            var c = await uow.SoftSkillUniversityGroups.GetDetailAsync(summary.Id);
            if (c is null) continue;
            var running = c.Batch.Status == BatchStatus.Running;
            var trainees = c.Trainees.Where(t => t.Application.Status is ApplicationStatus.Active or ApplicationStatus.Completed
                or ApplicationStatus.Certified or ApplicationStatus.NotCertified).ToList();
            var att = trainees.Select(t => SoftSkillRules.Attendance(t, c.Sessions, policy)).ToList();
            var held = c.Sessions.Where(s => s.Status == SessionStatus.Held).Sum(s => s.Hours);
            var planned = c.Sessions.Where(s => s.Status != SessionStatus.Cancelled).Sum(s => s.Hours);
            decimal? expected = null;
            if (running && today >= c.Batch.StartDate)
            {
                var span = c.Batch.EndDate.DayNumber - c.Batch.StartDate.DayNumber + 1;
                var gone = Math.Min(span, today.DayNumber - c.Batch.StartDate.DayNumber + 1);
                expected = Math.Round(policy.TotalHours * (decimal)gone / span, 1);
            }
            var results = c.Assessments.SelectMany(a => a.Results.Select(r => r.PassedFor(a))).ToList();
            var pendingLeave = c.Sessions.SelectMany(s => s.Attendance).Count(a => a.Status == AttendanceStatus.LeaveRequested);
            var mine = visits.Where(v => v.UniversityGroupId == c.Id).ToList();
            var avgRating = mine.Count > 0 && mine.All(v => v.AverageRating is not null) ? Math.Round(mine.Average(v => v.AverageRating!.Value), 2) : (decimal?)null;

            var flags = new List<string>();
            if (c.CoordinatorId is null) flags.Add("No coordinator named (cl.12 গ).");
            var below = att.Count(a => a.BelowMin);
            if (below > 0) flags.Add($"{below} trainee{(below == 1 ? "" : "s")} below {policy.MinAttendancePct}% attendance (cl.11 ছ).");
            if (trainees.Count == 0)
            {
                // Nothing to deliver at a university where nobody was selected; say so once instead of flagging every step.
                if (c.Batch.Status != BatchStatus.Selection) flags.Add("No trainee selected at this university.");
            }
            else
            {
                for (var no = 1; no <= SoftSkillRules.AssessmentCount; no++)
                {
                    var due = c.Batch.StartDate.AddMonths(no).AddDays(7);
                    var a = c.Assessments.FirstOrDefault(x => x.No == no);
                    if (today > due && (a is null || a.Results.Count == 0)) flags.Add($"Assessment {no} results not entered (cl.14 ক).");
                }
                if (expected is { } e && today <= c.Batch.EndDate && held + 8 < e)
                    flags.Add($"Behind schedule: {held:0.#} of about {e:0.#} hours held by now (cl.8).");
                if (today > c.Batch.EndDate && held < policy.TotalHours)
                    flags.Add($"Only {held:0.#} of {policy.TotalHours} hours were held (cl.8, cl.13).");
                if (running && today > c.Batch.StartDate.AddDays(30) && mine.Count == 0) flags.Add("No monitoring visit reported yet (cl.14 ঘ).");
                if (today > c.Batch.EndDate && c.FinalReportAt is null) flags.Add("Final evaluation report not sent (cl.14 ঙ).");
            }
            if (pendingLeave > 0) flags.Add($"{pendingLeave} leave request{(pendingLeave == 1 ? "" : "s")} waiting for the coordinator.");
            if (avgRating is < 3) flags.Add($"Low monitoring rating: {avgRating}/5.");

            list.Add(new UniversityGroupHealthDto(
                SoftSkillMapper.UniversityGroup(c, c.Trainees.Count(t => SoftSkillRules.SeatStatuses.Contains(t.Application.Status))),
                trainees.Count,
                att.Count == 0 ? 100 : (int)Math.Round(att.Average(a => a.Pct)),
                below, held, planned, policy.TotalHours, expected,
                c.Assessments.Count,
                results.Count == 0 ? null : (int)Math.Round(results.Count(x => x) * 100.0 / results.Count),
                pendingLeave, mine.Count, mine.Select(v => v.VisitedOn).Max(), avgRating, flags));
        }
        return list.OrderByDescending(h => h.Flags.Count).ThenBy(h => h.UniversityGroup.University.Name).ToList();
    }

    // ---------- helpers ----------

    /// <summary>The visit, if the caller is its monitor or manages monitoring; otherwise 404.</summary>
    private async Task<SoftSkillMonitoringVisit> LoadForCallerAsync(long id)
    {
        var v = await uow.SoftSkillVisits.GetDetailAsync(id) ?? throw new NotFoundException("Visit", id);
        if (v.MonitorId != me.RequireUserId() && !await permissions.HasAsync(Permissions.SoftSkill.MonitoringManage))
            throw new NotFoundException("Visit", id);
        return v;
    }

    private static VisitStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status == "all") return null;
        return Enum.TryParse<VisitStatus>(status, true, out var s) && Enum.IsDefined(s) ? s : throw new BadRequestException("Unknown visit status.");
    }

    private static string? Text(string? value, int max, string field)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length <= max ? v : throw new BadRequestException($"{field} can have at most {max} characters.");
    }
}
