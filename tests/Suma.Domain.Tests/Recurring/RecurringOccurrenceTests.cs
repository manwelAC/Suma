using Suma.Domain.Recurring;
using Xunit;

namespace Suma.Domain.Tests.Recurring;

public sealed class RecurringOccurrenceTests
{
    private static readonly DateOnly DueDate = new(2026, 9, 15);

    [Fact]
    public void Create_WithValidValues_CreatesPendingOccurrence()
    {
        var recurringTransactionId = Guid.NewGuid();

        var occurrence = new RecurringOccurrence(recurringTransactionId, DueDate);

        Assert.NotEqual(Guid.Empty, occurrence.Id);
        Assert.Equal(recurringTransactionId, occurrence.RecurringTransactionId);
        Assert.Equal(DueDate, occurrence.DueDate);
        Assert.Equal(RecurringOccurrenceStatus.Pending, occurrence.Status);
        Assert.Null(occurrence.TransactionId);
    }

    [Fact]
    public void Create_WithEmptyRecurringTransactionId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new RecurringOccurrence(Guid.Empty, DueDate));
    }

    [Fact]
    public void MarkPaid_FromPending_MarksPaidAndLinksTransaction()
    {
        var occurrence = CreateOccurrence();
        var transactionId = Guid.NewGuid();

        occurrence.MarkPaid(transactionId);

        Assert.Equal(RecurringOccurrenceStatus.Paid, occurrence.Status);
        Assert.Equal(transactionId, occurrence.TransactionId);
    }

    [Fact]
    public void MarkPaid_WithEmptyTransactionId_IsRejectedWithoutMutation()
    {
        var occurrence = CreateOccurrence();

        Assert.Throws<ArgumentException>(() => occurrence.MarkPaid(Guid.Empty));
        Assert.Equal(RecurringOccurrenceStatus.Pending, occurrence.Status);
        Assert.Null(occurrence.TransactionId);
    }

    [Fact]
    public void MarkPaid_FromPaid_IsRejectedWithoutMutation()
    {
        var occurrence = CreateOccurrence();
        var transactionId = Guid.NewGuid();
        occurrence.MarkPaid(transactionId);

        Assert.Throws<InvalidOperationException>(() => occurrence.MarkPaid(Guid.NewGuid()));
        Assert.Equal(RecurringOccurrenceStatus.Paid, occurrence.Status);
        Assert.Equal(transactionId, occurrence.TransactionId);
    }

    [Fact]
    public void MarkPaid_FromSkipped_IsRejectedWithoutMutation()
    {
        var occurrence = CreateOccurrence();
        occurrence.Skip();

        Assert.Throws<InvalidOperationException>(() => occurrence.MarkPaid(Guid.NewGuid()));
        Assert.Equal(RecurringOccurrenceStatus.Skipped, occurrence.Status);
        Assert.Null(occurrence.TransactionId);
    }

    [Fact]
    public void Skip_FromPending_MarksSkippedWithoutTransaction()
    {
        var occurrence = CreateOccurrence();

        occurrence.Skip();

        Assert.Equal(RecurringOccurrenceStatus.Skipped, occurrence.Status);
        Assert.Null(occurrence.TransactionId);
    }

    [Fact]
    public void Skip_FromPaid_IsRejectedWithoutMutation()
    {
        var occurrence = CreateOccurrence();
        var transactionId = Guid.NewGuid();
        occurrence.MarkPaid(transactionId);

        Assert.Throws<InvalidOperationException>(() => occurrence.Skip());
        Assert.Equal(RecurringOccurrenceStatus.Paid, occurrence.Status);
        Assert.Equal(transactionId, occurrence.TransactionId);
    }

    [Fact]
    public void Skip_FromSkipped_IsRejectedWithoutMutation()
    {
        var occurrence = CreateOccurrence();
        occurrence.Skip();

        Assert.Throws<InvalidOperationException>(() => occurrence.Skip());
        Assert.Equal(RecurringOccurrenceStatus.Skipped, occurrence.Status);
        Assert.Null(occurrence.TransactionId);
    }

    private static RecurringOccurrence CreateOccurrence() => new(Guid.NewGuid(), DueDate);
}
