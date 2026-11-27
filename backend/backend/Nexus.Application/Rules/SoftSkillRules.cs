using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Application.Rules;

/// <summary>
/// The policy numbers and lookups one Soft Skill calculation needs, loaded from the database.
/// <see cref="Holidays"/> are declared public holidays; with the weekend they are not working days (cl.11 ঝ).
/// </summary>
public sealed record SoftSkillSnapshot(
    SoftSkillPolicy Policy,
    IReadOnlyList<SoftSkillTopic> Topics,
    IReadOnlyList<SoftSkillGradeBand> GradeBands,
    IReadOnlySet<DateOnly> Holidays);

/// <summary>What the Soft Skill cl.7 check reads from the person's main education line.</summary>
public sealed record SoftSkillAcademic(
    EducationLevel? Level, EducationStatus? Status, int? CurrentYear, string? Programme,
    long? UniversityId, string? University, bool UniversityIsPublic, bool IdentityVerified);

/// <summary>
/// cl.11 (ছ) attendance over the sessions already held. Hours, because sessions differ in length.
/// <see cref="Pct"/> is 100 while nothing has been held.
/// </summary>
public sealed record SoftSkillAttendanceSummary(
    int HeldSessions, decimal HeldHours, decimal AttendedHours, int Present, int Leave, int Absent, int Pct, bool BelowMin);

/// <summary>One month's stipend (cl.10), worked out from the assessment, the month's attendance and the finance record.</summary>
public sealed record SoftSkillStipendView(
    int No, string Status, int Amount, string? Reason, decimal? Marks, int? MaxMarks, int MonthAttendancePct,
    DateTime? PaidAt, string? PaidBy, string? SlipNumber);

public static class StipendStatus
{
    public const string AwaitingAssessment = "Awaiting assessment";
    public const string NotEligible = "Not eligible";
    public const string Withheld = "Withheld";
    public const string ReadyToPay = "Ready to pay";
    public const string Paid = "Paid";
}

/// <summary>
/// cl.15 outcome. <see cref="Pending"/>: what is still missing before it can be issued.
/// <see cref="Reasons"/>: why the trainee would not be certified.
/// </summary>
public sealed record SoftSkillOutcome(
    bool Ready, bool Certifiable, string? Grade, decimal? Average, IReadOnlyList<string> Pending, IReadOnlyList<string> Reasons);

public sealed record SoftSkillBatchCheck(IReadOnlyList<string> Errors, IReadOnlyList<string> NeedsOverride, int? Year, DateOnly? EndDate);

/// <summary>
/// The UGC Soft Skill Training Policy as pure functions: no database, no clock. Clause references are to the Bengali policy.
/// </summary>
public static class SoftSkillRules
{
    public const int AssessmentCount = 3;   // cl.14 (ক)

    public static readonly ApplicationStatus[] SeatStatuses =
        [ApplicationStatus.Selected, ApplicationStatus.Active, ApplicationStatus.Completed, ApplicationStatus.Certified, ApplicationStatus.NotCertified];

    public static readonly ApplicationStatus[] LiveStatuses =
        [ApplicationStatus.Submitted, ApplicationStatus.Shortlisted, ApplicationStatus.Selected, ApplicationStatus.Waitlisted, ApplicationStatus.Active, ApplicationStatus.Completed];

    /// <summary>cl.7 (ঘ): statuses that mean the student already took part in this training.</summary>
    public static readonly ApplicationStatus[] DoneStatuses =
        [ApplicationStatus.Active, ApplicationStatus.Completed, ApplicationStatus.Certified, ApplicationStatus.NotCertified, ApplicationStatus.Terminated];

    public static readonly ApplicationStatus[] WithdrawableStatuses =
        [ApplicationStatus.Submitted, ApplicationStatus.Shortlisted, ApplicationStatus.Waitlisted, ApplicationStatus.Selected];

    public static readonly ApplicationStatus[] PendingStatuses =
        [ApplicationStatus.Submitted, ApplicationStatus.Shortlisted, ApplicationStatus.Waitlisted];

