using Suma.Domain.Transactions;

namespace Suma.Application.Abstractions.Persistence;

public interface IReportStore
{
    Task<IReadOnlyList<ReportCategoryFact>> GetCategoryFactsAsync(string currencyCode, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportAccountMovementFact>> GetAccountMovementFactsAsync(string currencyCode, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportAccountMovementDetailFact>> GetAccountMovementDetailsAsync(string currencyCode, DateOnly startDate, DateOnly endDate, Guid? accountId, CancellationToken cancellationToken = default);
}

public sealed record ReportCategoryFact(Guid CategoryId, string CategoryName, bool CategoryArchived, long IncomeMinor, long GrossExpenseMinor, long RefundMinor);

public sealed record ReportAccountMovementFact(Guid AccountId, string AccountName, bool AccountArchived, long IncomeInMinor, long RefundInMinor, long TransferInMinor, long ExpenseOutMinor, long TransferOutMinor);

public enum ReportMovementDirection { Inflow, Outflow }

public sealed record ReportAccountMovementDetailFact(Guid TransactionId, DateOnly TransactionDate, Guid AccountId, string AccountName, bool AccountArchived, ReportMovementDirection Direction, TransactionType Type, string? Counterparty, string? Category, string? Description, long AmountMinor, string CurrencyCode);
