using Nexus.Application.Data;
using Nexus.Application.Dtos.Internships;
using Nexus.Application.Dtos.SoftSkills;
using Nexus.Application.Rules;
using Nexus.Application.Services.Common;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.SoftSkills;

/// <summary>cl.9: the Commission screens applications and selects trainees, up to each university's seats (cl.6 ক).</summary>
public interface ISoftSkillApplicationAdminService
{
    Task<List<SoftSkillApplicationListItemDto>> ListAsync(long? batchId, long? universityGroupId, string? status);
    Task<SoftSkillApplicationDto> GetAsync(long id);
    Task<SoftSkillApplicationDto> ShortlistAsync(long id);
    Task<SoftSkillApplicationDto> ScoreAsync(long id, ScreeningScoreRequest request);
    Task<SoftSkillApplicationDto> SelectAsync(long id);
    Task<SoftSkillApplicationDto> WaitlistAsync(long id);
    Task<SoftSkillApplicationDto> RejectAsync(long id, ReasonRequest request);
}

public sealed class SoftSkillApplicationAdminService(
    IUnitOfWork uow, IAuditService audit, INotificationService notify, SoftSkillWorkflow workflow) : ISoftSkillApplicationAdminService
{
    private static readonly ApplicationStatus[] Selectable = [ApplicationStatus.Submitted, ApplicationStatus.Shortlisted, ApplicationStatus.Waitlisted];
    private static readonly ApplicationStatus[] Rejectable = [.. Selectable, ApplicationStatus.Selected];

    public async Task<List<SoftSkillApplicationListItemDto>> ListAsync(long? batchId, long? universityGroupId, string? status)
    {
        ApplicationStatus? s = null;
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            if (!StatusLabels.TryParseApplication(status, out var parsed)) throw new BadRequestException("Unknown application status.");
            s = parsed;
        }
        var apps = await uow.SoftSkillApplications.ListAsync(batchId, universityGroupId, s);
        return apps.OrderByDescending(a => a.AppliedAt).ThenByDescending(a => a.Id).Select(SoftSkillMapper.ListItem).ToList();
    }

    public async Task<SoftSkillApplicationDto> GetAsync(long id) => await DetailAsync(await LoadAsync(id));

    public async Task<SoftSkillApplicationDto> ShortlistAsync(long id)
    {
        var app = await LoadAsync(id);
        if (app.Status != ApplicationStatus.Submitted) throw new BadRequestException("Only submitted applications can be shortlisted.");
        workflow.SetStatus(app, ApplicationStatus.Shortlisted, "Shortlisted after document check");
        await audit.LogAsync("softskill.application.shortlist", "SoftSkillApplication", id, "Shortlisted");
        await notify.NotifyAsync(app.StudentId, "You are shortlisted", $"Your application to {app.Batch.Name} passed the first check.", Links.SoftSkillStudent);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<SoftSkillApplicationDto> ScoreAsync(long id, ScreeningScoreRequest r)
    {
        var app = await LoadAsync(id);
        if (!Selectable.Contains(app.Status)) throw new BadRequestException("Record a screening score only before selection.");
        if (r.Score is not { } score || score < 0 || score > 100) throw new BadRequestException("Score must be between 0 and 100.");
        app.ScreeningScore = score;
        await audit.LogAsync("softskill.application.score", "SoftSkillApplication", id, $"Screening score {score}");
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<SoftSkillApplicationDto> SelectAsync(long id)
    {
        var app = await LoadAsync(id);
        if (!Selectable.Contains(app.Status)) throw new BadRequestException("Only submitted, shortlisted or waitlisted applications can be selected.");
        if (app.Batch.Status is not (BatchStatus.ApplicationsOpen or BatchStatus.Selection or BatchStatus.Running))
            throw new BadRequestException($"Selection is closed: the batch is {app.Batch.Status.Label()}.");
        var taken = await uow.SoftSkillBatches.TakenSeatsAsync(app.UniversityGroupId);
        if (taken >= app.UniversityGroup.Capacity)
            throw new BadRequestException($"All {app.UniversityGroup.Capacity} seats at {app.UniversityGroup.University.Name} are filled. Waitlist instead (cl.6 ক).");

        // A seat filled after the training started goes straight into it.
        var running = app.Batch.Status == BatchStatus.Running;
        app.Trainee ??= new SoftSkillTrainee { UniversityGroupId = app.UniversityGroupId };
        workflow.SetStatus(app, running ? ApplicationStatus.Active : ApplicationStatus.Selected, running ? "Selected; joined the running training" : "Selected for the batch");
        await audit.LogAsync("softskill.application.select", "SoftSkillApplication", id, $"Selected for {app.Batch.Code} at {app.UniversityGroup.University.Name}");
        await notify.NotifyAsync(app.StudentId, "You are selected",
            $"Congratulations. You are selected for {app.Batch.Name} at {app.UniversityGroup.University.Name}, starting {app.Batch.StartDate:dd MMM yyyy}.", Links.SoftSkillStudent);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<SoftSkillApplicationDto> WaitlistAsync(long id)
    {
        var app = await LoadAsync(id);
        if (app.Status is not (ApplicationStatus.Submitted or ApplicationStatus.Shortlisted)) throw new BadRequestException("Only submitted or shortlisted applications can be waitlisted.");
        workflow.SetStatus(app, ApplicationStatus.Waitlisted, "Waitlisted — seats full");
        await audit.LogAsync("softskill.application.waitlist", "SoftSkillApplication", id, "Waitlisted");
        await notify.NotifyAsync(app.StudentId, "You are on the waiting list", "A seat will be offered if one opens at your university.", Links.SoftSkillStudent);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<SoftSkillApplicationDto> RejectAsync(long id, ReasonRequest r)
    {
        var app = await LoadAsync(id);
        if (!Rejectable.Contains(app.Status)) throw new BadRequestException("This application cannot be rejected now.");
        var reason = r.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason)) throw new BadRequestException("Give a reason for the rejection.");
        app.RejectionReason = reason;
        workflow.SetStatus(app, ApplicationStatus.Rejected, reason);
        await audit.LogAsync("softskill.application.reject", "SoftSkillApplication", id, reason);
        await notify.NotifyAsync(app.StudentId, "Application update", "Your Soft Skill application was not successful.", Links.SoftSkillStudent);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    private async Task<SoftSkillApplication> LoadAsync(long id) =>
        await uow.SoftSkillApplications.GetDetailAsync(id) ?? throw new NotFoundException("Application", id);

    private async Task<SoftSkillApplicationDto> DetailAsync(SoftSkillApplication app) =>
        SoftSkillMapper.Application(app, await SoftSkillMapper.AttachedAsync(uow, Modules.SoftSkill, app.Id));
}
