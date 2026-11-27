using Nexus.Application.Rules;

namespace Nexus.Application.Dtos.Internships;

// ---------- setup ----------

public sealed record PolicyDto(
    int BatchSize, int MaxBatchesPerYear, int MinMonths, int MaxMonths, decimal MonthlyAllowance,
    int MinAttendancePct, int ReportWorkingDays, int GraduateWindowMonths, int MaxPreferredDepartments,
    int MinStatementLength, int MinReportLength, int MinRecommendationLength, int MaxRecommendationLength,
    int MaxReviewNoteLength, bool EvaluationAfterReport,
    int MinMonthlyReportLength, int MaxMonthlyReportLength, int MaxMonthlyNoteLength);

public sealed record DepartmentDto(long Id, string Name);
public sealed record CriterionDto(long Id, string Key, string Label, int MaxScore);
public sealed record GradeBandDto(string Grade, string? NameBn, int Min);

public sealed record SetupDto(
    PolicyDto Policy,
    IReadOnlyList<DepartmentDto> Departments,
    IReadOnlyList<string> ExamModes,
    IReadOnlyList<CriterionDto> Criteria,
    IReadOnlyList<GradeBandDto> GradeBands,
    IReadOnlyList<string> BatchStatuses,
    IReadOnlyList<string> ApplicationStatuses,
    string Today);

// ---------- batches ----------

public sealed record SeatsDto(int Capacity, int Taken, int Left);
public sealed record BatchOverrideDto(string Reason, IReadOnlyList<string> Limits, DateTime? At);

public sealed record BatchDto(
    long Id, string Code, string Name, int Year, string Status, int Capacity, int DurationMonths,
    DateOnly ApplyFrom, DateOnly ApplyTo, DateOnly StartDate, string? Note, BatchOverrideDto? Override, SeatsDto Seats);

public sealed class CreateBatchRequest
{
    public string? Name { get; set; }
    public int? Capacity { get; set; }
    public int? DurationMonths { get; set; }
    public DateOnly? ApplyFrom { get; set; }
    public DateOnly? ApplyTo { get; set; }
    public DateOnly? StartDate { get; set; }
    public string? Note { get; set; }

    /// <summary>Required when the batch exceeds a cl.6 limit.</summary>
    public string? OverrideReason { get; set; }
}

public sealed class SetBatchStatusRequest
{
    public string? Status { get; set; }
}

// ---------- applications ----------

public sealed record PersonDto(long Id, string Name, string? Email, string? University);
public sealed record ExamDto(string Mode, DateOnly Date, int Score);
public sealed record HistoryDto(string Status, DateTime At, string? ByName, string? Note);

public sealed record ApplicationListItemDto(
    long Id, long BatchId, string BatchName, string BatchCode, PersonDto Student, string Status, DateTime AppliedAt,
    IReadOnlyList<string> PreferredDepartments, ExamDto? Exam, string? Basis);

public sealed record ApplicationDetailDto(
    long Id, long BatchId, string BatchName, string BatchCode, PersonDto Student, string Status, DateTime AppliedAt,
    IReadOnlyList<string> PreferredDepartments, ExamDto? Exam, string? Basis,
    string Statement, IReadOnlyList<string> Documents, string? RejectionReason,
    IReadOnlyList<HistoryDto> History, InternshipDto? Internship,
    IReadOnlyList<Documents.MyDocumentDto>? AttachedDocuments = null);

public sealed record MentorDto(long Id, string Name, string? Designation);
public sealed record PlacementDto(string Department, string? Mentor, DateOnly From, string? Note);
public sealed record DayDto(DateOnly Date, string Kind, string Label, string? CheckIn = null, string? CheckOut = null, int? LateMinutes = null);
public sealed record TodayRecordDto(string Status, string? CheckIn, string? CheckOut, int? LateMinutes);
public sealed record TodayDto(DateOnly Date, bool InPeriod, TodayRecordDto? Record);
public sealed record ReportDto(string Text, DateTime SubmittedAt, string Status, string? ReviewNote, bool Late);
public sealed record EvaluationDto(IReadOnlyDictionary<string, int> Scores, int Total, string Recommendation, string? EvaluatorName, DateTime At);
public sealed record CertificateDto(string Number, string Grade, DateTime IssuedAt);
public sealed record OutcomeDto(string Result, IReadOnlyList<string> Reasons);
public sealed record ComputedOutcomeDto(bool Ready, bool Certifiable, string? Grade, int? Total, IReadOnlyList<string> Reasons);

