using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.Users;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Users;

public interface IUserService
{
    Task<List<UserListItemDto>> ListAsync(string? role, string? search, bool toReviewOnly = false);
    Task<UserDetailDto> GetAsync(long id);
    Task<UserDetailDto> CreateAsync(CreateUserRequest request);
    Task<UserDetailDto> UpdateAsync(long id, UpdateUserRequest request);
    Task<UserDetailDto> SetActiveAsync(long id, bool isActive);
    Task ResetPasswordAsync(long id, string? newPassword);
    Task<List<RoleDto>> ListRolesAsync();
    Task<List<MentorOptionDto>> ListMentorsAsync();
}

/// <summary>
/// User administration. The role is chosen when the user is created and is not changed afterwards;
/// what each role may do lives in the RolePermission table.
/// </summary>
public sealed class UserService(
    UserManager<User> userManager, IUnitOfWork uow, ICurrentUserService me, IClock clock, IAuditService audit) : IUserService
{
    public async Task<List<UserListItemDto>> ListAsync(string? role, string? search, bool toReviewOnly = false)
    {
        var rows = await uow.Users.ListAsync(string.IsNullOrWhiteSpace(role) || role == "all" ? null : role, search, toReviewOnly);
        return rows.Select(r => new UserListItemDto(
            r.User.Id, r.User.FullName, r.User.Email!, r.User.PhoneNumber, r.Role, r.User.IsActive, r.User.Designation,
            r.User.DepartmentId, r.User.Department?.Name, r.User.MainEducation()?.Institution, r.User.CreatedAt,
            r.Documents, r.DocumentsToReview, r.User.StudentProfile?.IdentityVerified, r.WaitingSince)).ToList();
    }

    public async Task<UserDetailDto> GetAsync(long id)
    {
        var r = await uow.Users.GetAsync(id) ?? throw new NotFoundException("User", id);
        return ToDetail(r.User, r.Role);
    }

    public async Task<UserDetailDto> CreateAsync(CreateUserRequest r)
    {
        var role = Roles.All.FirstOrDefault(x => string.Equals(x, r.Role, StringComparison.OrdinalIgnoreCase))
            ?? throw new BadRequestException($"Choose a role: {string.Join(", ", Roles.All)}.");
        var fullName = Required(r.FullName, "Full name");
        var email = Required(r.Email, "Email");
        if (string.IsNullOrEmpty(r.Password)) throw new BadRequestException("Set an initial password.");
        if (await userManager.FindByEmailAsync(email) is not null) throw new ConflictException("A user with this email already exists.");
        await ValidateRoleFieldsAsync(role, r.DepartmentId, r.Student);

        var user = new User
        {
            FullName = fullName,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PhoneNumber = Clean(r.Phone),
            Designation = Clean(r.Designation),
            DepartmentId = role == Roles.Mentor ? r.DepartmentId : null,
            IsActive = true,
            CreatedAt = clock.UtcNow,
            CreatedById = me.UserId,
        };

        var created = await userManager.CreateAsync(user, r.Password);
        if (!created.Succeeded) throw new BadRequestException(string.Join(" ", created.Errors.Select(e => e.Description)));
        var added = await userManager.AddToRoleAsync(user, role);
        if (!added.Succeeded) throw new BadRequestException(string.Join(" ", added.Errors.Select(e => e.Description)));

        if (role == Roles.Student)
        {
            await uow.StudentProfiles.AddAsync(new StudentProfile { UserId = user.Id, IdentityVerified = r.Student!.IdentityVerified });
            if (MainFrom(r.Student, null) is { } main)
            {
                main.UserId = user.Id;
                await uow.Educations.AddAsync(main);
            }
        }

        await audit.LogAsync("user.create", "User", user.Id, $"{email} as {role}");
        await uow.SaveChangesAsync();
        return await GetAsync(user.Id);
    }

    public async Task<UserDetailDto> UpdateAsync(long id, UpdateUserRequest r)
    {
        var user = await uow.Users.GetTrackedWithProfileAsync(id) ?? throw new NotFoundException("User", id);
        var role = await uow.Users.GetRoleNameAsync(id);
        var fullName = Required(r.FullName, "Full name");
        var email = Required(r.Email, "Email");
        await ValidateRoleFieldsAsync(role, r.DepartmentId, r.Student);

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var other = await userManager.FindByEmailAsync(email);
            if (other is not null && other.Id != id) throw new ConflictException("A user with this email already exists.");
            await userManager.SetEmailAsync(user, email);
            await userManager.SetUserNameAsync(user, email);
            user.EmailConfirmed = true;
        }

        user.FullName = fullName;
        user.PhoneNumber = Clean(r.Phone);
        user.Designation = Clean(r.Designation);
        user.DepartmentId = role == Roles.Mentor ? r.DepartmentId : null;
        user.LastModifiedAt = clock.UtcNow;
        user.LastModifiedById = me.UserId;

        if (role == Roles.Student)
        {
            user.StudentProfile ??= new StudentProfile { UserId = user.Id };
            user.StudentProfile.IdentityVerified = r.Student!.IdentityVerified;
            // The administrator may correct the main education line at any time, even while it is locked for the student.
            var current = user.MainEducation();
            if (MainFrom(r.Student, current) is { } main && current is null) user.Educations.Add(main);
        }

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) throw new BadRequestException(string.Join(" ", result.Errors.Select(e => e.Description)));
        await audit.LogAsync("user.update", "User", id, email);
        await uow.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<UserDetailDto> SetActiveAsync(long id, bool isActive)
    {
        if (id == me.UserId && !isActive) throw new BadRequestException("You cannot deactivate your own account.");
        var user = await uow.Users.GetTrackedWithProfileAsync(id) ?? throw new NotFoundException("User", id);
        user.IsActive = isActive;
        user.LastModifiedAt = clock.UtcNow;
        user.LastModifiedById = me.UserId;
        await audit.LogAsync(isActive ? "user.activate" : "user.deactivate", "User", id, user.Email);
        await uow.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task ResetPasswordAsync(long id, string? newPassword)
    {
        if (string.IsNullOrEmpty(newPassword)) throw new BadRequestException("Enter the new password.");
        var user = await userManager.FindByIdAsync(id.ToString(CultureInfo.InvariantCulture)) ?? throw new NotFoundException("User", id);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded) throw new BadRequestException(string.Join(" ", result.Errors.Select(e => e.Description)));
        await audit.LogAsync("user.reset_password", "User", id, user.Email);
        await uow.SaveChangesAsync();
    }

    public async Task<List<RoleDto>> ListRolesAsync()
    {
        var roles = await uow.Users.ListRolesAsync();
        return roles.Select(r => new RoleDto(r.Id, r.Name!, r.Description)).ToList();
    }

    public async Task<List<MentorOptionDto>> ListMentorsAsync()
    {
        var mentors = await uow.Users.ListMentorsAsync();
        return mentors.Select(m => new MentorOptionDto(m.Id, m.FullName, m.Designation, m.DepartmentId, m.Department?.Name)).ToList();
    }

    // ---------- helpers ----------

    private async Task ValidateRoleFieldsAsync(string? role, long? departmentId, StudentProfileRequest? student)
    {
        if (role == Roles.Mentor)
        {
            var departments = await uow.Setup.GetDepartmentsAsync(activeOnly: true);
            if (departments.All(d => d.Id != departmentId)) throw new BadRequestException("Choose the mentor's department.");
        }
        if (role == Roles.Student)
        {
            if (student is null) throw new BadRequestException("Enter the student's academic details.");
            if (!string.IsNullOrWhiteSpace(student.Level) && !Enum.TryParse<AcademicLevel>(student.Level, true, out _))
                throw new BadRequestException("Level must be Undergraduate or Postgraduate.");
            if (student.CurrentYear is < 1 or > 6) throw new BadRequestException("Year of study must be 1–6.");
            if (!string.IsNullOrWhiteSpace(student.GraduationMonth) && ParseMonth(student.GraduationMonth) is null)
                throw new BadRequestException("Graduation month must look like 2026-06.");
        }
    }

    /// <summary>
    /// The user form's academic fields written to the main education line: <paramref name="main"/> when there is one,
    /// otherwise a new line. Null when the form leaves the university empty, so nothing is created.
    /// </summary>
    private Education? MainFrom(StudentProfileRequest s, Education? main)
    {
        var university = Clean(s.University);
        if (university is null) return null;
        var postgraduate = Enum.TryParse<AcademicLevel>(s.Level, true, out var level) && level == AcademicLevel.Postgraduate;
        var e = main ?? new Education { Institution = university, IsMain = true };
        e.Level = postgraduate ? (e.Level >= EducationLevel.Master ? e.Level : EducationLevel.Master) : EducationLevel.Bachelor;
        e.Institution = university;
        e.Programme = Clean(s.Programme);
        e.ResultMonth = ParseMonth(s.GraduationMonth);
        var finished = s.CurrentYear is null && e.ResultMonth is { } m && m <= AcademicRules.MonthOf(clock.Today);
        e.Status = finished ? EducationStatus.Completed : EducationStatus.Studying;
        e.CurrentYear = finished ? null : s.CurrentYear;
        return e;
    }

    private static DateOnly? ParseMonth(string? value) =>
        DateOnly.TryParseExact(value?.Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? throw new BadRequestException($"{field} is required.") : value.Trim();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The user form's academic fields, read from the main education line.</summary>
    private static StudentProfileDto StudentDto(StudentProfile s, Education? main) =>
        new(main?.Institution, main?.Programme,
            (main?.Level >= EducationLevel.Master ? AcademicLevel.Postgraduate : AcademicLevel.Undergraduate).ToString(),
            main?.Status == EducationStatus.Studying ? main.CurrentYear : null,
            main?.ResultMonth?.ToString("yyyy-MM"), s.IdentityVerified);

    private static UserDetailDto ToDetail(User u, string? role) =>
        new(u.Id, u.FullName, u.Email!, u.PhoneNumber, role, u.IsActive, u.Designation, u.DepartmentId, u.Department?.Name,
            u.StudentProfile is { } s ? StudentDto(s, u.MainEducation()) : null,
            u.CreatedAt);
}
