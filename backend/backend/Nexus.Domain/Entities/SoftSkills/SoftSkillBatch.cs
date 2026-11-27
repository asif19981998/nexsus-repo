using Nexus.Domain.Abstractions;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.SoftSkills;

/// <summary>
/// A national intake (cl.6 খ: at most 2 a year). Training runs at each taking part university as a <see cref="SoftSkillUniversityGroup"/>.
/// </summary>
public class SoftSkillBatch : Entity<long>
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int Year { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Draft;
    public DateOnly ApplyFrom { get; set; }
    public DateOnly ApplyTo { get; set; }
    public DateOnly StartDate { get; set; }

    /// <summary>Inclusive last day: the start plus the policy's duration (cl.8).</summary>
    public DateOnly EndDate { get; set; }
    public string? Note { get; set; }

    /// <summary>cl.6 (গ): the Commission's reason for going over the batches-per-year limit.</summary>
    public string? OverrideReason { get; set; }
    public long? OverrideById { get; set; }
    public DateTime? OverrideAt { get; set; }

    public ICollection<SoftSkillUniversityGroup> UniversityGroups { get; set; } = new List<SoftSkillUniversityGroup>();
    public ICollection<SoftSkillApplication> Applications { get; set; } = new List<SoftSkillApplication>();
}

/// <summary>
/// One university's part of a batch: its seats (cl.6 ক), its coordinator (cl.12 গ), its venue (cl.12 ঘ),
/// and the final evaluation report it sends the Commission (cl.14 ঙ).
/// </summary>
public class SoftSkillUniversityGroup : Entity<long>
{
    public const int MaxVenueLength = 300;
    public const int MaxFinalReportLength = 8000;

    public long BatchId { get; set; }
    public SoftSkillBatch Batch { get; set; } = default!;

    public long UniversityId { get; set; }
    public University University { get; set; } = default!;

    public int Capacity { get; set; }

    /// <summary>cl.6 (গ): the reason for a capacity above the policy's seats per university.</summary>
    public string? OverrideReason { get; set; }

    /// <summary>cl.12 (গ): an approved trainer of the same university.</summary>
    public long? CoordinatorId { get; set; }
    public User? Coordinator { get; set; }

    public string? Venue { get; set; }

    // cl.14 (ঙ) final evaluation report with recommendations
    public string? FinalReport { get; set; }
    public string? FinalRecommendation { get; set; }
    public DateTime? FinalReportAt { get; set; }
    public long? FinalReportById { get; set; }

    public ICollection<SoftSkillSession> Sessions { get; set; } = new List<SoftSkillSession>();
    public ICollection<SoftSkillAssessment> Assessments { get; set; } = new List<SoftSkillAssessment>();
    public ICollection<SoftSkillTrainee> Trainees { get; set; } = new List<SoftSkillTrainee>();
}

/// <summary>
/// A teacher's registration as a trainer (cl.12). One row per person: applying again after a rejection reuses it.
/// The administrator approves it and records the Training of Trainers (cl.12 খ) before any session can be assigned.
/// </summary>
public class SoftSkillTrainer : Entity<long>
{
    public const int MaxStatementLength = 2000;
    public const int MaxNoteLength = 1000;

    public long UserId { get; set; }
    public User User { get; set; } = default!;

    /// <summary>The university from the trainer profile when they applied.</summary>
    public long UniversityId { get; set; }
    public University University { get; set; } = default!;

    public TrainerStatus Status { get; set; } = TrainerStatus.Submitted;
    public required string Statement { get; set; }
    public DateTime AppliedAt { get; set; }

    public DateTime? DecidedAt { get; set; }
    public long? DecidedById { get; set; }
    public string? DecisionNote { get; set; }

    // cl.12 (খ) Training of Trainers
    public bool TotCompleted { get; set; }
    public DateOnly? TotCompletedOn { get; set; }
    public string? TotReference { get; set; }
}
