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

    /// <summary>Rate-limit policy for the anonymous password-reset endpoints (per client IP).</summary>
    public const string PasswordResetLimit = "password-reset";

    /// <summary>Email + password → JWT, plus the user's role and permissions.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request) => Ok(await auth.LoginAsync(request));

    /// <summary>Public sign-up for internship applicants: creates a Student account and signs it in.</summary>
    [AllowAnonymous]
    [EnableRateLimiting(RegisterLimit)]
    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register(RegisterRequest request) => Ok(await auth.RegisterAsync(request));

    /// <summary>Emails a reset link if an active account has this email. Always 204, so it does not reveal who is registered.</summary>
    [AllowAnonymous]
    [EnableRateLimiting(PasswordResetLimit)]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await auth.ForgotPasswordAsync(request);
        return NoContent();
    }

    /// <summary>Sets a new password with the token from the emailed link.</summary>
    [AllowAnonymous]
    [EnableRateLimiting(PasswordResetLimit)]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(CompletePasswordResetRequest request)
    {
        await auth.CompletePasswordResetAsync(request);
        return NoContent();
    }

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
