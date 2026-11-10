using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class MailMessageConfiguration : IEntityTypeConfiguration<MailMessage>
{
    public void Configure(EntityTypeBuilder<MailMessage> builder)
    {
        builder.ToTable("MailMessages");
        builder.Property(x => x.AccountKey).HasMaxLength(40).IsRequired();
        builder.Property(x => x.AccountAddress).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Folder).HasMaxLength(20).IsRequired();
        builder.Property(x => x.MessageId).HasMaxLength(500);
        builder.Property(x => x.FromName).HasMaxLength(200);
        builder.Property(x => x.FromAddress).HasMaxLength(320);
        builder.Property(x => x.ToAddresses).HasMaxLength(1000);
        builder.Property(x => x.Subject).HasMaxLength(500);
        builder.Property(x => x.Snippet).HasMaxLength(240);
        builder.Property(x => x.AttachmentNames).HasMaxLength(1000);

        // One row per message per mailbox: re-reading never duplicates.
        builder.HasIndex(x => new { x.AccountKey, x.Folder, x.UidValidity, x.Uid }).IsUnique();
        builder.HasIndex(x => x.SentAt);
        builder.HasIndex(x => new { x.AccountKey, x.SentAt });
    }
}

public sealed class MailAccountStateConfiguration : IEntityTypeConfiguration<MailAccountState>
{
    public void Configure(EntityTypeBuilder<MailAccountState> builder)
    {
        builder.ToTable("MailAccountStates");
        builder.Property(x => x.AccountKey).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Folder).HasMaxLength(20).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(400);
        builder.HasIndex(x => new { x.AccountKey, x.Folder }).IsUnique();
    }
}

public sealed class MailSendLogConfiguration : IEntityTypeConfiguration<MailSendLog>
{
    public void Configure(EntityTypeBuilder<MailSendLog> builder)
    {
        builder.ToTable("MailSendLogs");
        builder.Property(x => x.AccountKey).HasMaxLength(40).IsRequired();
        builder.Property(x => x.FromAddress).HasMaxLength(320).IsRequired();
        builder.Property(x => x.ToAddresses).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(500);
        builder.Property(x => x.SentByName).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => x.CreatedAt);
    }
}
