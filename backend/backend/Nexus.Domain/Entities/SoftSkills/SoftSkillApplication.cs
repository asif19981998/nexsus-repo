using Nexus.Domain.Abstractions;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.SoftSkills;

/// <summary>
/// A student's application to one batch, at their own university's university group (cl.9). Becomes a <see cref="SoftSkillTrainee"/>
/// once the Commission selects it. Statuses are the internship's: Submitted, Shortlisted, Waitlisted, Selected, Active…
/// </summary>
public class SoftSkillApplication : Entity<long>
{
    public const int MaxStatementLength = 4000;

    public long BatchId { get; set; }
    public SoftSkillBatch Batch { get; set; } = default!;

    public long UniversityGroupId { get; set; }
    public SoftSkillUniversityGroup UniversityGroup { get; set; } = default!;

    public long StudentId { get; set; }
    public User Student { get; set; } = default!;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    public DateTime AppliedAt { get; set; }
    public required string Statement { get; set; }

    /// <summary>"Year 2, University of Dhaka": the cl.7 facts when the student applied.</summary>
    public string? Basis { get; set; }

    /// <summary>Attached document codes, comma separated.</summary>
    public string Documents { get; set; } = "";
    public string? RejectionReason { get; set; }

    /// <summary>cl.9: the score of the Commission's screening, when it uses one (0–100).</summary>
    public int? ScreeningScore { get; set; }

    public ICollection<SoftSkillApplicationHistory> History { get; set; } = new List<SoftSkillApplicationHistory>();
    public SoftSkillTrainee? Trainee { get; set; }
}

public class SoftSkillApplicationHistory
{
    public long Id { get; set; }
    public long ApplicationId { get; set; }
    public SoftSkillApplication Application { get; set; } = default!;
    public ApplicationStatus Status { get; set; }
    public DateTime At { get; set; }
    public long? ById { get; set; }
    public string? ByName { get; set; }
    public string? Note { get; set; }
}

/// <summary>A selected student taking the training at a university group, with everything recorded during it.</summary>
public class SoftSkillTrainee : Entity<long>
{
    public long ApplicationId { get; set; }
    public SoftSkillApplication Application { get; set; } = default!;

    public long UniversityGroupId { get; set; }
    public SoftSkillUniversityGroup UniversityGroup { get; set; } = default!;

    /// <summary>cl.11 (ঞ): why the Commission cancelled the participation.</summary>
    public string? CancelReason { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>cl.15: average percent of the three assessments, fixed when the outcome is issued.</summary>
    public decimal? AverageScore { get; set; }
    public string? CertificateNumber { get; set; }
    public string? CertificateGrade { get; set; }
    public DateTime? CertificateIssuedAt { get; set; }

    /// <summary>Reasons when not certified, one per line.</summary>
    public string? OutcomeReasons { get; set; }

    public ICollection<SoftSkillAttendance> Attendance { get; set; } = new List<SoftSkillAttendance>();
    public ICollection<SoftSkillAssessmentResult> Results { get; set; } = new List<SoftSkillAssessmentResult>();
    public ICollection<SoftSkillStipend> Stipends { get; set; } = new List<SoftSkillStipend>();
    public SoftSkillLearningReport? Report { get; set; }
}
