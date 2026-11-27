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

/// <summary>cl.10 allowance: monthly bills across all interns.</summary>
public interface IBillService
{
    /// <param name="status">"all" (billable months, i.e. not Accruing) or one bill status.</param>
    Task<List<BillRowDto>> ListAsync(string? status);
    Task<List<BillRowDto>> PayAsync(long applicationId, string month, string? listStatus);
}

public sealed class BillService(
    IUnitOfWork uow, ISetupService setup, ICurrentUserService me, IClock clock, IAuditService audit, INotificationService notify) : IBillService
{
    public async Task<List<BillRowDto>> ListAsync(string? status)
    {
        var snap = await setup.GetSnapshotAsync();
        var today = clock.Today;
        var apps = await uow.Applications.ListInternsAsync(InternshipRules.InternStatuses, mentorId: null);
        var filter = string.IsNullOrWhiteSpace(status) ? "all" : status;

        return apps
            .SelectMany(a => InternshipRules.MonthlyBills(a.Internship!, today, snap)
                .Select(b => new BillRowDto(a.Id, a.Student.FullName, a.Internship!.Department.Name, a.Internship.Mentor?.FullName,
                    b.Month, b.PeriodDays, b.Gross, b.Deduction, b.DeductDays, b.Net, b.Attendance, b.Register, b.Scores, b.Status, b.WithheldReason,
                    b.Score, b.EvaluatedBy, b.PaidAt, b.PaidBy, b.SlipNumber)))
            .Where(b => filter == "all" ? b.Status != BillStatus.Accruing : b.Status == filter)
            .OrderByDescending(b => b.Month)
            .ThenByDescending(b => b.ApplicationId)
            .ToList();
    }

    public async Task<List<BillRowDto>> PayAsync(long applicationId, string month, string? listStatus)
    {
        var app = await uow.Applications.GetDetailAsync(applicationId);
        var bill = app?.Internship?.Bills.FirstOrDefault(b => b.Month == month);
        if (app is null || bill is null) throw new BadRequestException("The intern has not submitted this month's report.");
        if (bill.PaidAt is not null) throw new BadRequestException("Already paid.");

        // Paid only after the month is evaluated; the last month also waits for the accepted final report (cl.11).
        var view = InternshipRules.MonthlyBills(app.Internship!, clock.Today, await setup.GetSnapshotAsync()).First(b => b.Month == month);
        if (view.Status == BillStatus.AwaitingEvaluation) throw new BadRequestException("The month's report is not evaluated yet.");
        if (view.Status == BillStatus.Withheld) throw new BadRequestException(view.WithheldReason!);

        bill.PaidAt = clock.UtcNow;
        bill.PaidById = me.UserId;
        bill.PaidByName = me.FullName;
        bill.PaidAmount = view.Net!.Value;
        bill.SlipNumber = AllowanceBill.SlipNumberFor(app.Internship!.Id, month);
        await audit.LogAsync("bill.pay", "InternshipApplication", applicationId, $"{month}: BDT {view.Net}, slip {bill.SlipNumber}");
        await notify.NotifyAsync(app.StudentId, "Allowance paid", $"Your allowance for {month} (BDT {view.Net:N0}) is paid. Slip {bill.SlipNumber}.", Links.Student);
        await uow.SaveChangesAsync();
        return await ListAsync(listStatus);
    }
}

public interface IOverviewService
{
    Task<OverviewDto> GetAsync();
}

public sealed class OverviewService(IUnitOfWork uow, ISetupService setup, IClock clock) : IOverviewService
{
    private static readonly ApplicationStatus[] FunnelOrder =
    [
        ApplicationStatus.Submitted, ApplicationStatus.Shortlisted, ApplicationStatus.Waitlisted, ApplicationStatus.Selected,
        ApplicationStatus.Active, ApplicationStatus.Completed, ApplicationStatus.Certified, ApplicationStatus.NotCertified,
        ApplicationStatus.Rejected, ApplicationStatus.Terminated, ApplicationStatus.Withdrawn,
    ];

    public async Task<OverviewDto> GetAsync()
    {
        var snap = await setup.GetSnapshotAsync();
        var policy = snap.Policy;
        var today = clock.Today;
        var batches = await uow.Batches.ListAsync();
        var taken = await uow.Batches.TakenSeatsAsync();
        var apps = await uow.Applications.ListAsync(null, null);
        var interns = await uow.Applications.ListInternsAsync(InternshipRules.InternStatuses, mentorId: null);

        var active = interns.Where(a => a.Status == ApplicationStatus.Active).ToList();
        var atRisk = active
            .Select(a => (a, sum: InternshipRules.Summary(a.Internship!, today, snap)))
            .Where(x => x.sum.BelowMin)
            .Select(x => new RiskDto(x.a.Student.FullName, x.sum.Pct))
            .ToList();

        var seatsLeft = batches
            .Where(b => b.Status is BatchStatus.ApplicationsOpen or BatchStatus.Selection or BatchStatus.Running)
            .Sum(b => Math.Max(0, b.Capacity - taken.GetValueOrDefault(b.Id)));

        var actions = new List<OverviewActionDto>();
        void Add(int n, string label, string tab)
        {
            if (n > 0) actions.Add(new OverviewActionDto(n, label, tab));
        }
        Add(apps.Count(a => a.Status == ApplicationStatus.Submitted), "applications to screen", "applications");
        Add(apps.Count(a => a.Status == ApplicationStatus.Shortlisted && a.ExamScore is null), "shortlisted candidates need a test score", "applications");
        Add(apps.Count(a => a.Status == ApplicationStatus.Selected), "selected candidates to place in a department", "applications");
        Add(interns.Count(a => a.Internship!.Attendance.Any(r => r.Status == AttendanceStatus.LeaveRequested)), "interns with leave requests", "interns");
        Add(interns.Count(a => a.Internship!.Report?.Status == ReportStatus.Submitted), "final reports to review", "interns");
        Add(interns.Count(a => a.Status == ApplicationStatus.Completed), "completed internships awaiting evaluation or certificate", "interns");
        Add(interns.Sum(a => a.Internship!.Bills.Count(b => b.EvaluatedAt is null && b.PaidAt is null)), "monthly reports to evaluate", "interns");
        Add(interns.Sum(a => a.Internship!.Bills.Count(b => b.EvaluatedAt is not null && b.PaidAt is null)), "allowance bills to pay", "stipend");

        return new OverviewDto(
            batches.Count(b => b.Status == BatchStatus.ApplicationsOpen),
            apps.Count(a => a.Status is ApplicationStatus.Submitted or ApplicationStatus.Shortlisted or ApplicationStatus.Waitlisted),
            seatsLeft,
            active.Count,
            atRisk,
            actions,
            FunnelOrder.Select(s => new FunnelDto(s.Label(), apps.Count(a => a.Status == s))).ToList());
    }
}
