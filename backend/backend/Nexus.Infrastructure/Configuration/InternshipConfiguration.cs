using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities.Internships;

namespace Nexus.Infrastructure.Configuration;

public class InternshipBatchConfiguration : IEntityTypeConfiguration<InternshipBatch>
{
    public void Configure(EntityTypeBuilder<InternshipBatch> b)
    {
        b.ToTable("InternshipBatches");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.OverrideReason).HasMaxLength(1000);
        b.Property(x => x.OverrideLimits).HasMaxLength(1000);
        b.HasIndex(x => new { x.Year, x.Status });
    }
}

public class InternshipApplicationConfiguration : IEntityTypeConfiguration<InternshipApplication>
{
    public void Configure(EntityTypeBuilder<InternshipApplication> b)
    {
        b.ToTable("InternshipApplications");
        b.Property(x => x.Statement).HasMaxLength(4000).IsRequired();
        b.Property(x => x.Basis).HasMaxLength(50);
        b.Property(x => x.Documents).HasMaxLength(200);
        b.Property(x => x.RejectionReason).HasMaxLength(1000);

        b.HasOne(x => x.Batch).WithMany(x => x.Applications).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Internship).WithOne(x => x.Application).HasForeignKey<Internship>(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.StudentId);
        b.HasIndex(x => new { x.BatchId, x.Status });
    }
}

public class ApplicationPreferredDepartmentConfiguration : IEntityTypeConfiguration<ApplicationPreferredDepartment>
{
    public void Configure(EntityTypeBuilder<ApplicationPreferredDepartment> b)
    {
        b.ToTable("ApplicationPreferredDepartments");
        b.HasOne(x => x.Application).WithMany(x => x.PreferredDepartments).HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ApplicationId, x.DepartmentId }).IsUnique();
    }
}

public class ApplicationStatusHistoryConfiguration : IEntityTypeConfiguration<ApplicationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusHistory> b)
    {
        b.ToTable("ApplicationStatusHistory");
        b.Property(x => x.ByName).HasMaxLength(150);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.HasOne(x => x.Application).WithMany(x => x.History).HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class InternshipEntityConfiguration : IEntityTypeConfiguration<Internship>
{
    public void Configure(EntityTypeBuilder<Internship> b)
    {
        b.ToTable("Internships");
        b.HasIndex(x => x.ApplicationId).IsUnique();
        b.Property(x => x.Tasks).HasMaxLength(2000).IsRequired();
        b.Property(x => x.OutcomeResult).HasMaxLength(50);
        b.Property(x => x.OutcomeReasons).HasMaxLength(2000);
        b.Property(x => x.CertificateNumber).HasMaxLength(50);
        b.Property(x => x.CertificateGrade).HasMaxLength(50);
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.MentorId);
    }
}

public class InternshipPlacementConfiguration : IEntityTypeConfiguration<InternshipPlacement>
{
    public void Configure(EntityTypeBuilder<InternshipPlacement> b)
    {
        b.ToTable("InternshipPlacements");
        b.Property(x => x.Note).HasMaxLength(1000);
        b.HasOne(x => x.Internship).WithMany(x => x.Placements).HasForeignKey(x => x.InternshipId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> b)
    {
        b.ToTable("AttendanceRecords");
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.Note).HasMaxLength(200);
        b.Property(x => x.DeviceId).HasMaxLength(100);
        b.Property(x => x.AppVersion).HasMaxLength(30);
        b.Property(x => x.CheckInKey).HasMaxLength(64);
        b.Property(x => x.CheckOutKey).HasMaxLength(64);
        b.Property(x => x.CheckInPhotoPath).HasMaxLength(300);
        b.Property(x => x.CheckOutPhotoPath).HasMaxLength(300);
        b.HasOne(x => x.Internship).WithMany(x => x.Attendance).HasForeignKey(x => x.InternshipId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.InternshipId, x.Date }).IsUnique();
    }
}

public class AllowanceBillConfiguration : IEntityTypeConfiguration<AllowanceBill>
{
    public void Configure(EntityTypeBuilder<AllowanceBill> b)
    {
        b.ToTable("AllowanceBills");
        b.Property(x => x.Month).HasMaxLength(7).IsRequired();
        b.Property(x => x.Report).HasMaxLength(AllowanceBill.MaxReportLength);
        b.Property(x => x.EvaluationNote).HasMaxLength(AllowanceBill.MaxEvaluationNoteLength);
        b.Property(x => x.EvaluatedByName).HasMaxLength(150);
        b.Property(x => x.PaidByName).HasMaxLength(150);
        b.Property(x => x.SlipNumber).HasMaxLength(30);
        b.HasIndex(x => x.SlipNumber).IsUnique().HasFilter("[SlipNumber] IS NOT NULL");
        b.HasOne(x => x.Internship).WithMany(x => x.Bills).HasForeignKey(x => x.InternshipId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.InternshipId, x.Month }).IsUnique();
    }
}

public class InternshipReportConfiguration : IEntityTypeConfiguration<InternshipReport>
{
    public void Configure(EntityTypeBuilder<InternshipReport> b)
    {
        b.ToTable("InternshipReports");
        b.Property(x => x.Text).IsRequired();
        b.Property(x => x.ReviewNote).HasMaxLength(InternshipReport.MaxReviewNoteLength);
        b.HasOne(x => x.Internship).WithOne(x => x.Report).HasForeignKey<InternshipReport>(x => x.InternshipId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.InternshipId).IsUnique();
    }
}

public class InternshipEvaluationConfiguration : IEntityTypeConfiguration<InternshipEvaluation>
{
    public void Configure(EntityTypeBuilder<InternshipEvaluation> b)
    {
        b.ToTable("InternshipEvaluations");
        b.Property(x => x.Recommendation).HasMaxLength(InternshipEvaluation.MaxRecommendationLength).IsRequired();
        b.HasOne(x => x.Internship).WithOne(x => x.Evaluation).HasForeignKey<InternshipEvaluation>(x => x.InternshipId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Evaluator).WithMany().HasForeignKey(x => x.EvaluatorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.InternshipId).IsUnique();
    }
}

public class EvaluationScoreConfiguration : IEntityTypeConfiguration<EvaluationScore>
{
    public void Configure(EntityTypeBuilder<EvaluationScore> b)
    {
        b.ToTable("EvaluationScores");
        b.HasOne(x => x.Evaluation).WithMany(x => x.Scores).HasForeignKey(x => x.EvaluationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Criterion).WithMany().HasForeignKey(x => x.CriterionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EvaluationId, x.CriterionId }).IsUnique();
    }
}
