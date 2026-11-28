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

/// <summary>
/// An email waiting to be sent (outbox). It is saved in the same transaction as the change that caused it, so an
/// email goes out only when that change is committed. A background worker sends it and retries on failure.
/// </summary>
public class EmailMessage
{
    public const int MaxAttempts = 6;

    public long Id { get; set; }
    public long? UserId { get; set; }
    public required string ToAddress { get; set; }
    public string? ToName { get; set; }
    public required string Subject { get; set; }

    /// <summary>The HTML body. Cleared after sending when <see cref="IsSensitive"/>, so passwords and reset links do not stay in the database.</summary>
    public string? HtmlBody { get; set; }

    /// <summary>The body holds a password or a reset link.</summary>
    public bool IsSensitive { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? SentAt { get; set; }

    /// <summary>Set when every attempt failed; the worker stops trying.</summary>
    public bool Failed { get; set; }
    public string? LastError { get; set; }
}
