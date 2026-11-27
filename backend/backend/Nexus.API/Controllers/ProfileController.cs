using Microsoft.AspNetCore.Mvc;
using Nexus.Application.Dtos.Users;
using Nexus.Application.Services.Users;

namespace Nexus.API.Controllers;

/// <summary>
/// The signed-in user's own profile and education history. They belong to the account, not to a module, so they need
/// no permission beyond signing in; every call reads or changes only the caller's own data.
/// </summary>
[ApiController]
public class ProfileController(IProfileService profiles) : ControllerBase
{
    [HttpGet("api/v1/me/profile")]
    public async Task<ActionResult<MyProfileDto>> Get() => Ok(await profiles.GetMineAsync());

    [HttpPost("api/v1/me/educations")]
    public async Task<ActionResult<MyProfileDto>> AddEducation(EducationRequest request) => Ok(await profiles.AddEducationAsync(request));

    [HttpPut("api/v1/me/educations/{id:long}")]
    public async Task<ActionResult<MyProfileDto>> UpdateEducation(long id, EducationRequest request) => Ok(await profiles.UpdateEducationAsync(id, request));

    [HttpDelete("api/v1/me/educations/{id:long}")]
    public async Task<ActionResult<MyProfileDto>> DeleteEducation(long id) => Ok(await profiles.DeleteEducationAsync(id));

    /// <summary>Makes this line the one the UGC Internship's cl.7 check reads.</summary>
    [HttpPost("api/v1/me/educations/{id:long}/main")]
    public async Task<ActionResult<MyProfileDto>> SetMain(long id) => Ok(await profiles.SetMainEducationAsync(id));

    /// <summary>A Trainer account's workplace (Soft Skill cl.12 ক). Other roles get 403.</summary>
    [HttpPut("api/v1/me/trainer-profile")]
    public async Task<ActionResult<MyProfileDto>> UpdateTrainerProfile(Nexus.Application.Dtos.SoftSkills.TrainerProfileRequest request) =>
        Ok(await profiles.UpdateTrainerProfileAsync(request));
}
