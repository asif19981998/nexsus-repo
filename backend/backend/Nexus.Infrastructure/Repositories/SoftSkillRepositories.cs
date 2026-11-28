using Microsoft.EntityFrameworkCore;
using Nexus.Application.Data;
using Nexus.Application.Rules;
using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Infrastructure.Repositories;

public class UniversityRepository(NexusDbContext ctx) : BaseRepository<University>(ctx), IUniversityRepository
{
    public Task<List<University>> ListAsync(bool activeOnly) =>
        Set.AsNoTracking().Where(u => !activeOnly || u.IsActive).OrderBy(u => u.SortOrder).ThenBy(u => u.Name).ToListAsync();
}

public class SoftSkillSetupRepository(NexusDbContext ctx) : ISoftSkillSetupRepository
{
    public async Task<SoftSkillPolicy> GetPolicyAsync() =>
        await ctx.SoftSkillPolicies.AsNoTracking().OrderBy(p => p.Id).FirstOrDefaultAsync() ?? new SoftSkillPolicy();

    public Task<List<SoftSkillTopic>> GetTopicsAsync() =>
        ctx.SoftSkillTopics.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToListAsync();

    public Task<List<SoftSkillGradeBand>> GetGradeBandsAsync() =>
        ctx.SoftSkillGradeBands.AsNoTracking().OrderByDescending(g => g.MinAverage).ToListAsync();
}

public class SoftSkillBatchRepository(NexusDbContext ctx) : BaseRepository<SoftSkillBatch>(ctx), ISoftSkillBatchRepository
{
    public Task<List<SoftSkillBatch>> ListAsync() =>
        Set.AsNoTracking().AsSplitQuery()
            .Include(b => b.UniversityGroups).ThenInclude(c => c.University)
            .Include(b => b.UniversityGroups).ThenInclude(c => c.Coordinator)
            .OrderByDescending(b => b.StartDate).ThenByDescending(b => b.Id)
            .ToListAsync();

    public Task<SoftSkillBatch?> GetWithUniversityGroupsAsync(long id) =>
        Set.AsSplitQuery()
            .Include(b => b.UniversityGroups).ThenInclude(c => c.University)
            .Include(b => b.UniversityGroups).ThenInclude(c => c.Coordinator)
            .FirstOrDefaultAsync(b => b.Id == id);

    public Task<Dictionary<long, int>> TakenSeatsByUniversityGroupAsync() =>
        Ctx.SoftSkillApplications
            .Where(a => SoftSkillRules.SeatStatuses.Contains(a.Status))
            .GroupBy(a => a.UniversityGroupId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    public Task<int> TakenSeatsAsync(long universityGroupId) =>
        Ctx.SoftSkillApplications.CountAsync(a => a.UniversityGroupId == universityGroupId && SoftSkillRules.SeatStatuses.Contains(a.Status));

    public Task<int> CountInYearAsync(int year, bool includeCancelled) =>
        Set.CountAsync(b => b.Year == year && (includeCancelled || b.Status != BatchStatus.Cancelled));

    public async Task<int> CountDatedWorkOutsideAsync(long batchId, DateOnly from, DateOnly to)
    {
        var sessions = await Ctx.SoftSkillSessions.CountAsync(s =>
            s.UniversityGroup.BatchId == batchId && s.Status != SessionStatus.Cancelled && (s.Date < from || s.Date > to));
        var visits = await Ctx.SoftSkillMonitoringVisits.CountAsync(v =>
            v.UniversityGroup.BatchId == batchId && v.Status != VisitStatus.Cancelled && (v.PlannedOn < from || v.PlannedOn > to));
        return sessions + visits;
    }
}

public class SoftSkillApplicationRepository(NexusDbContext ctx) : BaseRepository<SoftSkillApplication>(ctx), ISoftSkillApplicationRepository
{
    private IQueryable<SoftSkillApplication> Detail() =>
        Set.AsSplitQuery()
            .Include(a => a.Batch)
            .Include(a => a.UniversityGroup).ThenInclude(c => c.University)
            .Include(a => a.Student).ThenInclude(s => s.Educations)
            .Include(a => a.History)
            .Include(a => a.Trainee);

    public Task<SoftSkillApplication?> GetDetailAsync(long id) => Detail().FirstOrDefaultAsync(a => a.Id == id);

    public Task<SoftSkillApplication?> GetLatestForStudentAsync(long studentId) =>
        Detail().Where(a => a.StudentId == studentId).OrderByDescending(a => a.AppliedAt).ThenByDescending(a => a.Id).FirstOrDefaultAsync();

