using Nexus.Domain.Abstractions;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.SoftSkills;

/// <summary>
/// cl.14 (ঘ): a monitoring visit to a university group by a Commission officer or an academia/industry expert.
/// Planned by the Commission; the monitor files the report with four ratings, findings and recommendations.
/// </summary>
public class SoftSkillMonitoringVisit : Entity<long>
{
    public const int MaxRating = 5;
    public const int MaxTextLength = 4000;
    public const int MaxPurposeLength = 500;

    public long UniversityGroupId { get; set; }
    public SoftSkillUniversityGroup UniversityGroup { get; set; } = default!;

    public long MonitorId { get; set; }
    public User Monitor { get; set; } = default!;

    /// <summary>The session to observe, when the visit is planned around one.</summary>
    public long? SessionId { get; set; }
    public SoftSkillSession? Session { get; set; }

    public DateOnly PlannedOn { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Planned;
    public string? Purpose { get; set; }

    // ---- the report ----
    public DateOnly? VisitedOn { get; set; }

    /// <summary>Quality of the trainers' delivery, 1–5.</summary>
    public int? DeliveryRating { get; set; }

    /// <summary>Trainees' participation, 1–5.</summary>
    public int? EngagementRating { get; set; }

    /// <summary>Punctuality and attendance records (cl.11 চ), 1–5.</summary>
    public int? AttendanceRating { get; set; }

    /// <summary>Venue and equipment the university provides (cl.12 ঘ), 1–5.</summary>
    public int? FacilitiesRating { get; set; }

    public string? Findings { get; set; }
    public string? Recommendations { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public decimal? AverageRating =>
        DeliveryRating is { } a && EngagementRating is { } b && AttendanceRating is { } c && FacilitiesRating is { } d
            ? Math.Round((a + b + c + d) / 4m, 2)
            : null;
}

/// <summary>
/// cl.17: an honorarium paid by the Commission. Until it is paid there is no row: what is due is worked out from held
/// sessions, coordinated university groups and submitted visits. One row per source (<see cref="Kind"/>, <see cref="SourceId"/>),
/// so nothing is paid twice; the amount is the rate on the day of payment.
/// </summary>
public class SoftSkillHonorarium
{
    public const int MaxNoteLength = 500;

    public long Id { get; set; }
    public HonorariumKind Kind { get; set; }

    /// <summary>The session, university group or visit id, by <see cref="Kind"/>.</summary>
    public long SourceId { get; set; }

    public long PersonId { get; set; }
    public User Person { get; set; } = default!;

    public long UniversityGroupId { get; set; }
    public SoftSkillUniversityGroup UniversityGroup { get; set; } = default!;

    public int Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public long? PaidById { get; set; }
    public string? PaidByName { get; set; }
    public required string SlipNumber { get; set; }
    public string? Note { get; set; }

    /// <summary>Unique per source, e.g. HON-S-000123 for session 123.</summary>
    public static string SlipNumberFor(HonorariumKind kind, long sourceId) =>
        $"HON-{kind switch { HonorariumKind.Session => "S", HonorariumKind.Coordination => "C", _ => "M" }}-{sourceId:D6}";
}
