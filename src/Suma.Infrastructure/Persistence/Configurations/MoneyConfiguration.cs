using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suma.Domain.ValueObjects;

namespace Suma.Infrastructure.Persistence.Configurations;

internal static class MoneyConfiguration
{
    public static void ConfigureMoney(
        this ComplexPropertyBuilder<Money> builder,
        string amountColumnName)
    {
        builder.Property(money => money.AmountMinor)
            .HasColumnName(amountColumnName)
            .HasColumnType("INTEGER");

        builder.Property(money => money.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .IsRequired();
    }
}
