using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.Users;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Users;

public sealed record IssuedToken(string Token, DateTime ExpiresAt);

/// <summary>Signs a JWT for a user. Implemented in Infrastructure.</summary>
public interface ITokenService
{
    IssuedToken Create(User user, string? role);
}

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<MeDto> GetMeAsync();

    /// <summary>Creates a Student account from the public sign-up form and signs it in.</summary>
    Task<LoginResponse> RegisterAsync(RegisterRequest request);
}

public sealed partial class AuthService(
    UserManager<User> users, IUnitOfWork uow, ITokenService tokens, IPermissionService permissions, ICurrentUserService me,
    IClock clock, IAuditService audit) : IAuthService
{
    private const string Invalid = "Invalid email or password.";
    private const int MaxNameLength = 150;
    private const int MaxEmailLength = 256;

    public async Task<LoginResponse> RegisterAsync(RegisterRequest r)
    {
        var fullName = r.FullName?.Trim();
        if (string.IsNullOrEmpty(fullName)) throw new BadRequestException("Enter your full name.");
        if (fullName.Length > MaxNameLength) throw new BadRequestException($"Full name can be at most {MaxNameLength} characters.");

        var email = r.Email?.Trim();
        if (string.IsNullOrEmpty(email)) throw new BadRequestException("Enter your email address.");
        if (email.Length > MaxEmailLength || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email || !email.Contains('.'))
            throw new BadRequestException("Enter a valid email address.");

        var phone = NormalizePhone(r.Phone) ?? throw new BadRequestException("Enter a valid Bangladeshi mobile number, e.g. 01712345678.");

        if (string.IsNullOrEmpty(r.Password)) throw new BadRequestException("Choose a password.");
        if (r.Password != r.ConfirmPassword) throw new BadRequestException("The two passwords do not match.");
        if (!r.Declaration) throw new BadRequestException("Confirm that the information you gave is correct.");

        // Only the roles open to the public; staff accounts are made by an administrator.
        var role = string.IsNullOrWhiteSpace(r.AccountType)
            ? Roles.Student
            : Roles.SelfSignUp.FirstOrDefault(x => string.Equals(x, r.AccountType.Trim(), StringComparison.OrdinalIgnoreCase))
              ?? throw new BadRequestException("Choose whether you are joining as a student or as a university teacher.");

        if (await users.FindByEmailAsync(email) is not null)
            throw new ConflictException("An account with this email already exists. Sign in instead.");
        if (await uow.Users.PhoneExistsAsync(phone))
            throw new ConflictException("An account with this mobile number already exists.");

        var user = new User
        {
            FullName = fullName,
            UserName = email,
            Email = email,
            PhoneNumber = phone,
            IsActive = true,
            CreatedAt = clock.UtcNow,
        };
        // Identity checks the password rules here, before anything is stored.
        var created = await users.CreateAsync(user, r.Password);
        if (!created.Succeeded) throw new BadRequestException(string.Join(" ", created.Errors.Select(e => e.Description)));
        var added = await users.AddToRoleAsync(user, role);
        if (!added.Succeeded) throw new BadRequestException(string.Join(" ", added.Errors.Select(e => e.Description)));

        // Empty until the person adds details; each programme's eligibility check reports what is missing.
        if (role == Roles.Trainer) await uow.TrainerProfiles.AddAsync(new TrainerProfile { UserId = user.Id });
        else await uow.StudentProfiles.AddAsync(new StudentProfile { UserId = user.Id });
        await audit.LogAsync("user.register", "User", user.Id, $"{email} signed up as {role}");
        await uow.SaveChangesAsync();

        var token = tokens.Create(user, role);
        return new LoginResponse(token.Token, token.ExpiresAt, await BuildMeAsync(user.Id));
    }

    /// <summary>"01712345678" from "01712-345678", "+8801712345678" or "8801712345678"; null if it is not a BD mobile number.</summary>
    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("880")) digits = digits[2..];
        return BdMobile().IsMatch(digits) ? digits : null;
    }

    [GeneratedRegex(@"^01[3-9]\d{8}$")]
    private static partial Regex BdMobile();

    public async Task<LoginResponse> LoginAsync(LoginRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrEmpty(r.Password)) throw new BadRequestException("Enter your email and password.");
        var user = await users.FindByEmailAsync(r.Email.Trim()) ?? throw new UnauthorizedException(Invalid);
        if (await users.IsLockedOutAsync(user)) throw new UnauthorizedException("Too many failed attempts. Try again in a few minutes.");

        if (!await users.CheckPasswordAsync(user, r.Password))
        {
            await users.AccessFailedAsync(user);
            throw new UnauthorizedException(Invalid);
        }
        if (!user.IsActive) throw new UnauthorizedException("This account is deactivated. Contact the administrator.");
        await users.ResetAccessFailedCountAsync(user);

        var role = await uow.Users.GetRoleNameAsync(user.Id);
        var token = tokens.Create(user, role);
        return new LoginResponse(token.Token, token.ExpiresAt, await BuildMeAsync(user.Id));
    }

    public async Task<MeDto> GetMeAsync() => await BuildMeAsync(me.RequireUserId());

    private async Task<MeDto> BuildMeAsync(long userId)
    {
        var found = await uow.Users.GetAsync(userId) ?? throw new UnauthorizedException("Your session has ended. Sign in again.");
        if (!found.User.IsActive) throw new UnauthorizedException("This account is deactivated. Contact the administrator.");
        var perms = await permissions.GetForUserAsync(userId);
        var u = found.User;
        // Attendance is for interns with a running internship; their employee id is their user id.
        var statuses = await uow.Applications.GetStatusesForStudentAsync(userId);
        long? employeeId = statuses.Contains(ApplicationStatus.Active) ? userId : null;
        return new MeDto(u.Id, u.FullName, u.Email!, found.Role, u.Designation, u.Department?.Name, perms.OrderBy(p => p).ToList(), employeeId);
    }
}
