using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.Documents;
using Nexus.Application.Dtos.Internships;
using Nexus.Application.Services.Documents;
using Nexus.Domain.Entities.Documents;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.Internships;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Internships;

/// <summary>Everything a student does for themself. Always acts on the caller's own latest application.</summary>
public interface IStudentInternshipService
{
    Task<EligibilityDto> GetEligibilityAsync();
    Task<ApplicationDetailDto?> GetMyApplicationAsync();
    Task<ApplicationDetailDto> ApplyAsync(ApplyRequest request);
    Task<ApplicationDetailDto> WithdrawAsync();
    Task<ApplicationDetailDto> RequestLeaveAsync(LeaveRequestBody request);
    Task<ApplicationDetailDto> SubmitBillAsync(string month, SubmitMonthlyReportRequest request);
    Task<ApplicationDetailDto> SubmitReportAsync(SubmitReportRequest request);
}

public sealed class StudentInternshipService(
    IUnitOfWork uow, ISetupService setup, ICurrentUserService me, IClock clock,
    IAuditService audit, INotificationService notify, ApplicationWorkflow workflow, IRequirementService requirements) : IStudentInternshipService
{
    public async Task<EligibilityDto> GetEligibilityAsync()
    {
        var userId = me.RequireUserId();
        var (studentProfile, main) = await uow.Users.GetAcademicAsync(userId);
        var profile = AcademicRules.Snapshot(studentProfile, main);
        var statuses = await uow.Applications.GetStatusesForStudentAsync(userId);
        var policy = (await setup.GetSnapshotAsync()).Policy;
        var r = InternshipRules.CheckEligibility(profile, statuses, clock.Today, policy);
        var checklist = await requirements.GetChecklistAsync(Modules.Internship, userId);
        var docs = DocumentsRule(checklist);
        return new EligibilityDto(r.Eligible && docs.Met, r.Basis, [.. r.Rules, docs]);
    }

    /// <summary>The checklist as one eligibility row, so the programme page shows it with the cl.7 conditions.</summary>
    private static EligibilityRule DocumentsRule(ModuleChecklistDto c) =>
        new("documents", "Nexus rule", "Required documents uploaded", c.Ready,
            c.Ready ? "Every required document is in My documents." : $"Missing: {string.Join(", ", c.Missing)}.", null);

    public async Task<ApplicationDetailDto?> GetMyApplicationAsync()
    {
        var app = await uow.Applications.GetLatestForStudentAsync(me.RequireUserId());
        return app is null ? null : await DetailAsync(app);
    }

    public async Task<ApplicationDetailDto> ApplyAsync(ApplyRequest r)
    {
        var userId = me.RequireUserId();
        var snap = await setup.GetSnapshotAsync();
        var policy = snap.Policy;
        var batch = await uow.Batches.GetByIdAsync(r.BatchId) ?? throw new NotFoundException("Batch", r.BatchId);
        var today = clock.Today;

        if (batch.Status != BatchStatus.ApplicationsOpen) throw new BadRequestException("This batch is not accepting applications.");
        if (today < batch.ApplyFrom || today > batch.ApplyTo)
            throw new BadRequestException($"The application window is {batch.ApplyFrom:dd MMM yyyy} to {batch.ApplyTo:dd MMM yyyy}.");

        var (studentProfile, main) = await uow.Users.GetAcademicAsync(userId);
        var profile = AcademicRules.Snapshot(studentProfile, main);
        var statuses = await uow.Applications.GetStatusesForStudentAsync(userId);
        var elig = InternshipRules.CheckEligibility(profile, statuses, today, policy);
        if (!elig.Eligible)
        {
            var failed = elig.Rules.FirstOrDefault(x => x.Group is null && !x.Met);
            throw new BadRequestException(failed is null ? "Not eligible under cl.7." : $"Not eligible: {failed.Label}.");
        }

        // Uploaded is enough; the administrator verifies documents while reviewing the application.
        var checklist = await requirements.GetChecklistAsync(Modules.Internship, userId);
        if (!checklist.Ready) throw new BadRequestException($"Upload these documents in My documents first: {string.Join(", ", checklist.Missing)}.");

        var statement = r.Statement?.Trim() ?? "";
        if (statement.Length < policy.MinStatementLength)
            throw new BadRequestException($"Write at least {policy.MinStatementLength} characters in the statement of purpose.");

        var departments = await uow.Setup.GetDepartmentsAsync(activeOnly: true);
        var prefs = r.PreferredDepartmentIds.Distinct().Where(id => departments.Any(d => d.Id == id)).Take(policy.MaxPreferredDepartments).ToList();
        if (prefs.Count == 0) throw new BadRequestException("Choose at least one preferred department.");

        var app = new InternshipApplication
        {
            BatchId = batch.Id,
            StudentId = userId,
            Status = ApplicationStatus.Submitted,
            AppliedAt = clock.UtcNow,
            Statement = statement,
            Basis = elig.Basis,
            PreferredDepartments = prefs.Select((id, i) => new ApplicationPreferredDepartment { DepartmentId = id, Rank = i + 1 }).ToList(),
        };
        app.History.Add(new ApplicationStatusHistory { Status = ApplicationStatus.Submitted, At = clock.UtcNow, ById = userId, ByName = me.FullName, Note = "Application submitted" });

        await uow.Applications.AddAsync(app);
        await uow.SaveChangesAsync();

        // The versions in the library now go with the application; later uploads do not change what was sent.
        var attached = checklist.Rows.Where(r => r.Document is not null).Select(r => r.Document!).ToList();
        foreach (var d in attached)
            await uow.Documents.AttachAsync(new ApplicationDocument { Module = Modules.Internship, ApplicationId = app.Id, UserDocumentId = d.Id });
        var codes = string.Join(",", attached.Select(d => d.TypeCode));
        app.Documents = codes.Length <= 200 ? codes : codes[..200];

        await audit.LogAsync("application.submit", "InternshipApplication", app.Id, $"{me.FullName} applied to {batch.Code} with {attached.Count} documents");
        await notify.NotifyHoldersAsync(Permissions.ApplicationScreen, "New UGC internship application", $"{me.FullName} applied to {batch.Name}.", Links.Manage);
        await notify.NotifyAsync(userId, "Application received", $"Your application to {batch.Name} is submitted.", Links.Student);
        await uow.SaveChangesAsync();

        return await DetailAsync(await MyLatestAsync());
    }

    public async Task<ApplicationDetailDto> WithdrawAsync()
    {
        var app = await MyLatestAsync();
        if (!InternshipRules.WithdrawableStatuses.Contains(app.Status)) throw new BadRequestException("This application can no longer be withdrawn.");
        workflow.SetStatus(app, ApplicationStatus.Withdrawn, "Withdrawn by applicant");
        await audit.LogAsync("application.withdraw", "InternshipApplication", app.Id, "Withdrawn by applicant");
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    // Signing in and out is done in the mobile app only (selfie and location): see AttendanceService.PunchAsync.

    public async Task<ApplicationDetailDto> RequestLeaveAsync(LeaveRequestBody r)
    {
        var (app, it) = await MyActiveInternshipAsync();
        if (r.Date is not { } date) throw new BadRequestException("Choose a date.");
        if (!InternshipRules.IsWorkingDay(date, (await setup.GetSnapshotAsync()).Holidays)) throw new BadRequestException("That date is a weekend or a holiday.");
        if (date < it.StartDate || date > it.EndDate) throw new BadRequestException("The date is outside your internship period.");
        var reason = r.Reason?.Trim() ?? "";
        if (reason.Length < 5) throw new BadRequestException("Give a valid reason (cl.11 ঞ).");
        if (it.Attendance.Any(a => a.Date == date)) throw new BadRequestException("You already have a record for that date.");

        it.Attendance.Add(new AttendanceRecord { Date = date, Status = AttendanceStatus.LeaveRequested, Reason = reason });
        await audit.LogAsync("leave.request", "InternshipApplication", app.Id, $"{date:yyyy-MM-dd}: {reason}");
        await notify.NotifyHoldersAsync(Permissions.InternViewAll, "Intern leave request", $"{me.FullName} asked for leave on {date:dd MMM yyyy}.", Links.Manage, it.MentorId);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<ApplicationDetailDto> SubmitBillAsync(string month, SubmitMonthlyReportRequest r)
    {
        var app = await MyLatestAsync();
        var it = app.Internship ?? throw new BadRequestException("No internship yet.");
        var bill = InternshipRules.MonthlyBills(it, clock.Today, await setup.GetSnapshotAsync()).FirstOrDefault(b => b.Month == month)
            ?? throw new BadRequestException("That month has not started yet or is outside your internship.");
        if (bill.Status != BillStatus.ReportDue)
            throw new BadRequestException(bill.Status == BillStatus.Accruing
                ? "The month is not over yet; submit its report after the last day."
                : "This month's report is already submitted.");
        var report = r.Report?.Trim() ?? "";
        if (report.Length < AllowanceBill.MinReportLength)
            throw new BadRequestException($"Write at least {AllowanceBill.MinReportLength} characters on the month's work.");
        if (report.Length > AllowanceBill.MaxReportLength)
            throw new BadRequestException($"The report can have at most {AllowanceBill.MaxReportLength} characters.");

        it.Bills.Add(new AllowanceBill { Month = month, Report = report, SubmittedAt = clock.UtcNow });
        await audit.LogAsync("bill.submit", "InternshipApplication", app.Id, month);
        await notify.NotifyHoldersAsync(Permissions.InternViewAll, "Monthly report submitted",
            $"{me.FullName}'s report for {month} awaits evaluation.", Links.Manage, it.MentorId);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    public async Task<ApplicationDetailDto> SubmitReportAsync(SubmitReportRequest r)
    {
        var app = await MyLatestAsync();
        if (app.Internship is not { } it || app.Status is not (ApplicationStatus.Active or ApplicationStatus.Completed))
            throw new BadRequestException("No internship to report on.");
        var policy = (await setup.GetSnapshotAsync()).Policy;
        var text = r.Text?.Trim() ?? "";
        if (text.Length < policy.MinReportLength) throw new BadRequestException($"The report needs at least {policy.MinReportLength} characters.");
        if (it.Report?.Status == ReportStatus.Accepted) throw new BadRequestException("Your report is already accepted.");

        if (it.Report is null)
        {
            it.Report = new InternshipReport { Text = text, SubmittedAt = clock.UtcNow, Status = ReportStatus.Submitted };
        }
        else
        {
            it.Report.Text = text;
            it.Report.SubmittedAt = clock.UtcNow;
            it.Report.Status = ReportStatus.Submitted;
        }
        await audit.LogAsync("report.submit", "InternshipApplication", app.Id, "Final report submitted");
        await notify.NotifyHoldersAsync(Permissions.InternViewAll, "Final report submitted", $"{me.FullName}'s final report awaits review.", Links.Manage, it.MentorId);
        await uow.SaveChangesAsync();
        return await DetailAsync(app);
    }

    // ---------- helpers ----------

    private async Task<InternshipApplication> MyLatestAsync() =>
        await uow.Applications.GetLatestForStudentAsync(me.RequireUserId()) ?? throw new NotFoundException("You have no UGC internship application.");

    private async Task<(InternshipApplication, Domain.Entities.Internships.Internship)> MyActiveInternshipAsync()
    {
        var app = await MyLatestAsync();
        if (app.Status != ApplicationStatus.Active || app.Internship is null) throw new BadRequestException("No active internship.");
        return (app, app.Internship);
    }

    private async Task<ApplicationDetailDto> DetailAsync(InternshipApplication app) =>
        InternshipMapper.Detail(app, await setup.GetSnapshotAsync(), clock.Today, await InternshipMapper.AttachedAsync(uow, app.Id));
}
