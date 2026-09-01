using Suma.Domain.Recurring;

namespace Suma.Application.Abstractions.Persistence;

public interface IRecurringTransactionStore
{
    Task<RecurringTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