public sealed record InternshipDto(
    DepartmentDto Department, MentorDto? Mentor, DateOnly StartDate, DateOnly EndDate, string Tasks,
    IReadOnlyList<PlacementDto> Placements,
    AttendanceSummary Attendance, IReadOnlyList<DayDto> RecentDays, TodayDto Today,
    IReadOnlyList<LeaveRequestView> LeaveRequests, IReadOnlyList<BillView> Bills,
    DateOnly ReportDeadline, ReportDto? Report, EvaluationDto? Evaluation, string? EvaluationBlockedReason,
    CertificateDto? Certificate, OutcomeDto? Outcome, ComputedOutcomeDto Computed);

public sealed record EligibilityDto(bool Eligible, string? Basis, IReadOnlyList<EligibilityRule> Rules);

public sealed record InternListItemDto(
    long Id, PersonDto Student, string BatchName, string Status, string Department, string? Mentor,
    DateOnly StartDate, DateOnly EndDate, int AttendancePct, bool BelowMin, int PendingLeave, bool ReportSubmitted, int MonthlyToEvaluate);

public sealed record BillRowDto(
    long ApplicationId, string StudentName, string Department, string? Mentor, string Month, int PeriodDays, int Gross, int? Deduction, int? DeductDays,
    int? Net, MonthAttendance Attendance, IReadOnlyList<RegisterDay> Register, MonthlyScores? Scores, string Status, string? WithheldReason,
    int? Score, string? EvaluatedBy, DateTime? PaidAt, string? PaidBy, string? SlipNumber);

public sealed record OverviewActionDto(int Count, string Label, string Tab);
public sealed record RiskDto(string Name, int Pct);
public sealed record FunnelDto(string Status, int Count);

public sealed record OverviewDto(
    int OpenBatches, int InSelection, int SeatsLeft, int ActiveInterns,
    IReadOnlyList<RiskDto> AtRisk, IReadOnlyList<OverviewActionDto> Actions, IReadOnlyList<FunnelDto> Funnel);

// ---------- requests ----------

public sealed class ApplyRequest
{
    public long BatchId { get; set; }
    public string? Statement { get; set; }
    public List<long> PreferredDepartmentIds { get; set; } = [];
}

public sealed class ReasonRequest
{
    public string? Reason { get; set; }
}

public sealed class RecordExamRequest
{
    public string? Mode { get; set; }
    public DateOnly? Date { get; set; }
    public int? Score { get; set; }
}

public sealed class PlaceRequest
{
    public long? DepartmentId { get; set; }
    public long? MentorId { get; set; }
    public DateOnly? StartDate { get; set; }
    public int? Months { get; set; }
    public string? Tasks { get; set; }
}

public sealed class TransferRequest
{
    public long? DepartmentId { get; set; }
    public long? MentorId { get; set; }
    public string? Reason { get; set; }
}

public sealed class LeaveRequestBody
{
    public DateOnly? Date { get; set; }
    public string? Reason { get; set; }
}

public sealed class DecideLeaveRequest
{
    public DateOnly? Date { get; set; }
    public bool Approve { get; set; }
}

public sealed class MarkAbsenceRequest
{
    public DateOnly? Date { get; set; }
    public bool Approved { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Admin correction of a day's sign-in and sign-out times. Times are "HH:mm" in Bangladesh time.</summary>
public sealed class SetAttendanceTimesRequest
{
    public DateOnly? Date { get; set; }
    public string? CheckIn { get; set; }
    public string? CheckOut { get; set; }
    public string? Reason { get; set; }
}

public sealed class SubmitReportRequest
{
    public string? Text { get; set; }
}

/// <summary>The month's report, which is also the month's allowance bill.</summary>
public sealed class SubmitMonthlyReportRequest
{
    public string? Report { get; set; }
}

public sealed class EvaluateMonthRequest
{
    /// <summary>cl.10 review, each 0–25.</summary>
    public int? Attendance { get; set; }
    public int? Conduct { get; set; }
    public int? Discipline { get; set; }
    public int? WorkSkill { get; set; }

    /// <summary>Absent days to deduct for the month, after checking its attendance.</summary>
    public int? AbsentDays { get; set; }
    public string? Note { get; set; }
}

public sealed class ReviewReportRequest
{
    public bool Accept { get; set; }
    public string? Note { get; set; }
}

public sealed class EvaluationRequest
{
    public Dictionary<string, int?> Scores { get; set; } = [];
    public string? Recommendation { get; set; }
}

public sealed record IssueOutcomeResult(bool Certified, string? Grade, IReadOnlyList<string> Reasons);
