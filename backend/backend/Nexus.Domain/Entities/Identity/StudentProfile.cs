using Nexus.Domain.Abstractions;

namespace Nexus.Domain.Entities.Identity;

/// <summary>
/// What only a student account carries. Academic details live in <see cref="Education"/> rows, shared by every module.
/// </summary>
public class StudentProfile : Entity<long>
{
    public long UserId { get; set; }
    public User User { get; set; } = default!;

    /// <summary>Set by the administrator, or when one of the person's identity documents is verified.</summary>
    public bool IdentityVerified { get; set; }
}
