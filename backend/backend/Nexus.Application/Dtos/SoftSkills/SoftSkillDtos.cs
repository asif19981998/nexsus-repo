using Nexus.Application.Dtos.Documents;
using Nexus.Application.Dtos.Internships;
using Nexus.Application.Rules;

namespace Nexus.Application.Dtos.SoftSkills;

// ---------- shared ----------

public sealed record UniversityDto(long Id, string Name, string? ShortName, string? NameBn, bool IsPublic);
public sealed record PersonRefDto(long Id, string Name, string? Email, string? Designation = null);

/// <summary>
/// One programme card on the "Programmes" page: what the signed-in person can join and whether they qualify.
/// Facts are the programme at a glance. ApplyBy and StartsOn come from the open batch that closes first.
/// </summary>
public sealed record ProgrammeCardDto(
    string Module, string Name, string Summary, string Link, bool Eligible, string? NotEligibleReason,
    int OpenBatches, string? MyStatus,
    IReadOnlyList<ProgrammeFactDto>? Facts = null, DateOnly? ApplyBy = null, DateOnly? StartsOn = null);

public sealed record ProgrammeFactDto(string Label, string Value);

// ---------- setup ----------

public sealed record SoftSkillPolicyDto(
    int SeatsPerUniversity, int MaxBatchesPerYear, int MinStudyYear, int MaxStudyYear, int DurationMonths, int TotalHours,
    decimal MonthlyStipend, int MinAttendancePct, int? StipendMinAttendancePct, bool ApprovedLeaveCountsAsPresent,
    int ReportWorkingDays, int AssessmentMaxMarks, int AssessmentPassMarks,
    int MinStatementLength, int MinReportLength, int MinFinalReportLength,
    decimal TrainerRatePerSession, decimal CoordinatorRatePerUniversityGroup, decimal ExpertRatePerVisit);

public sealed record TopicDto(long Id, string Code, string Name, string? NameBn, int Hours, int AssessmentNo);
public sealed record SoftSkillGradeBandDto(string Grade, string? NameBn, int Min);

public sealed record SoftSkillSetupDto(
    SoftSkillPolicyDto Policy,
    IReadOnlyList<TopicDto> Topics,
    IReadOnlyList<SoftSkillGradeBandDto> GradeBands,
    IReadOnlyList<UniversityDto> Universities,
    IReadOnlyList<string> BatchStatuses,
    IReadOnlyList<string> ApplicationStatuses,
    string Today);

// ---------- batches and university groups ----------

public sealed record UniversityGroupSummaryDto(
    long Id, long BatchId, string BatchCode, string BatchName, string BatchStatus, DateOnly StartDate, DateOnly EndDate,
    UniversityDto University, int Capacity, int Taken, int Left, PersonRefDto? Coordinator, string? Venue, string? OverrideReason,
    bool FinalReportSubmitted);

public sealed record SoftSkillBatchDto(
    long Id, string Code, string Name, int Year, string Status, DateOnly ApplyFrom, DateOnly ApplyTo, DateOnly StartDate, DateOnly EndDate,
    string? Note, string? OverrideReason, IReadOnlyList<UniversityGroupSummaryDto> UniversityGroups, int Capacity, int Taken);

public sealed class CreateSoftSkillBatchRequest
{
    public string? Name { get; set; }
    public DateOnly? ApplyFrom { get; set; }
    public DateOnly? ApplyTo { get; set; }
    public DateOnly? StartDate { get; set; }
    public string? Note { get; set; }

    /// <summary>Required when the batch exceeds the batches-per-year limit (cl.6 গ).</summary>
    public string? OverrideReason { get; set; }

    /// <summary>Universities taking part; each gets a university group with the policy's seats.</summary>
    public List<long> UniversityIds { get; set; } = [];
}

/// <summary>
/// Edit a batch's own fields (universities are managed separately). Everything while Draft or Applications open;
/// not the application window once selection starts; only name and note once it is running. Completed and
/// cancelled batches are read-only.
/// </summary>
public sealed class UpdateSoftSkillBatchRequest
{
    public string? Name { get; set; }
    public DateOnly? ApplyFrom { get; set; }
    public DateOnly? ApplyTo { get; set; }
    public DateOnly? StartDate { get; set; }
    public string? Note { get; set; }

