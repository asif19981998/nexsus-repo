using Nexus.Domain.Entities.Common;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Internships;
using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Application.Data;

public interface IInternshipBatchRepository : IRepository<InternshipBatch>
{
    /// <summary>All batches, newest start date first.</summary>
    Task<List<InternshipBatch>> ListAsync();

    /// <summary>Seats taken per batch id (Selected, Active, Completed, Certified, Not certified).</summary>
    Task<Dictionary<long, int>> TakenSeatsAsync();
    Task<int> TakenSeatsAsync(long batchId);

    /// <summary>Batches in a year. Cancelled ones are counted only when <paramref name="includeCancelled"/> is set.</summary>
    Task<int> CountInYearAsync(int year, bool includeCancelled);
}

public interface IInternshipApplicationRepository : IRepository<InternshipApplication>
{
    /// <summary>One application with its whole graph, tracked for updates.</summary>
    Task<InternshipApplication?> GetDetailAsync(long id);

    /// <summary>The student's most recent application with its whole graph, tracked.</summary>
    Task<InternshipApplication?> GetLatestForStudentAsync(long studentId);

    Task<List<ApplicationStatus>> GetStatusesForStudentAsync(long studentId);

    /// <summary>Applications for the admin list (no internship graph).</summary>
    Task<List<InternshipApplication>> ListAsync(long? batchId, ApplicationStatus? status);

    /// <summary>Applications that became internships, with attendance, bills and report loaded. Filtered to one mentor when given.</summary>
    Task<List<InternshipApplication>> ListInternsAsync(IReadOnlyCollection<ApplicationStatus> statuses, long? mentorId);

    Task<int> CountCertificatesInBatchAsync(long batchId);
}

public interface ISetupRepository
{
    Task<InternshipPolicy> GetPolicyAsync();

    /// <summary>The policy row, tracked for an update; added (unsaved) when the table is still empty.</summary>
    Task<InternshipPolicy> GetPolicyForUpdateAsync();
    Task<List<Department>> GetDepartmentsAsync(bool activeOnly);
    Task<List<EvaluationCriterion>> GetCriteriaAsync();
    Task<List<GradeBand>> GetGradeBandsAsync();
}

/// <summary>A user, the role name, and the current documents in their library (all, and those waiting for review).</summary>
/// <summary>A user with their role and document counts. <c>WaitingSince</c>: when the oldest file still waiting for review was uploaded.</summary>
public sealed record UserWithRole(User User, string? Role, int Documents = 0, int DocumentsToReview = 0, DateTime? WaitingSince = null);

public interface IUserRepository
{
    /// <param name="toReviewOnly">Only users with at least one document waiting for review.</param>
    Task<List<UserWithRole>> ListAsync(string? role, string? search, bool toReviewOnly = false);
    Task<UserWithRole?> GetAsync(long id);
    Task<User?> GetTrackedWithProfileAsync(long id);
    Task<List<User>> ListMentorsAsync();
    Task<string?> GetRoleNameAsync(long userId);
    Task<List<Role>> ListRolesAsync();

    /// <summary>Email address and name of an active user, for sending email. Null when the user is missing, inactive or has no email.</summary>
    Task<(string Email, string FullName)?> GetContactAsync(long userId);

    /// <summary>Active users whose role holds the permission. Used to address notifications.</summary>
    Task<List<long>> ListIdsWithPermissionAsync(string permission);

    /// <summary>True when any user has this phone number (compared as stored).</summary>
    Task<bool> PhoneExistsAsync(string phone);

    /// <summary>What the cl.7 check needs: the student profile and the main education line (not tracked).</summary>
    Task<(StudentProfile? Profile, Education? Main)> GetAcademicAsync(long userId);

    /// <summary>The user's education lines, highest level first (not tracked).</summary>
    Task<List<Education>> ListEducationsAsync(long userId);

    /// <summary>The trainer profile with its university (not tracked); null for other roles.</summary>
    Task<TrainerProfile?> GetTrainerProfileAsync(long userId);
}

public interface IPermissionRepository
{
    Task<List<string>> GetCodesForUserAsync(long userId);
}

public interface INotificationRepository : IRepository<Notification>
{
    Task<List<Notification>> ListForUserAsync(long userId, int take);
    Task<int> UnreadCountAsync(long userId);
    Task MarkAllReadAsync(long userId);
}

/// <summary>Uploaded documents in people's libraries.</summary>
public interface IDocumentRepository : IRepository<UserDocument>
{
    /// <summary>The current version of each of the user's documents, with its type (not tracked).</summary>
    Task<List<UserDocument>> ListCurrentForUserAsync(long userId);

    /// <summary>The user's current document of one type, tracked so it can be replaced.</summary>
    Task<UserDocument?> GetCurrentTrackedAsync(long userId, long documentTypeId);

    /// <summary>Checklist lines with their document types; all modules when <paramref name="module"/> is null.</summary>
    Task<List<ModuleDocumentRequirement>> ListRequirementsAsync(string? module = null);

    /// <summary>True when any application has this document version attached.</summary>
    Task<bool> IsAttachedAsync(long userDocumentId);

    /// <summary>
    /// True when an application still in progress, in any module, has this document version attached: an internship or
    /// Soft Skill application in <paramref name="liveStatuses"/>, or a trainer registration waiting for a decision.
    /// </summary>
    Task<bool> IsAttachedToLiveApplicationAsync(long userDocumentId, IReadOnlyCollection<ApplicationStatus> liveStatuses);

    /// <summary>The document versions sent with one application, with their types (not tracked).</summary>
    Task<List<UserDocument>> ListAttachedAsync(string module, long applicationId);

    Task AttachAsync(ApplicationDocument attachment);
}

public interface IUnitOfWork
{
    IInternshipBatchRepository Batches { get; }
    IInternshipApplicationRepository Applications { get; }
    ISetupRepository Setup { get; }
    IUserRepository Users { get; }
    IPermissionRepository Permissions { get; }
    INotificationRepository Notifications { get; }
    IRepository<AuditLog> AuditLogs { get; }
    IRepository<EmailMessage> Emails { get; }
    IRepository<StudentProfile> StudentProfiles { get; }
    IRepository<Education> Educations { get; }
    IRepository<Holiday> Holidays { get; }
    IRepository<Department> Departments { get; }
    IRepository<DocumentType> DocumentTypes { get; }
    IDocumentRepository Documents { get; }

    IUniversityRepository Universities { get; }
    IRepository<TrainerProfile> TrainerProfiles { get; }

    ISoftSkillSetupRepository SoftSkillSetup { get; }
    ISoftSkillBatchRepository SoftSkillBatches { get; }
    ISoftSkillUniversityGroupRepository SoftSkillUniversityGroups { get; }
    ISoftSkillApplicationRepository SoftSkillApplications { get; }
    ISoftSkillTrainerRepository SoftSkillTrainers { get; }
    ISoftSkillTraineeRepository SoftSkillTrainees { get; }
    ISoftSkillVisitRepository SoftSkillVisits { get; }
    ISoftSkillHonorariumRepository SoftSkillHonoraria { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
