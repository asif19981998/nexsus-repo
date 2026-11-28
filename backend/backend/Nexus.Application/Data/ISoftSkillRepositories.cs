using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Application.Data;

public interface IUniversityRepository : IRepository<University>
{
    /// <summary>By sort order, then name.</summary>
    Task<List<University>> ListAsync(bool activeOnly);
}

public interface ISoftSkillSetupRepository
{
    Task<SoftSkillPolicy> GetPolicyAsync();

    /// <summary>Active topics in cl.13 order.</summary>
    Task<List<SoftSkillTopic>> GetTopicsAsync();

    /// <summary>Highest band first.</summary>
    Task<List<SoftSkillGradeBand>> GetGradeBandsAsync();
}

public interface ISoftSkillBatchRepository : IRepository<SoftSkillBatch>
{
    /// <summary>Every batch with its university groups, universities and coordinators, newest first (not tracked).</summary>
    Task<List<SoftSkillBatch>> ListAsync();

    /// <summary>One batch with its university groups, tracked for updates.</summary>
    Task<SoftSkillBatch?> GetWithUniversityGroupsAsync(long id);

    /// <summary>Seats taken per university group id (Selected and later, not withdrawn, rejected or cancelled).</summary>
    Task<Dictionary<long, int>> TakenSeatsByUniversityGroupAsync();
    Task<int> TakenSeatsAsync(long universityGroupId);

    /// <summary>Batches in a year. Cancelled ones are counted only when <paramref name="includeCancelled"/> is set.</summary>
    Task<int> CountInYearAsync(int year, bool includeCancelled);

    /// <summary>Sessions and monitoring visits of the batch (not cancelled) dated outside <paramref name="from"/>–<paramref name="to"/>.</summary>
    Task<int> CountDatedWorkOutsideAsync(long batchId, DateOnly from, DateOnly to);
}

public interface ISoftSkillApplicationRepository : IRepository<SoftSkillApplication>
{
    /// <summary>One application with batch, university group, student, history and trainee, tracked.</summary>
    Task<SoftSkillApplication?> GetDetailAsync(long id);

    /// <summary>The student's most recent application, tracked.</summary>
    Task<SoftSkillApplication?> GetLatestForStudentAsync(long studentId);

    Task<List<ApplicationStatus>> GetStatusesForStudentAsync(long studentId);

    /// <summary>For the admin list (not tracked).</summary>
    Task<List<SoftSkillApplication>> ListAsync(long? batchId, long? universityGroupId, ApplicationStatus? status);

    /// <summary>A batch's applications in the given statuses, with history, tracked: used when the batch changes status.</summary>
    Task<List<SoftSkillApplication>> ListTrackedForBatchAsync(long batchId, IReadOnlyCollection<ApplicationStatus> statuses);
}

public interface ISoftSkillUniversityGroupRepository : IRepository<SoftSkillUniversityGroup>
{
    /// <summary>
    /// A university group with everything its screens need: batch, university, coordinator, sessions (topic, trainer, attendance),
    /// assessments with results, and trainees (student, report, stipends). Tracked.
    /// </summary>
    Task<SoftSkillUniversityGroup?> GetDetailAsync(long id);

    /// <summary>University groups with batch, university and coordinator. <paramref name="userId"/>: only those the person coordinates or trains.</summary>
    Task<List<SoftSkillUniversityGroup>> ListAsync(long? batchId, long? userId);
}

public interface ISoftSkillTrainerRepository : IRepository<SoftSkillTrainer>
{
    /// <summary>The person's trainer registration with its university, tracked.</summary>
    Task<SoftSkillTrainer?> GetForUserAsync(long userId);

    /// <summary>With user and university (not tracked).</summary>
    Task<List<SoftSkillTrainer>> ListAsync(TrainerStatus? status, long? universityId);
}

public interface ISoftSkillTraineeRepository : IRepository<SoftSkillTrainee>
{
    /// <summary>A trainee with application (student, batch, history), university group (batch, university, sessions, assessments) and own records. Tracked.</summary>
    Task<SoftSkillTrainee?> GetDetailAsync(long id);

    /// <summary>The same graph for many trainees (not tracked).</summary>
    Task<List<SoftSkillTrainee>> ListAsync(long? batchId, long? universityGroupId);
}

/// <summary>cl.14 (ঘ) monitoring visits.</summary>
public interface ISoftSkillVisitRepository : IRepository<SoftSkillMonitoringVisit>
{
    /// <summary>With university group (batch, university), monitor and session topic; not tracked. Null filters match all.</summary>
    Task<List<SoftSkillMonitoringVisit>> ListAsync(long? batchId, long? universityGroupId, long? monitorId, VisitStatus? status);

    /// <summary>One visit with the same graph, tracked.</summary>
    Task<SoftSkillMonitoringVisit?> GetDetailAsync(long id);
}

/// <summary>cl.17 honoraria: the paid rows and the sources that make an honorarium due.</summary>
public interface ISoftSkillHonorariumRepository : IRepository<SoftSkillHonorarium>
{
    /// <summary>Paid rows, filtered to one person or one batch when given (not tracked).</summary>
    Task<List<SoftSkillHonorarium>> ListPaidAsync(long? batchId, long? personId);

    /// <summary>Held sessions with university group (batch, university), trainer and topic (not tracked).</summary>
    Task<List<SoftSkillSession>> ListHeldSessionsAsync(long? batchId, long? trainerId);

    /// <summary>University groups whose final report is sent and that have a coordinator, with batch, university, coordinator (not tracked).</summary>
    Task<List<SoftSkillUniversityGroup>> ListReportedUniversityGroupsAsync(long? batchId, long? coordinatorId);

    /// <summary>Submitted visits with university group (batch, university) and monitor (not tracked).</summary>
    Task<List<SoftSkillMonitoringVisit>> ListSubmittedVisitsAsync(long? batchId, long? monitorId);

    Task<bool> IsPaidAsync(HonorariumKind kind, long sourceId);
}
