using Nexus.Domain.Abstractions;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.Internships;

/// <summary>A student's application to one batch. Becomes an internship once UGC places the student.</summary>
public class InternshipApplication : Entity<long>
{
    public long BatchId { get; set; }
    public InternshipBatch Batch { get; set; } = default!;

    public long StudentId { get; set; }
    public User Student { get; set; } = default!;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    public DateTime AppliedAt { get; set; }
    public required string Statement { get; set; }

    /// <summary>The cl.7 clause the student qualified under, e.g. "cl.7 (ka)".</summary>
    public string? Basis { get; set; }

    /// <summary>Attached documents, comma separated (CV, Transcript).</summary>
    public string Documents { get; set; } = "CV,Transcript";
    public string? RejectionReason { get; set; }

    // cl.9 selection test
    public ExamMode? ExamMode { get; set; }
    public DateOnly? ExamDate { get; set; }
    public int? ExamScore { get; set; }

    public ICollection<ApplicationPreferredDepartment> PreferredDepartments { get; set; } = new List<ApplicationPreferredDepartment>();
    public ICollection<ApplicationStatusHistory> History { get; set; } = new List<ApplicationStatusHistory>();
    public Internship? Internship { get; set; }
}

public class ApplicationPreferredDepartment
{
    public long Id { get; set; }
    public long ApplicationId { get; set; }
    public InternshipApplication Application { get; set; } = default!;
    public long DepartmentId { get; set; }
    public Department Department { get; set; } = default!;
    public int Rank { get; set; }
}

/// <summary>One row per status change of an application.</summary>
public class ApplicationStatusHistory
{
    public long Id { get; set; }
    public long ApplicationId { get; set; }
    public InternshipApplication Application { get; set; } = default!;
    public ApplicationStatus Status { get; set; }
    public DateTime At { get; set; }
    public long? ById { get; set; }
    public string? ByName { get; set; }
    public string? Note { get; set; }
}
