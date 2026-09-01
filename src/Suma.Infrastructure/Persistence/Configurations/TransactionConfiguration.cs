using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suma.Domain.Accounts;
using Suma.Domain.Categories;
using Suma.Domain.Transactions;

namespace Suma.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable(
            "transactions",
            table =>
            {
                table.HasCheckConstraint("ck_transactions_amount_positive", "amount_minor > 0");
                table.HasCheckConstraint(
                    "ck_transactions_distinct_accounts",
                    "source_account_id IS NULL OR destination_account_id IS NULL OR source_account_id <> destination_account_id");
            });
        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(transaction => transaction.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(transaction => transaction.SourceAccountId)
            .HasColumnName("source_account_id");
        builder.Property(transaction => transaction.DestinationAccountId)
            .HasColumnName("destination_account_id");
        builder.Property(transaction => transaction.CategoryId)
            .HasColumnName("category_id");
        builder.Property(transaction => transaction.OriginalTransactionId)
            .HasColumnName("original_transaction_id");
        builder.ComplexProperty(transaction => transaction.Amount)
            .ConfigureMoney("amount_minor");
        builder.Property(transaction => transaction.TransactionDate)
            .HasColumnName("transaction_date");
        builder.Property(transaction => transaction.Description)
            .HasColumnName("description")
            .HasMaxLength(500);
        builder.Property(transaction => transaction.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.HasIndex(transaction => transaction.TransactionDate);
        builder.HasIndex(transaction => transaction.SourceAccountId);
        builder.HasIndex(transaction => transaction.DestinationAccountId);
        builder.HasIndex(transaction => transaction.CategoryId);
        builder.HasIndex(transaction => transaction.OriginalTransactionId);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(transaction => transaction.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(transaction => transaction.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(transaction => transaction.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(transaction => transaction.OriginalTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
