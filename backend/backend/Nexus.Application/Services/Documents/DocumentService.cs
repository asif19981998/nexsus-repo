using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.Documents;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Documents;

public interface IDocumentService
{
    /// <summary>Document types a person can upload, in display order.</summary>
    Task<List<DocumentTypeDto>> ListTypesAsync();

    /// <summary>The signed-in user's library: the current version of each document.</summary>
    Task<List<MyDocumentDto>> ListMineAsync();

    /// <summary>Adds a document, or replaces the current one of the same type with a new version.</summary>
    Task<MyDocumentDto> UploadAsync(long documentTypeId, IncomingFile file);

    Task DeleteMineAsync(long documentId);

    /// <summary>A document's file, for its owner or a reviewer.</summary>
    Task<DocumentFile> OpenFileAsync(long documentId);

    // ---------- review (administrator) ----------

    /// <summary>Another user's library, for a reviewer.</summary>
    Task<List<MyDocumentDto>> ListForUserAsync(long userId);

    /// <summary>Marks the document checked. An identity document also marks the person's identity verified.</summary>
    Task<MyDocumentDto> VerifyAsync(long documentId);

    /// <summary>Sends the document back with a reason; the person uploads a new file.</summary>
    Task<MyDocumentDto> RejectAsync(long documentId, string? note);
}

