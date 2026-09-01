using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suma.Domain.Budgets;

namespace Suma.Infrastructure.Persistence.Configurations;

public sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable(
            "budgets",
            table =>
            {
                table.HasCheckConstraint("ck_budgets_period", "period_start <= period_end");
                table.HasCheckConstraint("ck_budgets_expected_income", "expected_income_minor >= 0");
            });
        builder.HasKey(budget => budget.Id);

        builder.Property(budget => budget.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(budget => budget.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(budget => budget.PeriodStart)
            .HasColumnName("period_start");
        builder.Property(budget => budget.PeriodEnd)
            .HasColumnName("period_end");
        builder.ComplexProperty(budget => budget.ExpectedIncome)
            .ConfigureMoney("expected_income_minor");
        builder.Ignore(budget => budget.CurrencyCode);
        builder.Property(budget => budget.IsArchived)
            .HasColumnName("is_archived");

        builder.HasIndex(budget => budget.PeriodStart);
        builder.HasIndex(budget => budget.PeriodEnd);
    }
}
