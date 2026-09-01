using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Recurring;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class RecurringTransactionStore(SumaDbContext context) : IRecurringTransactionStore
{
    public Task<RecurringTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.RecurringTransactions.SingleOrDefaultAsync(recurring => recurring.Id == id, cancellationToken);
}
