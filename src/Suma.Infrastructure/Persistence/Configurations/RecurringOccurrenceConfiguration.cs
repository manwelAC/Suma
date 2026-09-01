using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suma.Domain.Recurring;
using Suma.Domain.Transactions;

namespace Suma.Infrastructure.Persistence.Configurations;

public sealed class RecurringOccurrenceConfiguration : IEntityTypeConfiguration<RecurringOccurrence>
{
    public void Configure(EntityTypeBuilder<RecurringOccurrence> builder)
    {
        builder.ToTable("recurring_occurrences");
        builder.HasKey(occurrence => occurrence.Id);

        builder.Property(occurrence => occurrence.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(occurrence => occurrence.RecurringTransactionId)
            .HasColumnName("recurring_transaction_id");
        builder.Property(occurrence => occurrence.DueDate)
            .HasColumnName("due_date");
        builder.Property(occurrence => occurrence.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(occurrence => occurrence.TransactionId)
            .HasColumnName("transaction_id");

        builder.HasIndex(occurrence => occurrence.DueDate);
        builder.HasIndex(occurrence => occurrence.Status);
        builder.HasIndex(occurrence => occurrence.TransactionId);
        builder.HasIndex(occurrence => new { occurrence.RecurringTransactionId, occurrence.DueDate })
            .IsUnique();

        builder.HasOne<RecurringTransaction>()
            .WithMany()
            .HasForeignKey(occurrence => occurrence.RecurringTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(occurrence => occurrence.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
