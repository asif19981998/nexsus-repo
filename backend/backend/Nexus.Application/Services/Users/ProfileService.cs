using System.Globalization;
using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.SoftSkills;
using Nexus.Application.Dtos.Users;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.Application.Services.SoftSkills;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Users;

public interface IProfileService
{
    /// <summary>The signed-in user's profile and education history; any role.</summary>
    Task<MyProfileDto> GetMineAsync();

    Task<MyProfileDto> AddEducationAsync(EducationRequest request);
    Task<MyProfileDto> UpdateEducationAsync(long id, EducationRequest request);
    Task<MyProfileDto> DeleteEducationAsync(long id);

    /// <summary>Makes a bachelor's degree or higher the line the UGC Internship's cl.7 check reads.</summary>
    Task<MyProfileDto> SetMainEducationAsync(long id);

    /// <summary>A Trainer account's workplace: university, designation, department.</summary>
    Task<MyProfileDto> UpdateTrainerProfileAsync(TrainerProfileRequest request);
}

/// <summary>
/// The signed-in user's own profile, shared by every module (internship, Soft Skill).
/// Education is a list of lines; the main one feeds each programme's cl.7. While an application in progress in either
/// module relies on it the main line is locked, because reviewers read it; the other lines stay editable.
/// A trainer's university is locked the same way once a registration is waiting or approved.
/// Identity verification stays with the administrator.
/// </summary>
public sealed class ProfileService(IUnitOfWork uow, ICurrentUserService me, IClock clock, IAuditService audit) : IProfileService
{
    private const string LockedMessage = "An application in progress relies on your main education, so it cannot change now. Contact the administrator.";

    public async Task<MyProfileDto> GetMineAsync() => await BuildAsync(me.RequireUserId());

    public async Task<MyProfileDto> AddEducationAsync(EducationRequest r)
    {
        var userId = me.RequireUserId();
        var e = new Education { UserId = userId, Institution = "" };
        await ApplyAsync(e, r);
        var hasMain = await uow.Educations.AnyAsync(x => x.UserId == userId && x.IsMain);
        // The first bachelor's degree or higher becomes the main line by itself.
        var makeMain = Education.IsTertiary(e.Level) && (r.IsMain || !hasMain);
        if (makeMain && hasMain)
        {
            if (await IsMainLockedAsync(userId)) throw new ConflictException(LockedMessage);
            await ClearMainAsync(userId);
        }
        e.IsMain = makeMain;
        await uow.Educations.AddAsync(e);
        await audit.LogAsync("education.add", "Education", null, Describe(e));
        await uow.SaveChangesAsync();
        return await BuildAsync(userId);
    }

    public async Task<MyProfileDto> UpdateEducationAsync(long id, EducationRequest r)
    {
        var userId = me.RequireUserId();
        var e = await MineAsync(id, userId);
        if (e.IsMain && await IsMainLockedAsync(userId)) throw new ConflictException(LockedMessage);
        await ApplyAsync(e, r);
        if (e.IsMain && !Education.IsTertiary(e.Level))
            throw new BadRequestException("The main line must be a bachelor's degree or higher. Make another line the main one first.");
        await audit.LogAsync("education.update", "Education", e.Id, Describe(e));
        await uow.SaveChangesAsync();
        return await BuildAsync(userId);
    }

    public async Task<MyProfileDto> DeleteEducationAsync(long id)
    {
        var userId = me.RequireUserId();
        var e = await MineAsync(id, userId);
        if (e.IsMain && await IsMainLockedAsync(userId)) throw new ConflictException(LockedMessage);
        uow.Educations.Remove(e);
        await audit.LogAsync("education.delete", "Education", e.Id, Describe(e));
        await uow.SaveChangesAsync();
        return await BuildAsync(userId);
    }

    public async Task<MyProfileDto> SetMainEducationAsync(long id)
    {
        var userId = me.RequireUserId();
        var e = await MineAsync(id, userId);
        if (e.IsMain) return await BuildAsync(userId);
        if (!Education.IsTertiary(e.Level)) throw new BadRequestException("Only a bachelor's degree or higher can be the main line.");
        if (await IsMainLockedAsync(userId)) throw new ConflictException(LockedMessage);
        await ClearMainAsync(userId);
        e.IsMain = true;
        await audit.LogAsync("education.main", "Education", e.Id, Describe(e));
        await uow.SaveChangesAsync();
        return await BuildAsync(userId);
    }

