using Suma.Application.Common.Exceptions;
using Suma.Domain.Accounts;
using Suma.Domain.Categories;

namespace Suma.Application.Common;

internal static class Validation
{
    public static void RequireActive(Account account, string role)
    {
        if (account.IsArchived)
        {
            throw new ConflictException($"The {role} account is archived.");
        }
    }

    public static void RequireCategory(Category category, CategoryTransactionKind kind)
    {
        if (category.IsArchived)
        {
            throw new ConflictException("The category is archived.");
        }

        if (category.TransactionKind != kind)
        {
            throw new ConflictException($"The category must support {kind} transactions.");
        }
    }

    public static void RequireCurrency(string expected, string actual, string message)
    {
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(message);
        }
    }

    public static void RequireActualTransactionDate(DateOnly transactionDate, DateOnly today)
    {
        if (transactionDate > today)
        {
            throw new ApplicationValidationException("Transaction date cannot be in the future.");
        }
    }
}
