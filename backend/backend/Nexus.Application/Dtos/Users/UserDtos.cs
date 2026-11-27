namespace Nexus.Application.Dtos.Users;

public sealed class LoginRequest
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}

public sealed record MeDto(
    long Id, string FullName, string Email, string? Role, string? Designation, string? Department,
    IReadOnlyList<string> Permissions, long? EmployeeId = null);

/// <summary>Self sign-up of a student (role Student) or a university teacher (role Trainer). Details are added later in the profile.</summary>
public sealed class RegisterRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }

    /// <summary>Bangladeshi mobile number; "+880" or "880" in front is accepted and dropped.</summary>
    public string? Phone { get; set; }
    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }

    /// <summary>"The information I give is correct."</summary>
    public bool Declaration { get; set; }

    /// <summary>"Student" (default) or "Trainer" — a university teacher joining the Soft Skill programme.</summary>
    public string? AccountType { get; set; }
}

public sealed record LoginResponse(string Token, DateTime ExpiresAt, MeDto User);

public sealed record StudentProfileDto(
    string? University, string? Programme, string Level, int? CurrentYear, string? GraduationMonth, bool IdentityVerified);

public sealed record UserListItemDto(
    long Id, string FullName, string Email, string? Phone, string? Role, bool IsActive,
    string? Designation, long? DepartmentId, string? Department, string? University, DateTime CreatedAt,
    int Documents = 0, int DocumentsToReview = 0, bool? IdentityVerified = null, DateTime? WaitingSince = null);

public sealed record UserDetailDto(
    long Id, string FullName, string Email, string? Phone, string? Role, bool IsActive,
    string? Designation, long? DepartmentId, string? Department, StudentProfileDto? Student, DateTime CreatedAt);

public sealed record RoleDto(long Id, string Name, string? Description);

public sealed record MentorOptionDto(long Id, string Name, string? Designation, long? DepartmentId, string? Department);

public sealed class StudentProfileRequest
{
    public string? University { get; set; }
    public string? Programme { get; set; }

    /// <summary>"Undergraduate" or "Postgraduate".</summary>
    public string? Level { get; set; }
    public int? CurrentYear { get; set; }

    /// <summary>"YYYY-MM".</summary>
    public string? GraduationMonth { get; set; }
    public bool IdentityVerified { get; set; }
}

public sealed class CreateUserRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Password { get; set; }
    public string? Role { get; set; }
    public string? Designation { get; set; }
    public long? DepartmentId { get; set; }
    public StudentProfileRequest? Student { get; set; }
}

public sealed class UpdateUserRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Designation { get; set; }
    public long? DepartmentId { get; set; }
    public StudentProfileRequest? Student { get; set; }
}

public sealed class SetActiveRequest
{
    public bool IsActive { get; set; }
}

public sealed class ResetPasswordRequest
{
    public string? NewPassword { get; set; }
}

public sealed record NotificationDto(long Id, string Title, string? Body, string? Link, bool IsRead, DateTime CreatedAt);
public sealed record NotificationListDto(int Unread, IReadOnlyList<NotificationDto> Items);

/// <summary>
/// The signed-in user's own profile, whatever the role. IdentityVerified is null for accounts that are not students.
/// MainLocked is true while an application in progress relies on the main education line.
/// Trainer is set only on Trainer accounts: the university the teacher works at.
/// </summary>
public sealed record MyProfileDto(
    string FullName, string Email, string? Phone, string? Role, bool? IdentityVerified, bool MainLocked, IReadOnlyList<EducationDto> Educations,
    SoftSkills.TrainerProfileDto? Trainer = null, bool TrainerLocked = false);

/// <summary>
/// One education line. Level: Ssc, Hsc, Diploma, Bachelor, Master, Doctorate. Status: Studying or Completed.
/// UniversityId is set when the institution was picked from the university list.
/// </summary>
public sealed record EducationDto(
    long Id, string Level, string Institution, string? Programme, string Status, int? CurrentYear, string? ResultMonth, string? Result, bool IsMain,
    long? UniversityId = null);

public sealed class EducationRequest
{
    public string? Level { get; set; }

    /// <summary>Free text; ignored when <see cref="UniversityId"/> is given, which fills it with the university's name.</summary>
    public string? Institution { get; set; }

    /// <summary>A university from the list (bachelor's or higher). Soft Skill cl.7 reads it.</summary>
    public long? UniversityId { get; set; }
    public string? Programme { get; set; }

    /// <summary>"Studying" or "Completed".</summary>
    public string? Status { get; set; }

    /// <summary>Year of study while studying; required for a bachelor's degree.</summary>
    public int? CurrentYear { get; set; }

    /// <summary>"YYYY-MM": when the result came out (Completed, required) or is expected (Studying, optional).</summary>
    public string? ResultMonth { get; set; }

    /// <summary>"CGPA 3.62 / 4.00", "GPA 5.00".</summary>
    public string? Result { get; set; }

    /// <summary>Make this the line cl.7 reads. Only a bachelor's degree or higher.</summary>
    public bool IsMain { get; set; }
}
