using Microsoft.EntityFrameworkCore;
using Nexus.Application.Data;
using Nexus.Application.Security;
using Nexus.Domain.Entities.Common;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Enums;

namespace Nexus.Infrastructure.Repositories;

public class UserRepository(NexusDbContext ctx) : IUserRepository
{
    private IQueryable<string?> RoleOf(long userId) =>
        ctx.UserRoles.Where(ur => ur.UserId == userId).Join(ctx.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name);

    public async Task<List<UserWithRole>> ListAsync(string? role, string? search, bool toReviewOnly = false)
    {
        var q = ctx.Users.AsNoTracking()
            .Include(u => u.Department)
            .Include(u => u.StudentProfile)
            .Include(u => u.Educations)
            .Select(u => new
            {
                User = u,
                Role = ctx.UserRoles.Where(ur => ur.UserId == u.Id).Join(ctx.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).FirstOrDefault(),
                Documents = ctx.Set<UserDocument>().Count(d => d.UserId == u.Id && d.IsCurrent),
                ToReview = ctx.Set<UserDocument>().Count(d => d.UserId == u.Id && d.IsCurrent && d.Status == DocumentStatus.Submitted),
                WaitingSince = ctx.Set<UserDocument>()
                    .Where(d => d.UserId == u.Id && d.IsCurrent && d.Status == DocumentStatus.Submitted)
                    .Min(d => (DateTime?)d.CreatedAt),
            });

        if (toReviewOnly) q = q.Where(x => x.ToReview > 0);
        if (!string.IsNullOrWhiteSpace(role)) q = q.Where(x => x.Role == role);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.User.FullName.Contains(s) || x.User.Email!.Contains(s));
        }

        // Newest first. Seeded users share a CreatedAt, so the id breaks the tie.
        var rows = await q.OrderByDescending(x => x.User.CreatedAt).ThenByDescending(x => x.User.Id).ToListAsync();
        return rows.Select(x => new UserWithRole(x.User, x.Role, x.Documents, x.ToReview, x.WaitingSince)).ToList();
    }

    public async Task<UserWithRole?> GetAsync(long id)
    {
        var row = await ctx.Users.AsNoTracking()
            .Include(u => u.Department)
            .Include(u => u.StudentProfile)
            .Include(u => u.Educations)
            .Where(u => u.Id == id)
            .Select(u => new { User = u, Role = ctx.UserRoles.Where(ur => ur.UserId == u.Id).Join(ctx.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).FirstOrDefault() })
            .FirstOrDefaultAsync();
        return row is null ? null : new UserWithRole(row.User, row.Role);
    }

    public Task<User?> GetTrackedWithProfileAsync(long id) =>
        ctx.Users.Include(u => u.StudentProfile).Include(u => u.Educations).FirstOrDefaultAsync(u => u.Id == id);

    public Task<List<User>> ListMentorsAsync() =>
        ctx.Users.AsNoTracking()
            .Include(u => u.Department)
            .Where(u => u.IsActive && ctx.UserRoles.Any(ur => ur.UserId == u.Id && ctx.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Mentor)))
            .OrderBy(u => u.FullName)
            .ToListAsync();

    public Task<string?> GetRoleNameAsync(long userId) => RoleOf(userId).FirstOrDefaultAsync();

    public Task<List<Role>> ListRolesAsync() => ctx.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync();

    public Task<List<long>> ListIdsWithPermissionAsync(string permission) =>
        (from ur in ctx.UserRoles
         join rp in ctx.RolePermissions on ur.RoleId equals rp.RoleId
         join u in ctx.Users on ur.UserId equals u.Id
         where rp.Permission.Code == permission && u.IsActive
         select u.Id).Distinct().ToListAsync();

    public async Task<(string Email, string FullName)?> GetContactAsync(long userId)
    {
        var u = await ctx.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive && x.Email != null)
            .Select(x => new { x.Email, x.FullName })
            .FirstOrDefaultAsync();
        return u is null ? null : (u.Email!, u.FullName);
    }

    public Task<bool> PhoneExistsAsync(string phone) => ctx.Users.AnyAsync(u => u.PhoneNumber == phone);

    public async Task<(StudentProfile? Profile, Education? Main)> GetAcademicAsync(long userId)
    {
        var profile = await ctx.StudentProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        var main = await ctx.Educations.AsNoTracking().Include(e => e.University).FirstOrDefaultAsync(e => e.UserId == userId && e.IsMain);
        return (profile, main);
    }

    public Task<List<Education>> ListEducationsAsync(long userId) =>
        ctx.Educations.AsNoTracking().Where(e => e.UserId == userId).OrderByDescending(e => e.Level).ThenByDescending(e => e.Id).ToListAsync();

    public Task<TrainerProfile?> GetTrainerProfileAsync(long userId) =>
        ctx.TrainerProfiles.AsNoTracking().Include(p => p.University).FirstOrDefaultAsync(p => p.UserId == userId);
}

public class PermissionRepository(NexusDbContext ctx) : IPermissionRepository
{
    public Task<List<string>> GetCodesForUserAsync(long userId) =>
        (from ur in ctx.UserRoles
         join rp in ctx.RolePermissions on ur.RoleId equals rp.RoleId
         join u in ctx.Users on ur.UserId equals u.Id
         where ur.UserId == userId && u.IsActive
         select rp.Permission.Code).Distinct().ToListAsync();
}

public class NotificationRepository(NexusDbContext ctx) : BaseRepository<Notification>(ctx), INotificationRepository
{
    public Task<List<Notification>> ListForUserAsync(long userId, int take) =>
        Set.AsNoTracking().Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(take).ToListAsync();

    public Task<int> UnreadCountAsync(long userId) => Set.CountAsync(n => n.UserId == userId && !n.IsRead);

    public Task MarkAllReadAsync(long userId) =>
        Set.Where(n => n.UserId == userId && !n.IsRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
}
