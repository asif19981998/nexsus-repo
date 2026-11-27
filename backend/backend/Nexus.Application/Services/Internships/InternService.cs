using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.Internships;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.Internships;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Internships;

/// <summary>
/// Running internships: transfer, attendance, report, evaluation, completion, certificate, termination.
/// A caller with <see cref="Permissions.InternViewAll"/> reaches every intern; one with only
/// <see cref="Permissions.InternViewAssigned"/> reaches the interns they mentor.
/// </summary>
public interface IInternService
{
    Task<List<InternListItemDto>> ListAsync(string? status);
    Task<ApplicationDetailDto> GetAsync(long applicationId);
    Task<ApplicationDetailDto> TransferAsync(long applicationId, TransferRequest request);
    Task<ApplicationDetailDto> DecideLeaveAsync(long applicationId, DecideLeaveRequest request);
    Task<ApplicationDetailDto> MarkAbsenceAsync(long applicationId, MarkAbsenceRequest request);
    Task<ApplicationDetailDto> SetAttendanceTimesAsync(long applicationId, SetAttendanceTimesRequest request);
    Task<ApplicationDetailDto> ReviewReportAsync(long applicationId, ReviewReportRequest request);
    Task<ApplicationDetailDto> SubmitEvaluationAsync(long applicationId, EvaluationRequest request);
    Task<ApplicationDetailDto> EvaluateMonthAsync(long applicationId, string month, EvaluateMonthRequest request);
    Task<ApplicationDetailDto> CompleteAsync(long applicationId);
    Task<(ApplicationDetailDto Detail, IssueOutcomeResult Result)> IssueOutcomeAsync(long applicationId);
    Task<ApplicationDetailDto> TerminateAsync(long applicationId, ReasonRequest request);
}

