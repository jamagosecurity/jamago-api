using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class ProformaInvoiceConfiguration : IEntityTypeConfiguration<ProformaInvoice>
{
    public void Configure(EntityTypeBuilder<ProformaInvoice> builder)
    {
        builder.ToTable("ProformaInvoices");
        builder.Property(x => x.Number).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => x.BoqId).IsUnique();                      // one proforma per quotation
        builder.Property(x => x.IssuedByName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.PoNumber).HasMaxLength(60);
        builder.Property(x => x.CustomerName).HasMaxLength(200);   // same limits as the quotation's own fields
        builder.Property(x => x.ProjectName).HasMaxLength(200);
        builder.Property(x => x.ContactNumber).HasMaxLength(40);
        builder.Property(x => x.Location).HasMaxLength(200);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasOne(x => x.Boq).WithMany().HasForeignKey(x => x.BoqId).OnDelete(DeleteBehavior.Restrict);
        // A deleted quotation is hidden everywhere; so is its proforma.
        builder.HasQueryFilter(x => !x.Boq.IsDeleted);
    }
}
