using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suma.Domain.Accounts;
using Suma.Domain.Savings;

namespace Suma.Infrastructure.Persistence.Configurations;

public sealed class SavingsGoalConfiguration : IEntityTypeConfiguration<SavingsGoal>
{
    public void Configure(EntityTypeBuilder<SavingsGoal> builder)
    {
        builder.ToTable(
            "savings_goals",
            table => table.HasCheckConstraint("ck_savings_goals_target_positive", "target_amount_minor > 0"));
        builder.HasKey(goal => goal.Id);

        builder.Property(goal => goal.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(goal => goal.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.ComplexProperty(goal => goal.TargetAmount)
            .ConfigureMoney("target_amount_minor");
        builder.Ignore(goal => goal.CurrencyCode);
        builder.Property(goal => goal.TargetDate)
            .HasColumnName("target_date");
        builder.Property(goal => goal.DestinationAccountId)
            .HasColumnName("destination_account_id");
        builder.Property(goal => goal.IsArchived)
            .HasColumnName("is_archived");

        builder.HasIndex(goal => goal.IsArchived);
        builder.HasIndex(goal => goal.TargetDate);
        builder.HasIndex(goal => goal.DestinationAccountId);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(goal => goal.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
