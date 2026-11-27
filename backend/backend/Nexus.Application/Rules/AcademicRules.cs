using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Enums;

namespace Nexus.Application.Rules;

/// <summary>
/// What the cl.7 eligibility check reads: the person's main education line and whether their identity is verified.
/// <see cref="GraduationMonth"/> is the published result month once finished, or the expected one while studying.
/// </summary>
public sealed record AcademicSnapshot(
    string? University, string? Programme, AcademicLevel Level, int? CurrentYear, DateOnly? GraduationMonth, bool IdentityVerified);

public static class AcademicRules
{
    /// <summary>Null when the person has neither a student profile nor a main education line.</summary>
    public static AcademicSnapshot? Snapshot(StudentProfile? profile, Education? main)
    {
        if (profile is null && main is null) return null;
        return new AcademicSnapshot(
            main?.Institution,
            main?.Programme,
            main?.Level >= EducationLevel.Master ? AcademicLevel.Postgraduate : AcademicLevel.Undergraduate,
            main?.Status == EducationStatus.Studying ? main.CurrentYear : null,
            main?.ResultMonth,
            profile?.IdentityVerified == true);
    }

    /// <summary>
    /// Finished when the main education's result is published. No main line reads as still studying.
    /// The profile page and the document checklists use the same rule.
    /// </summary>
    public static bool IsGraduated(Education? main) => main?.Status == EducationStatus.Completed;

    /// <summary>The first day of <paramref name="today"/>'s month.</summary>
    public static DateOnly MonthOf(DateOnly today) => new(today.Year, today.Month, 1);
}
