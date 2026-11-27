using Nexus.Application.Data;
using Nexus.Domain.Entities.Common;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Setup;

namespace Nexus.Infrastructure;

public class UnitOfWork(
    NexusDbContext ctx,
    IInternshipBatchRepository batches,
    IInternshipApplicationRepository applications,
    ISetupRepository setup,
    IUserRepository users,
    IPermissionRepository permissions,
    INotificationRepository notifications,
    IRepository<AuditLog> auditLogs,
    IRepository<StudentProfile> studentProfiles,
    IRepository<Education> educations,
    IRepository<Holiday> holidays,
    IRepository<Department> departments,
    IRepository<DocumentType> documentTypes,
    IDocumentRepository documents,
    IUniversityRepository universities,
    IRepository<TrainerProfile> trainerProfiles,
    ISoftSkillSetupRepository softSkillSetup,
    ISoftSkillBatchRepository softSkillBatches,
    ISoftSkillUniversityGroupRepository softSkillUniversityGroups,
    ISoftSkillApplicationRepository softSkillApplications,
    ISoftSkillTrainerRepository softSkillTrainers,
    ISoftSkillTraineeRepository softSkillTrainees,
    ISoftSkillVisitRepository softSkillVisits,
    ISoftSkillHonorariumRepository softSkillHonoraria) : IUnitOfWork
{
    public IInternshipBatchRepository Batches => batches;
    public IInternshipApplicationRepository Applications => applications;
    public ISetupRepository Setup => setup;
    public IUserRepository Users => users;
    public IPermissionRepository Permissions => permissions;
    public INotificationRepository Notifications => notifications;
    public IRepository<AuditLog> AuditLogs => auditLogs;
    public IRepository<StudentProfile> StudentProfiles => studentProfiles;
    public IRepository<Education> Educations => educations;
    public IRepository<Holiday> Holidays => holidays;
    public IRepository<Department> Departments => departments;
    public IRepository<DocumentType> DocumentTypes => documentTypes;
    public IDocumentRepository Documents => documents;

    public IUniversityRepository Universities => universities;
    public IRepository<TrainerProfile> TrainerProfiles => trainerProfiles;

    public ISoftSkillSetupRepository SoftSkillSetup => softSkillSetup;
    public ISoftSkillBatchRepository SoftSkillBatches => softSkillBatches;
    public ISoftSkillUniversityGroupRepository SoftSkillUniversityGroups => softSkillUniversityGroups;
    public ISoftSkillApplicationRepository SoftSkillApplications => softSkillApplications;
    public ISoftSkillTrainerRepository SoftSkillTrainers => softSkillTrainers;
    public ISoftSkillTraineeRepository SoftSkillTrainees => softSkillTrainees;
    public ISoftSkillVisitRepository SoftSkillVisits => softSkillVisits;
    public ISoftSkillHonorariumRepository SoftSkillHonoraria => softSkillHonoraria;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => ctx.SaveChangesAsync(ct);
}
