using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Application.Services.Internships;
using Nexus.BuildingBlocks.Exceptions;

namespace Nexus.API.Controllers;

/// <summary>
/// Simple image upload. The file is saved under <c>files/</c> next to the API and served from <c>/files/...</c>;
/// the caller stores the returned path in its own table (for example an attendance selfie).
/// </summary>
[ApiController]
[Route("api/v1/file")]
[Authorize]
public class FilesController(IWebHostEnvironment env) : ControllerBase
{
    private const long MaxBytes = 10 * 1024 * 1024;

    public sealed record FileUploadResponse(string Path);

    [HttpPost("upload")]
    [RequestSizeLimit(MaxBytes + 1024 * 1024)]
    public async Task<ActionResult<FileUploadResponse>> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0) throw new BadRequestException("No file uploaded.");
        if (file.Length > MaxBytes) throw new BadRequestException("The photo is too large (maximum 10 MB).");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!UploadedFile.ImageExtensions.Contains(extension)) throw new BadRequestException("Only JPG, PNG or WEBP photos are allowed.");

        // The file must really be an image, whatever its name says.
        var head = new byte[12];
        await using (var peek = file.OpenReadStream())
            if (await peek.ReadAsync(head) < head.Length) throw new BadRequestException("The file is not a valid photo.");
        var isJpeg = head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF;
        var isPng = head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47;
        var isWebp = head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P';
        if (!isJpeg && !isPng && !isWebp) throw new BadRequestException("The file is not a valid photo.");

        var folder = Path.Combine(env.ContentRootPath, "files");
        Directory.CreateDirectory(folder);
        var name = $"{Guid.NewGuid():D}{extension}";
        await using (var target = new FileStream(Path.Combine(folder, name), FileMode.CreateNew))
            await file.CopyToAsync(target);

        return Ok(new FileUploadResponse($"/files/{name}"));
    }
}
