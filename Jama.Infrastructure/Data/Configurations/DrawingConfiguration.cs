using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class DrawingConfiguration : IEntityTypeConfiguration<Drawing>
{
    public void Configure(EntityTypeBuilder<Drawing> builder)
    {
        builder.ToTable("Drawings");

        builder.Property(x => x.DrawingNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ProjectName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ClientName).HasMaxLength(200);
        builder.Property(x => x.SiteLocation).HasMaxLength(200);
        builder.Property(x => x.ContactNumber).HasMaxLength(40);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.PreparedByName).HasMaxLength(200);
        builder.Property(x => x.ApprovedByName).HasMaxLength(200);
        builder.Property(x => x.RejectedByName).HasMaxLength(200);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // The reference may already be circulating, so no two may share one. Also
        // the backstop for two writers allocating a number at the same moment.
        builder.HasIndex(x => x.DrawingNumber).IsUnique();
        builder.HasIndex(x => x.PreparedById);
        // The approval queue and the two admin lists are all "this status,
        // newest first", which is the whole of what this index is for.
        builder.HasIndex(x => new { x.Status, x.CreatedAt });

        // Cascade: the trail and the files are part of the drawing rather than a
        // reference to something else, and orphan rows describe a document
        // nobody can open.
        builder.HasMany(x => x.ApprovalEvents)
            .WithOne(x => x.Drawing)
            .HasForeignKey(x => x.DrawingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Files)
            .WithOne(x => x.Drawing)
            .HasForeignKey(x => x.DrawingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// One uploaded file. Cascade takes these with their drawing; deleting a file on
/// its own goes through the storage layer first — see DeleteDrawingFile — so the
/// disk copy is never left behind an EF-only delete.
/// </summary>
public sealed class DrawingFileConfiguration : IEntityTypeConfiguration<DrawingFile>
{
    public void Configure(EntityTypeBuilder<DrawingFile> builder)
    {
        builder.ToTable("DrawingFiles");

        builder.Property(x => x.FileName).HasMaxLength(400).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(x => x.UploadedByName).HasMaxLength(200);

        builder.HasIndex(x => new { x.DrawingId, x.CreatedAt });
    }
}

/// <summary>
/// The approval trail. Append-only in practice — nothing in the application
/// updates or deletes a row — so there is no concurrency token and no soft
/// delete here, mirroring BoqApprovalEventConfiguration.
/// </summary>
public sealed class DrawingApprovalEventConfiguration : IEntityTypeConfiguration<DrawingApprovalEvent>
{
    public void Configure(EntityTypeBuilder<DrawingApprovalEvent> builder)
    {
        builder.ToTable("DrawingApprovalEvents");

        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ActorName).HasMaxLength(200);
        builder.Property(x => x.Reason).HasMaxLength(1000);

        // Read as "this drawing's trail, oldest first", every time.
        builder.HasIndex(x => new { x.DrawingId, x.CreatedAt });
    }
}
