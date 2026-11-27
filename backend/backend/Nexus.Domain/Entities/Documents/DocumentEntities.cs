using Nexus.Domain.Abstractions;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.Documents;

/// <summary>
/// A kind of document a person can keep in their library ("National ID card", "CV"…).
/// Belongs to the account, not to a module: every module (internship, training, jobs) asks for types from this list.
/// </summary>
public class DocumentType : Entity<long>
{
    public const int MaxCodeLength = 40;
    public const int MaxNameLength = 150;
    public const int MaxDescriptionLength = 300;

    /// <summary>Stable key used by code and seed data, e.g. "NID". Unique.</summary>
    public required string Code { get; set; }
    public required string Name { get; set; }

    /// <summary>Help shown under the upload field, e.g. "Front and back on one page".</summary>
    public string? Description { get; set; }
    public DocumentCategory Category { get; set; }

    /// <summary>Proves who the person is (NID, birth registration, passport). Verifying one verifies the account's identity.</summary>
    public bool IsIdentity { get; set; }

    /// <summary>Allowed extensions, comma separated: ".pdf,.jpg,.jpeg,.png".</summary>
    public required string Extensions { get; set; }
    public long MaxBytes { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>
/// One uploaded file in a person's document library. Uploading the same type again makes a new version;
/// <see cref="IsCurrent"/> marks the one the library shows and modules use.
/// </summary>
public class UserDocument : Entity<long>
{
    public const int MaxFileNameLength = 200;
    public const int MaxReviewNoteLength = 500;

    public long UserId { get; set; }
    public User User { get; set; } = default!;

    public long DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = default!;

    public int Version { get; set; } = 1;
    public bool IsCurrent { get; set; } = true;

    /// <summary>The name the person uploaded, cleaned of any path.</summary>
    public required string FileName { get; set; }

    /// <summary>Random name in private storage ("3f2a….pdf"); never sent to the browser.</summary>
    public required string StoredName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Submitted;
    public string? ReviewNote { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public long? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
}

/// <summary>
/// One line of a module's document checklist. Rows of a module that share a <see cref="GroupKey"/> are alternatives:
/// any one of them meets the line (National ID, birth registration or passport).
/// </summary>
public class ModuleDocumentRequirement : Entity<long>
{
    public const int MaxModuleLength = 40;
    public const int MaxNoteLength = 300;

    /// <summary>"internship" now; "training", "jobs" later.</summary>
    public required string Module { get; set; }

    /// <summary>Lines with the same key are alternatives.</summary>
    public required string GroupKey { get; set; }

    public long DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = default!;

    public bool IsRequired { get; set; } = true;
    public RequirementCondition Condition { get; set; } = RequirementCondition.Always;

    /// <summary>Shown to the applicant: "Issued in the last 3 months".</summary>
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// A document version sent with an application. Modules keep their own application tables, so this names the module
/// and the application id instead of holding a foreign key. A version listed here is never deleted.
/// </summary>
public class ApplicationDocument : Entity<long>
{
    public required string Module { get; set; }
    public long ApplicationId { get; set; }

    public long UserDocumentId { get; set; }
    public UserDocument UserDocument { get; set; } = default!;
}
