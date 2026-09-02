using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Recurring;

namespace Suma.Application.Recurring.SkipOccurrence;

public sealed class SkipOccurrenceUseCase(IRecurringOccurrenceStore occurrences, IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        var occurrence = await occurrences.GetByIdAsync(occurrenceId, cancellationToken) ?? throw new NotFoundException("Recurring occurrence was not found.");
        if (occurrence.Status != RecurringOccurrenceStatus.Pending) throw new ConflictException("Only a pending recurring occurrence can be skipped.");
        occurrence.Skip();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
