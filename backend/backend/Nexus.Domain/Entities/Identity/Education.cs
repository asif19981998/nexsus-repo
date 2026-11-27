using Nexus.Domain.Abstractions;
using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.Identity;

/// <summary>
/// One line of a person's education (SSC, HSC, bachelor's…). It belongs to the account, so every module reads the same
/// history. The <see cref="IsMain"/> line, a bachelor's degree or higher, is the one the UGC Internship's cl.7 check reads.
/// </summary>
public class Education : Entity<long>
{
    public const int MaxTextLength = 200;
    public const int MaxResultLength = 50;

    public long UserId { get; set; }
    public User User { get; set; } = default!;

    public EducationLevel Level { get; set; }

    /// <summary>University, college or school.</summary>
    public required string Institution { get; set; }

    /// <summary>Set when the institution is a university on the list; <see cref="Institution"/> then holds its name.</summary>
    public long? UniversityId { get; set; }
    public University? University { get; set; }

    /// <summary>Programme or department ("BSc in CSE"); the group for SSC/HSC ("Science").</summary>
    public string? Programme { get; set; }

    public EducationStatus Status { get; set; } = EducationStatus.Studying;

    /// <summary>Year of study while studying (1–6).</summary>
    public int? CurrentYear { get; set; }

    /// <summary>Month the result was published (Completed) or is expected (Studying). First day of that month.</summary>
    public DateOnly? ResultMonth { get; set; }

    /// <summary>As written by the person: "CGPA 3.62 / 4.00", "GPA 5.00".</summary>
    public string? Result { get; set; }

    /// <summary>The line cl.7 reads. At most one per person, and only a bachelor's degree or higher.</summary>
    public bool IsMain { get; set; }

    public static bool IsTertiary(EducationLevel level) => level >= EducationLevel.Bachelor;
}
