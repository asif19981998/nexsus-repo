using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.Users;
using Nexus.Application.Security;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.Common;

namespace Nexus.Application.Services.Common;

/// <summary>Front-end routes used as notification links.</summary>
public static class Links
{
    public const string Student = "/internship/programme";
    public const string Manage = "/internship/manage";
    public const string MyDocuments = "/account/documents";

    public const string SoftSkillStudent = "/softskill/programme";
    public const string SoftSkillManage = "/softskill/manage";
    public const string SoftSkillTrainer = "/softskill/trainer";
    public const string SoftSkillMonitoring = "/softskill/monitoring";
    public static string SoftSkillUniversityGroup(long id) => $"/softskill/university-groups/{id}";
}

public interface IAuditService
{
    /// <summary>Queues an audit row. It is saved with the caller's next SaveChanges, so it lands only if the change does.</summary>
    Task LogAsync(string action, string entityType, object? entityId, string? details);
}

public sealed class AuditService(IUnitOfWork uow, ICurrentUserService me, IClock clock) : IAuditService
{
    public Task LogAsync(string action, string entityType, object? entityId, string? details) =>
        uow.AuditLogs.AddAsync(new AuditLog
        {
            At = clock.UtcNow,
            ActorId = me.UserId,
            ActorName = me.FullName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            // Never let a long detail fail the whole transaction: keep the start, mark the cut.
            Details = details is { Length: > AuditLog.MaxDetailsLength } ? details[..(AuditLog.MaxDetailsLength - 1)] + "…" : details,
        });
}

public interface INotificationService
{
    /// <summary>Queues a notification; saved with the caller's next SaveChanges.</summary>
    Task NotifyAsync(long userId, string title, string? body, string? link);

    /// <summary>Queues a notification for every active user whose role holds the permission.</summary>
    Task NotifyHoldersAsync(string permission, string title, string? body, string? link, long? alsoUserId = null);

    Task<NotificationListDto> ListMineAsync();
    Task MarkReadAsync(long id);
    Task MarkAllReadAsync();
}

public sealed class NotificationService(IUnitOfWork uow, ICurrentUserService me, IClock clock) : INotificationService
{
    public Task NotifyAsync(long userId, string title, string? body, string? link) =>
        uow.Notifications.AddAsync(new Notification
        {
            UserId = userId,
            Title = title,
            Body = body,
            Link = link,
            CreatedAt = clock.UtcNow,
        });

    public async Task NotifyHoldersAsync(string permission, string title, string? body, string? link, long? alsoUserId = null)
    {
        var ids = (await uow.Users.ListIdsWithPermissionAsync(permission)).ToHashSet();
        if (alsoUserId is { } extra) ids.Add(extra);
        foreach (var id in ids) await NotifyAsync(id, title, body, link);
    }

    public async Task<NotificationListDto> ListMineAsync()
    {
        var userId = me.RequireUserId();
        var items = await uow.Notifications.ListForUserAsync(userId, 50);
        var unread = await uow.Notifications.UnreadCountAsync(userId);
        return new NotificationListDto(unread, items.Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt)).ToList());
    }

    public async Task MarkReadAsync(long id)
    {
        var userId = me.RequireUserId();
        var n = await uow.Notifications.GetByIdAsync(id);
        if (n is null || n.UserId != userId) throw new NotFoundException("Notification", id);
        n.IsRead = true;
        await uow.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync()
    {
        await uow.Notifications.MarkAllReadAsync(me.RequireUserId());
    }
}