    /// <summary>Required when moving the batch to a year that is already at the batches-per-year limit (cl.6 গ).</summary>
    public string? OverrideReason { get; set; }
}

public sealed class UniversityGroupRequest
{
    public long? UniversityId { get; set; }

    /// <summary>Seats; the policy's seats per university when empty.</summary>
    public int? Capacity { get; set; }
    public string? Venue { get; set; }

    /// <summary>Required when the capacity is above the policy's seats per university (cl.6 গ).</summary>
    public string? OverrideReason { get; set; }
}

public sealed class SetCoordinatorRequest
{
    public long? CoordinatorId { get; set; }
}

// ---------- applications ----------

public sealed record SoftSkillEligibilityDto(bool Eligible, string? Basis, IReadOnlyList<EligibilityRule> Rules, UniversityDto? University);

public sealed record OpenBatchDto(
    long BatchId, string Code, string Name, DateOnly ApplyFrom, DateOnly ApplyTo, DateOnly StartDate, DateOnly EndDate,
    long? UniversityGroupId, string? University, int? SeatsLeft, string? Venue);

public sealed record SoftSkillApplicationListItemDto(
    long Id, long BatchId, string BatchCode, string BatchName, long UniversityGroupId, string University, PersonDto Student,
    string Status, DateTime AppliedAt, string? Basis, int? ScreeningScore);

public sealed record SoftSkillApplicationDto(
    long Id, long BatchId, string BatchCode, string BatchName, string BatchStatus, long UniversityGroupId, string University,
    PersonDto Student, string Status, DateTime AppliedAt, string Statement, string? Basis, string? RejectionReason,
    int? ScreeningScore, IReadOnlyList<string> Documents, IReadOnlyList<HistoryDto> History, long? TraineeId,
    IReadOnlyList<MyDocumentDto>? AttachedDocuments = null);

public sealed class SoftSkillApplyRequest
{
    public long BatchId { get; set; }
    public string? Statement { get; set; }
}

public sealed class ScreeningScoreRequest
{
    public int? Score { get; set; }
}

// ---------- trainee ----------

public sealed record SessionDto(
    long Id, long UniversityGroupId, long TopicId, string Topic, int AssessmentNo, PersonRefDto Trainer, DateOnly Date,
    string StartTime, string EndTime, decimal Hours, string Status, string? Note, int Present, int Marked);

public sealed record TraineeSessionDto(
    SessionDto Session, string? Attendance, string? CheckIn, string? CheckOut, string? ReasonType, string? Reason, string? Note);

public sealed record AssessmentResultDto(
    int No, DateOnly? Date, decimal? Marks, int? MaxMarks, int? PassMarks, bool Absent, bool Malpractice, bool? Passed, string? Note);

public sealed record LearningReportDto(string Type, string Title, string Text, DateTime SubmittedAt, string Status, string? ReviewNote, bool Late);
public sealed record SoftSkillCertificateDto(string Number, string Grade, decimal? Average, DateTime IssuedAt);

public sealed record TraineeDto(
    long Id, long ApplicationId, PersonDto Student, string Status, long UniversityGroupId, UniversityDto University,
    string BatchCode, string BatchName, string BatchStatus, DateOnly StartDate, DateOnly EndDate,
    PersonRefDto? Coordinator, string? Venue,
    IReadOnlyList<TraineeSessionDto> Sessions, SoftSkillAttendanceSummary Attendance,
    IReadOnlyList<AssessmentResultDto> Assessments, IReadOnlyList<SoftSkillStipendView> Stipends,
    DateOnly ReportDeadline, LearningReportDto? Report,
    SoftSkillCertificateDto? Certificate, IReadOnlyList<string>? OutcomeReasons, string? CancelReason,
    SoftSkillOutcome Computed);

public sealed record MySoftSkillDto(SoftSkillApplicationDto Application, TraineeDto? Trainee);

public sealed record TraineeListItemDto(
    long Id, PersonDto Student, string Status, long UniversityGroupId, string University, string BatchCode,
    int AttendancePct, bool BelowMin, int AssessmentsDone, decimal? Average, string? Grade,
    string? ReportStatus, bool Ready, bool Certifiable, string? CertificateNumber);

