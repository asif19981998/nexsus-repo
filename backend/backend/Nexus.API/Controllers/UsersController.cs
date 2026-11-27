using Microsoft.AspNetCore.Mvc;
using Nexus.API.Security;
using Nexus.Application.Dtos.Users;
using Nexus.Application.Security;
using Nexus.Application.Services.Users;

namespace Nexus.API.Controllers;

[ApiController]
[Route("api/v1/users")]
public class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.UsersView)]
    public async Task<ActionResult<List<UserListItemDto>>> List([FromQuery] string? role, [FromQuery] string? search, [FromQuery] bool toReview = false) =>
        Ok(await users.ListAsync(role, search, toReview));

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.UsersView)]
    public async Task<ActionResult<UserDetailDto>> Get(long id) => Ok(await users.GetAsync(id));

    /// <summary>Create a user and give them a role (Admin, Student or Mentor).</summary>
    [HttpPost]
    [HasPermission(Permissions.UsersManage)]
    public async Task<ActionResult<UserDetailDto>> Create(CreateUserRequest request)
    {
        var created = await users.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.UsersManage)]
    public async Task<ActionResult<UserDetailDto>> Update(long id, UpdateUserRequest request) => Ok(await users.UpdateAsync(id, request));

    [HttpPost("{id:long}/active")]
    [HasPermission(Permissions.UsersManage)]
    public async Task<ActionResult<UserDetailDto>> SetActive(long id, SetActiveRequest request) => Ok(await users.SetActiveAsync(id, request.IsActive));

    [HttpPost("{id:long}/reset-password")]
    [HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> ResetPassword(long id, ResetPasswordRequest request)
    {
        await users.ResetPasswordAsync(id, request.NewPassword);
        return NoContent();
    }

    [HttpGet("roles")]
    [HasPermission(Permissions.UsersView, Permissions.UsersManage)]
    public async Task<ActionResult<List<RoleDto>>> Roles() => Ok(await users.ListRolesAsync());

    /// <summary>Active mentors, for the placement and transfer forms.</summary>
    [HttpGet("mentors")]
    [HasPermission(Permissions.InternPlace, Permissions.InternTransfer, Permissions.UsersView)]
    public async Task<ActionResult<List<MentorOptionDto>>> Mentors() => Ok(await users.ListMentorsAsync());
}