public sealed class InternService(
    IUnitOfWork uow, ISetupService setup, IPermissionService permissions, ICurrentUserService me, IClock clock,
    IAuditService audit, INotificationService notify, ApplicationWorkflow workflow) : IInternService
{
    public async Task<List<InternListItemDto>> ListAsync(string? status)
    {
        var statuses = InternshipRules.InternStatuses.ToList();
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            if (!StatusLabels.TryParseApplication(status, out var s) || !statuses.Contains(s)) throw new BadRequestException("Unknown intern status.");
            statuses = [s];
        }

        var apps = await uow.Applications.ListInternsAsync(statuses, await MentorScopeAsync());
        var snap = await setup.GetSnapshotAsync();
        var today = clock.Today;
        return apps
            .OrderByDescending(a => a.Internship!.Id)
            .Select(a =>
            {
                var it = a.Internship!;
                var sum = InternshipRules.Summary(it, today, snap);
                return new InternListItemDto(a.Id, InternshipMapper.Person(a.Student), a.Batch.Name, a.Status.Label(), it.Department.Name,
                    it.Mentor?.FullName, it.StartDate, it.EndDate, sum.Pct, sum.BelowMin,
                    it.Attendance.Count(r => r.Status == AttendanceStatus.LeaveRequested),
                    it.Report?.Status == ReportStatus.Submitted,
                    it.Bills.Count(b => b.EvaluatedAt is null && b.PaidAt is null));
            })
            .ToList();
    }

    public async Task<ApplicationDetailDto> GetAsync(long id) => await DetailAsync(await LoadAsync(id));

    public async Task<ApplicationDetailDto> TransferAsync(long id, TransferRequest r)
    {
        var app = await LoadAsync(id);
        var it = app.Internship!;
        if (app.Status != ApplicationStatus.Active) throw new BadRequestException("Only active interns can be transferred.");
        var dept = (await uow.Setup.GetDepartmentsAsync(activeOnly: true)).FirstOrDefault(d => d.Id == r.DepartmentId)
            ?? throw new BadRequestException("Choose a UGC department.");
        var mentorId = r.MentorId ?? it.MentorId;
        if (dept.Id == it.DepartmentId && mentorId == it.MentorId) throw new BadRequestException("The intern is already in this department with this mentor.");
        if (r.MentorId is { } newMentor && (await uow.Users.ListMentorsAsync()).All(m => m.Id != newMentor))
            throw new BadRequestException("The chosen mentor is not an active mentor.");
        var reason = r.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason)) throw new BadRequestException("Give a reason for the transfer.");

        it.DepartmentId = dept.Id;
        it.MentorId = mentorId;
        it.Placements.Add(new InternshipPlacement { DepartmentId = dept.Id, MentorId = mentorId, From = clock.Today, Note = reason });
        await audit.LogAsync("intern.transfer", "InternshipApplication", id, $"→ {dept.Name}: {reason}");
        await notify.NotifyAsync(app.StudentId, "Department transfer", $"UGC moved you to {dept.Name}.", Links.Student);
        if (mentorId is { } m && m != me.UserId)
            await notify.NotifyAsync(m, "Intern assigned to you", $"{app.Student.FullName} now works in {dept.Name} under your supervision.", Links.Manage);
        await uow.SaveChangesAsync();
        return await DetailAsync(await LoadAsync(id));
    }

    public async Task<ApplicationDetailDto> DecideLeaveAsync(long id, DecideLeaveRequest r)
    {
        var app = await LoadAsync(id);
        var rec = app.Internship!.Attendance.FirstOrDefault(a => a.Date == r.Date);
        if (rec is null || rec.Status != AttendanceStatus.LeaveRequested) throw new BadRequestException("Leave request not found.");

        if (r.Approve)
        {
            rec.Status = AttendanceStatus.Leave;
            rec.Approved = true;
        }
        else
        {
            rec.Status = AttendanceStatus.Absent;
            rec.Approved = false;
            rec.Note = "Leave rejected";
        }
        rec.DecidedById = me.UserId;

        var date = rec.Date.ToString("dd MMM yyyy");
        await audit.LogAsync(r.Approve ? "leave.approve" : "leave.reject", "InternshipApplication", id, rec.Date.ToString("yyyy-MM-dd"));
        await notify.NotifyAsync(app.StudentId, r.Approve ? "Leave approved" : "Leave rejected",
            $"Your leave request for {date} was {(r.Approve ? "approved" : "rejected; it counts as an unapproved absence")}.", Links.Student);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<ApplicationDetailDto> MarkAbsenceAsync(long id, MarkAbsenceRequest r)
    {
        var app = await LoadAsync(id);
        var it = app.Internship!;
        if (app.Status != ApplicationStatus.Active) throw new BadRequestException("No active internship.");
        if (r.Date is not { } date) throw new BadRequestException("Choose a date.");
        if (!InternshipRules.IsWorkingDay(date, (await setup.GetSnapshotAsync()).Calendar)) throw new BadRequestException("That date is a weekend or a holiday.");
        if (date < it.StartDate || date > it.EndDate) throw new BadRequestException("The date is outside the internship period.");
        if (date > clock.Today) throw new BadRequestException("An absence can be marked only for today or an earlier day.");
        var reason = r.Reason?.Trim() ?? "";
        if (r.Approved && reason.Length == 0) throw new BadRequestException("Give the reason for an approved absence.");

        var rec = it.Attendance.FirstOrDefault(a => a.Date == date);

        // Marking an absence can replace a sign-in or a leave request; record what was there so the change is traceable.
        var replaced = rec switch
        {
            null => null,
            { Status: AttendanceStatus.Present } => $"replaces sign-in {InternshipMapper.Time(rec.CheckIn) ?? "?"}–{InternshipMapper.Time(rec.CheckOut) ?? "?"}",
            _ => $"replaces {InternshipMapper.AttendanceLabel(rec.Status).ToLowerInvariant()}{(rec.Approved ? " (approved)" : "")}",
        };

        if (rec is null)
        {
            rec = new AttendanceRecord { Date = date };
            it.Attendance.Add(rec);
        }
        // The punch itself (times, photos, location) is kept: marking absent changes the day's status only.
        rec.Status = AttendanceStatus.Absent;
        rec.Approved = r.Approved;
        rec.Reason = reason;
        rec.DecidedById = me.UserId;

        await audit.LogAsync("attendance.mark_absent", "InternshipApplication", id, $"{date:yyyy-MM-dd} ({(r.Approved ? "approved" : "unapproved")}){(replaced is null ? "" : $"; {replaced}")}");
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    /// <summary>Admin only: correct a day's sign-in and sign-out times (forgotten punch, wrong time). The change is audited.</summary>
    public async Task<ApplicationDetailDto> SetAttendanceTimesAsync(long id, SetAttendanceTimesRequest r)
    {
        if (await uow.Users.GetRoleNameAsync(me.RequireUserId()) != Roles.Admin)
            throw new ForbiddenException("Only an administrator can change sign-in and sign-out times.");

        var app = await LoadAsync(id);
        var it = app.Internship!;
        if (app.Status != ApplicationStatus.Active) throw new BadRequestException("No active internship.");
        if (r.Date is not { } date) throw new BadRequestException("Choose a date.");
        if (!InternshipRules.IsWorkingDay(date, (await setup.GetSnapshotAsync()).Calendar)) throw new BadRequestException("That date is a weekend or a holiday.");
        if (date < it.StartDate || date > it.EndDate) throw new BadRequestException("The date is outside the internship period.");
        if (date > clock.Today) throw new BadRequestException("Times can be set only for today or an earlier day.");
        if (!TimeOnly.TryParse(r.CheckIn, out var checkIn)) throw new BadRequestException("Enter the sign-in time as HH:mm.");
        TimeOnly? checkOut = null;
        if (!string.IsNullOrWhiteSpace(r.CheckOut))
        {
            if (!TimeOnly.TryParse(r.CheckOut, out var outTime)) throw new BadRequestException("Enter the sign-out time as HH:mm.");
            if (outTime <= checkIn) throw new BadRequestException("Sign-out must be after sign-in.");
            checkOut = outTime;
        }
        var reason = r.Reason?.Trim() ?? "";
        if (reason.Length < 5) throw new BadRequestException("Give the reason for the change.");

        var rec = it.Attendance.FirstOrDefault(a => a.Date == date);
        var before = rec is null ? "no record" : $"{rec.Status} {rec.CheckIn:HH\\:mm}-{rec.CheckOut:HH\\:mm}";
        if (rec is null)
        {
            rec = new AttendanceRecord { Date = date };
            it.Attendance.Add(rec);
        }

        rec.Status = AttendanceStatus.Present;
        rec.Approved = false;
        rec.Reason = null;
        rec.CheckIn = checkIn;
        rec.CheckInAtUtc = BangladeshClock.ToUtc(date, checkIn);
        rec.CheckOut = checkOut;
        rec.CheckOutAtUtc = checkOut is { } o ? BangladeshClock.ToUtc(date, o) : null;
        var note = $"Times edited by admin: {reason}";
        rec.Note = note.Length > 200 ? note[..200] : note;
        rec.DecidedById = me.UserId;

        await audit.LogAsync("attendance.set_times", "InternshipApplication", id,
            $"{date:yyyy-MM-dd} {before} -> {checkIn:HH\\:mm}-{(checkOut is { } c ? c.ToString("HH\\:mm") : "open")}: {reason}");
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<ApplicationDetailDto> ReviewReportAsync(long id, ReviewReportRequest r)
    {
        var app = await LoadAsync(id);
        var report = app.Internship!.Report ?? throw new BadRequestException("No report submitted.");
        if (report.Status == ReportStatus.Accepted) throw new BadRequestException("The report is already accepted.");
        var note = r.Note?.Trim();
        if (!r.Accept && string.IsNullOrWhiteSpace(note)) throw new BadRequestException("Say what must be revised.");
        if (note is { Length: > InternshipReport.MaxReviewNoteLength })
            throw new BadRequestException($"The note can have at most {InternshipReport.MaxReviewNoteLength} characters.");

        report.Status = r.Accept ? ReportStatus.Accepted : ReportStatus.RevisionRequested;
        report.ReviewNote = note;
        report.ReviewedById = me.UserId;
        report.ReviewedAt = clock.UtcNow;
        await audit.LogAsync(r.Accept ? "report.accept" : "report.revise", "InternshipApplication", id, note);
        await notify.NotifyAsync(app.StudentId, r.Accept ? "Report accepted" : "Report needs revision",
            r.Accept ? "Your final report was accepted." : note, Links.Student);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<ApplicationDetailDto> SubmitEvaluationAsync(long id, EvaluationRequest r)
    {
        var app = await LoadAsync(id);
        var it = app.Internship!;
        if (app.Status is not (ApplicationStatus.Active or ApplicationStatus.Completed)) throw new BadRequestException("No internship to evaluate.");
        var snap = await setup.GetSnapshotAsync();
        var criteria = snap.Criteria;
        if (InternshipRules.EvaluationBlockedReason(it, clock.Today, snap.Policy) is { } blocked) throw new BadRequestException(blocked);

        var scores = new List<EvaluationScore>();
        foreach (var c in criteria)
        {
            var v = r.Scores.GetValueOrDefault(c.Key);
            if (v is null || v < 0 || v > c.MaxScore) throw new BadRequestException($"{c.Label}: score must be 0–{c.MaxScore}.");
            scores.Add(new EvaluationScore { CriterionId = c.Id, Score = v.Value });
        }
        var recommendation = r.Recommendation?.Trim();
        if (string.IsNullOrWhiteSpace(recommendation)) throw new BadRequestException("Write the department's recommendation (cl.14).");
        if (recommendation.Length < snap.Policy.MinRecommendationLength)
            throw new BadRequestException($"The recommendation needs at least {snap.Policy.MinRecommendationLength} characters.");
        if (recommendation.Length > InternshipEvaluation.MaxRecommendationLength)
            throw new BadRequestException($"The recommendation can have at most {InternshipEvaluation.MaxRecommendationLength} characters.");

        var total = scores.Sum(s => s.Score);
        // Keep what is being replaced in the audit, so an overwritten evaluation can still be traced (FR-X1).
        string? replaced = null;
        if (it.Evaluation is { } old)
        {
            var keyById = criteria.ToDictionary(c => c.Id, c => c.Key);
            var oldScores = string.Join(", ", old.Scores.Select(s => $"{keyById.GetValueOrDefault(s.CriterionId, s.CriterionId.ToString())} {s.Score}"));
            replaced = $"; replaced {old.Total}/100 by {old.Evaluator?.FullName ?? "unknown"} on {old.EvaluatedAt:yyyy-MM-dd} ({oldScores}); " +
                       $"old recommendation: \"{old.Recommendation}\"";
        }
        if (it.Evaluation is null)
        {
            it.Evaluation = new InternshipEvaluation { Recommendation = recommendation, Total = total, EvaluatorId = me.UserId, EvaluatedAt = clock.UtcNow, Scores = scores };
        }
        else
        {
            it.Evaluation.Scores.Clear();
            foreach (var s in scores) it.Evaluation.Scores.Add(s);
            it.Evaluation.Total = total;
            it.Evaluation.Recommendation = recommendation;
            it.Evaluation.EvaluatorId = me.UserId;
            it.Evaluation.EvaluatedAt = clock.UtcNow;
        }

        await audit.LogAsync("evaluation.submit", "InternshipApplication", id, $"Total {total}/100{replaced}");
        await uow.SaveChangesAsync();
        return await DetailAsync(await LoadAsync(id));
    }

    /// <summary>cl.10: the mentor or department scores the month's report. Payment waits for this.</summary>
    public async Task<ApplicationDetailDto> EvaluateMonthAsync(long id, string month, EvaluateMonthRequest r)
    {
        var app = await LoadAsync(id);
        var bill = app.Internship!.Bills.FirstOrDefault(b => b.Month == month)
            ?? throw new BadRequestException("The intern has not submitted this month's report.");
        if (bill.PaidAt is not null) throw new BadRequestException("This month is already paid; its evaluation is final.");
        // cl.10: attendance, conduct, discipline and work skill, each 0–25.
        var parts = new (string Label, int? Value)[] { ("Attendance", r.Attendance), ("Conduct", r.Conduct), ("Discipline", r.Discipline), ("Work skill", r.WorkSkill) };
        foreach (var (label, value) in parts)
            if (value is not { } v || v < 0 || v > AllowanceBill.MaxCriterionScore)
                throw new BadRequestException($"{label}: score must be 0–{AllowanceBill.MaxCriterionScore}.");
        var score = parts.Sum(p => p.Value!.Value);
        var view = InternshipRules.MonthlyBills(app.Internship, clock.Today, await setup.GetSnapshotAsync()).First(b => b.Month == month);
        if (r.AbsentDays is not { } absent || absent < 0 || absent > view.PeriodDays)
            throw new BadRequestException($"Absent days must be 0–{view.PeriodDays} (the month's working days in the internship).");
        var note = r.Note?.Trim();
        if (string.IsNullOrWhiteSpace(note)) throw new BadRequestException("Write a short note on the month's work.");
        if (note.Length > AllowanceBill.MaxEvaluationNoteLength)
            throw new BadRequestException($"The note can have at most {AllowanceBill.MaxEvaluationNoteLength} characters.");

        var replaced = bill.EvaluatedAt is null ? "" : $"; replaced {bill.Score}/100, {bill.AbsentDays?.ToString() ?? "?"} absent, by {bill.EvaluatedByName} on {bill.EvaluatedAt:yyyy-MM-dd}: \"{bill.EvaluationNote}\"";
        bill.Score = score;
        bill.AttendanceScore = r.Attendance;
        bill.ConductScore = r.Conduct;
        bill.DisciplineScore = r.Discipline;
        bill.WorkSkillScore = r.WorkSkill;
        bill.AbsentDays = absent;
        bill.EvaluationNote = note;
        bill.EvaluatedAt = clock.UtcNow;
        bill.EvaluatedById = me.UserId;
        bill.EvaluatedByName = me.FullName;
        await audit.LogAsync("bill.evaluate", "InternshipApplication", id, $"{month}: {score}/100 (attendance {r.Attendance}, conduct {r.Conduct}, discipline {r.Discipline}, work skill {r.WorkSkill}), {absent} absent day(s) (register: {view.Attendance.Unapproved} unapproved){replaced}");
        await notify.NotifyAsync(app.StudentId, "Monthly report evaluated", $"Your report for {month} scored {score}/100.", Links.Student);
        await notify.NotifyHoldersAsync(Permissions.BillPay, "Allowance ready to pay", $"{app.Student.FullName}'s allowance for {month} is evaluated.", Links.Manage);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<ApplicationDetailDto> CompleteAsync(long id)
    {
        var app = await LoadAsync(id);
        if (app.Status != ApplicationStatus.Active) throw new BadRequestException("Only active internships can be marked complete.");
        var policy = (await setup.GetSnapshotAsync()).Policy;
        workflow.SetStatus(app, ApplicationStatus.Completed, "Internship period finished");
        await audit.LogAsync("intern.complete", "InternshipApplication", id, "Marked complete");
        await notify.NotifyAsync(app.StudentId, "Internship completed",
            $"Submit your final report within {policy.ReportWorkingDays} working days of the end date.", Links.Student);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<(ApplicationDetailDto, IssueOutcomeResult)> IssueOutcomeAsync(long id)
    {
        var app = await LoadAsync(id);
        var it = app.Internship!;
        if (app.Status != ApplicationStatus.Completed) throw new BadRequestException("Mark the internship complete first.");
        var snap = await setup.GetSnapshotAsync();
        var outcome = InternshipRules.ComputeOutcome(it, clock.Today, snap);
        if (!outcome.Ready) throw new BadRequestException(outcome.Reasons.FirstOrDefault() ?? "Evaluation and an accepted report are required.");

        IssueOutcomeResult result;
        if (outcome.Certifiable)
        {
            var seq = await uow.Applications.CountCertificatesInBatchAsync(app.BatchId) + 1;
            it.CertificateNumber = InternshipRules.CertificateNumber(app.Batch.Code, seq);
            it.CertificateGrade = outcome.Grade;
            it.CertificateIssuedAt = clock.UtcNow;
            workflow.SetStatus(app, ApplicationStatus.Certified, $"Grade: {outcome.Grade}");
            await audit.LogAsync("certificate.issue", "InternshipApplication", id, $"{it.CertificateNumber} — {outcome.Grade}");
            await notify.NotifyAsync(app.StudentId, "Certificate issued", $"Your UGC internship certificate ({outcome.Grade}) is ready.", Links.Student);
            result = new IssueOutcomeResult(true, outcome.Grade, []);
        }
        else
        {
            it.OutcomeResult = ApplicationStatus.NotCertified.Label();
            it.OutcomeReasons = string.Join('\n', outcome.Reasons);
            workflow.SetStatus(app, ApplicationStatus.NotCertified, string.Join(' ', outcome.Reasons));
            await audit.LogAsync("certificate.deny", "InternshipApplication", id, string.Join(' ', outcome.Reasons));
            await notify.NotifyAsync(app.StudentId, "Internship outcome", "The required standard was not met, so no certificate is issued.", Links.Student);
            result = new IssueOutcomeResult(false, null, outcome.Reasons);
        }

        await uow.SaveChangesAsync();
        return (await DetailAsync(app), result);
    }

    public async Task<ApplicationDetailDto> TerminateAsync(long id, ReasonRequest r)
    {
        var app = await LoadAsync(id);
        if (app.Status != ApplicationStatus.Active) throw new BadRequestException("Only active internships can be terminated.");
        var reason = r.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason)) throw new BadRequestException("Give the reason (cl.11 ঠ).");
        app.Internship!.OutcomeResult = ApplicationStatus.Terminated.Label();
        app.Internship.OutcomeReasons = reason;
        workflow.SetStatus(app, ApplicationStatus.Terminated, reason);
        await audit.LogAsync("intern.terminate", "InternshipApplication", id, reason);
        await notify.NotifyAsync(app.StudentId, "Internship terminated", reason, Links.Student);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    // ---------- helpers ----------

    /// <summary>Null when the caller sees every intern; their own id when they see only the interns they mentor.</summary>
    private async Task<long?> MentorScopeAsync()
    {
        if (await permissions.HasAsync(Permissions.InternViewAll)) return null;
        if (await permissions.HasAsync(Permissions.InternViewAssigned)) return me.RequireUserId();
        throw new ForbiddenException("You do not have permission to view interns.");
    }

    private async Task<InternshipApplication> LoadAsync(long id)
    {
        var app = await uow.Applications.GetDetailAsync(id);
        if (app?.Internship is null) throw new NotFoundException("Intern", id);
        var scope = await MentorScopeAsync();
        if (scope is { } mentorId && app.Internship.MentorId != mentorId) throw new ForbiddenException("This intern is not assigned to you.");
        return app;
    }

    private async Task<ApplicationDetailDto> DetailAsync(InternshipApplication app) =>
        InternshipMapper.Detail(app, await setup.GetSnapshotAsync(), clock.Today, await InternshipMapper.AttachedAsync(uow, app.Id));
}
