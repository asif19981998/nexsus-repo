using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.SoftSkills;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.SoftSkills;

public interface ISoftSkillSetupService
{
    /// <summary>Policy numbers and lookups the Soft Skill screens need.</summary>
    Task<SoftSkillSetupDto> GetSetupAsync();

    Task<SoftSkillSnapshot> GetSnapshotAsync();

    /// <summary>Active universities, for profile pickers and university group forms.</summary>
    Task<List<UniversityDto>> ListUniversitiesAsync();
}

public sealed class SoftSkillSetupService(IUnitOfWork uow, IClock clock) : ISoftSkillSetupService
{
    public async Task<SoftSkillSetupDto> GetSetupAsync()
    {
        var s = await GetSnapshotAsync();
        var p = s.Policy;
        return new SoftSkillSetupDto(
            new SoftSkillPolicyDto(p.SeatsPerUniversity, p.MaxBatchesPerYear, p.MinStudyYear, p.MaxStudyYear, p.DurationMonths, p.TotalHours,
                p.MonthlyStipend, p.MinAttendancePct, p.StipendMinAttendancePct, p.ApprovedLeaveCountsAsPresent, p.ReportWorkingDays,
                p.AssessmentMaxMarks, p.AssessmentPassMarks, p.MinStatementLength, p.MinReportLength, p.MinFinalReportLength,
                p.TrainerRatePerSession, p.CoordinatorRatePerUniversityGroup, p.ExpertRatePerVisit),
            s.Topics.Select(SoftSkillMapper.Topic).ToList(),
            s.GradeBands.Select(g => new SoftSkillGradeBandDto(g.Grade, g.NameBn, g.MinAverage)).ToList(),
            await ListUniversitiesAsync(),
            StatusLabels.BatchLabels,
            StatusLabels.ApplicationLabels,
            clock.Today.ToString("yyyy-MM-dd"));
    }

    public async Task<SoftSkillSnapshot> GetSnapshotAsync()
    {
        var policy = await uow.SoftSkillSetup.GetPolicyAsync();
        var topics = await uow.SoftSkillSetup.GetTopicsAsync();
        var bands = await uow.SoftSkillSetup.GetGradeBandsAsync();
        // One working week for the whole Commission: the weekly off days live on the internship policy.
        var calendar = WorkCalendar.From(await uow.Setup.GetPolicyAsync(), (await uow.Holidays.GetAllAsync()).Select(h => h.Date));
        return new SoftSkillSnapshot(policy, topics, bands, calendar);
    }

    public async Task<List<UniversityDto>> ListUniversitiesAsync() =>
        (await uow.Universities.ListAsync(activeOnly: true)).Select(SoftSkillMapper.University).ToList();
}

/// <summary>Status changes on a Soft Skill application always go through here so the history row is never forgotten.</summary>
public sealed class SoftSkillWorkflow(ICurrentUserService me, IClock clock)
{
    public void SetStatus(SoftSkillApplication app, ApplicationStatus status, string? note)
    {
        if (app.Status == status) return;
        app.Status = status;
        app.History.Add(new SoftSkillApplicationHistory
        {
            Status = status,
            At = clock.UtcNow,
            ById = me.UserId,
            ByName = me.FullName,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        });
    }
}
