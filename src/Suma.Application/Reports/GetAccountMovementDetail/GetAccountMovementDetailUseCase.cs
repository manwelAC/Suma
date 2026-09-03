using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;
using Suma.Domain.ValueObjects;

namespace Suma.Application.Reports.GetAccountMovementDetail;

public sealed record AccountMovementDetailRequest(string CurrencyCode, DateOnly StartDate, DateOnly EndDate, Guid? AccountId = null);
public sealed record AccountMovementDetailRow(Guid TransactionId, DateOnly TransactionDate, Guid AccountId, string AccountName, bool AccountArchived, ReportMovementDirection Direction, Domain.Transactions.TransactionType Type, string? Counterparty, string? Category, string? Description, long AmountMinor, string CurrencyCode);

public sealed class GetAccountMovementDetailUseCase(IReportStore reports)
{
    public async Task<IReadOnlyList<AccountMovementDetailRow>> ExecuteAsync(AccountMovementDetailRequest request, CancellationToken cancellationToken = default)
    {
        if (request.StartDate > request.EndDate) throw new ApplicationValidationException("Report start date must be on or before end date.");
        var currency = new Money(0, request.CurrencyCode).CurrencyCode;
        return (await reports.GetAccountMovementDetailsAsync(currency, request.StartDate, request.EndDate, request.AccountId, cancellationToken))
            .OrderByDescending(item => item.TransactionDate).ThenByDescending(item => item.TransactionId).ThenBy(item => item.Direction)
            .Select(item => new AccountMovementDetailRow(item.TransactionId, item.TransactionDate, item.AccountId, item.AccountName, item.AccountArchived, item.Direction, item.Type, item.Counterparty, item.Category, item.Description, item.AmountMinor, item.CurrencyCode)).ToArray();
    }
}
