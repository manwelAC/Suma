using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suma.Domain.Budgets;
using Suma.Domain.Categories;

namespace Suma.Infrastructure.Persistence.Configurations;

public sealed class BudgetAllocationConfiguration : IEntityTypeConfiguration<BudgetAllocation>
{
    public void Configure(EntityTypeBuilder<BudgetAllocation> builder)
    {
        builder.ToTable(
            "budget_allocations",
            table => table.HasCheckConstraint("ck_budget_allocations_amount_positive", "amount_minor > 0"));
        builder.HasKey(allocation => allocation.Id);

        builder.Property(allocation => allocation.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(allocation => allocation.BudgetId)
            .HasColumnName("budget_id");
        builder.Property(allocation => allocation.CategoryId)
            .HasColumnName("category_id");
        builder.ComplexProperty(allocation => allocation.Amount)
            .ConfigureMoney("amount_minor");
        builder.Ignore(allocation => allocation.CurrencyCode);
        builder.Property(allocation => allocation.ReserveFromAvailable)
            .HasColumnName("reserve_from_available");

        builder.HasIndex(allocation => allocation.BudgetId);
        builder.HasIndex(allocation => allocation.CategoryId);
        builder.HasIndex(allocation => new { allocation.BudgetId, allocation.CategoryId })
            .IsUnique();

        builder.HasOne<Budget>()
            .WithMany()
            .HasForeignKey(allocation => allocation.BudgetId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(allocation => allocation.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
