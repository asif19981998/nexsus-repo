using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Nexus.Application.Security;
using Nexus.BuildingBlocks.Exceptions;

namespace Nexus.Infrastructure.Identity;

/// <summary>Reads the caller from the validated JWT (claims are not remapped, so "sub" stays "sub").</summary>
public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public long? UserId =>
        long.TryParse(Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

    public string? FullName => Principal?.FindFirst(JwtRegisteredClaimNames.Name)?.Value;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public long RequireUserId() => UserId ?? throw new UnauthorizedException("Sign in to continue.");
}
