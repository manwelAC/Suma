using Suma.Domain.Accounts;

namespace Suma.Desktop.ViewModels;

public sealed record AccountEditorInput(
    string Name,
    AccountType Type,
    bool IncludeInAvailableToSpend,
    long OpeningBalanceMinor = 0,
    string CurrencyCode = "",
    string? AccountNumber = null);
