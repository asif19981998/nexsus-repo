using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities.SoftSkills;

namespace Nexus.Infrastructure.Configuration;

// Delete rules: a batch owns its university groups, a university group owns its sessions and assessments, an application owns its trainee,
// and a trainee owns its stipends and report. Every other link is Restrict, so SQL Server never sees two cascade paths.

public class SoftSkillPolicyConfiguration : IEntityTypeConfiguration<SoftSkillPolicy>
{
    public void Configure(EntityTypeBuilder<SoftSkillPolicy> b)
    {
        b.ToTable("SoftSkillPolicy");
        b.Property(p => p.MonthlyStipend).HasPrecision(18, 2);
        b.Property(p => p.TrainerRatePerSession).HasPrecision(18, 2);
        b.Property(p => p.CoordinatorRatePerUniversityGroup).HasPrecision(18, 2);
        b.Property(p => p.ExpertRatePerVisit).HasPrecision(18, 2);
    }
}

public class SoftSkillTopicConfiguration : IEntityTypeConfiguration<SoftSkillTopic>
{
    public void Configure(EntityTypeBuilder<SoftSkillTopic> b)
    {
        b.ToTable("SoftSkillTopics");
        b.Property(t => t.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(t => t.Code).IsUnique();
        b.Property(t => t.Name).HasMaxLength(SoftSkillTopic.MaxNameLength).IsRequired();
        b.Property(t => t.NameBn).HasMaxLength(SoftSkillTopic.MaxNameLength);
    }
}

public class SoftSkillGradeBandConfiguration : IEntityTypeConfiguration<SoftSkillGradeBand>
{
    public void Configure(EntityTypeBuilder<SoftSkillGradeBand> b)
    {
        b.ToTable("SoftSkillGradeBands");
        b.Property(g => g.Grade).HasMaxLength(50).IsRequired();
        b.Property(g => g.NameBn).HasMaxLength(50);
    }
}

public class SoftSkillBatchConfiguration : IEntityTypeConfiguration<SoftSkillBatch>
{
    public void Configure(EntityTypeBuilder<SoftSkillBatch> b)
    {
        b.ToTable("SoftSkillBatches");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.OverrideReason).HasMaxLength(1000);
        b.HasIndex(x => new { x.Year, x.Status });
    }
}

public class SoftSkillUniversityGroupConfiguration : IEntityTypeConfiguration<SoftSkillUniversityGroup>
{
    public void Configure(EntityTypeBuilder<SoftSkillUniversityGroup> b)
    {
        b.ToTable("SoftSkillUniversityGroups");
        b.Property(x => x.OverrideReason).HasMaxLength(1000);
        b.Property(x => x.Venue).HasMaxLength(SoftSkillUniversityGroup.MaxVenueLength);
        b.Property(x => x.FinalReport).HasMaxLength(SoftSkillUniversityGroup.MaxFinalReportLength);
        b.Property(x => x.FinalRecommendation).HasMaxLength(SoftSkillUniversityGroup.MaxFinalReportLength);
        b.HasOne(x => x.Batch).WithMany(x => x.UniversityGroups).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.University).WithMany().HasForeignKey(x => x.UniversityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Coordinator).WithMany().HasForeignKey(x => x.CoordinatorId).OnDelete(DeleteBehavior.Restrict);
        // cl.6 (ক): one university group per university in a batch.
        b.HasIndex(x => new { x.BatchId, x.UniversityId }).IsUnique();
        b.HasIndex(x => x.CoordinatorId);
    }
}

public class SoftSkillTrainerConfiguration : IEntityTypeConfiguration<SoftSkillTrainer>
{
    public void Configure(EntityTypeBuilder<SoftSkillTrainer> b)
    {
        b.ToTable("SoftSkillTrainers");
        b.Property(x => x.Statement).HasMaxLength(SoftSkillTrainer.MaxStatementLength).IsRequired();
        b.Property(x => x.DecisionNote).HasMaxLength(SoftSkillTrainer.MaxNoteLength);
        b.Property(x => x.TotReference).HasMaxLength(200);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.University).WithMany().HasForeignKey(x => x.UniversityId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasIndex(x => new { x.UniversityId, x.Status });
    }
}

