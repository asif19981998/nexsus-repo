using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities.Documents;

namespace Nexus.Infrastructure.Configuration;

public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> b)
    {
        b.ToTable("DocumentTypes");
        b.Property(t => t.Code).HasMaxLength(DocumentType.MaxCodeLength).IsRequired();
        b.HasIndex(t => t.Code).IsUnique();
        b.Property(t => t.Name).HasMaxLength(DocumentType.MaxNameLength).IsRequired();
        b.Property(t => t.Description).HasMaxLength(DocumentType.MaxDescriptionLength);
        b.Property(t => t.Extensions).HasMaxLength(100).IsRequired();
    }
}

public class UserDocumentConfiguration : IEntityTypeConfiguration<UserDocument>
{
    public void Configure(EntityTypeBuilder<UserDocument> b)
    {
        b.ToTable("UserDocuments");
        b.Property(d => d.FileName).HasMaxLength(UserDocument.MaxFileNameLength).IsRequired();
        b.Property(d => d.StoredName).HasMaxLength(100).IsRequired();
        b.HasIndex(d => d.StoredName).IsUnique();
        b.Property(d => d.ContentType).HasMaxLength(100).IsRequired();
        b.Property(d => d.ReviewNote).HasMaxLength(UserDocument.MaxReviewNoteLength);
        b.Property(d => d.ReviewedByName).HasMaxLength(150);
        b.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(d => d.DocumentType).WithMany().HasForeignKey(d => d.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        // One current version per person and type.
        b.HasIndex(d => new { d.UserId, d.DocumentTypeId }).IsUnique().HasFilter("[IsCurrent] = 1");
    }
}

public class ModuleDocumentRequirementConfiguration : IEntityTypeConfiguration<ModuleDocumentRequirement>
{
    public void Configure(EntityTypeBuilder<ModuleDocumentRequirement> b)
    {
        b.ToTable("ModuleDocumentRequirements");
        b.Property(r => r.Module).HasMaxLength(ModuleDocumentRequirement.MaxModuleLength).IsRequired();
        b.Property(r => r.GroupKey).HasMaxLength(40).IsRequired();
        b.Property(r => r.Note).HasMaxLength(ModuleDocumentRequirement.MaxNoteLength);
        b.HasOne(r => r.DocumentType).WithMany().HasForeignKey(r => r.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        // A type appears once per module.
        b.HasIndex(r => new { r.Module, r.DocumentTypeId }).IsUnique();
    }
}

public class ApplicationDocumentConfiguration : IEntityTypeConfiguration<ApplicationDocument>
{
    public void Configure(EntityTypeBuilder<ApplicationDocument> b)
    {
        b.ToTable("ApplicationDocuments");
        b.Property(a => a.Module).HasMaxLength(ModuleDocumentRequirement.MaxModuleLength).IsRequired();
        // The file must outlive the person's changes to their library while an application refers to it.
        b.HasOne(a => a.UserDocument).WithMany().HasForeignKey(a => a.UserDocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.Module, a.ApplicationId, a.UserDocumentId }).IsUnique();
        b.HasIndex(a => a.UserDocumentId);
    }
}
