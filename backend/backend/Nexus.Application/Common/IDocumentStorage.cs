namespace Nexus.Application.Common;

/// <summary>
/// Private file storage for user documents. Unlike the public <c>/files</c> folder, nothing stored here is
/// reachable by URL; the API streams a file only after checking who asks. Implemented in the API project.
/// </summary>
public interface IDocumentStorage
{
    /// <summary>Saves the stream under a new random name with the given extension (".pdf") and returns that name.</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default);

    /// <summary>Opens a stored file for reading; null when it is missing or the name is not one this storage made.</summary>
    Stream? OpenRead(string storedName);

    /// <summary>Deletes a stored file. Missing files are ignored.</summary>
    void Delete(string storedName);
}
