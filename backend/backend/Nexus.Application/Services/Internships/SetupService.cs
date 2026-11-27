using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.Internships;
using Nexus.Application.Rules;
using Nexus.Domain.Entities.Internships;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Internships;

public interface ISetupService
{
    /// <summary>Policy numbers and lookups the screens need: departments, exam modes, criteria, grades, statuses.</summary>
    Task<SetupDto> GetSetupAsync();

    Task<PolicySnapshot> GetSnapshotAsync();
}

public sealed class SetupService(IUnitOfWork uow, IClock clock) : ISetupService
{
    public async Task<SetupDto> GetSetupAsync()
    {
        var s = await GetSnapshotAsync();
        var p = s.Policy;
        var departments = await uow.Setup.GetDepartmentsAsync(activeOnly: true);
        return new SetupDto(
            new PolicyDto(p.BatchSize, p.MaxBatchesPerYear, p.MinMonths, p.MaxMonths, p.MonthlyAllowance, p.MinAttendancePct,
                p.ReportWorkingDays, p.GraduateWindowMonths, p.MaxPreferredDepartments, p.MinStatementLength, p.MinReportLength,
                p.MinRecommendationLength, InternshipEvaluation.MaxRecommendationLength, InternshipReport.MaxReviewNoteLength, p.EvaluationAfterReport,
                AllowanceBill.MinReportLength, AllowanceBill.MaxReportLength, AllowanceBill.MaxEvaluationNoteLength),
            departments.Select(d => new DepartmentDto(d.Id, d.Name)).ToList(),
            Enum.GetNames<ExamMode>(),
            s.Criteria.Select(c => new CriterionDto(c.Id, c.Key, c.Label, c.MaxScore)).ToList(),
            s.GradeBands.Select(g => new GradeBandDto(g.Grade, g.NameBn, g.MinScore)).ToList(),
            StatusLabels.BatchLabels,
            StatusLabels.ApplicationLabels,
            clock.Today.ToString("yyyy-MM-dd"));
    }

    public async Task<PolicySnapshot> GetSnapshotAsync()
    {
        var policy = await uow.Setup.GetPolicyAsync();
        var criteria = await uow.Setup.GetCriteriaAsync();
        var bands = await uow.Setup.GetGradeBandsAsync();
        var calendar = WorkCalendar.From(policy, (await uow.Holidays.GetAllAsync()).Select(h => h.Date));
        return new PolicySnapshot(policy, criteria, bands, calendar);
    }
}