    public Task<List<ApplicationStatus>> GetStatusesForStudentAsync(long studentId) =>
        Set.Where(a => a.StudentId == studentId).Select(a => a.Status).ToListAsync();

    public Task<List<SoftSkillApplication>> ListAsync(long? batchId, long? universityGroupId, ApplicationStatus? status) =>
        Set.AsNoTracking().AsSplitQuery()
            .Include(a => a.Batch)
            .Include(a => a.UniversityGroup).ThenInclude(c => c.University)
            .Include(a => a.Student).ThenInclude(s => s.Educations)
            .Where(a => (batchId == null || a.BatchId == batchId) && (universityGroupId == null || a.UniversityGroupId == universityGroupId) && (status == null || a.Status == status))
            .ToListAsync();

    public Task<List<SoftSkillApplication>> ListTrackedForBatchAsync(long batchId, IReadOnlyCollection<ApplicationStatus> statuses) =>
        Set.Include(a => a.History).Include(a => a.Trainee)
            .Where(a => a.BatchId == batchId && statuses.Contains(a.Status))
            .ToListAsync();
}

public class SoftSkillUniversityGroupRepository(NexusDbContext ctx) : BaseRepository<SoftSkillUniversityGroup>(ctx), ISoftSkillUniversityGroupRepository
{
    public Task<SoftSkillUniversityGroup?> GetDetailAsync(long id) =>
        Set.AsSplitQuery()
            .Include(c => c.Batch)
            .Include(c => c.University)
            .Include(c => c.Coordinator)
            .Include(c => c.Sessions).ThenInclude(s => s.Topic)
            .Include(c => c.Sessions).ThenInclude(s => s.Trainer)
            .Include(c => c.Sessions).ThenInclude(s => s.Attendance)
            .Include(c => c.Assessments).ThenInclude(a => a.Results)
            .Include(c => c.Trainees).ThenInclude(t => t.Application).ThenInclude(a => a.Student)
            .Include(c => c.Trainees).ThenInclude(t => t.Report)
            .Include(c => c.Trainees).ThenInclude(t => t.Stipends)
            .FirstOrDefaultAsync(c => c.Id == id);

    public Task<List<SoftSkillUniversityGroup>> ListAsync(long? batchId, long? userId) =>
        Set.AsNoTracking().AsSplitQuery()
            .Include(c => c.Batch)
            .Include(c => c.University)
            .Include(c => c.Coordinator)
            .Include(c => c.Trainees)
            .Include(c => c.Sessions)
            .Where(c => (batchId == null || c.BatchId == batchId)
                && (userId == null || c.CoordinatorId == userId || c.Sessions.Any(s => s.TrainerId == userId)))
            .OrderByDescending(c => c.Batch.StartDate).ThenBy(c => c.University.Name)
            .ToListAsync();
}

public class SoftSkillTrainerRepository(NexusDbContext ctx) : BaseRepository<SoftSkillTrainer>(ctx), ISoftSkillTrainerRepository
{
    public Task<SoftSkillTrainer?> GetForUserAsync(long userId) =>
        Set.Include(t => t.University).FirstOrDefaultAsync(t => t.UserId == userId);

    public Task<List<SoftSkillTrainer>> ListAsync(TrainerStatus? status, long? universityId) =>
        Set.AsNoTracking()
            .Include(t => t.User).ThenInclude(u => u.TrainerProfile)
            .Include(t => t.University)
            .Where(t => (status == null || t.Status == status) && (universityId == null || t.UniversityId == universityId))
            .OrderByDescending(t => t.AppliedAt)
            .ToListAsync();
}

public class SoftSkillTraineeRepository(NexusDbContext ctx) : BaseRepository<SoftSkillTrainee>(ctx), ISoftSkillTraineeRepository
{
    private IQueryable<SoftSkillTrainee> Graph(IQueryable<SoftSkillTrainee> q) =>
        q.AsSplitQuery()
            .Include(t => t.Application).ThenInclude(a => a.Student).ThenInclude(s => s.Educations)
            .Include(t => t.Application).ThenInclude(a => a.Batch)
            .Include(t => t.Application).ThenInclude(a => a.History)
            .Include(t => t.UniversityGroup).ThenInclude(c => c.Batch)
            .Include(t => t.UniversityGroup).ThenInclude(c => c.University)
            .Include(t => t.UniversityGroup).ThenInclude(c => c.Coordinator)
            .Include(t => t.UniversityGroup).ThenInclude(c => c.Sessions).ThenInclude(s => s.Topic)
            .Include(t => t.UniversityGroup).ThenInclude(c => c.Sessions).ThenInclude(s => s.Trainer)
            .Include(t => t.UniversityGroup).ThenInclude(c => c.Assessments)
            .Include(t => t.Attendance)
            .Include(t => t.Results)
            .Include(t => t.Stipends)
            .Include(t => t.Report);