public class SoftSkillApplicationConfiguration : IEntityTypeConfiguration<SoftSkillApplication>
{
    public void Configure(EntityTypeBuilder<SoftSkillApplication> b)
    {
        b.ToTable("SoftSkillApplications");
        b.Property(x => x.Statement).HasMaxLength(SoftSkillApplication.MaxStatementLength).IsRequired();
        b.Property(x => x.Basis).HasMaxLength(300);
        b.Property(x => x.Documents).HasMaxLength(200);
        b.Property(x => x.RejectionReason).HasMaxLength(1000);
        b.HasOne(x => x.Batch).WithMany(x => x.Applications).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UniversityGroup).WithMany().HasForeignKey(x => x.UniversityGroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Trainee).WithOne(x => x.Application).HasForeignKey<SoftSkillTrainee>(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.StudentId);
        b.HasIndex(x => new { x.BatchId, x.Status });
        b.HasIndex(x => new { x.UniversityGroupId, x.Status });
    }
}

public class SoftSkillApplicationHistoryConfiguration : IEntityTypeConfiguration<SoftSkillApplicationHistory>
{
    public void Configure(EntityTypeBuilder<SoftSkillApplicationHistory> b)
    {
        b.ToTable("SoftSkillApplicationHistory");
        b.Property(x => x.ByName).HasMaxLength(150);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.HasOne(x => x.Application).WithMany(x => x.History).HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SoftSkillTraineeConfiguration : IEntityTypeConfiguration<SoftSkillTrainee>
{
    public void Configure(EntityTypeBuilder<SoftSkillTrainee> b)
    {
        b.ToTable("SoftSkillTrainees");
        b.HasIndex(x => x.ApplicationId).IsUnique();
        b.Property(x => x.CancelReason).HasMaxLength(1000);
        b.Property(x => x.AverageScore).HasPrecision(5, 2);
        b.Property(x => x.CertificateNumber).HasMaxLength(50);
        b.HasIndex(x => x.CertificateNumber).IsUnique().HasFilter("[CertificateNumber] IS NOT NULL");
        b.Property(x => x.CertificateGrade).HasMaxLength(50);
        b.Property(x => x.OutcomeReasons).HasMaxLength(2000);
        b.HasOne(x => x.UniversityGroup).WithMany(x => x.Trainees).HasForeignKey(x => x.UniversityGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SoftSkillSessionConfiguration : IEntityTypeConfiguration<SoftSkillSession>
{
    public void Configure(EntityTypeBuilder<SoftSkillSession> b)
    {
        b.ToTable("SoftSkillSessions");
        b.Ignore(x => x.Hours);
        b.Property(x => x.Note).HasMaxLength(SoftSkillSession.MaxNoteLength);
        b.HasOne(x => x.UniversityGroup).WithMany(x => x.Sessions).HasForeignKey(x => x.UniversityGroupId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Topic).WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UniversityGroupId, x.Date });
        b.HasIndex(x => new { x.TrainerId, x.Date });
    }
}

public class SoftSkillAttendanceConfiguration : IEntityTypeConfiguration<SoftSkillAttendance>
{
    public void Configure(EntityTypeBuilder<SoftSkillAttendance> b)
    {
        b.ToTable("SoftSkillAttendance");
        b.Property(x => x.Reason).HasMaxLength(SoftSkillAttendance.MaxReasonLength);
        b.Property(x => x.Note).HasMaxLength(200);
        b.HasOne(x => x.Session).WithMany(x => x.Attendance).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Trainee).WithMany(x => x.Attendance).HasForeignKey(x => x.TraineeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SessionId, x.TraineeId }).IsUnique();
    }
}

public class SoftSkillAssessmentConfiguration : IEntityTypeConfiguration<SoftSkillAssessment>
{
    public void Configure(EntityTypeBuilder<SoftSkillAssessment> b)
    {
        b.ToTable("SoftSkillAssessments");
        b.HasOne(x => x.UniversityGroup).WithMany(x => x.Assessments).HasForeignKey(x => x.UniversityGroupId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UniversityGroupId, x.No }).IsUnique();
    }
}

