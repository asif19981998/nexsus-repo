using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nexus.Application.Services.Users;
using Nexus.Domain.Entities.Identity;

namespace Nexus.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "Nexus.API";
    public string Audience { get; set; } = "Nexus.Web";

    /// <summary>HMAC signing key, at least 32 characters. Keep it out of source control in production.</summary>
    public string Key { get; set; } = "";
    public int ExpiryHours { get; set; } = 8;
}

/// <summary>
/// Issues the access token. It carries identity only (id, name, email, role); permissions are read
/// from the database on each request so a change in RolePermission takes effect without signing in again.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public IssuedToken Create(User user, string? role)
    {
        var o = options.Value;
        var expires = DateTime.UtcNow.AddHours(o.ExpiryHours);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        if (!string.IsNullOrEmpty(role)) claims.Add(new Claim("role", role));

        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, notBefore: DateTime.UtcNow, expires: expires, signingCredentials: creds);
        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
