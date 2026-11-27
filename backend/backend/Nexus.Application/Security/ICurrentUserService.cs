namespace Nexus.Application.Security;

/// <summary>The signed-in caller, read from the JWT.</summary>
public interface ICurrentUserService
{
    long? UserId { get; }
    string? FullName { get; }
    bool IsAuthenticated { get; }

    /// <summary>The caller's id, or 401 when there is none.</summary>
    long RequireUserId();
}

/// <summary>The caller's permissions, read from the database once per request.</summary>
public interface IPermissionService
{
    Task<IReadOnlySet<string>> GetForUserAsync(long userId);
    Task<bool> HasAsync(string permission);

    /// <summary>Throws 403 unless the caller holds the permission.</summary>
    Task RequireAsync(string permission);
}
