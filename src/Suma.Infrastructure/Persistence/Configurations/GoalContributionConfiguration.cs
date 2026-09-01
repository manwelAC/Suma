using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suma.Domain.Savings;
using Suma.Domain.Transactions;

namespace Suma.Infrastructure.Persistence.Configurations;

public sealed class GoalContributionConfiguration : IEntityTypeConfiguration<GoalContribution>
{
    public void Configure(EntityTypeBuilder<GoalContribution> builder)
    {
        builder.ToTable(
            "goal_contributions",
            table => table.HasCheckConstraint("ck_goal_contributions_amount_positive", "amount_minor > 0"));
        builder.HasKey(contribution => contribution.Id);

        builder.Property(contribution => contribution.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(contribution => contribution.SavingsGoalId)
            .HasColumnName("savings_goal_id");
        builder.Property(contribution => contribution.TransactionId)
            .HasColumnName("transaction_id");
        builder.Property(contribution => contribution.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.ComplexProperty(contribution => contribution.Amount)
            .ConfigureMoney("amount_minor");

        builder.HasIndex(contribution => contribution.SavingsGoalId);
        builder.HasIndex(contribution => contribution.TransactionId);

        builder.HasOne<SavingsGoal>()
            .WithMany()
            .HasForeignKey(contribution => contribution.SavingsGoalId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(contribution => contribution.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
