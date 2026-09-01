using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suma.Domain.Accounts;

namespace Suma.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(account => account.Id);

        builder.Property(account => account.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(account => account.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(account => account.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.ComplexProperty(account => account.OpeningBalance)
            .ConfigureMoney("opening_balance_minor");
        builder.Ignore(account => account.CurrencyCode);
        builder.Property(account => account.IncludeInAvailableToSpend)
            .HasColumnName("include_in_available_to_spend");
        builder.Property(account => account.IsArchived)
            .HasColumnName("is_archived");
    }
}
