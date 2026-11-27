using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Application.Dtos.Users;
using Nexus.Application.Services.Common;
using Nexus.Application.Services.Users;

namespace Nexus.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Rate-limit policy for the anonymous sign-up endpoint (per client IP).</summary>
    public const string RegisterLimit = "register";

    /// <summary>Email + password → JWT, plus the user's role and permissions.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request) => Ok(await auth.LoginAsync(request));

    /// <summary>Public sign-up for internship applicants: creates a Student account and signs it in.</summary>
    [AllowAnonymous]
    [EnableRateLimiting(RegisterLimit)]
    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register(RegisterRequest request) => Ok(await auth.RegisterAsync(request));

    /// <summary>The signed-in user with permissions read fresh from the database.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<MeDto>> Me() => Ok(await auth.GetMeAsync());
}

[ApiController]
[Route("api/v1/notifications")]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationListDto>> List() => Ok(await notifications.ListMineAsync());

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        await notifications.MarkReadAsync(id);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await notifications.MarkAllReadAsync();
        return NoContent();
    }
}
