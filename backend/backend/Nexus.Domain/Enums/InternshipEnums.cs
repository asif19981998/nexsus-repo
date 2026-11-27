namespace Nexus.Domain.Enums;

public enum BatchStatus
{
    Draft = 1,
    ApplicationsOpen = 2,
    Selection = 3,
    Running = 4,
    Completed = 5,
    Cancelled = 6,
}

public enum ApplicationStatus
{
    Submitted = 1,
    Shortlisted = 2,
    Waitlisted = 3,
    Selected = 4,
    Active = 5,
    Completed = 6,
    Certified = 7,
    NotCertified = 8,
    Rejected = 9,
    Withdrawn = 10,
    Terminated = 11,
}

/// <summary>cl.9: the selection test is written, oral or online.</summary>
public enum ExamMode
{
    Written = 1,
    Oral = 2,
    Online = 3,
}

public enum AttendanceStatus
{
    Present = 1,
    LeaveRequested = 2,
    Leave = 3,
    Absent = 4,
}

public enum ReportStatus
{
    Submitted = 1,
    RevisionRequested = 2,
    Accepted = 3,
}

public enum AcademicLevel
{
    Undergraduate = 1,
    Postgraduate = 2,
}
