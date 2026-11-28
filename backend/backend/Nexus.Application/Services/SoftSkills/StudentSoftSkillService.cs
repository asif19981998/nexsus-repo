using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.Documents;
using Nexus.Application.Dtos.SoftSkills;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.Application.Services.Documents;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.SoftSkills;

/// <summary>Everything a student does for themself in the Soft Skill programme. Always acts on the caller's latest application.</summary>
public interface IStudentSoftSkillService
{
    Task<SoftSkillEligibilityDto> GetEligibilityAsync();

    /// <summary>Batches open for applications, with the caller's university's university group when it takes part.</summary>
    Task<List<OpenBatchDto>> ListOpenBatchesAsync();

    Task<MySoftSkillDto?> GetMineAsync();
    Task<MySoftSkillDto> ApplyAsync(SoftSkillApplyRequest request);
    Task<MySoftSkillDto> WithdrawAsync();
    Task<MySoftSkillDto> RequestLeaveAsync(SoftSkillLeaveRequest request);
    Task<MySoftSkillDto> SubmitReportAsync(LearningReportRequest request);
}

public sealed class StudentSoftSkillService(
    IUnitOfWork uow, ISoftSkillSetupService setup, ICurrentUserService me, IClock clock, IAuditService audit,
    INotificationService notify, SoftSkillWorkflow workflow, IRequirementService requirements) : IStudentSoftSkillService
{
    public async Task<SoftSkillEligibilityDto> GetEligibilityAsync()
    {
        var userId = me.RequireUserId();
        var (academic, university) = await AcademicAsync(userId);
        var statuses = await uow.SoftSkillApplications.GetStatusesForStudentAsync(userId);
        var policy = (await setup.GetSnapshotAsync()).Policy;
        var r = SoftSkillRules.CheckEligibility(academic, statuses, policy);
        var docs = DocumentsRule(await requirements.GetChecklistAsync(Modules.SoftSkill, userId));
        return new SoftSkillEligibilityDto(r.Eligible && docs.Met, r.Basis, [.. r.Rules, docs], university);
    }

    public async Task<List<OpenBatchDto>> ListOpenBatchesAsync()
    {
        var userId = me.RequireUserId();
        var (academic, _) = await AcademicAsync(userId);
        var today = clock.Today;
        var batches = await uow.SoftSkillBatches.ListAsync();
        var taken = await uow.SoftSkillBatches.TakenSeatsByUniversityGroupAsync();
        return batches
            .Where(b => b.Status == BatchStatus.ApplicationsOpen && today <= b.ApplyTo)
            .Select(b =>
            {
                var c = academic?.UniversityId is { } uid ? b.UniversityGroups.FirstOrDefault(x => x.UniversityId == uid) : null;
                return new OpenBatchDto(b.Id, b.Code, b.Name, b.ApplyFrom, b.ApplyTo, b.StartDate, b.EndDate,
                    c?.Id, c?.University.Name, c is null ? null : Math.Max(0, c.Capacity - taken.GetValueOrDefault(c.Id)), c?.Venue);
            })
            .ToList();
    }

    public async Task<MySoftSkillDto?> GetMineAsync()
    {
        var app = await uow.SoftSkillApplications.GetLatestForStudentAsync(me.RequireUserId());
        return app is null ? null : await DetailAsync(app);
    }

    public async Task<MySoftSkillDto> ApplyAsync(SoftSkillApplyRequest r)
    {
        var userId = me.RequireUserId();
        var policy = (await setup.GetSnapshotAsync()).Policy;
        var batch = await uow.SoftSkillBatches.GetWithUniversityGroupsAsync(r.BatchId) ?? throw new NotFoundException("Batch", r.BatchId);
        var today = clock.Today;
        if (batch.Status != BatchStatus.ApplicationsOpen) throw new BadRequestException("This batch is not accepting applications.");
        if (today < batch.ApplyFrom || today > batch.ApplyTo)
            throw new BadRequestException($"The application window is {batch.ApplyFrom:dd MMM yyyy} to {batch.ApplyTo:dd MMM yyyy}.");

        var (academic, _) = await AcademicAsync(userId);
        var statuses = await uow.SoftSkillApplications.GetStatusesForStudentAsync(userId);
        var elig = SoftSkillRules.CheckEligibility(academic, statuses, policy);
        if (!elig.Eligible)
        {
            var failed = elig.Rules.First(x => !x.Met);
            throw new BadRequestException($"Not eligible: {failed.Label}.");
        }

        // cl.6 (ক): seats are per university, so the student applies at their own university's university group.
        var universityGroup = batch.UniversityGroups.FirstOrDefault(c => c.UniversityId == academic!.UniversityId)
            ?? throw new BadRequestException($"{academic!.University} does not take part in this batch.");

        var checklist = await requirements.GetChecklistAsync(Modules.SoftSkill, userId);
        if (!checklist.Ready) throw new BadRequestException($"Upload these documents in My documents first: {string.Join(", ", checklist.Missing)}.");

        var statement = r.Statement?.Trim() ?? "";
        if (statement.Length < policy.MinStatementLength)
            throw new BadRequestException($"Write at least {policy.MinStatementLength} characters on why you want to join.");
        if (statement.Length > SoftSkillApplication.MaxStatementLength)
            throw new BadRequestException($"The statement can have at most {SoftSkillApplication.MaxStatementLength} characters.");

        var app = new SoftSkillApplication
        {
            BatchId = batch.Id,
            UniversityGroupId = universityGroup.Id,
            StudentId = userId,
            Status = ApplicationStatus.Submitted,
            AppliedAt = clock.UtcNow,
            Statement = statement,
            Basis = elig.Basis,
        };
        app.History.Add(new SoftSkillApplicationHistory { Status = ApplicationStatus.Submitted, At = clock.UtcNow, ById = userId, ByName = me.FullName, Note = "Application submitted" });
        await uow.SoftSkillApplications.AddAsync(app);
        await uow.SaveChangesAsync();

        // The library's versions now go with the application; later uploads do not change what was sent.
        var attached = checklist.Rows.Where(x => x.Document is not null).Select(x => x.Document!).ToList();
        foreach (var d in attached)
            await uow.Documents.AttachAsync(new ApplicationDocument { Module = Modules.SoftSkill, ApplicationId = app.Id, UserDocumentId = d.Id });
        var codes = string.Join(",", attached.Select(d => d.TypeCode));
        app.Documents = codes.Length <= 200 ? codes : codes[..200];

        await audit.LogAsync("softskill.application.submit", "SoftSkillApplication", app.Id, $"{me.FullName} applied to {batch.Code} at {universityGroup.University.Name}");
        await notify.NotifyHoldersAsync(Permissions.SoftSkill.ApplicationScreen, "New Soft Skill application",
            $"{me.FullName} ({universityGroup.University.Name}) applied to {batch.Name}.", Links.SoftSkillApplication(app.Id));
        await notify.NotifyAsync(userId, "Application received", $"Your application to {batch.Name} is submitted.", Links.SoftSkillStudent);
        await uow.SaveChangesAsync();
        return await DetailAsync(await MyLatestAsync());
    }

    public async Task<MySoftSkillDto> WithdrawAsync()
    {
        var app = await MyLatestAsync();
        if (!SoftSkillRules.WithdrawableStatuses.Contains(app.Status)) throw new BadRequestException("This application can no longer be withdrawn.");
        workflow.SetStatus(app, ApplicationStatus.Withdrawn, "Withdrawn by applicant");
        await audit.LogAsync("softskill.application.withdraw", "SoftSkillApplication", app.Id, "Withdrawn by applicant");
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<MySoftSkillDto> RequestLeaveAsync(SoftSkillLeaveRequest r)
    {
        var (app, trainee) = await MyActiveTraineeAsync();
        var session = trainee.UniversityGroup.Sessions.FirstOrDefault(s => s.Id == r.SessionId) ?? throw new BadRequestException("Choose one of your university group's sessions.");
        if (session.Status != SessionStatus.Scheduled) throw new BadRequestException("Leave can be asked only for a session that has not been held.");
        if (session.Date < clock.Today) throw new BadRequestException("That session is in the past.");
        if (!Enum.TryParse<LeaveReasonType>(r.ReasonType, true, out var type) || !Enum.IsDefined(type))
            throw new BadRequestException("Choose the reason: health or another acceptable reason (cl.11 ছ).");
        var reason = r.Reason?.Trim() ?? "";
        if (reason.Length < 5) throw new BadRequestException("Explain the reason (cl.11 ছ).");
        if (reason.Length > SoftSkillAttendance.MaxReasonLength) throw new BadRequestException($"The reason can have at most {SoftSkillAttendance.MaxReasonLength} characters.");
        if (trainee.Attendance.Any(a => a.SessionId == session.Id)) throw new BadRequestException("You already have a record for that session.");

        trainee.Attendance.Add(new SoftSkillAttendance { SessionId = session.Id, Status = AttendanceStatus.LeaveRequested, ReasonType = type, Reason = reason });
        await audit.LogAsync("softskill.leave.request", "SoftSkillTrainee", trainee.Id, $"Session {session.Id} on {session.Date:yyyy-MM-dd}: {type}, {reason}");
        if (trainee.UniversityGroup.CoordinatorId is { } coordinator)
            await notify.NotifyAsync(coordinator, "Soft Skill leave request", $"{me.FullName} asked for leave on {session.Date:dd MMM yyyy}.", Links.SoftSkillUniversityGroup(trainee.UniversityGroupId, "leave"));
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<MySoftSkillDto> SubmitReportAsync(LearningReportRequest r)
    {
        var app = await MyLatestAsync();
        if (app.Trainee is null || app.Status is not (ApplicationStatus.Active or ApplicationStatus.Completed))
            throw new BadRequestException("Only a trainee in training can submit the learning report.");
        var trainee = await uow.SoftSkillTrainees.GetDetailAsync(app.Trainee.Id) ?? throw new NotFoundException("Trainee", app.Trainee.Id);
        var policy = (await setup.GetSnapshotAsync()).Policy;
        if (!Enum.TryParse<LearningReportType>(r.Type, true, out var type) || !Enum.IsDefined(type))
            throw new BadRequestException("Choose a learning report or a small project (cl.11 ঝ).");
        var title = r.Title?.Trim() ?? "";
        if (title.Length < 3 || title.Length > SoftSkillLearningReport.MaxTitleLength)
            throw new BadRequestException($"Give a title of 3–{SoftSkillLearningReport.MaxTitleLength} characters.");
        var text = r.Text?.Trim() ?? "";
        if (text.Length < policy.MinReportLength) throw new BadRequestException($"Write at least {policy.MinReportLength} characters.");
        if (text.Length > SoftSkillLearningReport.MaxTextLength) throw new BadRequestException($"At most {SoftSkillLearningReport.MaxTextLength} characters.");
        if (trainee.Report?.Status == ReportStatus.Accepted) throw new BadRequestException("Your report is already accepted.");

        if (trainee.Report is null)
        {
            trainee.Report = new SoftSkillLearningReport { Type = type, Title = title, Text = text, SubmittedAt = clock.UtcNow };
        }
        else
        {
            trainee.Report.Type = type;
            trainee.Report.Title = title;
            trainee.Report.Text = text;
            trainee.Report.SubmittedAt = clock.UtcNow;
            trainee.Report.Status = ReportStatus.Submitted;
        }
        await audit.LogAsync("softskill.report.submit", "SoftSkillTrainee", trainee.Id, $"{type}: {title}");
        if (trainee.UniversityGroup.CoordinatorId is { } coordinator)
            await notify.NotifyAsync(coordinator, "Learning report submitted", $"{me.FullName}'s {(type == LearningReportType.MiniProject ? "small project" : "learning report")} awaits review.", Links.SoftSkillUniversityGroup(trainee.UniversityGroupId, "trainees"));
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    // ---------- helpers ----------

    /// <summary>The checklist as one eligibility row, so the programme page shows it with the cl.7 conditions.</summary>
    private static EligibilityRule DocumentsRule(ModuleChecklistDto c) =>
        new("documents", "Nexus rule", "Required documents uploaded", c.Ready,
            c.Ready ? "Every required document is in My documents." : $"Missing: {string.Join(", ", c.Missing)}.", null);

    private async Task<(SoftSkillAcademic? Academic, UniversityDto? University)> AcademicAsync(long userId)
    {
        var (profile, main) = await uow.Users.GetAcademicAsync(userId);
        if (profile is null && main is null) return (null, null);
        var u = main?.University;
        var academic = new SoftSkillAcademic(main?.Level, main?.Status, main?.CurrentYear, main?.Programme,
            u?.Id, u?.Name ?? main?.Institution, u is { IsPublic: true, IsActive: true }, profile?.IdentityVerified == true);
        return (academic, u is null ? null : SoftSkillMapper.University(u));
    }

    private async Task<SoftSkillApplication> MyLatestAsync() =>
        await uow.SoftSkillApplications.GetLatestForStudentAsync(me.RequireUserId()) ?? throw new NotFoundException("You have no Soft Skill application.");

    private async Task<(SoftSkillApplication App, SoftSkillTrainee Trainee)> MyActiveTraineeAsync()
    {
        var app = await MyLatestAsync();
        if (app.Trainee is null || app.Status != ApplicationStatus.Active) throw new BadRequestException("You are not in a running training.");
        var trainee = await uow.SoftSkillTrainees.GetDetailAsync(app.Trainee.Id) ?? throw new NotFoundException("Trainee", app.Trainee.Id);
        return (app, trainee);
    }

    private async Task<MySoftSkillDto> DetailAsync(SoftSkillApplication app)
    {
        var attached = await SoftSkillMapper.AttachedAsync(uow, Modules.SoftSkill, app.Id);
        TraineeDto? trainee = null;
        if (app.Trainee is not null)
        {
            var t = await uow.SoftSkillTrainees.GetDetailAsync(app.Trainee.Id);
            if (t is not null) trainee = SoftSkillMapper.Trainee(t, await setup.GetSnapshotAsync(), clock.Today);
        }
        return new MySoftSkillDto(SoftSkillMapper.Application(app, attached), trainee);
    }
}
