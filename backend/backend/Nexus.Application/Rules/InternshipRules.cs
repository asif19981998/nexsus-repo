using System.Text.RegularExpressions;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Internships;
using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Enums;

namespace Nexus.Application.Rules;

/// <summary>
/// The policy numbers and lookup tables one calculation needs, loaded from the database.
/// <see cref="Holidays"/> are the declared public holidays: like the weekend, they are not working days.
/// </summary>
public sealed record PolicySnapshot(
    InternshipPolicy Policy,
    IReadOnlyList<EvaluationCriterion> Criteria,
    IReadOnlyList<GradeBand> GradeBands,
    IReadOnlySet<DateOnly> Holidays);

public sealed record EligibilityRule(string Id, string Clause, string Label, bool Met, string Detail, string? Group);
public sealed record EligibilityResult(bool Eligible, string? Basis, IReadOnlyList<EligibilityRule> Rules);

public sealed record DayState(string Kind, string Label, bool Deduct, bool Present);

/// <summary>The month's register so far: closed working days, and how each was marked. Unapproved absences are what usually get deducted.</summary>
public sealed record MonthAttendance(int ClosedDays, int Present, int Approved, int Unapproved);

/// <summary>One working day of the month's attendance register, which goes with the bill (cl.11 জ).</summary>
public sealed record RegisterDay(DateOnly Date, string Kind, string Label, TimeOnly? CheckIn, TimeOnly? CheckOut, string? Reason);

/// <summary>cl.10 monthly review, each 0–25. Null on months evaluated before the four criteria existed.</summary>
public sealed record MonthlyScores(int Attendance, int Conduct, int Discipline, int WorkSkill);

public sealed record AttendanceSummary(int Required, int Present, int Approved, int Unapproved, int Pct, bool BelowMin);

public sealed record LeaveRequestView(DateOnly Date, string? Reason, string Status);

/// <summary>
/// One month of the allowance. <see cref="DeductDays"/>, <see cref="Deduction"/> and <see cref="Net"/> stay null
/// until the mentor evaluates the month and enters the absent days; <see cref="Attendance"/> is the register to check them against.
/// </summary>
public sealed record BillView(
    string Month, int PeriodDays, int? DeductDays, int DailyRate, int Gross, int? Deduction, int? Net,
    MonthAttendance Attendance, IReadOnlyList<RegisterDay> Register, MonthlyScores? Scores,
    string Status, bool IsLast, string? WithheldReason,
    string? Report, DateTime? SubmittedAt,
    int? Score, string? EvaluationNote, string? EvaluatedBy, DateTime? EvaluatedAt,
    DateTime? PaidAt, string? PaidBy, string? SlipNumber);

/// <summary>Monthly allowance statuses, in the order a month moves through them (cl.10).</summary>
public static class BillStatus
{
    public const string Accruing = "Accruing";
    public const string ReportDue = "Report due";
    public const string AwaitingEvaluation = "Awaiting evaluation";
    public const string Withheld = "Withheld";
    public const string ReadyToPay = "Ready to pay";
    public const string Paid = "Paid";
}

public sealed record OutcomeView(bool Ready, bool Certifiable, string? Grade, int? Total, IReadOnlyList<string> Reasons, AttendanceSummary Attendance);

public sealed record BatchCheck(IReadOnlyList<string> Errors, IReadOnlyList<string> NeedsOverride, int? Year);

/// <summary>
/// The UGC Internship Policy as pure functions: no database, no clock. Ported from the prototype's
/// src/lib/ugcInternship.js so both give the same numbers. Clause references are to the Bengali policy.
/// </summary>
public static partial class InternshipRules
{
    public static readonly ApplicationStatus[] SeatStatuses =
        [ApplicationStatus.Selected, ApplicationStatus.Active, ApplicationStatus.Completed, ApplicationStatus.Certified, ApplicationStatus.NotCertified];

    public static readonly ApplicationStatus[] LiveStatuses =
        [ApplicationStatus.Submitted, ApplicationStatus.Shortlisted, ApplicationStatus.Selected, ApplicationStatus.Waitlisted, ApplicationStatus.Active, ApplicationStatus.Completed];

    /// <summary>Statuses that mean the student already had a UGC internship (cl.8: not renewable).</summary>
    public static readonly ApplicationStatus[] DoneStatuses =
        [ApplicationStatus.Active, ApplicationStatus.Completed, ApplicationStatus.Certified, ApplicationStatus.NotCertified, ApplicationStatus.Terminated];

