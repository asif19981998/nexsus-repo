using Nexus.Domain.Entities.Documents;

namespace Nexus.Application.Rules;

/// <summary>File checks for the document library.</summary>
public static class DocumentRules
{
    /// <summary>The largest file any document type may allow.</summary>
    public const long MaxBytesCap = 10 * 1024 * 1024;

    /// <summary>The only file kinds storage accepts; a document type may allow fewer.</summary>
    public static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
    };

    public const int SniffBytes = 8;

    /// <summary>".pdf,.jpg" → [".pdf", ".jpg"], keeping only kinds storage accepts.</summary>
    public static List<string> ExtensionsOf(DocumentType t) =>
        t.Extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .Where(ContentTypes.ContainsKey)
            .Distinct()
            .ToList();

    /// <summary>"PDF, JPG or PNG" for messages.</summary>
    public static string Describe(IEnumerable<string> extensions)
    {
        var names = extensions.Select(e => e.TrimStart('.').ToUpperInvariant()).Where(n => n != "JPEG").Distinct().ToList();
        return names.Count <= 1 ? string.Join("", names) : $"{string.Join(", ", names[..^1])} or {names[^1]}";
    }

    /// <summary>
    /// The content type the file really has, judged from its first bytes, or null when they do not match the extension.
    /// A renamed executable or HTML page is refused whatever its name says.
    /// </summary>
    public static string? Sniff(string extension, ReadOnlySpan<byte> head)
    {
        if (!ContentTypes.TryGetValue(extension, out var type)) return null;
        var ok = type switch
        {
            "application/pdf" => head.Length >= 5 && head[..5].SequenceEqual("%PDF-"u8),
            "image/jpeg" => head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
            "image/png" => head.Length >= 8 && head[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            _ => false,
        };
        return ok ? type : null;
    }

    /// <summary>A display name safe to store and send back: no path, no control characters, at most the column length.</summary>
    public static string CleanFileName(string? name)
    {
        var bare = Path.GetFileName(name ?? "").Trim();
        var clean = new string(bare.Where(c => !char.IsControl(c) && c is not ('"' or '\\' or '/')).ToArray());
        if (clean.Length == 0) clean = "document";
        return clean.Length <= UserDocument.MaxFileNameLength ? clean : clean[^UserDocument.MaxFileNameLength..];
    }
}
