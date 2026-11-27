using System.Text.RegularExpressions;
using Nexus.Application.Common;

namespace Nexus.API.Storage;

/// <summary>
/// Keeps user documents in <c>&lt;content root&gt;/private-files/documents</c> (or <c>Storage:DocumentsPath</c>).
/// That folder is not served as static files, so the only way to a document is the authorized API endpoint.
/// </summary>
public sealed partial class LocalDocumentStorage : IDocumentStorage
{
    private readonly string _root;

    public LocalDocumentStorage(IWebHostEnvironment env, IConfiguration config)
    {
        var configured = config["Storage:DocumentsPath"];
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "private-files", "documents")
            : Path.GetFullPath(configured, env.ContentRootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default)
    {
        var name = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        if (!StoredName().IsMatch(name)) throw new ArgumentException("Unsupported file extension.", nameof(extension));
        await using var target = new FileStream(Path.Combine(_root, name), FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(target, ct);
        return name;
    }

    public Stream? OpenRead(string storedName)
    {
        var path = PathOf(storedName);
        return path is not null && File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    public void Delete(string storedName)
    {
        var path = PathOf(storedName);
        if (path is not null && File.Exists(path)) File.Delete(path);
    }

    /// <summary>Only names this storage made (32 hex digits and a known extension), so no path can escape the folder.</summary>
    private string? PathOf(string storedName) => StoredName().IsMatch(storedName) ? Path.Combine(_root, storedName) : null;

    [GeneratedRegex(@"^[0-9a-f]{32}\.(pdf|jpg|jpeg|png)$")]
    private static partial Regex StoredName();
}
