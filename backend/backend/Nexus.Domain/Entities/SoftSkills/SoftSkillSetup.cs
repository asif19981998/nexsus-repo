using Nexus.Domain.Abstractions;

namespace Nexus.Domain.Entities.SoftSkills;

/// <summary>
/// The numbers in the UGC Soft Skill Training Policy. A single row: change it in the database when the Commission
/// changes a rule. Clause references are to the Bengali policy.
/// </summary>
public class SoftSkillPolicy : Entity<long>
{
    public int SeatsPerUniversity { get; set; } = 30;        // cl.6 (ক)
    public int MaxBatchesPerYear { get; set; } = 2;          // cl.6 (খ)
    public int MinStudyYear { get; set; } = 1;               // cl.7 (ক)
    public int MaxStudyYear { get; set; } = 2;               // cl.7 (ক)
    public int DurationMonths { get; set; } = 3;             // cl.8, not renewable
    public int TotalHours { get; set; } = 48;                // cl.8, cl.13
    public decimal MonthlyStipend { get; set; } = 5000;      // cl.10 (ক), the minimum
    public int MinAttendancePct { get; set; } = 80;          // cl.11 (ছ)

    /// <summary>
    /// cl.10 (খ): attendance the month's sessions need before its stipend is paid. Null: only the assessment result counts.
    /// </summary>
    public int? StipendMinAttendancePct { get; set; }

    /// <summary>True: an approved leave counts as attended in the cl.11 (ছ) percentage. The policy is silent; default false.</summary>
    public bool ApprovedLeaveCountsAsPresent { get; set; }

    public int ReportWorkingDays { get; set; } = 15;         // cl.11 (ঝ)
    public int AssessmentMaxMarks { get; set; } = 100;       // cl.14 (খ), set by the Commission
    public int AssessmentPassMarks { get; set; } = 50;       // cl.14 (খ), set by the Commission
    public int MinStatementLength { get; set; } = 30;
    public int MinReportLength { get; set; } = 100;
    public int MinFinalReportLength { get; set; } = 100;     // cl.14 (ঙ)

    // cl.17: honorarium rates follow the government's notices, so they start at 0 and the Commission sets them.
    // Nothing can be paid at a rate of 0.
    public decimal TrainerRatePerSession { get; set; }
    public decimal CoordinatorRatePerUniversityGroup { get; set; }
    public decimal ExpertRatePerVisit { get; set; }
}

/// <summary>
/// One of the six training topics of cl.13, each 8 hours. Called a topic here because "module" already means a programme
/// (internship, soft skill) in this system. <see cref="AssessmentNo"/> is the monthly assessment (1–3) that covers it (cl.14 ক).
/// </summary>
public class SoftSkillTopic : Entity<long>
{
    public const int MaxNameLength = 150;

    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? NameBn { get; set; }
    public int Hours { get; set; } = 8;
    public int AssessmentNo { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>cl.15 certificate category and the minimum average (percent of the three assessments) for it.</summary>
public class SoftSkillGradeBand : Entity<long>
{
    public required string Grade { get; set; }
    public string? NameBn { get; set; }
    public int MinAverage { get; set; }
}
