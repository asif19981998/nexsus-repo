using Microsoft.AspNetCore.Mvc;
using Nexus.API.Security;
using Nexus.Application.Dtos.Documents;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Documents;

namespace Nexus.API.Controllers;

/// <summary>
/// The document library. It belongs to the account, not to a module, so the "me" endpoints need only a signed-in user
/// and touch only that user's own documents. Files are streamed only through <see cref="DocumentFile"/>.
/// </summary>
[ApiController]
public class DocumentsController(IDocumentService documents, IRequirementService requirements) : ControllerBase
{
    /// <summary>The largest document plus room for the form fields.</summary>
    private const long MaxRequestBytes = DocumentRules.MaxBytesCap + 1024 * 1024;

    [HttpGet("api/v1/document-types")]
    public async Task<ActionResult<List<DocumentTypeDto>>> Types() => Ok(await documents.ListTypesAsync());

    [HttpGet("api/v1/me/documents")]
    public async Task<ActionResult<List<MyDocumentDto>>> Mine() => Ok(await documents.ListMineAsync());

    /// <summary>For every programme that asks for documents: what the signed-in user has, and what is still missing.</summary>
    [HttpGet("api/v1/me/requirements")]
    public async Task<ActionResult<List<ModuleChecklistDto>>> MyRequirements() => Ok(await requirements.ListMineAsync());

    /// <summary>multipart/form-data: <c>documentTypeId</c> and <c>file</c>. Replaces the current document of that type.</summary>
    [HttpPost("api/v1/me/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<ActionResult<MyDocumentDto>> Upload([FromForm] long documentTypeId, IFormFile? file)
    {
        if (file is null) return Problem(title: "BadRequestException", detail: "Choose a file to upload.", statusCode: StatusCodes.Status400BadRequest);
        return Ok(await documents.UploadAsync(documentTypeId, new IncomingFile(file.FileName, file.Length, file.OpenReadStream)));
    }

    [HttpDelete("api/v1/me/documents/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await documents.DeleteMineAsync(id);
        return NoContent();
    }

    // ---------- review (administrator) ----------

    /// <summary>A user's document library, for a reviewer.</summary>
    [HttpGet("api/v1/users/{userId:long}/documents")]
    [HasPermission(Permissions.UsersView)]
    public async Task<ActionResult<List<MyDocumentDto>>> ForUser(long userId) => Ok(await documents.ListForUserAsync(userId));

    [HttpPost("api/v1/documents/{id:long}/verify")]
    [HasPermission(Permissions.UsersManage)]
    public async Task<ActionResult<MyDocumentDto>> Verify(long id) => Ok(await documents.VerifyAsync(id));

    [HttpPost("api/v1/documents/{id:long}/reject")]
    [HasPermission(Permissions.UsersManage)]
    public async Task<ActionResult<MyDocumentDto>> Reject(long id, RejectDocumentRequest request) => Ok(await documents.RejectAsync(id, request.Note));

    /// <summary>A document's file, for its owner or a reviewer. Never cached, never sniffed by the browser.</summary>
    [HttpGet("api/v1/documents/{id:long}/file")]
    public async Task<IActionResult> DocumentFile(long id)
    {
        var file = await documents.OpenFileAsync(id);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(file.Content, file.ContentType, file.FileName);
    }
}
