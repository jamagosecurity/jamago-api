using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> builder)
    {
        builder.ToTable("Quotations");

        builder.Property(x => x.QuoteNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CustomerCompany).HasMaxLength(200);
        builder.Property(x => x.CustomerEmail).HasMaxLength(256);
        builder.Property(x => x.CustomerPhone).HasMaxLength(40);
        builder.Property(x => x.CustomerAddress).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.Terms).HasMaxLength(2000);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountTotal).HasPrecision(18, 2);
        builder.Property(x => x.TaxTotal).HasPrecision(18, 2);
        builder.Property(x => x.GrandTotal).HasPrecision(18, 2);

        // The reference may already be on a document in a customer's inbox, so no
        // two quotations may ever carry the same one. Also the backstop for two
        // writers allocating a number at the same moment.
        builder.HasIndex(x => x.QuoteNumber).IsUnique();

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.Quotation)
            .HasForeignKey(x => x.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuotationLineConfiguration : IEntityTypeConfiguration<QuotationLine>
{
    public void Configure(EntityTypeBuilder<QuotationLine> builder)
    {
        builder.ToTable("QuotationLines");

        builder.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ModelNo).HasMaxLength(120);
        builder.Property(x => x.Brand).HasMaxLength(120);
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.Property(x => x.Quantity).HasPrecision(12, 2);
        builder.Property(x => x.UnitRate).HasPrecision(18, 2);
        builder.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        builder.Property(x => x.TaxPercent).HasPrecision(5, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);

        builder.HasIndex(x => new { x.QuotationId, x.SortOrder });

        // SetNull, not Cascade: retiring a stock item must never delete lines off
        // quotations already sent. The line keeps its copied name and price and
        // simply stops pointing at a catalogue entry.
        builder.HasOne(x => x.Camera)
            .WithMany()
            .HasForeignKey(x => x.CameraId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