    public Task<SoftSkillTrainee?> GetDetailAsync(long id) => Graph(Set).FirstOrDefaultAsync(t => t.Id == id);

    public Task<List<SoftSkillTrainee>> ListAsync(long? batchId, long? universityGroupId) =>
        Graph(Set.AsNoTrackingWithIdentityResolution())
            .Where(t => (batchId == null || t.UniversityGroup.BatchId == batchId) && (universityGroupId == null || t.UniversityGroupId == universityGroupId))
            .OrderBy(t => t.Application.Student.FullName)
            .ToListAsync();
}

public class SoftSkillVisitRepository(NexusDbContext ctx) : BaseRepository<SoftSkillMonitoringVisit>(ctx), ISoftSkillVisitRepository
{
    private IQueryable<SoftSkillMonitoringVisit> Graph(IQueryable<SoftSkillMonitoringVisit> q) =>
        q.Include(v => v.UniversityGroup).ThenInclude(c => c.Batch)
            .Include(v => v.UniversityGroup).ThenInclude(c => c.University)
            .Include(v => v.Monitor)
            .Include(v => v.Session).ThenInclude(s => s!.Topic);

    public Task<List<SoftSkillMonitoringVisit>> ListAsync(long? batchId, long? universityGroupId, long? monitorId, VisitStatus? status) =>
        Graph(Set.AsNoTracking())
            .Where(v => (batchId == null || v.UniversityGroup.BatchId == batchId) && (universityGroupId == null || v.UniversityGroupId == universityGroupId)
                && (monitorId == null || v.MonitorId == monitorId) && (status == null || v.Status == status))
            .OrderByDescending(v => v.PlannedOn).ThenByDescending(v => v.Id)
            .ToListAsync();

    public Task<SoftSkillMonitoringVisit?> GetDetailAsync(long id) => Graph(Set).FirstOrDefaultAsync(v => v.Id == id);
}

public class SoftSkillHonorariumRepository(NexusDbContext ctx) : BaseRepository<SoftSkillHonorarium>(ctx), ISoftSkillHonorariumRepository
{
    public Task<List<SoftSkillHonorarium>> ListPaidAsync(long? batchId, long? personId) =>
        Set.AsNoTracking()
            .Include(h => h.Person)
            .Include(h => h.UniversityGroup).ThenInclude(c => c.Batch)
            .Include(h => h.UniversityGroup).ThenInclude(c => c.University)
            .Where(h => (batchId == null || h.UniversityGroup.BatchId == batchId) && (personId == null || h.PersonId == personId))
            .ToListAsync();

    public Task<List<SoftSkillSession>> ListHeldSessionsAsync(long? batchId, long? trainerId) =>
        Ctx.SoftSkillSessions.AsNoTracking()
            .Include(s => s.UniversityGroup).ThenInclude(c => c.Batch)
            .Include(s => s.UniversityGroup).ThenInclude(c => c.University)
            .Include(s => s.Trainer)
            .Include(s => s.Topic)
            .Where(s => s.Status == SessionStatus.Held && (batchId == null || s.UniversityGroup.BatchId == batchId) && (trainerId == null || s.TrainerId == trainerId))
            .ToListAsync();

    public Task<List<SoftSkillUniversityGroup>> ListReportedUniversityGroupsAsync(long? batchId, long? coordinatorId) =>
        Ctx.SoftSkillUniversityGroups.AsNoTracking()
            .Include(c => c.Batch).Include(c => c.University).Include(c => c.Coordinator)
            .Where(c => c.FinalReportAt != null && c.CoordinatorId != null
                && (batchId == null || c.BatchId == batchId) && (coordinatorId == null || c.CoordinatorId == coordinatorId))
            .ToListAsync();

    public Task<List<SoftSkillMonitoringVisit>> ListSubmittedVisitsAsync(long? batchId, long? monitorId) =>
        Ctx.SoftSkillMonitoringVisits.AsNoTracking()
            .Include(v => v.UniversityGroup).ThenInclude(c => c.Batch)
            .Include(v => v.UniversityGroup).ThenInclude(c => c.University)
            .Include(v => v.Monitor)
            .Where(v => v.Status == VisitStatus.Submitted && (batchId == null || v.UniversityGroup.BatchId == batchId) && (monitorId == null || v.MonitorId == monitorId))
            .ToListAsync();

    public Task<bool> IsPaidAsync(HonorariumKind kind, long sourceId) => Set.AnyAsync(h => h.Kind == kind && h.SourceId == sourceId);
}
