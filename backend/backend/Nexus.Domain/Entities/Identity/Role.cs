using Microsoft.AspNetCore.Identity;

namespace Nexus.Domain.Entities.Identity;

public class Role : IdentityRole<long>
{
    public string? Description { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    public Role()
    {
    }

    public Role(string name, string? description = null) : base(name)
    {
        Description = description;
    }
}

/// <summary>A single capability, e.g. "internship.batch.manage". Granted to roles through <see cref="RolePermission"/>.</summary>
public class Permission
{
    public long Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Module { get; set; }
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>Which role holds which permission. Edited directly in the database for now.</summary>
public class RolePermission
{
    public long RoleId { get; set; }
    public Role Role { get; set; } = default!;
    public long PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
}
