using Microsoft.EntityFrameworkCore;
using Nexus.Application.Data;
using Nexus.Application.Rules;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Enums;

namespace Nexus.Infrastructure.Repositories;

public class DocumentRepository(NexusDbContext ctx) : BaseRepository<UserDocument>(ctx), IDocumentRepository
{
    public Task<List<UserDocument>> ListCurrentForUserAsync(long userId) =>
        Set.AsNoTracking()
            .Include(d => d.DocumentType)
            .Where(d => d.UserId == userId && d.IsCurrent)
            .OrderBy(d => d.DocumentType.Category).ThenBy(d => d.DocumentType.SortOrder)
            .ToListAsync();

    public Task<UserDocument?> GetCurrentTrackedAsync(long userId, long documentTypeId) =>
        Set.FirstOrDefaultAsync(d => d.UserId == userId && d.DocumentTypeId == documentTypeId && d.IsCurrent);

    public Task<List<ModuleDocumentRequirement>> ListRequirementsAsync(string? module = null) =>
        Ctx.ModuleDocumentRequirements.AsNoTracking()
            .Include(r => r.DocumentType)
            .Where(r => module == null || r.Module == module)
            .OrderBy(r => r.Module).ThenBy(r => r.SortOrder).ThenBy(r => r.Id)
            .ToListAsync();

    public Task<bool> IsAttachedAsync(long userDocumentId) =>
        Ctx.ApplicationDocuments.AnyAsync(a => a.UserDocumentId == userDocumentId);

    public async Task<bool> IsAttachedToLiveApplicationAsync(long userDocumentId, IReadOnlyCollection<ApplicationStatus> liveStatuses)
    {
        var attachments = Ctx.ApplicationDocuments.Where(a => a.UserDocumentId == userDocumentId);
        return await attachments.AnyAsync(a =>
            (a.Module == Modules.Internship && Ctx.InternshipApplications.Any(app => app.Id == a.ApplicationId && liveStatuses.Contains(app.Status)))
            || (a.Module == Modules.SoftSkill && Ctx.SoftSkillApplications.Any(app => app.Id == a.ApplicationId && liveStatuses.Contains(app.Status)))
            || (a.Module == Modules.SoftSkillTrainer && Ctx.SoftSkillTrainers.Any(t => t.Id == a.ApplicationId && t.Status == TrainerStatus.Submitted)));
    }

    public Task<List<UserDocument>> ListAttachedAsync(string module, long applicationId) =>
        Set.AsNoTracking()
            .Include(d => d.DocumentType)
            .Where(d => Ctx.ApplicationDocuments.Any(a => a.Module == module && a.ApplicationId == applicationId && a.UserDocumentId == d.Id))
            .OrderBy(d => d.DocumentType.Category).ThenBy(d => d.DocumentType.SortOrder)
            .ToListAsync();

    public async Task AttachAsync(ApplicationDocument attachment) => await Ctx.ApplicationDocuments.AddAsync(attachment);
}
