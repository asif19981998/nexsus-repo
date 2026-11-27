using Nexus.Domain.Abstractions;

namespace Nexus.Domain.Entities.Setup;

/// <summary>A UGC department that hosts interns (assumption A-6).</summary>
public class Department : Entity<long>
{
    public required string Name { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Where interns of this department work. A punch counts only within <see cref="RadiusMeters"/> of this point.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int RadiusMeters { get; set; } = 200;
}

/// <summary>A day nobody works. Admin-managed; the mobile app shows it as a holiday.</summary>
public class Holiday : Entity<long>
{
    public DateOnly Date { get; set; }
    public required string Name { get; set; }
}

/// <summary>One line of the cl.14 department evaluation. The maximum scores of all criteria add up to 100.</summary>
public class EvaluationCriterion : Entity<long>
{
    public required string Key { get; set; }
    public required string Label { get; set; }
    public int MaxScore { get; set; } = 20;
    public int SortOrder { get; set; }
}

/// <summary>cl.15 certificate grade and the minimum evaluation total for it (assumption A-2).</summary>
public class GradeBand : Entity<long>
{
    public required string Grade { get; set; }
    public string? NameBn { get; set; }
    public int MinScore { get; set; }
}

/// <summary>
/// The numbers in the UGC Internship Policy. A single row: change it in the database when the Commission changes a rule.
/// </summary>
public class InternshipPolicy : Entity<long>
{
    public int BatchSize { get; set; } = 20;                // cl.6 (ka)
    public int MaxBatchesPerYear { get; set; } = 2;         // cl.6 (kha)
    public int MinMonths { get; set; } = 3;                 // cl.8
    public int MaxMonths { get; set; } = 6;                 // cl.8
    public decimal MonthlyAllowance { get; set; } = 20000;  // cl.10 (ka), excluding VAT and tax
    public int MinAttendancePct { get; set; } = 80;         // cl.11 (nya)
    public int ReportWorkingDays { get; set; } = 15;        // cl.11 (ta)
    public int GraduateWindowMonths { get; set; } = 12;     // cl.7 (kha, ga)
    public int MaxPreferredDepartments { get; set; } = 3;
    public int MinStatementLength { get; set; } = 30;
    public int MinReportLength { get; set; } = 60;
    public int MinRecommendationLength { get; set; } = 30;  // cl.14 recommendation to the Commission

    /// <summary>
    /// When the department may evaluate (cl.14). True: once the intern has submitted the final report.
    /// False: once the internship period has ended.
    /// </summary>
    public bool EvaluationAfterReport { get; set; } = true;

    // Daily shift used by the attendance app (Bangladesh time).
    public string ShiftName { get; set; } = "Day shift";
    public TimeOnly ShiftStart { get; set; } = new(9, 0);
    public TimeOnly ShiftEnd { get; set; } = new(17, 0);
}

/// <summary>
/// A university, shared by every module. Education lines and trainer profiles point here, so rules that depend on the
/// university (Soft Skill cl.7: public universities only; cl.6: seats per university) read a fact, not free text.
/// </summary>
public class University : Entity<long>
{
    public const int MaxNameLength = 200;
    public const int MaxShortNameLength = 20;

    public required string Name { get; set; }
    public string? NameBn { get; set; }
    public string? ShortName { get; set; }

    /// <summary>Public university under the Commission (Soft Skill cl.2 ঝ).</summary>
    public bool IsPublic { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
