using Suma.Application.Abstractions.Persistence;
using Suma.Application.Abstractions.Time;
using Suma.Application.Common;
using Suma.Application.Common.Exceptions;
using Suma.Application.Transactions;
using Suma.Domain.Accounts;
using Suma.Domain.Categories;
using Suma.Domain.Recurring;
using Suma.Domain.Transactions;

namespace Suma.Application.Recurring.MarkOccurrencePaid;

public sealed class MarkOccurrencePaidUseCase(
    IRecurringOccurrenceStore occurrences,
    IRecurringTransactionStore recurringTransactions,
    IAccountStore accounts,
    ICategoryStore categories,
    ITransactionStore transactions,
    IUnitOfWork unitOfWork,
    IDateProvider dateProvider)
{
    public async Task<TransactionResult> ExecuteAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        var occurrence = await occurrences.GetByIdAsync(occurrenceId, cancellationToken)
            ?? throw new NotFoundException("Recurring occurrence was not found.");
        if (occurrence.Status != RecurringOccurrenceStatus.Pending)
        {
            throw new ConflictException("Only a pending recurring occurrence can be marked paid.");
        }

        Validation.RequireActualTransactionDate(occurrence.DueDate, dateProvider.Today);

        var recurring = await recurringTransactions.GetByIdAsync(occurrence.RecurringTransactionId, cancellationToken)
            ?? throw new NotFoundException("Recurring transaction was not found.");
        var transaction = recurring.Type switch
        {
            TransactionType.Expense => await CreateExpenseAsync(recurring, occurrence.DueDate, cancellationToken),
            TransactionType.Income => await CreateIncomeAsync(recurring, occurrence.DueDate, cancellationToken),
            TransactionType.Transfer => await CreateTransferAsync(recurring, occurrence.DueDate, cancellationToken),
            _ => throw new ConflictException("Recurring Refund transactions are not supported.")
        };
        await transactions.AddAsync(transaction, cancellationToken);
        occurrence.MarkPaid(transaction.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TransactionResult.From(transaction);
    }

    private async Task<Transaction> CreateExpenseAsync(
        RecurringTransaction recurring,
        DateOnly transactionDate,
        CancellationToken cancellationToken)
    {
        var account = await GetAccountAsync(recurring.SourceAccountId!.Value, "Source", cancellationToken);
        var category = await GetCategoryAsync(recurring.CategoryId!.Value, cancellationToken);
        Validation.RequireActive(account, "source");
        Validation.RequireCategory(category, CategoryTransactionKind.Expense);
        Validation.RequireCurrency(account.CurrencyCode, recurring.Amount.CurrencyCode, "Transaction currency must match the source account.");
        return Transaction.CreateExpense(account.Id, category.Id, recurring.Amount, transactionDate, recurring.Description, recurring.Notes);
    }

    private async Task<Transaction> CreateIncomeAsync(
        RecurringTransaction recurring,
        DateOnly transactionDate,
        CancellationToken cancellationToken)
    {
        var account = await GetAccountAsync(recurring.DestinationAccountId!.Value, "Destination", cancellationToken);
        var category = await GetCategoryAsync(recurring.CategoryId!.Value, cancellationToken);
        Validation.RequireActive(account, "destination");
        Validation.RequireCategory(category, CategoryTransactionKind.Income);
        Validation.RequireCurrency(account.CurrencyCode, recurring.Amount.CurrencyCode, "Transaction currency must match the destination account.");
        return Transaction.CreateIncome(account.Id, category.Id, recurring.Amount, transactionDate, recurring.Description, recurring.Notes);
    }

    private async Task<Transaction> CreateTransferAsync(
        RecurringTransaction recurring,
        DateOnly transactionDate,
        CancellationToken cancellationToken)
    {
        var sourceAccountId = recurring.SourceAccountId!.Value;
        var destinationAccountId = recurring.DestinationAccountId!.Value;
        if (sourceAccountId == destinationAccountId)
        {
            throw new ConflictException("Source and destination accounts must be different.");
        }

        var source = await GetAccountAsync(sourceAccountId, "Source", cancellationToken);
        var destination = await GetAccountAsync(destinationAccountId, "Destination", cancellationToken);
        Validation.RequireActive(source, "source");
        Validation.RequireActive(destination, "destination");
        Validation.RequireCurrency(source.CurrencyCode, destination.CurrencyCode, "Transfer accounts must use the same currency.");
        Validation.RequireCurrency(source.CurrencyCode, recurring.Amount.CurrencyCode, "Transfer currency must match both accounts.");
        return Transaction.CreateTransfer(source.Id, destination.Id, recurring.Amount, transactionDate, recurring.Description, recurring.Notes);
    }

    private async Task<Account> GetAccountAsync(Guid accountId, string role, CancellationToken cancellationToken) =>
        await accounts.GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException($"{role} account was not found.");

    private async Task<Category> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken) =>
        await categories.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");
}