    // ---------- helpers ----------

    private async Task<Education> MineAsync(long id, long userId)
    {
        var e = await uow.Educations.GetByIdAsync(id);
        return e is not null && e.UserId == userId ? e : throw new NotFoundException("Education", id);
    }

    /// <summary>Unsets the current main line and saves, so the one-main-per-person index never sees two.</summary>
    private async Task ClearMainAsync(long userId)
    {
        foreach (var old in await uow.Educations.FindAsync(x => x.UserId == userId && x.IsMain))
        {
            old.IsMain = false;
            uow.Educations.Update(old);
        }
        await uow.SaveChangesAsync();
    }

    private async Task<bool> IsMainLockedAsync(long userId) =>
        (await uow.Applications.GetStatusesForStudentAsync(userId)).Any(s => InternshipRules.LiveStatuses.Contains(s))
        || (await uow.SoftSkillApplications.GetStatusesForStudentAsync(userId)).Any(s => SoftSkillRules.LiveStatuses.Contains(s));

    private async Task<bool> IsTrainerLockedAsync(long userId) =>
        await uow.SoftSkillTrainers.AnyAsync(t => t.UserId == userId && (t.Status == TrainerStatus.Submitted || t.Status == TrainerStatus.Approved));

    public async Task<MyProfileDto> UpdateTrainerProfileAsync(TrainerProfileRequest r)
    {
        var userId = me.RequireUserId();
        if (await uow.Users.GetRoleNameAsync(userId) != Roles.Trainer) throw new ForbiddenException("Only trainer accounts have a workplace profile.");
        var user = await uow.Users.GetTrackedWithProfileAsync(userId) ?? throw new NotFoundException("User", userId);
        var existing = await uow.TrainerProfiles.GetSingleAsync(p => p.UserId == userId);
        var profile = existing is null ? new TrainerProfile { UserId = userId } : (await uow.TrainerProfiles.GetByIdAsync(existing.Id))!;

        if (r.UniversityId is not { } universityId) throw new BadRequestException("Choose your university.");
        var university = await uow.Universities.GetByIdAsync(universityId);
        if (university is null || !university.IsActive) throw new BadRequestException("Choose your university from the list.");
        if (profile.UniversityId != universityId && profile.UniversityId is not null && await IsTrainerLockedAsync(userId))
            throw new ConflictException("Your trainer registration names your university, so it cannot change now. Contact the administrator.");

        var designation = r.Designation?.Trim();
        if (string.IsNullOrEmpty(designation)) throw new BadRequestException("Enter your designation, e.g. Assistant Professor.");
        if (designation.Length > 150) throw new BadRequestException("Designation can be at most 150 characters.");
        var dept = r.AcademicDepartment?.Trim();
        if (dept is { Length: > TrainerProfile.MaxTextLength }) throw new BadRequestException($"Department can be at most {TrainerProfile.MaxTextLength} characters.");
        var employeeId = r.EmployeeId?.Trim();
        if (employeeId is { Length: > 50 }) throw new BadRequestException("Staff id can be at most 50 characters.");

        profile.UniversityId = universityId;
        profile.AcademicDepartment = string.IsNullOrEmpty(dept) ? null : dept;
        profile.EmployeeId = string.IsNullOrEmpty(employeeId) ? null : employeeId;
        if (existing is null) await uow.TrainerProfiles.AddAsync(profile);
        user.Designation = designation;
        await audit.LogAsync("trainer.profile", "TrainerProfile", existing?.Id, $"{university.Name}, {designation}");
        await uow.SaveChangesAsync();
        return await BuildAsync(userId);
    }

