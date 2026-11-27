using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nexus.Domain.Entities.Common;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Internships;
using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Entities.SoftSkills;

namespace Nexus.Infrastructure;

public class NexusDbContext(DbContextOptions<NexusDbContext> options) : IdentityDbContext<User, Role, long>(options)
{
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<Education> Educations => Set<Education>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<UserDocument> UserDocuments => Set<UserDocument>();
    public DbSet<ModuleDocumentRequirement> ModuleDocumentRequirements => Set<ModuleDocumentRequirement>();
    public DbSet<ApplicationDocument> ApplicationDocuments => Set<ApplicationDocument>();

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<EvaluationCriterion> EvaluationCriteria => Set<EvaluationCriterion>();
    public DbSet<GradeBand> GradeBands => Set<GradeBand>();
    public DbSet<InternshipPolicy> InternshipPolicies => Set<InternshipPolicy>();

    public DbSet<InternshipBatch> InternshipBatches => Set<InternshipBatch>();
    public DbSet<InternshipApplication> InternshipApplications => Set<InternshipApplication>();
    public DbSet<ApplicationPreferredDepartment> ApplicationPreferredDepartments => Set<ApplicationPreferredDepartment>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistory => Set<ApplicationStatusHistory>();
    public DbSet<Internship> Internships => Set<Internship>();
    public DbSet<InternshipPlacement> InternshipPlacements => Set<InternshipPlacement>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AllowanceBill> AllowanceBills => Set<AllowanceBill>();
    public DbSet<InternshipReport> InternshipReports => Set<InternshipReport>();
    public DbSet<InternshipEvaluation> InternshipEvaluations => Set<InternshipEvaluation>();
    public DbSet<EvaluationScore> EvaluationScores => Set<EvaluationScore>();

    public DbSet<University> Universities => Set<University>();
    public DbSet<TrainerProfile> TrainerProfiles => Set<TrainerProfile>();

    public DbSet<SoftSkillPolicy> SoftSkillPolicies => Set<SoftSkillPolicy>();
    public DbSet<SoftSkillTopic> SoftSkillTopics => Set<SoftSkillTopic>();
    public DbSet<SoftSkillGradeBand> SoftSkillGradeBands => Set<SoftSkillGradeBand>();
    public DbSet<SoftSkillBatch> SoftSkillBatches => Set<SoftSkillBatch>();
    public DbSet<SoftSkillUniversityGroup> SoftSkillUniversityGroups => Set<SoftSkillUniversityGroup>();
    public DbSet<SoftSkillTrainer> SoftSkillTrainers => Set<SoftSkillTrainer>();
    public DbSet<SoftSkillApplication> SoftSkillApplications => Set<SoftSkillApplication>();
    public DbSet<SoftSkillApplicationHistory> SoftSkillApplicationHistory => Set<SoftSkillApplicationHistory>();
    public DbSet<SoftSkillTrainee> SoftSkillTrainees => Set<SoftSkillTrainee>();
    public DbSet<SoftSkillSession> SoftSkillSessions => Set<SoftSkillSession>();
    public DbSet<SoftSkillAttendance> SoftSkillAttendance => Set<SoftSkillAttendance>();
    public DbSet<SoftSkillAssessment> SoftSkillAssessments => Set<SoftSkillAssessment>();
    public DbSet<SoftSkillAssessmentResult> SoftSkillAssessmentResults => Set<SoftSkillAssessmentResult>();
    public DbSet<SoftSkillStipend> SoftSkillStipends => Set<SoftSkillStipend>();
    public DbSet<SoftSkillLearningReport> SoftSkillLearningReports => Set<SoftSkillLearningReport>();
    public DbSet<SoftSkillMonitoringVisit> SoftSkillMonitoringVisits => Set<SoftSkillMonitoringVisit>();
    public DbSet<SoftSkillHonorarium> SoftSkillHonoraria => Set<SoftSkillHonorarium>();

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Shorter names for the Identity tables.
        builder.Entity<User>().ToTable("Users");
        builder.Entity<Role>().ToTable("Roles");
        builder.Entity<IdentityUserRole<long>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<long>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<long>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<long>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<long>>().ToTable("RoleClaims");

        builder.ApplyConfigurationsFromAssembly(typeof(NexusDbContext).Assembly);

        // Every DateTime is stored as UTC. SQL Server drops the kind, so restore it on read;
        // otherwise the API would serialize the value without "Z" and browsers would read it as local time.
        var utc = new ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNullable = new ValueConverter<DateTime?, DateTime?>(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(DateTime)) property.SetValueConverter(utc);
                else if (property.ClrType == typeof(DateTime?)) property.SetValueConverter(utcNullable);
            }
        }
    }
}
