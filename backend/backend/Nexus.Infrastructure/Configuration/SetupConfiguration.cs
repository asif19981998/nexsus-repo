using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities.Common;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Setup;

namespace Nexus.Infrastructure.Configuration;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments");
        b.Property(d => d.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(d => d.Name).IsUnique();
    }
}

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> b)
    {
        b.ToTable("Holidays");
        b.Property(h => h.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(h => h.Date).IsUnique();
    }
}

public class EvaluationCriterionConfiguration : IEntityTypeConfiguration<EvaluationCriterion>
{
    public void Configure(EntityTypeBuilder<EvaluationCriterion> b)
    {
        b.ToTable("EvaluationCriteria");
        b.Property(c => c.Key).HasMaxLength(50).IsRequired();
        b.HasIndex(c => c.Key).IsUnique();
        b.Property(c => c.Label).HasMaxLength(150).IsRequired();
    }
}

public class GradeBandConfiguration : IEntityTypeConfiguration<GradeBand>
{
    public void Configure(EntityTypeBuilder<GradeBand> b)
    {
        b.ToTable("GradeBands");
        b.Property(g => g.Grade).HasMaxLength(50).IsRequired();
        b.Property(g => g.NameBn).HasMaxLength(50);
    }
}

public class InternshipPolicyConfiguration : IEntityTypeConfiguration<InternshipPolicy>
{
    public void Configure(EntityTypeBuilder<InternshipPolicy> b)
    {
        b.ToTable("InternshipPolicy");
        b.Property(p => p.MonthlyAllowance).HasPrecision(18, 2);
        b.Property(p => p.ShiftName).HasMaxLength(60);
        b.Property(p => p.WeekendDays).HasMaxLength(80).IsRequired().HasDefaultValue("Friday,Saturday");
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.Property(n => n.Title).HasMaxLength(200).IsRequired();
        b.Property(n => n.Body).HasMaxLength(1000);
        b.Property(n => n.Link).HasMaxLength(300);
        b.HasIndex(n => new { n.UserId, n.IsRead });
        b.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.Property(a => a.Action).HasMaxLength(100).IsRequired();
        b.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        b.Property(a => a.EntityId).HasMaxLength(50);
        b.Property(a => a.ActorName).HasMaxLength(150);
        b.Property(a => a.Details).HasMaxLength(AuditLog.MaxDetailsLength);
        b.HasIndex(a => a.At);
    }
}

public class UniversityConfiguration : IEntityTypeConfiguration<University>
{
    public void Configure(EntityTypeBuilder<University> b)
    {
        b.ToTable("Universities");
        b.Property(u => u.Name).HasMaxLength(University.MaxNameLength).IsRequired();
        b.HasIndex(u => u.Name).IsUnique();
        b.Property(u => u.NameBn).HasMaxLength(University.MaxNameLength);
        b.Property(u => u.ShortName).HasMaxLength(University.MaxShortNameLength);
    }
}
