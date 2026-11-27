using Microsoft.EntityFrameworkCore;
using Nexus.Application.Data;
using Nexus.Application.Rules;
using Nexus.Domain.Entities.Internships;
using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Enums;

namespace Nexus.Infrastructure.Repositories;

public class InternshipBatchRepository(NexusDbContext ctx) : BaseRepository<InternshipBatch>(ctx), IInternshipBatchRepository
{
    public Task<List<InternshipBatch>> ListAsync() =>
        Set.AsNoTracking().OrderByDescending(b => b.Id).ToListAsync();

    public Task<Dictionary<long, int>> TakenSeatsAsync() =>
        Ctx.InternshipApplications
            .Where(a => InternshipRules.SeatStatuses.Contains(a.Status))
            .GroupBy(a => a.BatchId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    public Task<int> TakenSeatsAsync(long batchId) =>
        Ctx.InternshipApplications.CountAsync(a => a.BatchId == batchId && InternshipRules.SeatStatuses.Contains(a.Status));

    public Task<int> CountInYearAsync(int year, bool includeCancelled) =>
        Set.CountAsync(b => b.Year == year && (includeCancelled || b.Status != BatchStatus.Cancelled));
}

public class InternshipApplicationRepository(NexusDbContext ctx) : BaseRepository<InternshipApplication>(ctx), IInternshipApplicationRepository
{
    /// <summary>The whole application graph. Split queries keep the attendance rows from multiplying the other joins.</summary>
    private IQueryable<InternshipApplication> Detail() =>
        Set.AsSplitQuery()
            .Include(a => a.Batch)
            .Include(a => a.Student).ThenInclude(s => s.Educations)
            .Include(a => a.PreferredDepartments).ThenInclude(p => p.Department)
            .Include(a => a.History)
            .Include(a => a.Internship).ThenInclude(i => i!.Department)
            .Include(a => a.Internship).ThenInclude(i => i!.Mentor)
            .Include(a => a.Internship).ThenInclude(i => i!.Placements).ThenInclude(p => p.Department)
            .Include(a => a.Internship).ThenInclude(i => i!.Placements).ThenInclude(p => p.Mentor)
            .Include(a => a.Internship).ThenInclude(i => i!.Attendance)
            .Include(a => a.Internship).ThenInclude(i => i!.Bills)
            .Include(a => a.Internship).ThenInclude(i => i!.Report)
            .Include(a => a.Internship).ThenInclude(i => i!.Evaluation).ThenInclude(e => e!.Scores)
            .Include(a => a.Internship).ThenInclude(i => i!.Evaluation).ThenInclude(e => e!.Evaluator);

    public Task<InternshipApplication?> GetDetailAsync(long id) => Detail().FirstOrDefaultAsync(a => a.Id == id);

    public Task<InternshipApplication?> GetLatestForStudentAsync(long studentId) =>
        Detail().Where(a => a.StudentId == studentId).OrderByDescending(a => a.AppliedAt).ThenByDescending(a => a.Id).FirstOrDefaultAsync();

    public Task<List<ApplicationStatus>> GetStatusesForStudentAsync(long studentId) =>
        Set.Where(a => a.StudentId == studentId).Select(a => a.Status).ToListAsync();

    public Task<List<InternshipApplication>> ListAsync(long? batchId, ApplicationStatus? status) =>
        Set.AsNoTracking().AsSplitQuery()
            .Include(a => a.Batch)
            .Include(a => a.Student).ThenInclude(s => s.Educations)
            .Include(a => a.PreferredDepartments).ThenInclude(p => p.Department)
            .Where(a => (batchId == null || a.BatchId == batchId) && (status == null || a.Status == status))
            .ToListAsync();

    public Task<List<InternshipApplication>> ListInternsAsync(IReadOnlyCollection<ApplicationStatus> statuses, long? mentorId) =>
        Set.AsNoTracking().AsSplitQuery()
            .Include(a => a.Batch)
            .Include(a => a.Student).ThenInclude(s => s.Educations)
            .Include(a => a.Internship).ThenInclude(i => i!.Department)
            .Include(a => a.Internship).ThenInclude(i => i!.Mentor)
            .Include(a => a.Internship).ThenInclude(i => i!.Attendance)
            .Include(a => a.Internship).ThenInclude(i => i!.Bills)
            .Include(a => a.Internship).ThenInclude(i => i!.Report)
            .Where(a => a.Internship != null && statuses.Contains(a.Status) && (mentorId == null || a.Internship.MentorId == mentorId))
            .ToListAsync();

    public Task<int> CountCertificatesInBatchAsync(long batchId) =>
        Set.CountAsync(a => a.BatchId == batchId && a.Internship != null && a.Internship.CertificateNumber != null);
}

public class SetupRepository(NexusDbContext ctx) : ISetupRepository
{
    public async Task<InternshipPolicy> GetPolicyAsync() =>
        await ctx.InternshipPolicies.AsNoTracking().OrderBy(p => p.Id).FirstOrDefaultAsync() ?? new InternshipPolicy();

    public Task<List<Department>> GetDepartmentsAsync(bool activeOnly) =>
        ctx.Departments.AsNoTracking().Where(d => !activeOnly || d.IsActive).OrderBy(d => d.SortOrder).ThenBy(d => d.Name).ToListAsync();

    public Task<List<EvaluationCriterion>> GetCriteriaAsync() =>
        ctx.EvaluationCriteria.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync();

    public Task<List<GradeBand>> GetGradeBandsAsync() =>
        ctx.GradeBands.AsNoTracking().OrderByDescending(g => g.MinScore).ToListAsync();
}