    /// <summary>Applications whose trainee belongs on the university group's lists (a selected student who withdrew does not).</summary>
    public static readonly ApplicationStatus[] TraineeStatuses =
        [ApplicationStatus.Selected, ApplicationStatus.Active, ApplicationStatus.Completed, ApplicationStatus.Certified, ApplicationStatus.NotCertified, ApplicationStatus.Terminated];

    // ---------- eligibility (cl.7) ----------

    public static EligibilityResult CheckEligibility(SoftSkillAcademic? a, IEnumerable<ApplicationStatus> ownStatuses, SoftSkillPolicy policy)
    {
        var statuses = ownStatuses.ToList();
        var studying = a is { Level: EducationLevel.Bachelor, Status: EducationStatus.Studying };
        var yearOk = studying && a!.CurrentYear is { } y && y >= policy.MinStudyYear && y <= policy.MaxStudyYear;
        var years = policy.MinStudyYear == policy.MaxStudyYear ? $"year {policy.MinStudyYear}" : $"year {policy.MinStudyYear}–{policy.MaxStudyYear}";

        var rules = new List<EligibilityRule>
        {
            new("year", "cl.7 (ক)", $"Regular undergraduate student in {years}", yearOk,
                a is null ? "No main education line on the profile."
                : !studying ? "The main education line is not a bachelor's degree you are still studying."
                : $"Year {a.CurrentYear?.ToString() ?? "?"} of {(string.IsNullOrWhiteSpace(a.Programme) ? "a bachelor's programme" : a.Programme)}",
                null),
            new("public", "cl.7 (ক, ঙ)", "Student of a public university", a?.UniversityIsPublic == true,
                a?.UniversityId is null ? "Choose your university from the list on your profile."
                : a.UniversityIsPublic ? a.University! : $"{a.University} is not a public university.",
                null),
            new("identity", "Nexus rule", "Identity verified on Nexus", a?.IdentityVerified == true,
                $"Identity status: {(a?.IdentityVerified == true ? "Verified" : "Not verified")}", null),
            new("not-done", "cl.7 (ঘ)", "Has not taken this training before", !statuses.Any(s => DoneStatuses.Contains(s)),
                "One Soft Skill training per person; it is not renewable (cl.8).", null),
            new("no-live", "Nexus rule", "No other Soft Skill application in progress", !statuses.Any(s => LiveStatuses.Contains(s)),
                "One live application at a time.", null),
        };
        var eligible = rules.All(r => r.Met);
        var basis = eligible ? $"Year {a!.CurrentYear}, {a.University}" : null;
        return new EligibilityResult(eligible, basis, rules);
    }

    // ---------- batches (cl.6, cl.8) ----------

    public sealed record BatchDraft(string? Name, DateOnly? ApplyFrom, DateOnly? ApplyTo, DateOnly? StartDate);

    /// <param name="batchesInYear">Non-cancelled batches already in the start date's year.</param>
    public static SoftSkillBatchCheck ValidateNewBatch(BatchDraft d, Func<int, int> batchesInYear, SoftSkillPolicy policy)
    {
        var errors = new List<string>();
        var needs = new List<string>();
        if (string.IsNullOrWhiteSpace(d.Name)) errors.Add("Batch name is required.");
        if (d.StartDate is null) errors.Add("Start date is required.");
        if (d.ApplyFrom is null || d.ApplyTo is null) errors.Add("Application window is required.");
        else if (d.ApplyFrom > d.ApplyTo) errors.Add("Application window ends before it starts.");
        if (d.ApplyTo is not null && d.StartDate is not null && d.ApplyTo >= d.StartDate) errors.Add("Applications must close before the start date.");

        int? year = d.StartDate?.Year;
        if (year is { } y)
        {
            var inYear = batchesInYear(y);
            if (inYear >= policy.MaxBatchesPerYear) needs.Add($"{y} already has {inYear} batches; the limit is {policy.MaxBatchesPerYear} (cl.6 খ).");
        }
        DateOnly? end = d.StartDate is { } s ? InternshipRules.EndDateFor(s, policy.DurationMonths) : null;
        return new SoftSkillBatchCheck(errors, needs, year, end);
    }

