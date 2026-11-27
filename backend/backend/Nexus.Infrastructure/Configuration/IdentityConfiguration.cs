using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities.Identity;

namespace Nexus.Infrastructure.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        b.Property(u => u.Designation).HasMaxLength(150);

        b.HasOne(u => u.Department).WithMany().HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(u => u.StudentProfile).WithOne(p => p.User).HasForeignKey<StudentProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(u => u.TrainerProfile).WithOne(p => p.User).HasForeignKey<TrainerProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(u => u.UserRoles).WithOne().HasForeignKey(ur => ur.UserId).IsRequired();
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.Property(r => r.Description).HasMaxLength(300);
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("Permissions");
        b.Property(p => p.Code).HasMaxLength(100).IsRequired();
        b.HasIndex(p => p.Code).IsUnique();
        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.Module).HasMaxLength(100).IsRequired();
        b.Property(p => p.Description).HasMaxLength(500);
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("RolePermissions");
        b.HasKey(rp => new { rp.RoleId, rp.PermissionId });
        b.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions).HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> b)
    {
        b.ToTable("StudentProfiles");
        b.HasIndex(p => p.UserId).IsUnique();
    }
}

public class EducationConfiguration : IEntityTypeConfiguration<Education>
{
    public void Configure(EntityTypeBuilder<Education> b)
    {
        b.ToTable("Educations");
        b.Property(e => e.Institution).HasMaxLength(Education.MaxTextLength).IsRequired();
        b.Property(e => e.Programme).HasMaxLength(Education.MaxTextLength);
        b.Property(e => e.Result).HasMaxLength(Education.MaxResultLength);
        b.HasOne(e => e.User).WithMany(u => u.Educations).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(e => e.University).WithMany().HasForeignKey(e => e.UniversityId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => e.UserId, "IX_Educations_UserId");
        // At most one main line per person.
        b.HasIndex(e => e.UserId, "IX_Educations_UserId_Main").IsUnique().HasFilter("[IsMain] = 1");
    }
}

public class TrainerProfileConfiguration : IEntityTypeConfiguration<TrainerProfile>
{
    public void Configure(EntityTypeBuilder<TrainerProfile> b)
    {
        b.ToTable("TrainerProfiles");
        b.HasIndex(p => p.UserId).IsUnique();
        b.Property(p => p.AcademicDepartment).HasMaxLength(TrainerProfile.MaxTextLength);
        b.Property(p => p.EmployeeId).HasMaxLength(50);
        b.HasOne(p => p.University).WithMany().HasForeignKey(p => p.UniversityId).OnDelete(DeleteBehavior.Restrict);
    }
}
