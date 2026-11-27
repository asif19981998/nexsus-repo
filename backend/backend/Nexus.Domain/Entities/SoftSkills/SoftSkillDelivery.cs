using Nexus.Domain.Abstractions;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.SoftSkills;

/// <summary>One class of a university group on one topic, taken by one trainer (cl.8: about 4 hours a week, 48 in all).</summary>
public class SoftSkillSession : Entity<long>
{
    public const int MaxNoteLength = 500;

    public long UniversityGroupId { get; set; }
    public SoftSkillUniversityGroup UniversityGroup { get; set; } = default!;

    public long TopicId { get; set; }
    public SoftSkillTopic Topic { get; set; } = default!;

    public long TrainerId { get; set; }
    public User Trainer { get; set; } = default!;

    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;
    public string? Note { get; set; }

    public ICollection<SoftSkillAttendance> Attendance { get; set; } = new List<SoftSkillAttendance>();

    public decimal Hours => (decimal)(EndTime - StartTime).TotalHours;
}

/// <summary>cl.11 (চ, ছ): a trainee's attendance at one session, with arrival and departure, or a leave request.</summary>
public class SoftSkillAttendance
{
    public const int MaxReasonLength = 500;

    public long Id { get; set; }
    public long SessionId { get; set; }
    public SoftSkillSession Session { get; set; } = default!;
    public long TraineeId { get; set; }
    public SoftSkillTrainee Trainee { get; set; } = default!;

    public AttendanceStatus Status { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }

    public LeaveReasonType? ReasonType { get; set; }
    public string? Reason { get; set; }

    /// <summary>"Leave rejected" when the coordinator turned a request down.</summary>
    public string? Note { get; set; }
    public long? MarkedById { get; set; }
    public DateTime? MarkedAt { get; set; }
}

/// <summary>cl.14 (ক): the end-of-month assessment of a university group on the two topics of that month. Three in all.</summary>
public class SoftSkillAssessment : Entity<long>
{
    public long UniversityGroupId { get; set; }
    public SoftSkillUniversityGroup UniversityGroup { get; set; } = default!;

    /// <summary>1, 2 or 3.</summary>
    public int No { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>Copied from the policy when created, so later policy changes do not rewrite past results.</summary>
    public int MaxMarks { get; set; }
    public int PassMarks { get; set; }

    public ICollection<SoftSkillAssessmentResult> Results { get; set; } = new List<SoftSkillAssessmentResult>();
}

public class SoftSkillAssessmentResult
{
    public const int MaxNoteLength = 500;

    public long Id { get; set; }
    public long AssessmentId { get; set; }
    public SoftSkillAssessment Assessment { get; set; } = default!;
    public long TraineeId { get; set; }
    public SoftSkillTrainee Trainee { get; set; } = default!;

    public decimal? Marks { get; set; }
    public bool Absent { get; set; }

    /// <summary>cl.11 (জ): copying or unfair means. The assessment counts as failed.</summary>
    public bool Malpractice { get; set; }
    public string? Note { get; set; }
    public long? EnteredById { get; set; }
    public DateTime EnteredAt { get; set; }

    public bool PassedFor(SoftSkillAssessment a) => !Absent && !Malpractice && Marks is { } m && m >= a.PassMarks;
}

/// <summary>
/// cl.10: one month's stipend. The row exists once the finance section has paid or withheld it; before that the
/// status is worked out from the assessment and attendance. One per trainee and assessment (cl.10 ঘ).
/// </summary>
public class SoftSkillStipend
{
    public const int MaxReasonLength = 500;

    public long Id { get; set; }
    public long TraineeId { get; set; }
    public SoftSkillTrainee Trainee { get; set; } = default!;
    public int AssessmentNo { get; set; }

    public DateTime? PaidAt { get; set; }
    public long? PaidById { get; set; }
    public string? PaidByName { get; set; }
    public int? PaidAmount { get; set; }
    public string? SlipNumber { get; set; }

    /// <summary>cl.10 (খ): withheld after checking attendance, conduct or discipline.</summary>
    public string? WithheldReason { get; set; }
    public DateTime? WithheldAt { get; set; }
    public long? WithheldById { get; set; }

    /// <summary>Unique per trainee and month, e.g. SSK-2027-00012-2.</summary>
    public static string SlipNumberFor(int year, long traineeId, int no) => $"SSK-{year}-{traineeId:D5}-{no}";
}

/// <summary>cl.11 (ঝ): the learning report or small project, due 15 working days after the training ends.</summary>
public class SoftSkillLearningReport
{
    public const int MaxTitleLength = 200;
    public const int MaxTextLength = 8000;
    public const int MaxReviewNoteLength = 1000;

    public long Id { get; set; }
    public long TraineeId { get; set; }
    public SoftSkillTrainee Trainee { get; set; } = default!;
    public LearningReportType Type { get; set; }
    public required string Title { get; set; }
    public required string Text { get; set; }
    public DateTime SubmittedAt { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Submitted;
    public string? ReviewNote { get; set; }
    public long? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
