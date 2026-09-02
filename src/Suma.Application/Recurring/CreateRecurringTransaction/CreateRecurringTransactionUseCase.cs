using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Categories;
using Suma.Domain.Recurring;
using Suma.Domain.ValueObjects;

namespace Suma.Application.Recurring.CreateRecurringTransaction;

public sealed record RecurringScheduleInput(
    long AmountMinor,
    RecurrenceFrequencyUnit FrequencyUnit,
    int IntervalCount,
    DateOnly StartDate,
    DateOnly? EndDate,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    int? MonthOfYear,
    string? Description,
    string? Notes);

public sealed record CreateRecurringExpenseRequest(Guid SourceAccountId, Guid CategoryId, RecurringScheduleInput Schedule);
public sealed record CreateRecurringIncomeRequest(Guid DestinationAccountId, Guid CategoryId, RecurringScheduleInput Schedule);
public sealed record CreateRecurringTransferRequest(Guid SourceAccountId, Guid DestinationAccountId, RecurringScheduleInput Schedule);
public sealed record CreateRecurringTransactionResult(Guid Id);

public sealed class CreateRecurringTransactionUseCase(IAccountStore accounts, ICategoryStore categories, IRecurringTransactionStore recurringTransactions, IUnitOfWork unitOfWork)
{
    public async Task<CreateRecurringTransactionResult> ExecuteExpenseAsync(CreateRecurringExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var account = await AccountAsync(request.SourceAccountId, "Source", cancellationToken);
        var category = await CategoryAsync(request.CategoryId, cancellationToken);
        Validation.RequireActive(account, "source");
        Validation.RequireCategory(category, CategoryTransactionKind.Expense);
        var schedule = request.Schedule;
        var recurring = RecurringTransaction.CreateExpense(account.Id, category.Id, Money(schedule, account.CurrencyCode), schedule.FrequencyUnit, schedule.IntervalCount, schedule.StartDate, schedule.EndDate, schedule.DayOfWeek, schedule.DayOfMonth, schedule.MonthOfYear, schedule.Description, schedule.Notes);
        return await SaveAsync(recurring, cancellationToken);
    }

    public async Task<CreateRecurringTransactionResult> ExecuteIncomeAsync(CreateRecurringIncomeRequest request, CancellationToken cancellationToken = default)
    {
        var account = await AccountAsync(request.DestinationAccountId, "Destination", cancellationToken);
        var category = await CategoryAsync(request.CategoryId, cancellationToken);
        Validation.RequireActive(account, "destination");
        Validation.RequireCategory(category, CategoryTransactionKind.Income);
        var schedule = request.Schedule;
        var recurring = RecurringTransaction.CreateIncome(account.Id, category.Id, Money(schedule, account.CurrencyCode), schedule.FrequencyUnit, schedule.IntervalCount, schedule.StartDate, schedule.EndDate, schedule.DayOfWeek, schedule.DayOfMonth, schedule.MonthOfYear, schedule.Description, schedule.Notes);
        return await SaveAsync(recurring, cancellationToken);
    }

    public async Task<CreateRecurringTransactionResult> ExecuteTransferAsync(CreateRecurringTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SourceAccountId == request.DestinationAccountId) throw new ApplicationValidationException("Source and destination accounts must be different.");
        var source = await AccountAsync(request.SourceAccountId, "Source", cancellationToken);
        var destination = await AccountAsync(request.DestinationAccountId, "Destination", cancellationToken);
        Validation.RequireActive(source, "source");
        Validation.RequireActive(destination, "destination");
        Validation.RequireCurrency(source.CurrencyCode, destination.CurrencyCode, "Transfer accounts must use the same currency.");
        var schedule = request.Schedule;
        var recurring = RecurringTransaction.CreateTransfer(source.Id, destination.Id, Money(schedule, source.CurrencyCode), schedule.FrequencyUnit, schedule.IntervalCount, schedule.StartDate, schedule.EndDate, schedule.DayOfWeek, schedule.DayOfMonth, schedule.MonthOfYear, schedule.Description, schedule.Notes);
        return await SaveAsync(recurring, cancellationToken);
    }

    private async Task<CreateRecurringTransactionResult> SaveAsync(RecurringTransaction recurring, CancellationToken cancellationToken)
    {
        await recurringTransactions.AddAsync(recurring, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new(recurring.Id);
    }

    private async Task<Suma.Domain.Accounts.Account> AccountAsync(Guid id, string role, CancellationToken cancellationToken) =>
        await accounts.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException($"{role} account was not found.");

    private async Task<Suma.Domain.Categories.Category> CategoryAsync(Guid id, CancellationToken cancellationToken) =>
        await categories.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Category was not found.");

    private static Money Money(RecurringScheduleInput schedule, string currencyCode) => new(schedule.AmountMinor, currencyCode);
}
