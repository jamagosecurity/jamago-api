using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public class VipClientConfiguration : IEntityTypeConfiguration<VipClient>
{
    public void Configure(EntityTypeBuilder<VipClient> builder)
    {
        builder.Property(x => x.ClientName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProjectName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FolderName).HasMaxLength(400).IsRequired();

        // One VIP project per login account: the client portal resolves the
        // project from the signed-in user, so a second row would be ambiguous.
        builder.HasIndex(x => x.AdminUserId).IsUnique();

        builder.HasOne(x => x.Account)
            .WithOne()
            .HasForeignKey<VipClient>(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Folders)
            .WithOne(x => x.VipClient)
            .HasForeignKey(x => x.VipClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VipClientFolderConfiguration : IEntityTypeConfiguration<VipClientFolder>
{
    public void Configure(EntityTypeBuilder<VipClientFolder> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);

        // A project holds one folder of each kind.
        builder.HasIndex(x => new { x.VipClientId, x.Kind }).IsUnique();

        builder.HasMany(x => x.Documents)
            .WithOne(x => x.Folder)
            .HasForeignKey(x => x.VipClientFolderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VipClientDocumentConfiguration : IEntityTypeConfiguration<VipClientDocument>
{
    public void Configure(EntityTypeBuilder<VipClientDocument> builder)
    {
        builder.Property(x => x.FileName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(400).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(150).IsRequired();

        builder.HasIndex(x => x.VipClientFolderId);
    }
}
