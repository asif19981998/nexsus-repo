using Microsoft.AspNetCore.Identity;
using Nexus.Domain.Entities.Setup;

namespace Nexus.Domain.Entities.Identity;

/// <summary>ASP.NET Identity user. Each user holds exactly one role (Admin, Student, Mentor or Trainer).</summary>
public class User : IdentityUser<long>
{
    public required string FullName { get; set; }

    /// <summary>Job title, shown for mentors ("Deputy Director, Research").</summary>
    public string? Designation { get; set; }

    /// <summary>Home department of a mentor.</summary>
    public long? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public long? CreatedById { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public long? LastModifiedById { get; set; }

    public StudentProfile? StudentProfile { get; set; }

    /// <summary>Only on Trainer accounts: the university the teacher works at.</summary>
    public TrainerProfile? TrainerProfile { get; set; }

    /// <summary>Education history; see <see cref="Education"/>.</summary>
    public ICollection<Education> Educations { get; set; } = new List<Education>();

    /// <summary>The education line cl.7 reads, when the person has one. Needs <see cref="Educations"/> loaded.</summary>
    public Education? MainEducation() => Educations.FirstOrDefault(e => e.IsMain);

    public ICollection<IdentityUserRole<long>> UserRoles { get; set; } = new List<IdentityUserRole<long>>();
}