public sealed class SoftSkillLeaveRequest
{
    public long SessionId { get; set; }

    /// <summary>"Health" or "Other" (cl.11 ছ).</summary>
    public string? ReasonType { get; set; }
    public string? Reason { get; set; }
}

public sealed class LearningReportRequest
{
    /// <summary>"Report" or "MiniProject" (cl.11 ঝ).</summary>
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? Text { get; set; }
}

// ---------- trainers ----------

public sealed record TrainerProfileDto(
    long? UniversityId, string? University, bool UniversityIsPublic, string? Designation, string? AcademicDepartment, string? EmployeeId);

public sealed class TrainerProfileRequest
{
    public long? UniversityId { get; set; }
    public string? Designation { get; set; }
    public string? AcademicDepartment { get; set; }
    public string? EmployeeId { get; set; }
}

public sealed record TrainerDto(
    long Id, PersonRefDto Person, string? AcademicDepartment, UniversityDto University, string Status, string Statement,
    DateTime AppliedAt, DateTime? DecidedAt, string? DecisionNote, bool TotCompleted, DateOnly? TotCompletedOn, string? TotReference,
    bool CanTrain, IReadOnlyList<MyDocumentDto>? AttachedDocuments = null);

public sealed record TrainerEligibilityDto(bool Eligible, IReadOnlyList<EligibilityRule> Rules);

public sealed record MyTrainerDto(
    TrainerProfileDto Profile, TrainerEligibilityDto Eligibility, TrainerDto? Registration,
    IReadOnlyList<UniversityGroupSummaryDto> UniversityGroups, IReadOnlyList<SessionDto> UpcomingSessions);

public sealed class TrainerApplyRequest
{
    public string? Statement { get; set; }
}

public sealed class TrainerDecisionRequest
{
    public string? Note { get; set; }
}

public sealed class TotRequest
{
    public DateOnly? CompletedOn { get; set; }
    public string? Reference { get; set; }
}

// ---------- university group work (coordinator, trainer) ----------

public sealed record UniversityGroupAccessDto(bool Coordinate, bool EnterMarks, bool TakeAttendanceAll, bool Monitor = false, bool ManageMonitoring = false);
public sealed record TopicProgressDto(long Id, string Name, int AssessmentNo, int Hours, decimal Scheduled, decimal Held);
public sealed record TrainerOptionDto(long UserId, string Name, string? Designation, bool CanTrain);

public sealed record UniversityGroupTraineeDto(
    long Id, PersonDto Student, string Status, SoftSkillAttendanceSummary Attendance, IReadOnlyList<AssessmentResultDto> Assessments,
    LearningReportDto? Report, string? CertificateGrade, string? CancelReason);

public sealed record LeaveRequestDto(
    long AttendanceId, long SessionId, long TraineeId, string Student, DateOnly Date, string Topic, string? ReasonType, string? Reason, string Status);

public sealed record AssessmentDto(long Id, int No, DateOnly Date, int MaxMarks, int PassMarks, int Entered, int Passed);

public sealed record UniversityGroupDetailDto(
    UniversityGroupSummaryDto UniversityGroup, IReadOnlyList<TopicProgressDto> Topics, IReadOnlyList<SessionDto> Sessions,
    IReadOnlyList<UniversityGroupTraineeDto> Trainees, IReadOnlyList<LeaveRequestDto> LeaveRequests, IReadOnlyList<AssessmentDto> Assessments,
    string? FinalReport, string? FinalRecommendation, DateTime? FinalReportAt, DateOnly ReportDeadline,
    IReadOnlyList<TrainerOptionDto> Trainers, UniversityGroupAccessDto Access, IReadOnlyList<VisitDto>? Visits = null);

public sealed class SessionRequest
{
    public long? TopicId { get; set; }
    public long? TrainerId { get; set; }
    public DateOnly? Date { get; set; }

    /// <summary>"HH:mm".</summary>
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? Note { get; set; }
}

public sealed class AttendanceMark
{
    public long TraineeId { get; set; }

    /// <summary>"Present", "Absent" or "Leave".</summary>
    public string? Status { get; set; }
    public string? CheckIn { get; set; }
    public string? CheckOut { get; set; }
}