    private async Task ApplyAsync(Education e, EducationRequest r)
    {
        if (!Enum.TryParse<EducationLevel>(r.Level, true, out var level) || !Enum.IsDefined(level))
            throw new BadRequestException("Choose the level: SSC, HSC, diploma, bachelor's, master's or doctorate.");
        if (!Enum.TryParse<EducationStatus>(r.Status, true, out var status) || !Enum.IsDefined(status))
            throw new BadRequestException("Choose whether you are still studying or have completed it.");

        // A university from the list fixes the name; Soft Skill cl.7 reads whether it is public.
        long? universityId = null;
        string institution;
        if (r.UniversityId is { } uid && Education.IsTertiary(level))
        {
            var university = await uow.Universities.GetByIdAsync(uid);
            if (university is null || !university.IsActive) throw new BadRequestException("Choose a university from the list.");
            universityId = university.Id;
            institution = university.Name;
        }
        else
        {
            institution = Text(r.Institution, "Institution", required: true)!;
        }
        var programme = Text(r.Programme, level >= EducationLevel.Diploma ? "Programme / department" : "Group", required: level >= EducationLevel.Diploma);
        var result = r.Result?.Trim();
        if (string.IsNullOrEmpty(result)) result = null;
        else if (result.Length > Education.MaxResultLength) throw new BadRequestException($"Result can be at most {Education.MaxResultLength} characters.");

        var thisMonth = AcademicRules.MonthOf(clock.Today);
        DateOnly? month = null;
        if (!string.IsNullOrWhiteSpace(r.ResultMonth))
            month = ParseMonth(r.ResultMonth) ?? throw new BadRequestException("The month must look like 2026-06.");

        int? year = null;
        if (status == EducationStatus.Studying)
        {
            if (r.CurrentYear is not null and not (>= 1 and <= 6)) throw new BadRequestException("Year of study must be 1–6.");
            if (level == EducationLevel.Bachelor && r.CurrentYear is null) throw new BadRequestException("Choose your year of study.");
            year = r.CurrentYear;
            if (month < thisMonth) throw new BadRequestException("The expected result month has passed. If the result is out, choose Completed.");
        }
        else
        {
            if (month is null) throw new BadRequestException("Enter the month the result was published.");
            if (month > thisMonth) throw new BadRequestException("The result month cannot be in the future. If you are still studying, choose Studying.");
        }

        e.Level = level;
        e.Institution = institution;
        e.UniversityId = universityId;
        e.Programme = programme;
        e.Status = status;
        e.CurrentYear = year;
        e.ResultMonth = month;
        e.Result = result;
    }

    private async Task<MyProfileDto> BuildAsync(long userId)
    {
        var found = await uow.Users.GetAsync(userId) ?? throw new NotFoundException("User", userId);
        var u = found.User;
        var educations = await uow.Users.ListEducationsAsync(userId);
        var locked = educations.Any(e => e.IsMain) && await IsMainLockedAsync(userId);
        TrainerProfileDto? trainer = null;
        var trainerLocked = false;
        if (found.Role == Roles.Trainer)
        {
            trainer = TrainerService.ProfileDto(await uow.Users.GetTrainerProfileAsync(userId), u.Designation);
            trainerLocked = await IsTrainerLockedAsync(userId);
        }
        return new MyProfileDto(u.FullName, u.Email!, u.PhoneNumber, found.Role, u.StudentProfile?.IdentityVerified, locked,
            educations.Select(ToDto).ToList(), trainer, trainerLocked);
    }

    public static EducationDto ToDto(Education e) =>
        new(e.Id, e.Level.ToString(), e.Institution, e.Programme, e.Status.ToString(), e.CurrentYear,
            e.ResultMonth?.ToString("yyyy-MM", CultureInfo.InvariantCulture), e.Result, e.IsMain, e.UniversityId);

    private static string Describe(Education e) =>
        $"{e.Level}, {e.Institution}{(e.Programme is null ? "" : ", " + e.Programme)}, {e.Status}" +
        $"{(e.CurrentYear is null ? "" : $", year {e.CurrentYear}")}{(e.ResultMonth is null ? "" : $", {e.ResultMonth:yyyy-MM}")}";

    private static string? Text(string? value, string field, bool required)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return required ? throw new BadRequestException($"{field} is required.") : null;
        if (v.Length > Education.MaxTextLength) throw new BadRequestException($"{field} can be at most {Education.MaxTextLength} characters.");
        return v;
    }

    private static DateOnly? ParseMonth(string value) =>
        DateOnly.TryParseExact(value.Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
