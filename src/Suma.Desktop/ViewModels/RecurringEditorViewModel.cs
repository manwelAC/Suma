using Suma.Application.Categories.GetCategories;
using Suma.Desktop.Operations.Accounts;
using Suma.Desktop.Operations.Categories;
using Suma.Domain.Categories;

namespace Suma.Desktop.ViewModels;

public sealed class RecurringEditorViewModel(IAccountOperations accounts, ICategoryOperations categories)
{
    public IReadOnlyList<RecurringAccountOption> Accounts { get; private set; } = [];
    public IReadOnlyList<RecurringCategoryOption> ExpenseCategories { get; private set; } = [];
    public IReadOnlyList<RecurringCategoryOption> IncomeCategories { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public async Task<bool> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var accountResults = await accounts.GetAsync(false, cancellationToken);
            var expenseResults = await categories.GetAsync(new(CategoryTransactionKind.Expense, false), cancellationToken);
            var incomeResults = await categories.GetAsync(new(CategoryTransactionKind.Income, false), cancellationToken);
            Accounts = accountResults.Select(item => new RecurringAccountOption(item.Id, item.Name, item.CurrencyCode)).ToArray();
            ExpenseCategories = expenseResults.Select(item => new RecurringCategoryOption(item.Id, item.Name)).ToArray();
            IncomeCategories = incomeResults.Select(item => new RecurringCategoryOption(item.Id, item.Name)).ToArray();
            ErrorMessage = null;
            return true;
        }
        catch
        {
            ErrorMessage = "Suma could not load recurring transaction options. Try again.";
            return false;
        }
    }
}
