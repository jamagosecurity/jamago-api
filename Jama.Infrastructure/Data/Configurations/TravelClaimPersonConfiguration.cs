using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class TravelClaimPersonConfiguration : IEntityTypeConfiguration<TravelClaimPerson>
{
    public void Configure(EntityTypeBuilder<TravelClaimPerson> builder)
    {
        builder.ToTable("TravelClaimPeople");
        builder.Property(x => x.PersonName).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => x.PersonId);
    }
}
