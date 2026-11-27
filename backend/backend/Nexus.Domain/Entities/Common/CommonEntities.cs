namespace Nexus.Domain.Entities.Common;

/// <summary>An in-app notification for one user.</summary>
public class Notification
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Who did what, when. Written for every state change.</summary>
public class AuditLog
{
    public const int MaxDetailsLength = 2000;

    public long Id { get; set; }
    public DateTime At { get; set; }
    public long? ActorId { get; set; }
    public string? ActorName { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
}
