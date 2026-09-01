using Suma.Domain.Common;
using Suma.Domain.ValueObjects;

namespace Suma.Domain.Transactions;

public sealed class Transaction : Entity
{
    private Transaction(
        TransactionType type,
        Guid? sourceAccountId,
        Guid? destinationAccountId,
        Guid? categoryId,
        Guid? originalTransactionId,
        Money amount,
        DateOnly transactionDate,
        string? description,
        string? notes)
    {
        Type = type;
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
        CategoryId = categoryId;
        OriginalTransactionId = originalTransactionId;
        Amount = amount;
        TransactionDate = transactionDate;
        Description = NormalizeOptionalText(description);
        Notes = NormalizeOptionalText(notes);
    }

    public TransactionType Type { get; }

    public Guid? SourceAccountId { get; }

    public Guid? DestinationAccountId { get; }

    public Guid? CategoryId { get; }

    public Guid? OriginalTransactionId { get; }

    public Money Amount { get; }

    public DateOnly TransactionDate { get; }

    public string? Description { get; }

    public string? Notes { get; }

    public static Transaction CreateExpense(
        Guid sourceAccountId,
        Guid categoryId,
        Money amount,
        DateOnly transactionDate,
        string? description = null,
        string? notes = null)
    {
        EnsureNotEmpty(sourceAccountId, nameof(sourceAccountId));
        EnsureNotEmpty(categoryId, nameof(categoryId));
        EnsurePositiveAmount(amount);

        return new Transaction(
            TransactionType.Expense,
            sourceAccountId,
            null,
            categoryId,
            null,
            amount,
            transactionDate,
            description,
            notes);
    }

    public static Transaction CreateIncome(
        Guid destinationAccountId,
        Guid categoryId,
        Money amount,
        DateOnly transactionDate,
        string? description = null,
        string? notes = null)
    {
        EnsureNotEmpty(destinationAccountId, nameof(destinationAccountId));
        EnsureNotEmpty(categoryId, nameof(categoryId));
        EnsurePositiveAmount(amount);

        return new Transaction(
            TransactionType.Income,
            null,
            destinationAccountId,
            categoryId,
            null,
            amount,
            transactionDate,
            description,
            notes);
    }

    public static Transaction CreateTransfer(
        Guid sourceAccountId,
        Guid destinationAccountId,
        Money amount,
        DateOnly transactionDate,
        string? description = null,
        string? notes = null)
    {
        EnsureNotEmpty(sourceAccountId, nameof(sourceAccountId));
        EnsureNotEmpty(destinationAccountId, nameof(destinationAccountId));
        EnsurePositiveAmount(amount);

        if (sourceAccountId == destinationAccountId)
        {
            throw new ArgumentException(
                "Source and destination accounts must be different.",
                nameof(destinationAccountId));
        }

        return new Transaction(
            TransactionType.Transfer,
            sourceAccountId,
            destinationAccountId,
            null,
            null,
            amount,
            transactionDate,
            description,
            notes);
    }

    public static Transaction CreateRefund(
        Guid destinationAccountId,
        Guid categoryId,
        Guid originalTransactionId,
        Money amount,
        DateOnly transactionDate,
        string? description = null,
        string? notes = null)
    {
        EnsureNotEmpty(destinationAccountId, nameof(destinationAccountId));
        EnsureNotEmpty(categoryId, nameof(categoryId));
        EnsureNotEmpty(originalTransactionId, nameof(originalTransactionId));
        EnsurePositiveAmount(amount);

        return new Transaction(
            TransactionType.Refund,
            null,
            destinationAccountId,
            categoryId,
            originalTransactionId,
            amount,
            transactionDate,
            description,
            notes);
    }

    private static void EnsureNotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }

    private static void EnsurePositiveAmount(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!amount.IsPositive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount.AmountMinor,
                "Transaction amount must be greater than zero.");
        }
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
