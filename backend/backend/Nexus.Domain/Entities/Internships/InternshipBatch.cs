using Nexus.Domain.Abstractions;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.Internships;

/// <summary>An intake of interns (cl.6). At most 20 seats and 2 batches a year unless overridden with a reason.</summary>
public class InternshipBatch : Entity<long>
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int Year { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Draft;
    public int Capacity { get; set; }
    public int DurationMonths { get; set; }
    public DateOnly ApplyFrom { get; set; }
    public DateOnly ApplyTo { get; set; }
    public DateOnly StartDate { get; set; }
    public string? Note { get; set; }

    /// <summary>cl.6 (ga): the Commission's reason for going over a limit. Null when no limit was exceeded.</summary>
    public string? OverrideReason { get; set; }

    /// <summary>Which limits the override covers, one per line.</summary>
    public string? OverrideLimits { get; set; }
    public long? OverrideById { get; set; }
    public DateTime? OverrideAt { get; set; }

    public ICollection<InternshipApplication> Applications { get; set; } = new List<InternshipApplication>();
}
