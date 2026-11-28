using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.Internships;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.Internships;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Internships;

/// <summary>Screening, testing, selection and placement (cl.6, cl.8, cl.9, cl.12).</summary>
public interface IApplicationAdminService
{
    Task<List<ApplicationListItemDto>> ListAsync(long? batchId, string? status);
    Task<ApplicationDetailDto> GetAsync(long id);
    Task<ApplicationDetailDto> ShortlistAsync(long id);
    Task<ApplicationDetailDto> RejectAsync(long id, ReasonRequest request);
    Task<ApplicationDetailDto> RecordExamAsync(long id, RecordExamRequest request);
    Task<ApplicationDetailDto> SelectAsync(long id);
    Task<ApplicationDetailDto> WaitlistAsync(long id);
    Task<ApplicationDetailDto> PlaceAsync(long id, PlaceRequest request);
}

public sealed class ApplicationAdminService(
    IUnitOfWork uow, ISetupService setup, IClock clock, IAuditService audit, INotificationService notify, ApplicationWorkflow workflow)
    : IApplicationAdminService
{
    private static readonly ApplicationStatus[] Rejectable =
        [ApplicationStatus.Submitted, ApplicationStatus.Shortlisted, ApplicationStatus.Waitlisted, ApplicationStatus.Selected];

    private static readonly ApplicationStatus[] Testable = [ApplicationStatus.Shortlisted, ApplicationStatus.Waitlisted];

    public async Task<List<ApplicationListItemDto>> ListAsync(long? batchId, string? status)
    {
        ApplicationStatus? s = null;
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            if (!StatusLabels.TryParseApplication(status, out var parsed)) throw new BadRequestException("Unknown application status.");
            s = parsed;
        }
        var apps = await uow.Applications.ListAsync(batchId, s);
        return apps
            .OrderByDescending(a => a.AppliedAt)
            .ThenByDescending(a => a.Id)
            .Select(InternshipMapper.ListItem)
            .ToList();
    }

    public async Task<ApplicationDetailDto> GetAsync(long id) => await DetailAsync(await LoadAsync(id));

    public async Task<ApplicationDetailDto> ShortlistAsync(long id)
    {
        var app = await LoadAsync(id);
        if (app.Status != ApplicationStatus.Submitted) throw new BadRequestException("Only submitted applications can be shortlisted.");
        workflow.SetStatus(app, ApplicationStatus.Shortlisted, "Invited to the selection test");
        await audit.LogAsync("application.shortlist", "InternshipApplication", id, "Shortlisted");
        await notify.NotifyAsync(app.StudentId, "You are shortlisted", "UGC invited you to the selection test (written, oral or online).", Links.Student);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<ApplicationDetailDto> RejectAsync(long id, ReasonRequest r)
    {
        var app = await LoadAsync(id);
        if (!Rejectable.Contains(app.Status)) throw new BadRequestException("This application cannot be rejected now.");
        var reason = r.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason)) throw new BadRequestException("Give a reason for the rejection.");
        app.RejectionReason = reason;
        workflow.SetStatus(app, ApplicationStatus.Rejected, reason);
        await audit.LogAsync("application.reject", "InternshipApplication", id, reason);
        await notify.NotifyAsync(app.StudentId, "Application update", "Your UGC internship application was not successful.", Links.Student);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<ApplicationDetailDto> RecordExamAsync(long id, RecordExamRequest r)
    {
        var app = await LoadAsync(id);
        if (!Testable.Contains(app.Status)) throw new BadRequestException("Record a test score only for shortlisted or waitlisted candidates.");
        if (r.Score is not { } score || score < 0 || score > 100) throw new BadRequestException("Score must be between 0 and 100.");
        if (!StatusLabels.TryParseExamMode(r.Mode, out var mode) || r.Date is null) throw new BadRequestException("Choose the test mode and date.");

        app.ExamMode = mode;
        app.ExamDate = r.Date;
        app.ExamScore = score;
        await audit.LogAsync("exam.record", "InternshipApplication", id, $"{mode} test, score {score}");
        await notify.NotifyAsync(app.StudentId, "Test result recorded", $"Your {mode.ToString().ToLowerInvariant()} test result ({score}/100) is recorded.", Links.Student);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<ApplicationDetailDto> SelectAsync(long id)
    {
        var app = await LoadAsync(id);
        if (!Testable.Contains(app.Status)) throw new BadRequestException("Only shortlisted or waitlisted candidates can be selected.");
        if (app.ExamScore is null) throw new BadRequestException("Record the test score before selecting (cl.9).");
        var taken = await uow.Batches.TakenSeatsAsync(app.BatchId);
        if (taken >= app.Batch.Capacity)
            throw new BadRequestException($"All {app.Batch.Capacity} seats are filled. Waitlist this candidate instead (cl.6).");

        workflow.SetStatus(app, ApplicationStatus.Selected, "Selected for the batch");
        await audit.LogAsync("application.select", "InternshipApplication", id, $"Selected for {app.Batch.Code}");
        await notify.NotifyAsync(app.StudentId, "You are selected", $"Congratulations. You are selected for {app.Batch.Name}. UGC will place you in a department.", Links.Student);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<ApplicationDetailDto> WaitlistAsync(long id)
    {
        var app = await LoadAsync(id);
        if (app.Status != ApplicationStatus.Shortlisted) throw new BadRequestException("Only shortlisted candidates can be waitlisted.");
        if (app.ExamScore is null) throw new BadRequestException("Record the test score first.");
        workflow.SetStatus(app, ApplicationStatus.Waitlisted, "Waitlisted — seats full");
        await audit.LogAsync("application.waitlist", "InternshipApplication", id, "Waitlisted");
        await notify.NotifyAsync(app.StudentId, "You are on the waiting list", "A seat will be offered if one opens.", Links.Student);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<ApplicationDetailDto> PlaceAsync(long id, PlaceRequest r)
    {
        var app = await LoadAsync(id);
        if (app.Status != ApplicationStatus.Selected) throw new BadRequestException("Only selected candidates can be placed.");
        var policy = (await setup.GetSnapshotAsync()).Policy;

        var departments = await uow.Setup.GetDepartmentsAsync(activeOnly: true);
        var dept = departments.FirstOrDefault(d => d.Id == r.DepartmentId) ?? throw new BadRequestException("Choose a UGC department.");
        if (r.MentorId is not { } mentorId) throw new BadRequestException("Choose the supervising mentor.");
        var mentor = (await uow.Users.ListMentorsAsync()).FirstOrDefault(m => m.Id == mentorId)
            ?? throw new BadRequestException("The chosen mentor is not an active mentor.");
        if (r.StartDate is not { } start) throw new BadRequestException("Set the start date.");
        if (r.Months is not { } months || months < policy.MinMonths || months > policy.MaxMonths)
            throw new BadRequestException($"Duration must be {policy.MinMonths}–{policy.MaxMonths} months (cl.8).");

        var end = InternshipRules.EndDateFor(start, months);
        var it = new Domain.Entities.Internships.Internship
        {
            DepartmentId = dept.Id,
            MentorId = mentor.Id,
            StartDate = start,
            EndDate = end,
            Tasks = string.IsNullOrWhiteSpace(r.Tasks)
                ? "Work assigned by the supervisor in line with the intern's education and background (cl.13)."
                : r.Tasks.Trim(),
        };
        it.Placements.Add(new InternshipPlacement { DepartmentId = dept.Id, MentorId = mentor.Id, From = start, Note = "Initial placement" });
        app.Internship = it;
        workflow.SetStatus(app, ApplicationStatus.Active, $"Placed in {dept.Name}");

        await audit.LogAsync("intern.place", "InternshipApplication", id, $"Placed in {dept.Name} with {mentor.FullName} ({start:yyyy-MM-dd} → {end:yyyy-MM-dd})");
        await notify.NotifyAsync(app.StudentId, "Internship placement", $"You are placed in {dept.Name}, starting {start:dd MMM yyyy}. Your mentor is {mentor.FullName}.", Links.Student);
        await notify.NotifyAsync(mentor.Id, "New intern assigned", $"{app.Student.FullName} joins {dept.Name} under your supervision from {start:dd MMM yyyy}.", Links.Intern(app.Id));
        await uow.SaveChangesAsync();

        // Reload so the new internship's department and mentor navigations are populated.
        return await DetailAsync(await LoadAsync(id));
    }

    private async Task<InternshipApplication> LoadAsync(long id) =>
        await uow.Applications.GetDetailAsync(id) ?? throw new NotFoundException("Application", id);

    private async Task<ApplicationDetailDto> DetailAsync(InternshipApplication app) =>
        InternshipMapper.Detail(app, await setup.GetSnapshotAsync(), clock.Today, await InternshipMapper.AttachedAsync(uow, app.Id));
}
