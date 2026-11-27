using Nexus.Domain.Abstractions;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.Internships;

/// <summary>The placement that follows a selected application: department, mentor, period, and everything recorded during it.</summary>
public class Internship : Entity<long>
{
    public long ApplicationId { get; set; }
    public InternshipApplication Application { get; set; } = default!;

    public long DepartmentId { get; set; }
    public Department Department { get; set; } = default!;

    /// <summary>The supervising mentor (cl.14). Sees and evaluates this intern.</summary>
    public long? MentorId { get; set; }
    public User? Mentor { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>Inclusive last day.</summary>
    public DateOnly EndDate { get; set; }
    public required string Tasks { get; set; }

    /// <summary>"Not certified" or "Terminated" with reasons; null while running or once certified.</summary>
    public string? OutcomeResult { get; set; }
    public string? OutcomeReasons { get; set; }

    public string? CertificateNumber { get; set; }
    public string? CertificateGrade { get; set; }
    public DateTime? CertificateIssuedAt { get; set; }

    public ICollection<InternshipPlacement> Placements { get; set; } = new List<InternshipPlacement>();
    public ICollection<AttendanceRecord> Attendance { get; set; } = new List<AttendanceRecord>();
    public ICollection<AllowanceBill> Bills { get; set; } = new List<AllowanceBill>();
    public InternshipReport? Report { get; set; }
    public InternshipEvaluation? Evaluation { get; set; }
}

/// <summary>cl.12: department history. The first row is the initial placement; later rows are transfers.</summary>
public class InternshipPlacement
{
    public long Id { get; set; }
    public long InternshipId { get; set; }
    public Internship Internship { get; set; } = default!;
    public long DepartmentId { get; set; }
    public Department Department { get; set; } = default!;
    public long? MentorId { get; set; }
    public User? Mentor { get; set; }
    public DateOnly From { get; set; }
    public string? Note { get; set; }
}

/// <summary>cl.11 (cha, ja): one row per working day the intern signed in, asked for leave, or was marked absent.</summary>
public class AttendanceRecord
{
    public long Id { get; set; }
    public long InternshipId { get; set; }
    public Internship Internship { get; set; } = default!;
    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public string? Reason { get; set; }
    public bool Approved { get; set; }

    /// <summary>"Leave rejected" when a leave request was turned down.</summary>
    public string? Note { get; set; }
    public long? DecidedById { get; set; }

    // ---- mobile punch details (UTC instants, GPS and device of each punch) ----
    public DateTime? CheckInAtUtc { get; set; }
    public DateTime? CheckOutAtUtc { get; set; }
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public double? CheckInAccuracyMeters { get; set; }
    public double? CheckInDistanceMeters { get; set; }
    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }
    public double? CheckOutAccuracyMeters { get; set; }
    public double? CheckOutDistanceMeters { get; set; }
    public string? DeviceId { get; set; }
    public string? AppVersion { get; set; }

    /// <summary>The app's idempotency keys: a retried punch with the same key changes nothing.</summary>
    public string? CheckInKey { get; set; }
    public string? CheckOutKey { get; set; }

    /// <summary>Selfie taken with the front camera at each punch. A server path such as <c>/files/xxxx.jpg</c>.</summary>
    public string? CheckInPhotoPath { get; set; }
    public string? CheckOutPhotoPath { get; set; }
}

/// <summary>
/// cl.10 (kha): one month of the intern's allowance. The intern submits the month's report (this is the bill),
/// the mentor or department evaluates it, then the allowance is paid and a slip issued.
/// Amounts are computed from attendance until payment, when the paid amount is fixed.
/// </summary>
public class AllowanceBill
{
    public const int MinReportLength = 30;
    public const int MaxReportLength = 4000;
    public const int MaxEvaluationNoteLength = 1000;
    /// <summary>cl.10: the month is reviewed on attendance, conduct, discipline and work skill, each out of this; the total is out of 100.</summary>
    public const int MaxCriterionScore = 25;
    public const int MaxScore = 4 * MaxCriterionScore;

    public long Id { get; set; }
    public long InternshipId { get; set; }
    public Internship Internship { get; set; } = default!;

    /// <summary>"YYYY-MM".</summary>
    public required string Month { get; set; }

    /// <summary>The intern's report on the month's work. Null only on bills from before monthly reports.</summary>
    public string? Report { get; set; }
    public DateTime SubmittedAt { get; set; }

    /// <summary>Monthly evaluation total, 0–<see cref="MaxScore"/>: the sum of the four cl.10 scores. The bill cannot be paid before it.</summary>
    public int? Score { get; set; }
    public int? AttendanceScore { get; set; }
    public int? ConductScore { get; set; }
    public int? DisciplineScore { get; set; }
    public int? WorkSkillScore { get; set; }

    /// <summary>Absent days to deduct, entered by the mentor with the evaluation after checking the month's attendance.</summary>
    public int? AbsentDays { get; set; }
    public string? EvaluationNote { get; set; }
    public DateTime? EvaluatedAt { get; set; }
    public long? EvaluatedById { get; set; }
    public string? EvaluatedByName { get; set; }

    public DateTime? PaidAt { get; set; }
    public long? PaidById { get; set; }
    public string? PaidByName { get; set; }

    /// <summary>The net amount at the time of payment.</summary>
    public int? PaidAmount { get; set; }
    public string? SlipNumber { get; set; }

    /// <summary>Unique per internship and month, e.g. ALW-202703-00012.</summary>
    public static string SlipNumberFor(long internshipId, string month) => $"ALW-{month.Replace("-", "")}-{internshipId:D5}";
}

/// <summary>cl.11 (ta): the final report, due 15 working days after the end date.</summary>
public class InternshipReport
{
    public const int MaxReviewNoteLength = 1000;

    public long Id { get; set; }
    public long InternshipId { get; set; }
    public Internship Internship { get; set; } = default!;
    public required string Text { get; set; }
    public DateTime SubmittedAt { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Submitted;
    public string? ReviewNote { get; set; }
    public long? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

/// <summary>cl.14: the department's evaluation and recommendation.</summary>
public class InternshipEvaluation
{
    public const int MaxRecommendationLength = 2000;

    public long Id { get; set; }
    public long InternshipId { get; set; }
    public Internship Internship { get; set; } = default!;
    public int Total { get; set; }
    public required string Recommendation { get; set; }
    public long? EvaluatorId { get; set; }
    public User? Evaluator { get; set; }
    public DateTime EvaluatedAt { get; set; }
    public ICollection<EvaluationScore> Scores { get; set; } = new List<EvaluationScore>();
}

public class EvaluationScore
{
    public long Id { get; set; }
    public long EvaluationId { get; set; }
    public InternshipEvaluation Evaluation { get; set; } = default!;
    public long CriterionId { get; set; }
    public EvaluationCriterion Criterion { get; set; } = default!;
    public int Score { get; set; }
}
