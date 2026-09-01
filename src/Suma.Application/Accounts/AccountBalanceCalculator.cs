using Suma.Application.Common.Exceptions;
using Suma.Domain.Accounts;
using Suma.Domain.Transactions;

namespace Suma.Application.Accounts;

internal static class AccountBalanceCalculator
{
    public static long Calculate(Account account, IEnumerable<Transaction> transactions)
    {
        var balance = account.OpeningBalance.AmountMinor;
        foreach (var transaction in transactions)
        {
            if (!string.Equals(transaction.Amount.CurrencyCode, account.CurrencyCode, StringComparison.Ordinal))
            {
                throw new ConflictException("Ledger transaction currency does not match the account.");
            }

            balance = checked(balance + GetEffect(transaction, account.Id));
        }

        return balance;
    }

    private static long GetEffect(Transaction transaction, Guid accountId) => transaction.Type switch
    {
        TransactionType.Expense when transaction.SourceAccountId == accountId => -transaction.Amount.AmountMinor,
        TransactionType.Income when transaction.DestinationAccountId == accountId => transaction.Amount.AmountMinor,
        TransactionType.Refund when transaction.DestinationAccountId == accountId => transaction.Amount.AmountMinor,
        TransactionType.Transfer when transaction.SourceAccountId == accountId => -transaction.Amount.AmountMinor,
        TransactionType.Transfer when transaction.DestinationAccountId == accountId => transaction.Amount.AmountMinor,
        _ => 0
    };
}