public class SoftSkillAssessmentResultConfiguration : IEntityTypeConfiguration<SoftSkillAssessmentResult>
{
    public void Configure(EntityTypeBuilder<SoftSkillAssessmentResult> b)
    {
        b.ToTable("SoftSkillAssessmentResults");
        b.Property(x => x.Marks).HasPrecision(6, 2);
        b.Property(x => x.Note).HasMaxLength(SoftSkillAssessmentResult.MaxNoteLength);
        b.HasOne(x => x.Assessment).WithMany(x => x.Results).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Trainee).WithMany(x => x.Results).HasForeignKey(x => x.TraineeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.AssessmentId, x.TraineeId }).IsUnique();
    }
}

public class SoftSkillStipendConfiguration : IEntityTypeConfiguration<SoftSkillStipend>
{
    public void Configure(EntityTypeBuilder<SoftSkillStipend> b)
    {
        b.ToTable("SoftSkillStipends");
        b.Property(x => x.PaidByName).HasMaxLength(150);
        b.Property(x => x.SlipNumber).HasMaxLength(50);
        b.Property(x => x.WithheldReason).HasMaxLength(SoftSkillStipend.MaxReasonLength);
        b.HasOne(x => x.Trainee).WithMany(x => x.Stipends).HasForeignKey(x => x.TraineeId).OnDelete(DeleteBehavior.Cascade);
        // cl.10 (ঘ): one stipend per month, however many tests that month had.
        b.HasIndex(x => new { x.TraineeId, x.AssessmentNo }).IsUnique();
    }
}

public class SoftSkillLearningReportConfiguration : IEntityTypeConfiguration<SoftSkillLearningReport>
{
    public void Configure(EntityTypeBuilder<SoftSkillLearningReport> b)
    {
        b.ToTable("SoftSkillLearningReports");
        b.Property(x => x.Title).HasMaxLength(SoftSkillLearningReport.MaxTitleLength).IsRequired();
        b.Property(x => x.Text).HasMaxLength(SoftSkillLearningReport.MaxTextLength).IsRequired();
        b.Property(x => x.ReviewNote).HasMaxLength(SoftSkillLearningReport.MaxReviewNoteLength);
        b.HasOne(x => x.Trainee).WithOne(x => x.Report).HasForeignKey<SoftSkillLearningReport>(x => x.TraineeId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.TraineeId).IsUnique();
    }
}

public class SoftSkillMonitoringVisitConfiguration : IEntityTypeConfiguration<SoftSkillMonitoringVisit>
{
    public void Configure(EntityTypeBuilder<SoftSkillMonitoringVisit> b)
    {
        b.ToTable("SoftSkillMonitoringVisits");
        b.Ignore(x => x.AverageRating);
        b.Property(x => x.Purpose).HasMaxLength(SoftSkillMonitoringVisit.MaxPurposeLength);
        b.Property(x => x.Findings).HasMaxLength(SoftSkillMonitoringVisit.MaxTextLength);
        b.Property(x => x.Recommendations).HasMaxLength(SoftSkillMonitoringVisit.MaxTextLength);
        b.HasOne(x => x.UniversityGroup).WithMany().HasForeignKey(x => x.UniversityGroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Monitor).WithMany().HasForeignKey(x => x.MonitorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UniversityGroupId, x.Status });
        b.HasIndex(x => new { x.MonitorId, x.Status });
    }
}

public class SoftSkillHonorariumConfiguration : IEntityTypeConfiguration<SoftSkillHonorarium>
{
    public void Configure(EntityTypeBuilder<SoftSkillHonorarium> b)
    {
        b.ToTable("SoftSkillHonoraria");
        b.Property(x => x.PaidByName).HasMaxLength(150);
        b.Property(x => x.SlipNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.Note).HasMaxLength(SoftSkillHonorarium.MaxNoteLength);
        b.HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UniversityGroup).WithMany().HasForeignKey(x => x.UniversityGroupId).OnDelete(DeleteBehavior.Restrict);
        // cl.17: one payment per session, university group or visit.
        b.HasIndex(x => new { x.Kind, x.SourceId }).IsUnique();
        b.HasIndex(x => x.PersonId);
    }
}