public sealed class AttendanceSheetRequest
{
    public List<AttendanceMark> Marks { get; set; } = [];
}

public sealed class LeaveDecisionRequest
{
    public long AttendanceId { get; set; }
    public bool Approve { get; set; }
    public string? Note { get; set; }
}

public sealed class AssessmentRequest
{
    public int No { get; set; }
    public DateOnly? Date { get; set; }
}

public sealed class ResultMark
{
    public long TraineeId { get; set; }
    public decimal? Marks { get; set; }
    public bool Absent { get; set; }
    public bool Malpractice { get; set; }
    public string? Note { get; set; }
}

public sealed class ResultsRequest
{
    public List<ResultMark> Results { get; set; } = [];
}

public sealed class ReportReviewRequest
{
    public long TraineeId { get; set; }
    public bool Accept { get; set; }
    public string? Note { get; set; }
}

public sealed class FinalReportRequest
{
    public string? Report { get; set; }
    public string? Recommendation { get; set; }
}

// ---------- stipends and overview ----------

public sealed record StipendRowDto(
    long TraineeId, string Student, string University, string BatchCode, int No, string Status, int Amount, string? Reason,
    decimal? Marks, int? MaxMarks, int MonthAttendancePct, DateTime? PaidAt, string? PaidBy, string? SlipNumber);

public sealed record SoftSkillOverviewDto(
    int OpenBatches, int UniversityGroups, int SeatsLeft, int ApplicationsToScreen, int ActiveTrainees, int TrainersPending,
    int StipendsReady, int Certified, IReadOnlyList<FunnelDto> Funnel);

// ---------- monitoring (cl.14 ঘ) ----------

public sealed record VisitDto(
    long Id, long UniversityGroupId, string University, string BatchCode, string BatchName, PersonRefDto Monitor,
    long? SessionId, string? Session, DateOnly PlannedOn, string Status, string? Purpose,
    DateOnly? VisitedOn, int? Delivery, int? Engagement, int? Attendance, int? Facilities, decimal? Average,
    string? Findings, string? Recommendations, DateTime? SubmittedAt);

/// <summary>Someone who can be sent on a visit: a Monitor (expert) or a Commission officer.</summary>
public sealed record MonitorOptionDto(long Id, string Name, string? Designation, string Role);

public sealed class PlanVisitRequest
{
    public long? UniversityGroupId { get; set; }
    public long? MonitorId { get; set; }
    public DateOnly? PlannedOn { get; set; }
    public long? SessionId { get; set; }
    public string? Purpose { get; set; }
}

public sealed class VisitReportRequest
{
    public DateOnly? VisitedOn { get; set; }
    public int? Delivery { get; set; }
    public int? Engagement { get; set; }
    public int? Attendance { get; set; }
    public int? Facilities { get; set; }
    public string? Findings { get; set; }
    public string? Recommendations { get; set; }
}

/// <summary>One university group's health for the Commission's monitoring screen, with what needs attention.</summary>
public sealed record UniversityGroupHealthDto(
    UniversityGroupSummaryDto UniversityGroup, int Trainees, int AverageAttendancePct, int BelowMinAttendance,
    decimal HoursHeld, decimal HoursPlanned, int TotalHours, decimal? ExpectedHoursByNow,
    int AssessmentsSet, int? PassRatePct, int PendingLeave,
    int Visits, DateOnly? LastVisit, decimal? AverageRating, IReadOnlyList<string> Flags);

// ---------- honorarium (cl.17) ----------

public sealed record HonorariumRowDto(
    string Kind, long SourceId, PersonRefDto Person, string Role, long UniversityGroupId, string University, string BatchCode,
    DateOnly Date, string Description, int Amount, string Status, DateTime? PaidAt, string? PaidBy, string? SlipNumber);

public sealed record HonorariumRatesDto(decimal TrainerPerSession, decimal CoordinatorPerUniversityGroup, decimal ExpertPerVisit);

public sealed class HonorariumRatesRequest
{
    public decimal? TrainerPerSession { get; set; }
    public decimal? CoordinatorPerUniversityGroup { get; set; }
    public decimal? ExpertPerVisit { get; set; }
}
