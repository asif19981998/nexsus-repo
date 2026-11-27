using Nexus.Domain.Abstractions;
using Nexus.Domain.Entities.Setup;

namespace Nexus.Domain.Entities.Identity;

/// <summary>
/// What only a Trainer account carries: the university teacher's workplace (Soft Skill cl.12 ক: trainers are the
/// university's own teachers). The job title is <see cref="User.Designation"/>.
/// </summary>
public class TrainerProfile : Entity<long>
{
    public const int MaxTextLength = 150;

    public long UserId { get; set; }
    public User User { get; set; } = default!;

    public long? UniversityId { get; set; }
    public University? University { get; set; }

    /// <summary>The teacher's own academic department ("Department of English").</summary>
    public string? AcademicDepartment { get; set; }

    /// <summary>The university's staff id, if the teacher gives one.</summary>
    public string? EmployeeId { get; set; }
}
