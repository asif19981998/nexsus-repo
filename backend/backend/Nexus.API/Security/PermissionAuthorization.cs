using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Nexus.Application.Security;

namespace Nexus.API.Security;

/// <summary>
/// Allows the action when the caller's role holds at least one of the listed permissions.
/// <c>[HasPermission(Permissions.BatchManage)]</c>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string Prefix = "perm:";

    public HasPermissionAttribute(params string[] anyOf)
    {
        if (anyOf.Length == 0) throw new ArgumentException("Name at least one permission.", nameof(anyOf));
        Policy = Prefix + string.Join('|', anyOf);
    }
}

public sealed class PermissionRequirement(IReadOnlyList<string> anyOf) : IAuthorizationRequirement
{
    public IReadOnlyList<string> AnyOf { get; } = anyOf;
}

/// <summary>Builds a policy on the fly for every "perm:a|b" name, so permissions need no registration.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(HasPermissionAttribute.Prefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);

        var codes = policyName[HasPermissionAttribute.Prefix.Length..].Split('|', StringSplitOptions.RemoveEmptyEntries);
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(codes))
            .Build();
    }
}

/// <summary>Checks the requirement against the RolePermission table (via the scoped <see cref="IPermissionService"/>).</summary>
public sealed class PermissionAuthorizationHandler(IPermissionService permissions) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        foreach (var code in requirement.AnyOf)
        {
            if (await permissions.HasAsync(code))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
