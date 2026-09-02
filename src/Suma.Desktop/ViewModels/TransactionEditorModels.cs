using Suma.Domain.Accounts;
using Suma.Domain.Categories;
using Suma.Application.Transactions.GetRefundableExpenses;

namespace Suma.Desktop.ViewModels;

public sealed record TransactionAccountOption(Guid Id, string Name, AccountType Type, string CurrencyCode)
{
    public string Display => $"{Name} — {(Type == AccountType.EWallet ? "E-Wallet" : Type)} — {CurrencyCode}";
}

public sealed record TransactionCategoryOption(Guid Id, string Name, CategoryTransactionKind Kind)
{
    public string Display => Name;
}

public sealed record RefundableExpenseOption(RefundableExpenseResult Expense)
{
    public Guid Id => Expense.Id;

    public string Display => $"{Expense.TransactionDate:MMM d} • {Expense.Description ?? Expense.CategoryName} • {MoneyText.Format(Expense.OriginalAmountMinor, Expense.CurrencyCode)} • {MoneyText.Format(Expense.RemainingAmountMinor, Expense.CurrencyCode)} remaining";
}

public sealed record ExpenseEditorInput(Guid AccountId, Guid CategoryId, long AmountMinor, string CurrencyCode, DateOnly Date, string? Description, string? Notes);

public sealed record IncomeEditorInput(Guid AccountId, Guid CategoryId, long AmountMinor, string CurrencyCode, DateOnly Date, string? Description, string? Notes);

public sealed record TransferEditorInput(Guid SourceAccountId, Guid DestinationAccountId, long AmountMinor, string CurrencyCode, DateOnly Date, string? Description, string? Notes);

public sealed record RefundEditorInput(Guid OriginalTransactionId, Guid DestinationAccountId, Guid CategoryId, long AmountMinor, string CurrencyCode, DateOnly Date, string? Description, string? Notes);