    // ---------- attendance (cl.11 চ, ছ) ----------

    /// <summary>True when the record counts as attending the session.</summary>
    public static bool Attended(SoftSkillAttendance? r, SoftSkillPolicy policy) =>
        r is not null && (r.Status == AttendanceStatus.Present || (r.Status == AttendanceStatus.Leave && policy.ApprovedLeaveCountsAsPresent));

    /// <param name="heldSessions">The university group's held sessions to count, e.g. all of them, or one month's.</param>
    public static SoftSkillAttendanceSummary Attendance(SoftSkillTrainee t, IEnumerable<SoftSkillSession> heldSessions, SoftSkillPolicy policy)
    {
        var sessions = heldSessions.Where(s => s.Status == SessionStatus.Held).ToList();
        var byId = t.Attendance.ToDictionary(a => a.SessionId);
        decimal held = 0, attended = 0;
        int present = 0, leave = 0, absent = 0;
        foreach (var s in sessions)
        {
            held += s.Hours;
            var r = byId.GetValueOrDefault(s.Id);
            if (Attended(r, policy)) attended += s.Hours;
            switch (r?.Status)
            {
                case AttendanceStatus.Present: present++; break;
                case AttendanceStatus.Leave: leave++; break;
                default: absent++; break;   // absent, a leave request never approved, or not marked
            }
        }
        var pct = held == 0 ? 100 : (int)Math.Floor(attended * 100 / held);
        return new SoftSkillAttendanceSummary(sessions.Count, held, attended, present, leave, absent, pct, held > 0 && pct < policy.MinAttendancePct);
    }

    /// <summary>The sessions of month <paramref name="no"/>: those on the topics its assessment covers (cl.14 ক).</summary>
    public static IEnumerable<SoftSkillSession> MonthSessions(SoftSkillUniversityGroup c, int no) => c.Sessions.Where(s => s.Topic.AssessmentNo == no);

    // ---------- assessments (cl.14) ----------

    /// <summary>Percent of the maximum; absent or malpractice is zero.</summary>
    public static decimal Percent(SoftSkillAssessmentResult r, SoftSkillAssessment a) =>
        r.Absent || r.Malpractice || r.Marks is not { } m || a.MaxMarks <= 0 ? 0 : Math.Round(m * 100 / a.MaxMarks, 2);

    public static SoftSkillAssessmentResult? ResultFor(SoftSkillTrainee t, SoftSkillAssessment? a) =>
        a is null ? null : t.Results.FirstOrDefault(r => r.AssessmentId == a.Id);

    // ---------- stipend (cl.10) ----------

    public static List<SoftSkillStipendView> Stipends(SoftSkillTrainee t, SoftSkillUniversityGroup c, SoftSkillPolicy policy)
    {
        var list = new List<SoftSkillStipendView>();
        for (var no = 1; no <= AssessmentCount; no++)
        {
            var row = t.Stipends.FirstOrDefault(s => s.AssessmentNo == no);
            var a = c.Assessments.FirstOrDefault(x => x.No == no);
            var r = ResultFor(t, a);
            var monthPct = Attendance(t, MonthSessions(c, no), policy).Pct;
            var amount = (int)policy.MonthlyStipend;

            if (row?.PaidAt is not null)
            {
                list.Add(new(no, StipendStatus.Paid, row.PaidAmount ?? amount, null, r?.Marks, a?.MaxMarks, monthPct, row.PaidAt, row.PaidByName, row.SlipNumber));
                continue;
            }
            string status;
            string? reason = null;
            if (t.CancelledAt is not null)
            {
                status = StipendStatus.NotEligible;
                reason = "Participation cancelled (cl.11 ঞ).";
            }
            else if (a is null || r is null)
            {
                status = StipendStatus.AwaitingAssessment;
            }
            else if (!r.PassedFor(a))
            {
                status = StipendStatus.NotEligible;
                reason = r.Absent ? "Absent from the assessment (cl.10 গ)."
                    : r.Malpractice ? "Unfair means in the assessment (cl.11 জ)."
                    : $"Failed the assessment: {r.Marks:0.##} of {a.MaxMarks}, pass mark {a.PassMarks} (cl.10 গ).";
            }
            else if (policy.StipendMinAttendancePct is { } min && monthPct < min)
            {
                status = StipendStatus.NotEligible;
                reason = $"Attendance this month {monthPct}% is below {min}% (cl.10 খ).";
            }
            else if (row?.WithheldReason is { } withheld)
            {
                status = StipendStatus.Withheld;
                reason = withheld;
            }
            else
            {
                status = StipendStatus.ReadyToPay;
            }
            var shown = status is StipendStatus.ReadyToPay or StipendStatus.Withheld or StipendStatus.AwaitingAssessment ? amount : 0;
            list.Add(new(no, status, shown, reason, r?.Marks, a?.MaxMarks, monthPct, null, null, null));
        }
        return list;
    }

