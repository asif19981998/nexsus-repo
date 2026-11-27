using Nexus.Application.Data;
using Nexus.Application.Dtos.Documents;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Documents;

public interface IRequirementService
{
    /// <summary>The signed-in user's checklist for every programme that asks for documents.</summary>
    Task<List<ModuleChecklistDto>> ListMineAsync();

    /// <summary>One programme's checklist for a person, against their library as it is now.</summary>
    Task<ModuleChecklistDto> GetChecklistAsync(string module, long userId);
}

/// <summary>
/// Matches a programme's document requirements against a person's library. Lines that depend on the academic profile
/// (studentship certificate while studying, degree certificate once finished) apply only when the profile says so.
/// </summary>
public sealed class RequirementService(IUnitOfWork uow, ICurrentUserService me, IPermissionService permissions) : IRequirementService
{
    /// <summary>
    /// The permission that lets a person apply to each programme. A checklist is listed only to people who can apply to
    /// it, so a trainer sees the trainer registration's documents and not the students' (student ID, studentship…).
    /// A module missing here is listed to everyone.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ApplyPermission = new Dictionary<string, string>
    {
        [Modules.Internship] = Permissions.ApplicationApply,
        [Modules.SoftSkill] = Permissions.SoftSkill.Apply,
        [Modules.SoftSkillTrainer] = Permissions.SoftSkill.TrainerApply,
    };

    public async Task<List<ModuleChecklistDto>> ListMineAsync()
    {
        var userId = me.RequireUserId();
        var requirements = await uow.Documents.ListRequirementsAsync();
        var context = await LoadAsync(userId);
        var result = new List<ModuleChecklistDto>();
        foreach (var g in requirements.GroupBy(r => r.Module))
        {
            if (ApplyPermission.TryGetValue(g.Key, out var permission) && !await permissions.HasAsync(permission)) continue;
            result.Add(Build(g.Key, g.ToList(), context));
        }
        return result;
    }

    public async Task<ModuleChecklistDto> GetChecklistAsync(string module, long userId) =>
        Build(module, await uow.Documents.ListRequirementsAsync(module), await LoadAsync(userId));

    private sealed record Context(IReadOnlyDictionary<long, UserDocument> ByType, bool Graduated);

    private async Task<Context> LoadAsync(long userId)
    {
        var docs = await uow.Documents.ListCurrentForUserAsync(userId);
        var (_, main) = await uow.Users.GetAcademicAsync(userId);
        return new Context(docs.ToDictionary(d => d.DocumentTypeId), AcademicRules.IsGraduated(main));
    }

    private static ModuleChecklistDto Build(string module, List<ModuleDocumentRequirement> requirements, Context c)
    {
        var rows = requirements
            .Where(r => r.Condition == RequirementCondition.Always || (r.Condition == RequirementCondition.Graduated) == c.Graduated)
            .GroupBy(r => r.GroupKey)
            .Select(g =>
            {
                var lines = g.OrderBy(r => r.SortOrder).ToList();
                // Prefer a verified document, then any that is not rejected.
                var docs = lines.Select(r => c.ByType.GetValueOrDefault(r.DocumentTypeId)).OfType<UserDocument>().Where(d => d.Status != DocumentStatus.Rejected).ToList();
                var doc = docs.FirstOrDefault(d => d.Status == DocumentStatus.Verified) ?? docs.FirstOrDefault();
                var first = lines[0];
                return new ChecklistRowDto(
                    g.Key,
                    lines.Select(r => new RequirementTypeDto(r.DocumentTypeId, r.DocumentType.Code, r.DocumentType.Name)).ToList(),
                    lines.Any(r => r.IsRequired),
                    first.Condition.ToString(),
                    lines.Select(r => r.Note).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                    doc is not null,
                    doc is null ? null : DocumentService.ToDto(doc));
            })
            .ToList();
        var missing = rows.Where(r => r.Required && !r.Met).Select(r => string.Join(" or ", r.Types.Select(t => t.Name))).ToList();
        return new ModuleChecklistDto(module, Modules.NameOf(module), missing.Count == 0, rows, missing);
    }
}
