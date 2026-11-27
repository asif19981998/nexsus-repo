namespace Nexus.Domain.Enums;

/// <summary>A teacher's standing as a Soft Skill trainer (cl.12).</summary>
public enum TrainerStatus
{
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Suspended = 4,
}

public enum SessionStatus
{
    Scheduled = 1,

    /// <summary>Attendance has been taken.</summary>
    Held = 2,
    Cancelled = 3,
}

/// <summary>cl.11 (ছ): absence only for health or another acceptable reason.</summary>
public enum LeaveReasonType
{
    Health = 1,
    Other = 2,
}

/// <summary>cl.11 (ঝ): a learning report or a small project.</summary>
public enum LearningReportType
{
    Report = 1,
    MiniProject = 2,
}

/// <summary>cl.14 (ঘ) monitoring visit.</summary>
public enum VisitStatus
{
    Planned = 1,
    Submitted = 2,
    Cancelled = 3,
}

/// <summary>cl.17: what an honorarium pays for.</summary>
public enum HonorariumKind
{
    /// <summary>A trainer, per session held.</summary>
    Session = 1,

    /// <summary>A university group's coordinator, once the university's final report is sent.</summary>
    Coordination = 2,

    /// <summary>A monitor, per visit reported.</summary>
    MonitoringVisit = 3,
}
