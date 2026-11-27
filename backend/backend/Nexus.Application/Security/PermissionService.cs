using Nexus.Application.Data;
using Nexus.BuildingBlocks.Exceptions;

namespace Nexus.Application.Security;

/// <summary>
/// Reads the caller's permissions from RolePermission once per request (this service is scoped).
/// Because it reads the database rather than the token, a change to RolePermission applies on the next request.
/// </summary>
public sealed class PermissionService(IUnitOfWork uow, ICurrentUserService me) : IPermissionService
{
    private readonly Dictionary<long, IReadOnlySet<string>> _cache = new();

    public async Task<IReadOnlySet<string>> GetForUserAsync(long userId)
    {
        if (_cache.TryGetValue(userId, out var hit)) return hit;
        var codes = await uow.Permissions.GetCodesForUserAsync(userId);
        IReadOnlySet<string> set = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
        _cache[userId] = set;
        return set;
    }

    public async Task<bool> HasAsync(string permission) =>
        me.UserId is { } id && (await GetForUserAsync(id)).Contains(permission);

    public async Task RequireAsync(string permission)
    {
        if (!await HasAsync(permission)) throw new ForbiddenException("You do not have permission to do this.");
    }
}
