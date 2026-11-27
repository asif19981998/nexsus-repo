namespace Nexus.Application.Dtos.Documents;

public sealed record DocumentTypeDto(
    long Id, string Code, string Name, string? Description, string Category, bool IsIdentity,
    IReadOnlyList<string> Extensions, long MaxBytes);

/// <summary>The current version of one document in a person's library.</summary>
public sealed record MyDocumentDto(
    long Id, long DocumentTypeId, string TypeCode, string TypeName, string Category, int Version,
    string FileName, string ContentType, long SizeBytes, string Status, string? ReviewNote,
    DateTime UploadedAt, DateTime? ReviewedAt, string? ReviewedBy);

public sealed class RejectDocumentRequest
{
    /// <summary>What is wrong, shown to the person: "The scan is cut off at the bottom."</summary>
    public string? Note { get; set; }
}

/// <summary>An uploaded file as the controller hands it over; <see cref="Open"/> may be called more than once.</summary>
public sealed record IncomingFile(string? FileName, long Length, Func<Stream> Open);

public sealed record DocumentFile(Stream Content, string ContentType, string FileName);

public sealed record RequirementTypeDto(long Id, string Code, string Name);

/// <summary>
/// One line of a programme's document checklist. <see cref="Types"/> are alternatives (any one is enough).
/// Met when the person has a current document of one of them that was not rejected; it need not be verified yet.
/// </summary>
public sealed record ChecklistRowDto(
    string GroupKey, IReadOnlyList<RequirementTypeDto> Types, bool Required, string Condition, string? Note, bool Met, MyDocumentDto? Document);

/// <summary>A programme's checklist for one person. Ready when every required line that applies is met.</summary>
public sealed record ModuleChecklistDto(string Module, string ModuleName, bool Ready, IReadOnlyList<ChecklistRowDto> Rows, IReadOnlyList<string> Missing);
