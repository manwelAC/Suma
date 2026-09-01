using Suma.Domain.Common;

namespace Suma.Domain.Recurring;

public sealed class RecurringOccurrence : Entity
{
    public RecurringOccurrence(Guid recurringTransactionId, DateOnly dueDate)
    {
        if (recurringTransactionId == Guid.Empty)
        {
            throw new ArgumentException("Recurring transaction identifier cannot be empty.", nameof(recurringTransactionId));
        }

        RecurringTransactionId = recurringTransactionId;
        DueDate = dueDate;
        Status = RecurringOccurrenceStatus.Pending;
    }

    public Guid RecurringTransactionId { get; }

    public DateOnly DueDate { get; }

    public RecurringOccurrenceStatus Status { get; private set; }

    public Guid? TransactionId { get; private set; }

    public void MarkPaid(Guid transactionId)
    {
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException("Transaction identifier cannot be empty.", nameof(transactionId));
        }

        EnsurePending();

        Status = RecurringOccurrenceStatus.Paid;
        TransactionId = transactionId;
    }

    public void Skip()
    {
        EnsurePending();
        Status = RecurringOccurrenceStatus.Skipped;
    }

    private void EnsurePending()
    {
        if (Status != RecurringOccurrenceStatus.Pending)
        {
            throw new InvalidOperationException(
                $"A {Status} recurring occurrence cannot transition to another status.");
        }
    }
}
