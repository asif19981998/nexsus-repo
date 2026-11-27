using Nexus.Application.Data;
using Nexus.Application.Dtos.SoftSkills;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.Application.Services.Internships;
using Nexus.Application.Services.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Programmes;

/// <summary>
/// The programmes the signed-in person can join, one card per module, each with its own eligibility.
/// A student sees the internship and the Soft Skill training; a university teacher sees the trainer registration.
/// </summary>
public interface IProgrammeCatalogService
{
    Task<List<ProgrammeCardDto>> ListMineAsync();
}

public sealed class ProgrammeCatalogService(
    IUnitOfWork uow, IPermissionService permissions, ICurrentUserService me,
    IStudentInternshipService internship, IBatchService internshipBatches,
    IStudentSoftSkillService softSkill, ITrainerService trainers) : IProgrammeCatalogService
{
    public async Task<List<ProgrammeCardDto>> ListMineAsync()
    {
        var userId = me.RequireUserId();
        var cards = new List<ProgrammeCardDto>();

        if (await permissions.HasAsync(Permissions.ProgrammeView))
        {
            var e = await internship.GetEligibilityAsync();
            var open = await internshipBatches.ListOpenAsync();
            var first = open.OrderBy(b => b.ApplyTo).FirstOrDefault();
            var latest = (await uow.Applications.GetLatestForStudentAsync(userId))?.Status;
            cards.Add(new ProgrammeCardDto(Modules.Internship, Modules.NameOf(Modules.Internship),
                "3–6 months in a UGC department for final-year students and recent graduates, with a monthly allowance.",
                Links.Student, e.Eligible, FirstUnmet(e.Rules), open.Count, latest?.Label(),
                [new("Duration", "3–6 months"), new("Where", "A UGC department"), new("For", "Final-year students and recent graduates"), new("Support", "Monthly allowance")],
                first?.ApplyTo, first?.StartDate));
        }

        if (await permissions.HasAsync(Permissions.SoftSkill.ProgrammeView))
        {
            var e = await softSkill.GetEligibilityAsync();
            var open = await softSkill.ListOpenBatchesAsync();
            var first = open.OrderBy(b => b.ApplyTo).FirstOrDefault();
            var latest = (await uow.SoftSkillApplications.GetLatestForStudentAsync(userId))?.Status;
            cards.Add(new ProgrammeCardDto(Modules.SoftSkill, Modules.NameOf(Modules.SoftSkill),
                "48 hours over 3 months at your own university for 1st and 2nd year students, with a stipend for each month you pass.",
                Links.SoftSkillStudent, e.Eligible, FirstUnmet(e.Rules), open.Count, latest?.Label(),
                [new("Duration", "48 hours over 3 months"), new("Where", "Your own university"), new("For", "1st and 2nd year students"), new("Support", "Stipend for each month you pass")],
                first?.ApplyTo, first?.StartDate));
        }

        if (await permissions.HasAsync(Permissions.SoftSkill.TrainerApply))
        {
            var mine = await trainers.GetMineAsync();
            cards.Add(new ProgrammeCardDto(Modules.SoftSkillTrainer, Modules.NameOf(Modules.SoftSkillTrainer),
                "Teach Soft Skill sessions at your university after the UGC Training of Trainers.",
                Links.SoftSkillTrainer, mine.Eligibility.Eligible || mine.Registration is not null,
                mine.Registration is null ? FirstUnmet(mine.Eligibility.Rules) : null,
                0, mine.Registration?.Status,
                [new("Role", "Teach Soft Skill sessions"), new("Where", "Your own university"), new("Before you teach", "UGC Training of Trainers"), new("Support", "Honorarium per session")]));
        }

        return cards;
    }

    private static string? FirstUnmet(IReadOnlyList<EligibilityRule> rules)
    {
        // The internship's academic rules are alternatives: report them only when none is met.
        var grouped = rules.Where(r => r.Group is not null).GroupBy(r => r.Group).Where(g => !g.Any(r => r.Met)).Select(g => g.First());
        var failed = rules.Where(r => r.Group is null && !r.Met).Concat(grouped).FirstOrDefault();
        return failed is null ? null : $"{failed.Label} — {failed.Detail}";
    }
}