    // ---------- learning report (cl.11 ঝ) ----------

    public static DateOnly ReportDeadline(DateOnly endDate, int workingDays, IReadOnlySet<DateOnly> holidays) =>
        InternshipRules.AddWorkingDays(endDate, workingDays, holidays);

    // ---------- outcome (cl.15) ----------

    public static SoftSkillOutcome Outcome(SoftSkillTrainee t, SoftSkillUniversityGroup c, DateOnly today, SoftSkillSnapshot s)
    {
        var policy = s.Policy;
        var pending = new List<string>();
        var reasons = new List<string>();

        if (t.CancelledAt is not null) reasons.Add($"Participation cancelled: {t.CancelReason}");

        var percents = new List<decimal>();
        for (var no = 1; no <= AssessmentCount; no++)
        {
            var a = c.Assessments.FirstOrDefault(x => x.No == no);
            var r = ResultFor(t, a);
            if (a is null || r is null) pending.Add($"Assessment {no} result not recorded.");
            else percents.Add(Percent(r, a));
        }
        decimal? average = percents.Count == AssessmentCount ? Math.Round(percents.Average(), 2) : null;

        var att = Attendance(t, c.Sessions, policy);
        if (today <= c.Batch.EndDate) pending.Add($"The training runs until {c.Batch.EndDate:dd MMM yyyy}.");
        if (att.BelowMin) reasons.Add($"Attendance {att.Pct}% is below {policy.MinAttendancePct}% (cl.11 ছ).");

        var deadline = ReportDeadline(c.Batch.EndDate, policy.ReportWorkingDays, s.Holidays);
        switch (t.Report?.Status)
        {
            case ReportStatus.Accepted:
                break;
            case null when today > deadline:
                reasons.Add($"No learning report or small project by {deadline:dd MMM yyyy} (cl.11 ঝ).");
                break;
            case null:
                pending.Add($"Learning report due by {deadline:dd MMM yyyy}.");
                break;
            default:
                pending.Add("The coordinator has not accepted the learning report yet.");
                break;
        }
        if (c.FinalReportAt is null) pending.Add("The university has not sent its final evaluation report (cl.14 ঙ).");

        string? grade = null;
        if (average is { } avg)
        {
            grade = s.GradeBands.Where(b => avg >= b.MinAverage).OrderByDescending(b => b.MinAverage).FirstOrDefault()?.Grade;
            if (grade is null)
            {
                var lowest = s.GradeBands.OrderBy(b => b.MinAverage).FirstOrDefault();
                reasons.Add($"Average {avg:0.##}% is below {(lowest is null ? "the pass level" : $"{lowest.Grade} ({lowest.MinAverage}%)")} (cl.15).");
            }
        }
        // A cancelled trainee can be closed at once; otherwise every pending item must be settled first.
        var ready = t.CancelledAt is not null || pending.Count == 0;
        var certifiable = ready && reasons.Count == 0 && grade is not null;
        return new SoftSkillOutcome(ready, certifiable, certifiable ? grade : null, average, pending, reasons);
    }

    public static string CertificateNumberFor(int year, long traineeId) => $"UGC-SSK-{year}-{traineeId:D5}";
}
