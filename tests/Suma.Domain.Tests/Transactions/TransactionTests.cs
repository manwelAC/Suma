using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Domain.Tests.Transactions;

public sealed class TransactionTests
{
    private static readonly DateOnly TransactionDate = new(2099, 12, 31);

    [Fact]
    public void CreateExpense_WithValidValues_CreatesExpenseShape()
    {
        var sourceAccountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var amount = new Money(50_000, "PHP");

        var transaction = Transaction.CreateExpense(
            sourceAccountId,
            categoryId,
            amount,
            TransactionDate,
            "  GrabFood  ",
            "  Dinner with friends  ");

        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.Equal(TransactionType.Expense, transaction.Type);
        Assert.Equal(sourceAccountId, transaction.SourceAccountId);
        Assert.Null(transaction.DestinationAccountId);
        Assert.Equal(categoryId, transaction.CategoryId);
        Assert.Null(transaction.OriginalTransactionId);
        Assert.Same(amount, transaction.Amount);
        Assert.Equal(50_000, transaction.Amount.AmountMinor);
        Assert.Equal("PHP", transaction.Amount.CurrencyCode);
        Assert.Equal(TransactionDate, transaction.TransactionDate);
        Assert.Equal("GrabFood", transaction.Description);
        Assert.Equal("Dinner with friends", transaction.Notes);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  GrabFood  ", "GrabFood")]
    public void CreateExpense_NormalizesDescription(string? supplied, string? expected)
    {
        var transaction = Transaction.CreateExpense(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PositiveAmount(),
            TransactionDate,
            description: supplied);

        Assert.Equal(expected, transaction.Description);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Paid in cash  ", "Paid in cash")]
    public void CreateExpense_NormalizesNotes(string? supplied, string? expected)
    {
        var transaction = Transaction.CreateExpense(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PositiveAmount(),
            TransactionDate,
            notes: supplied);

        Assert.Equal(expected, transaction.Notes);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CreateExpense_WithEmptyRequiredId_IsRejected(bool emptySource, bool emptyCategory)
    {
        var sourceAccountId = emptySource ? Guid.Empty : Guid.NewGuid();
        var categoryId = emptyCategory ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(
            () => Transaction.CreateExpense(sourceAccountId, categoryId, PositiveAmount(), TransactionDate));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateExpense_WithNonPositiveAmount_IsRejected(long amountMinor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Transaction.CreateExpense(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Money(amountMinor, "PHP"),
                TransactionDate));
    }

    [Fact]
    public void CreateIncome_WithValidValues_CreatesIncomeShape()
    {
        var destinationAccountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var amount = PositiveAmount();

        var transaction = Transaction.CreateIncome(
            destinationAccountId,
            categoryId,
            amount,
            TransactionDate);

        Assert.Equal(TransactionType.Income, transaction.Type);
        Assert.Null(transaction.SourceAccountId);
        Assert.Equal(destinationAccountId, transaction.DestinationAccountId);
        Assert.Equal(categoryId, transaction.CategoryId);
        Assert.Null(transaction.OriginalTransactionId);
        Assert.Same(amount, transaction.Amount);
        Assert.Equal(TransactionDate, transaction.TransactionDate);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CreateIncome_WithEmptyRequiredId_IsRejected(bool emptyDestination, bool emptyCategory)
    {
        var destinationAccountId = emptyDestination ? Guid.Empty : Guid.NewGuid();
        var categoryId = emptyCategory ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(
            () => Transaction.CreateIncome(
                destinationAccountId,
                categoryId,
                PositiveAmount(),
                TransactionDate));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateIncome_WithNonPositiveAmount_IsRejected(long amountMinor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Transaction.CreateIncome(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Money(amountMinor, "PHP"),
                TransactionDate));
    }

    [Fact]
    public void CreateTransfer_WithValidValues_CreatesTransferShape()
    {
        var sourceAccountId = Guid.NewGuid();
        var destinationAccountId = Guid.NewGuid();
        var amount = PositiveAmount();

        var transaction = Transaction.CreateTransfer(
            sourceAccountId,
            destinationAccountId,
            amount,
            TransactionDate);

        Assert.Equal(TransactionType.Transfer, transaction.Type);
        Assert.Equal(sourceAccountId, transaction.SourceAccountId);
        Assert.Equal(destinationAccountId, transaction.DestinationAccountId);
        Assert.Null(transaction.CategoryId);
        Assert.Null(transaction.OriginalTransactionId);
        Assert.Same(amount, transaction.Amount);
        Assert.Equal(TransactionDate, transaction.TransactionDate);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CreateTransfer_WithEmptyRequiredAccountId_IsRejected(bool emptySource, bool emptyDestination)
    {
        var sourceAccountId = emptySource ? Guid.Empty : Guid.NewGuid();
        var destinationAccountId = emptyDestination ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(
            () => Transaction.CreateTransfer(
                sourceAccountId,
                destinationAccountId,
                PositiveAmount(),
                TransactionDate));
    }

    [Fact]
    public void CreateTransfer_WithSameSourceAndDestination_IsRejected()
    {
        var accountId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(
            () => Transaction.CreateTransfer(accountId, accountId, PositiveAmount(), TransactionDate));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateTransfer_WithNonPositiveAmount_IsRejected(long amountMinor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Transaction.CreateTransfer(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Money(amountMinor, "PHP"),
                TransactionDate));
    }

    [Fact]
    public void CreateRefund_WithValidValues_CreatesRefundShape()
    {
        var destinationAccountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var originalTransactionId = Guid.NewGuid();
        var amount = PositiveAmount();

        var transaction = Transaction.CreateRefund(
            destinationAccountId,
            categoryId,
            originalTransactionId,
            amount,
            TransactionDate);

        Assert.Equal(TransactionType.Refund, transaction.Type);
        Assert.Null(transaction.SourceAccountId);
        Assert.Equal(destinationAccountId, transaction.DestinationAccountId);
        Assert.Equal(categoryId, transaction.CategoryId);
        Assert.Equal(originalTransactionId, transaction.OriginalTransactionId);
        Assert.Same(amount, transaction.Amount);
        Assert.Equal(TransactionDate, transaction.TransactionDate);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void CreateRefund_WithEmptyRequiredId_IsRejected(
        bool emptyDestination,
        bool emptyCategory,
        bool emptyOriginalTransaction)
    {
        var destinationAccountId = emptyDestination ? Guid.Empty : Guid.NewGuid();
        var categoryId = emptyCategory ? Guid.Empty : Guid.NewGuid();
        var originalTransactionId = emptyOriginalTransaction ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(
            () => Transaction.CreateRefund(
                destinationAccountId,
                categoryId,
                originalTransactionId,
                PositiveAmount(),
                TransactionDate));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateRefund_WithNonPositiveAmount_IsRejected(long amountMinor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Transaction.CreateRefund(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Money(amountMinor, "PHP"),
                TransactionDate));
    }

    [Theory]
    [InlineData(TransactionType.Expense)]
    [InlineData(TransactionType.Income)]
    [InlineData(TransactionType.Transfer)]
    [InlineData(TransactionType.Refund)]
    public void Create_WithNullAmount_IsRejected(TransactionType type)
    {
        Assert.Throws<ArgumentNullException>(() => CreateWithNullAmount(type));
    }

    private static void CreateWithNullAmount(TransactionType type)
    {
        switch (type)
        {
            case TransactionType.Expense:
                Transaction.CreateExpense(Guid.NewGuid(), Guid.NewGuid(), null!, TransactionDate);
                break;
            case TransactionType.Income:
                Transaction.CreateIncome(Guid.NewGuid(), Guid.NewGuid(), null!, TransactionDate);
                break;
            case TransactionType.Transfer:
                Transaction.CreateTransfer(Guid.NewGuid(), Guid.NewGuid(), null!, TransactionDate);
                break;
            case TransactionType.Refund:
                Transaction.CreateRefund(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null!,
                    TransactionDate);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private static Money PositiveAmount() => new(50_000, "PHP");
}