    public static readonly ApplicationStatus[] InternStatuses =
        [ApplicationStatus.Active, ApplicationStatus.Completed, ApplicationStatus.Certified, ApplicationStatus.NotCertified, ApplicationStatus.Terminated];

    public static readonly ApplicationStatus[] WithdrawableStatuses =
        [ApplicationStatus.Submitted, ApplicationStatus.Shortlisted, ApplicationStatus.Waitlisted, ApplicationStatus.Selected];

    // ---------- dates ----------

    /// <summary>Bangladesh government week: Friday and Saturday are the weekend.</summary>
    public static bool IsWeekend(DateOnly d) => d.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;

    /// <summary>A working day is neither a weekend day nor a declared holiday. Web and mobile attendance both count these.</summary>
    public static bool IsWorkingDay(DateOnly d, IReadOnlySet<DateOnly> holidays) => !IsWeekend(d) && !holidays.Contains(d);

    public static List<DateOnly> WorkingDaysBetween(DateOnly from, DateOnly to, IReadOnlySet<DateOnly> holidays)
    {
        var list = new List<DateOnly>();
        for (var d = from; d <= to; d = d.AddDays(1))
            if (IsWorkingDay(d, holidays)) list.Add(d);
        return list;
    }

    public static DateOnly AddWorkingDays(DateOnly d, int n, IReadOnlySet<DateOnly> holidays)
    {
        var cur = d;
        while (n > 0)
        {
            cur = cur.AddDays(1);
            if (IsWorkingDay(cur, holidays)) n--;
        }
        return cur;
    }

    /// <summary>Inclusive end date of a period of whole months: 11 Jan + 3 months ends 10 Apr.</summary>
    public static DateOnly EndDateFor(DateOnly start, int months) => start.AddMonths(months).AddDays(-1);

    public static string MonthKey(DateOnly d) => $"{d.Year:D4}-{d.Month:D2}";

    public static List<string> MonthsInRange(DateOnly from, DateOnly to)
    {
        var list = new List<string>();
        var cur = new DateOnly(from.Year, from.Month, 1);
        var last = new DateOnly(to.Year, to.Month, 1);
        for (; cur <= last; cur = cur.AddMonths(1)) list.Add(MonthKey(cur));
        return list;
    }