/// <summary>
/// The document library. It belongs to the account, so the same files serve every module (internship, training, jobs).
/// Files go to private storage; only <see cref="OpenFileAsync"/> hands them out, after checking who asks.
/// </summary>
public sealed class DocumentService(
    IUnitOfWork uow, ICurrentUserService me, IPermissionService permissions, IDocumentStorage storage, IAuditService audit,
    INotificationService notify, IClock clock) : IDocumentService
{
    public async Task<List<DocumentTypeDto>> ListTypesAsync()
    {
        var types = await uow.DocumentTypes.FindAsync(t => t.IsActive);
        return types.OrderBy(t => t.Category).ThenBy(t => t.SortOrder).ThenBy(t => t.Name).Select(ToDto).ToList();
    }

    public async Task<List<MyDocumentDto>> ListMineAsync() =>
        (await uow.Documents.ListCurrentForUserAsync(me.RequireUserId())).Select(ToDto).ToList();

    public async Task<MyDocumentDto> UploadAsync(long documentTypeId, IncomingFile file)
    {
        var userId = me.RequireUserId();
        var type = await uow.DocumentTypes.GetByIdAsync(documentTypeId);
        if (type is null || !type.IsActive) throw new BadRequestException("Choose a document type.");

        var (extension, contentType) = await CheckFileAsync(type, file);
        var previous = await uow.Documents.GetCurrentTrackedAsync(userId, type.Id);

        string stored;
        await using (var content = file.Open()) stored = await storage.SaveAsync(content, extension);

        var doc = new UserDocument
        {
            UserId = userId,
            DocumentTypeId = type.Id,
            Version = (previous?.Version ?? 0) + 1,
            FileName = DocumentRules.CleanFileName(file.FileName),
            StoredName = stored,
            ContentType = contentType,
            SizeBytes = file.Length,
        };
        // A version an application was sent with stays (no longer current); any other old version goes.
        var keepPrevious = previous is not null && await uow.Documents.IsAttachedAsync(previous.Id);
        try
        {
            if (previous is not null)
            {
                if (keepPrevious) previous.IsCurrent = false;
                else uow.Documents.Remove(previous);
            }
            await uow.Documents.AddAsync(doc);
            await audit.LogAsync(previous is null ? "document.upload" : "document.replace", "UserDocument", null, $"{type.Code} v{doc.Version}: {doc.FileName}");
            await uow.SaveChangesAsync();
        }
        catch
        {
            storage.Delete(stored);
            throw;
        }
        if (previous is not null && !keepPrevious) storage.Delete(previous.StoredName);

        doc.DocumentType = type;
        return ToDto(doc);
    }

    public async Task DeleteMineAsync(long documentId)
    {
        var userId = me.RequireUserId();
        var doc = await uow.Documents.GetByIdAsync(documentId);
        if (doc is null || doc.UserId != userId || !doc.IsCurrent) throw new NotFoundException("Document", documentId);
        if (await uow.Documents.IsAttachedToLiveApplicationAsync(doc.Id, InternshipRules.LiveStatuses))
            throw new ConflictException("Your application in progress uses this document, so it cannot be deleted. Upload a new version instead.");

        // An application from the past still points at it: hide it from the library but keep the file.
        var keep = await uow.Documents.IsAttachedAsync(doc.Id);
        if (keep) doc.IsCurrent = false;
        else uow.Documents.Remove(doc);
        await audit.LogAsync("document.delete", "UserDocument", doc.Id, $"v{doc.Version}: {doc.FileName}{(keep ? " (kept for past applications)" : "")}");
        await uow.SaveChangesAsync();
        if (!keep) storage.Delete(doc.StoredName);
    }

    public async Task<DocumentFile> OpenFileAsync(long documentId)
    {
        var userId = me.RequireUserId();
        var doc = await uow.Documents.GetByIdAsync(documentId);
        // Someone else's document looks the same as a missing one. Reviewers of users see every document;
        // reviewers of applications see the versions sent with an application.
        if (doc is null || (doc.UserId != userId && !await permissions.HasAsync(Permissions.UsersView)
                && !(await permissions.HasAsync(Permissions.ApplicationView) && await uow.Documents.IsAttachedAsync(doc.Id))))
            throw new NotFoundException("Document", documentId);
        var content = storage.OpenRead(doc.StoredName) ?? throw new NotFoundException("The file of this document is missing.");
        return new DocumentFile(content, doc.ContentType, doc.FileName);
    }

    // ---------- review (administrator) ----------

    public async Task<List<MyDocumentDto>> ListForUserAsync(long userId)
    {
        await permissions.RequireAsync(Permissions.UsersView);
        if (await uow.Users.GetAsync(userId) is null) throw new NotFoundException("User", userId);
        return (await uow.Documents.ListCurrentForUserAsync(userId)).Select(ToDto).ToList();
    }

    public async Task<MyDocumentDto> VerifyAsync(long documentId)
    {
        var (doc, type) = await ForReviewAsync(documentId);
        Review(doc, DocumentStatus.Verified, null);
        await audit.LogAsync("document.verify", "UserDocument", doc.Id, $"{type.Code} v{doc.Version} of user {doc.UserId}");

        var identityNow = false;
        if (type.IsIdentity)
        {
            // One verified identity document proves who the person is, for every programme.
            var user = await uow.Users.GetTrackedWithProfileAsync(doc.UserId);
            if (user?.StudentProfile is { IdentityVerified: false } profile)
            {
                profile.IdentityVerified = true;
                identityNow = true;
                await audit.LogAsync("user.identity.verify", "User", doc.UserId, $"from {type.Code} v{doc.Version}");
            }
        }

        await notify.NotifyAsync(doc.UserId,
            identityNow ? "Your identity is verified" : $"{type.Name} verified",
            identityNow ? $"Your {type.Name} was checked. Every programme now accepts your identity." : $"The administrator checked your {type.Name}.",
            Links.MyDocuments);
        await uow.SaveChangesAsync();
        doc.DocumentType = type;
        return ToDto(doc);
    }

    public async Task<MyDocumentDto> RejectAsync(long documentId, string? note)
    {
        var reason = note?.Trim();
        if (string.IsNullOrEmpty(reason)) throw new BadRequestException("Write what is wrong, so the person knows what to upload.");
        if (reason.Length > UserDocument.MaxReviewNoteLength)
            throw new BadRequestException($"The note can be at most {UserDocument.MaxReviewNoteLength} characters.");

        var (doc, type) = await ForReviewAsync(documentId);
        Review(doc, DocumentStatus.Rejected, reason);
        await audit.LogAsync("document.reject", "UserDocument", doc.Id, $"{type.Code} v{doc.Version} of user {doc.UserId}: {reason}");
        await notify.NotifyAsync(doc.UserId, $"{type.Name} needs a new file", reason, Links.MyDocuments);
        await uow.SaveChangesAsync();
        doc.DocumentType = type;
        return ToDto(doc);
    }

    /// <summary>The tracked document and its type, after checking the caller may review it.</summary>
    private async Task<(UserDocument Doc, DocumentType Type)> ForReviewAsync(long documentId)
    {
        await permissions.RequireAsync(Permissions.UsersManage);
        var doc = await uow.Documents.GetByIdAsync(documentId);
        if (doc is null) throw new NotFoundException("Document", documentId);
        if (doc.UserId == me.RequireUserId()) throw new BadRequestException("You cannot review your own document.");
        if (!doc.IsCurrent) throw new ConflictException("A newer version of this document was uploaded. Review that one.");
        var type = await uow.DocumentTypes.GetByIdAsync(doc.DocumentTypeId) ?? throw new NotFoundException("DocumentType", doc.DocumentTypeId);
        return (doc, type);
    }

    private void Review(UserDocument doc, DocumentStatus status, string? note)
    {
        doc.Status = status;
        doc.ReviewNote = note;
        doc.ReviewedAt = clock.UtcNow;
        doc.ReviewedById = me.UserId;
        doc.ReviewedByName = me.FullName;
    }

    // ---------- helpers ----------

    private static async Task<(string Extension, string ContentType)> CheckFileAsync(DocumentType type, IncomingFile f)
    {
        var allowed = DocumentRules.ExtensionsOf(type);
        var formats = DocumentRules.Describe(allowed);
        if (f.Length <= 0) throw new BadRequestException("The file is empty.");
        if (f.Length > type.MaxBytes) throw new BadRequestException($"{type.Name} can be at most {Megabytes(type.MaxBytes)}.");

        var extension = Path.GetExtension(f.FileName ?? "").ToLowerInvariant();
        if (!allowed.Contains(extension)) throw new BadRequestException($"Upload {type.Name} as {formats}.");

        var head = new byte[DocumentRules.SniffBytes];
        int read;
        await using (var s = f.Open()) read = await s.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false);
        var contentType = DocumentRules.Sniff(extension, head.AsSpan(0, read))
            ?? throw new BadRequestException($"The file is not a real {extension.TrimStart('.').ToUpperInvariant()}. Upload {type.Name} as {formats}.");
        return (extension, contentType);
    }

    private static string Megabytes(long bytes) => $"{bytes / (1024.0 * 1024):0.#} MB";

    private static DocumentTypeDto ToDto(DocumentType t) =>
        new(t.Id, t.Code, t.Name, t.Description, t.Category.ToString(), t.IsIdentity, DocumentRules.ExtensionsOf(t), t.MaxBytes);

    internal static MyDocumentDto ToDto(UserDocument d) =>
        new(d.Id, d.DocumentTypeId, d.DocumentType.Code, d.DocumentType.Name, d.DocumentType.Category.ToString(), d.Version,
            d.FileName, d.ContentType, d.SizeBytes, d.Status.ToString(), d.ReviewNote, d.CreatedAt, d.ReviewedAt, d.ReviewedByName);
}
