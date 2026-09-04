using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suma.Domain.Accounts;
using Suma.Domain.Categories;
using Suma.Domain.Recurring;

namespace Suma.Infrastructure.Persistence.Configurations;

public sealed class RecurringTransactionConfiguration : IEntityTypeConfiguration<RecurringTransaction>
{
    public void Configure(EntityTypeBuilder<RecurringTransaction> builder)
    {
        builder.ToTable(
            "recurring_transactions",
            table =>
            {
                table.HasCheckConstraint("ck_recurring_transactions_amount_positive", "amount_minor > 0");
                table.HasCheckConstraint("ck_recurring_transactions_interval", "interval_count > 0");
                table.HasCheckConstraint(
                    "ck_recurring_transactions_day_of_month",
                    "day_of_month IS NULL OR day_of_month BETWEEN 1 AND 31");
                table.HasCheckConstraint(
                    "ck_recurring_transactions_month_of_year",
                    "month_of_year IS NULL OR month_of_year BETWEEN 1 AND 12");
                table.HasCheckConstraint(
                    "ck_recurring_transactions_dates",
                    "end_date IS NULL OR start_date <= end_date");
                table.HasCheckConstraint(
                    "ck_recurring_transactions_distinct_accounts",
                    "source_account_id IS NULL OR destination_account_id IS NULL OR source_account_id <> destination_account_id");
            });
        builder.HasKey(recurring => recurring.Id);

        builder.Property(recurring => recurring.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(recurring => recurring.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(recurring => recurring.SourceAccountId)
            .HasColumnName("source_account_id");
        builder.Property(recurring => recurring.DestinationAccountId)
            .HasColumnName("destination_account_id");
        builder.Property(recurring => recurring.CategoryId)
            .HasColumnName("category_id");
        builder.ComplexProperty(recurring => recurring.Amount)
            .ConfigureMoney("amount_minor");
        builder.Property(recurring => recurring.FrequencyUnit)
            .HasColumnName("frequency_unit")
            .HasConversion(
                unit => unit.ToString(),
                value => ParseFrequencyUnit(value))
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(recurring => recurring.IntervalCount)
            .HasColumnName("interval_count");
        builder.Property(recurring => recurring.DayOfWeek)
            .HasColumnName("day_of_week")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(recurring => recurring.DayOfMonth)
            .HasColumnName("day_of_month");
        builder.Property(recurring => recurring.MonthOfYear)
            .HasColumnName("month_of_year");
        builder.Property(recurring => recurring.StartDate)
            .HasColumnName("start_date");
        builder.Property(recurring => recurring.EndDate)
            .HasColumnName("end_date");
        builder.Property(recurring => recurring.Description)
            .HasColumnName("description")
            .HasMaxLength(500);
        builder.Property(recurring => recurring.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);
        builder.Property(recurring => recurring.IsActive)
            .HasColumnName("is_active");

        builder.HasIndex(recurring => recurring.IsActive);
        builder.HasIndex(recurring => recurring.StartDate);
        builder.HasIndex(recurring => recurring.EndDate);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(recurring => recurring.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(recurring => recurring.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(recurring => recurring.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static RecurrenceFrequencyUnit ParseFrequencyUnit(string value) =>
        value.Trim() switch
        {
            "Day" or "Daily" => RecurrenceFrequencyUnit.Day,
            "Week" or "Weekly" => RecurrenceFrequencyUnit.Week,
            "Month" or "Monthly" => RecurrenceFrequencyUnit.Month,
            "Year" or "Yearly" or "Annual" or "Annually" => RecurrenceFrequencyUnit.Year,
            _ => Enum.Parse<RecurrenceFrequencyUnit>(value, true)
        };
}