    private static (DateOnly First, DateOnly Last) MonthBounds(string key)
    {
        var y = int.Parse(key[..4]);
        var m = int.Parse(key[5..7]);
        var first = new DateOnly(y, m, 1);
        return (first, first.AddMonths(1).AddDays(-1));
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    // ---------- eligibility (cl.7, cl.8) ----------

    [GeneratedRegex(@"^(m\.?sc|m\.?s\b|mba|m\.?a\b|m\.?phil|ph\.?d|master)", RegexOptions.IgnoreCase)]
    private static partial Regex PostgraduateProgramme();

    public static EligibilityResult CheckEligibility(AcademicSnapshot? profile, IEnumerable<ApplicationStatus> ownStatuses, DateOnly today, InternshipPolicy policy)
    {
        var statuses = ownStatuses.ToList();
        var programme = profile?.Programme ?? "";
        var postgraduate = profile?.Level == AcademicLevel.Postgraduate || PostgraduateProgramme().IsMatch(programme);
        var finalYear = profile is { Level: AcademicLevel.Undergraduate, CurrentYear: >= 4 };

        int? since = profile?.GraduationMonth is { } g ? (today.Year - g.Year) * 12 + (today.Month - g.Month) : null;
        var recentGrad = since is >= 0 && since <= policy.GraduateWindowMonths;

        var rules = new List<EligibilityRule>
        {
            new("academic-a", "cl.7 (ক)", "4th-year undergraduate or postgraduate student", finalYear || postgraduate,
                postgraduate ? $"Postgraduate: {programme}"
                : finalYear ? $"Year {profile!.CurrentYear} of {(string.IsNullOrWhiteSpace(programme) ? "university programme" : programme)}"
                : "Profile shows a lower year or no university programme.",
                "academic"),
            new("academic-b", "cl.7 (খ, গ)", $"Finished a bachelor's/master's within the last {policy.GraduateWindowMonths} months", recentGrad,
                since is null ? "No graduation date on the profile."
                : since < 0 ? "Graduation is still ahead."
                : $"Graduated about {since} month{(since == 1 ? "" : "s")} ago.",
                "academic"),
            new("identity", "Nexus rule", "Identity verified on Nexus", profile?.IdentityVerified == true,
                $"Identity status: {(profile?.IdentityVerified == true ? "Verified" : "Not verified")}", null),
            new("not-done", "cl.8", "No earlier UGC internship (not renewable)", !statuses.Any(s => DoneStatuses.Contains(s)),
                "One UGC internship per person.", null),
            new("no-live", "Nexus rule", "No other application in progress", !statuses.Any(s => LiveStatuses.Contains(s)),
                "One live application at a time.", null),
        };

        var academicOk = rules.Where(r => r.Group == "academic").Any(r => r.Met);
        var eligible = academicOk && rules.Where(r => r.Group is null).All(r => r.Met);
        var basis = rules.FirstOrDefault(r => r.Group == "academic" && r.Met)?.Clause;
        return new EligibilityResult(eligible, basis, rules);
    }

    // ---------- batches (cl.6, cl.8) ----------

    public sealed record BatchDraft(string? Name, int? Capacity, int? DurationMonths, DateOnly? ApplyFrom, DateOnly? ApplyTo, DateOnly? StartDate);

    /// <param name="batchesInYear">Non-cancelled batches already in the start date's year.</param>
    public static BatchCheck ValidateNewBatch(BatchDraft d, Func<int, int> batchesInYear, InternshipPolicy policy)
    {
        var errors = new List<string>();
        var needs = new List<string>();
        if (string.IsNullOrWhiteSpace(d.Name)) errors.Add("Batch name is required.");
        if (d.StartDate is null) errors.Add("Start date is required.");
        if (d.ApplyFrom is null || d.ApplyTo is null) errors.Add("Application window is required.");
        else if (d.ApplyFrom > d.ApplyTo) errors.Add("Application window ends before it starts.");
        if (d.ApplyTo is not null && d.StartDate is not null && d.ApplyTo >= d.StartDate) errors.Add("Applications must close before the start date.");
        if (d.DurationMonths is null || d.DurationMonths < policy.MinMonths || d.DurationMonths > policy.MaxMonths)
            errors.Add($"Duration must be {policy.MinMonths}–{policy.MaxMonths} months (cl.8).");
        if (d.Capacity is null || d.Capacity < 1) errors.Add("Capacity must be a whole number of at least 1.");
        else if (d.Capacity > policy.BatchSize) needs.Add($"Capacity {d.Capacity} is above the {policy.BatchSize}-seat limit (cl.6 ক).");

        int? year = d.StartDate?.Year;
        if (year is { } y)
        {
            var inYear = batchesInYear(y);
            if (inYear >= policy.MaxBatchesPerYear) needs.Add($"{y} already has {inYear} batches; the limit is {policy.MaxBatchesPerYear} (cl.6 খ).");
        }
        return new BatchCheck(errors, needs, year);
    }

    // ---------- attendance (cl.11) ----------

    /// <summary>Working days of the internship that are already closed (before today and not after the end date).</summary>
    public static List<DateOnly> ClosedDays(Internship it, DateOnly today, IReadOnlySet<DateOnly> holidays)
    {
        var upto = Min(it.EndDate, today.AddDays(-1));
        return upto < it.StartDate ? [] : WorkingDaysBetween(it.StartDate, upto, holidays);
    }

    public static DayState DayStatus(AttendanceRecord? rec, DateOnly date, DateOnly today)
    {
        if (rec is not null)
        {
            switch (rec.Status)
            {
                case AttendanceStatus.Present: return new("present", "Present", false, true);
                case AttendanceStatus.LeaveRequested: return new("leave-requested", "Leave requested", date < today, false);
                case AttendanceStatus.Leave: return new("leave", "Approved leave", false, false);
                case AttendanceStatus.Absent:
                    return rec.Approved ? new("absent-approved", "Absent (approved)", false, false) : new("absent", "Absent", true, false);
            }
        }
        return date < today ? new("absent-no-signin", "Absent (no sign-in)", true, false) : new("open", "Open", false, false);
    }

    /// <summary>
    /// Minutes after the shift start the intern signed in (0 when on time); null on a day without a sign-in.
    /// Shown only: lateness is never deducted.
    /// </summary>
    public static int? LateMinutes(AttendanceRecord? rec, InternshipPolicy policy) =>
        rec is { Status: AttendanceStatus.Present, CheckIn: { } t }
            ? Math.Max(0, (int)Math.Round((t.ToTimeSpan() - policy.ShiftStart.ToTimeSpan()).TotalMinutes))
            : null;

    public static AttendanceSummary Summary(Internship it, DateOnly today, PolicySnapshot p)
    {
        var policy = p.Policy;
        var byDate = it.Attendance.ToDictionary(a => a.Date);
        var days = ClosedDays(it, today, p.Holidays);
        int present = 0, approved = 0, unapproved = 0;
        foreach (var d in days)
        {
            var s = DayStatus(byDate.GetValueOrDefault(d), d, today);
            if (s.Present) present++;
            else if (s.Deduct) unapproved++;
            else approved++;
        }

        // Today counts once the intern has signed in.
        var required = days.Count;
        if (byDate.TryGetValue(today, out var t) && t.Status == AttendanceStatus.Present
            && today >= it.StartDate && today <= it.EndDate && IsWorkingDay(today, p.Holidays))
        {
            present++;
            required++;
        }

        var pct = required > 0 ? (int)Math.Round(present * 100.0 / required, MidpointRounding.AwayFromZero) : 100;
        return new AttendanceSummary(required, present, approved, unapproved, pct, required > 0 && pct < policy.MinAttendancePct);
    }

    /// <summary>Every leave the intern asked for, newest first. Status: Pending, Approved or Rejected.</summary>
    public static List<LeaveRequestView> LeaveRequests(Internship it) =>
        it.Attendance
            .Select(r => r.Status switch
            {
                AttendanceStatus.LeaveRequested => new LeaveRequestView(r.Date, r.Reason, "Pending"),
                AttendanceStatus.Leave => new LeaveRequestView(r.Date, r.Reason, "Approved"),
                AttendanceStatus.Absent when r.Note == "Leave rejected" => new LeaveRequestView(r.Date, r.Reason, "Rejected"),
                _ => null,
            })
            .OfType<LeaveRequestView>()
            .OrderByDescending(l => l.Date)
            .ToList();

    // ---------- allowance (cl.10, cl.11) ----------

    /// <remarks>Holidays are not working days, so they neither earn nor lose a day's allowance.</remarks>
    public static List<BillView> MonthlyBills(Internship it, DateOnly today, PolicySnapshot p)
    {
        var policy = p.Policy;
        var holidays = p.Holidays;
        var byDate = it.Attendance.ToDictionary(a => a.Date);
        var records = it.Bills.ToDictionary(b => b.Month);
        var keys = MonthsInRange(it.StartDate, it.EndDate);
        var lastKey = keys[^1];
        var reportOk = it.Report?.Status == ReportStatus.Accepted;
        var allowance = (double)policy.MonthlyAllowance;

        // Months appear one at a time: a month is listed once it has started.
        return keys.Where(key => Max(it.StartDate, MonthBounds(key).First) <= today).Select(key =>
        {
            var (first, last) = MonthBounds(key);
            var monthDays = WorkingDaysBetween(first, last, holidays).Count;
            var from = Max(it.StartDate, first);
            var to = Min(it.EndDate, last);
            var periodDays = WorkingDaysBetween(from, to, holidays);
            var dailyRate = monthDays > 0 ? allowance / monthDays : 0;
            var gross = (int)Math.Round(allowance * (monthDays > 0 ? (double)periodDays.Count / monthDays : 0), MidpointRounding.AwayFromZero);

            int present = 0, approved = 0, unapproved = 0;
            var closed = periodDays.Where(d => d < today).ToList();
            foreach (var d in closed)
            {
                var s = DayStatus(byDate.GetValueOrDefault(d), d, today);
                if (s.Present) present++;
                else if (s.Deduct) unapproved++;
                else approved++;
            }
            var register = periodDays.Where(d => d <= today).Select(d =>
            {
                var rec = byDate.GetValueOrDefault(d);
                var s = DayStatus(rec, d, today);
                return new RegisterDay(d, s.Kind, s.Label, rec?.CheckIn, rec?.CheckOut, rec?.Reason);
            }).ToList();

            var record = records.GetValueOrDefault(key);
            MonthlyScores? scores = record is { AttendanceScore: { } sa, ConductScore: { } sc, DisciplineScore: { } sd, WorkSkillScore: { } sw }
                ? new MonthlyScores(sa, sc, sd, sw)
                : null;
            // The mentor enters the absent days to deduct when evaluating the month. Bills evaluated or paid before
            // that existed fall back to the register's unapproved absences.
            int? deductDays = record?.AbsentDays ?? (record?.EvaluatedAt is not null || record?.PaidAt is not null ? unapproved : null);
            int? deduction = deductDays is { } dd ? (int)Math.Round(dd * dailyRate, MidpointRounding.AwayFromZero) : null;
            // Once paid, the amount is what was paid.
            int? net = record?.PaidAmount ?? (deduction is { } ded ? Math.Max(0, gross - ded) : null);
            var periodOver = to < today;
            var isLast = key == lastKey;

            // Month ends → intern submits the month's report → mentor evaluates → paid with a slip.
            string status;
            if (record?.PaidAt is not null) status = BillStatus.Paid;
            else if (record is null) status = periodOver ? BillStatus.ReportDue : BillStatus.Accruing;
            else if (record.EvaluatedAt is null) status = BillStatus.AwaitingEvaluation;
            else if (isLast && !reportOk) status = BillStatus.Withheld;
            else status = BillStatus.ReadyToPay;

            return new BillView(key, periodDays.Count, deductDays, (int)Math.Round(dailyRate, MidpointRounding.AwayFromZero), gross, deduction, net,
                new MonthAttendance(closed.Count, present, approved, unapproved), register, scores, status, isLast,
                isLast && !reportOk && record?.PaidAt is null ? "Last month is released only after the final report is accepted (cl.11 ট)." : null,
                record?.Report, record?.SubmittedAt,
                record?.Score, record?.EvaluationNote, record?.EvaluatedByName, record?.EvaluatedAt,
                record?.PaidAt, record?.PaidByName, record?.SlipNumber);
        }).ToList();
    }

    // ---------- report, evaluation, certificate (cl.11, cl.14, cl.15) ----------

    public static DateOnly ReportDeadline(Internship it, PolicySnapshot p) => AddWorkingDays(it.EndDate, p.Policy.ReportWorkingDays, p.Holidays);

    /// <summary>
    /// cl.14: why the department cannot evaluate yet, or null when it can.
    /// <see cref="InternshipPolicy.EvaluationAfterReport"/> picks the trigger: the final report, or the end of the period.
    /// </summary>
    public static string? EvaluationBlockedReason(Internship it, DateOnly today, InternshipPolicy policy)
    {
        if (policy.EvaluationAfterReport)
            return it.Report is null ? "The department evaluates after the intern submits the final report (cl.14)." : null;
        return today <= it.EndDate ? $"The department evaluates after the internship ends on {it.EndDate:dd MMM yyyy} (cl.14)." : null;
    }

    public static string? GradeFromScore(int total, IReadOnlyList<GradeBand> bands) =>
        bands.OrderByDescending(b => b.MinScore).FirstOrDefault(b => total >= b.MinScore)?.Grade;

    public static OutcomeView ComputeOutcome(Internship it, DateOnly today, PolicySnapshot p)
    {
        var reasons = new List<string>();
        var att = Summary(it, today, p);
        if (att.BelowMin) reasons.Add($"Attendance {att.Pct}% is below {p.Policy.MinAttendancePct}% (cl.11 ঞ).");
        if (it.Report?.Status != ReportStatus.Accepted) reasons.Add("Final report is not accepted yet (cl.11 ট).");
        if (it.Evaluation is null) reasons.Add("Department evaluation is missing (cl.14).");

        int? total = it.Evaluation?.Total;
        var grade = total is { } t ? GradeFromScore(t, p.GradeBands) : null;
        if (total is not null && grade is null)
        {
            var pass = p.GradeBands.Count > 0 ? p.GradeBands.Min(b => b.MinScore) : 0;
            reasons.Add($"Evaluation score {total} is below the pass mark of {pass}.");
        }

        var ready = it.Evaluation is not null && it.Report?.Status == ReportStatus.Accepted;
        var certifiable = ready && !att.BelowMin && grade is not null;
        return new OutcomeView(ready, certifiable, grade, total, reasons, att);
    }

    public static string CertificateNumber(string batchCode, int seq) => $"{batchCode}-{seq:D3}";
}
