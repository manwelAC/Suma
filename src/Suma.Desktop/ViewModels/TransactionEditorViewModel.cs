using System.Collections.ObjectModel;
using Suma.Application.Categories.GetCategories;
using Suma.Application.Common.Exceptions;
using Suma.Desktop.Operations.Accounts;
using Suma.Desktop.Operations.Categories;
using Suma.Domain.Categories;

namespace Suma.Desktop.ViewModels;

public sealed class TransactionEditorViewModel(IAccountOperations accounts, ICategoryOperations categories) : ViewModelBase
{
    private string? errorMessage;

    public ObservableCollection<TransactionAccountOption> Accounts { get; } = [];

    public ObservableCollection<TransactionCategoryOption> ExpenseCategories { get; } = [];

    public ObservableCollection<TransactionCategoryOption> IncomeCategories { get; } = [];

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public async Task<bool> LoadAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        try
        {
            var accountTask = accounts.GetAsync(archived: false, cancellationToken);
            var expenseTask = categories.GetAsync(new GetCategoriesRequest(CategoryTransactionKind.Expense, Archived: false), cancellationToken);
            var incomeTask = categories.GetAsync(new GetCategoriesRequest(CategoryTransactionKind.Income, Archived: false), cancellationToken);
            await Task.WhenAll(accountTask, expenseTask, incomeTask);

            Accounts.Clear();
            foreach (var account in await accountTask)
            {
                Accounts.Add(new(account.Id, account.Name, account.Type, account.CurrencyCode));
            }

            ReplaceCategories(ExpenseCategories, await expenseTask);
            ReplaceCategories(IncomeCategories, await incomeTask);
            return true;
        }
        catch (Exception exception)
        {
            Accounts.Clear();
            ExpenseCategories.Clear();
            IncomeCategories.Clear();
            ErrorMessage = exception switch
            {
                ApplicationValidationException or ConflictException or NotFoundException => exception.Message,
                _ => "Suma could not load transaction options. Try again."
            };
            return false;
        }
    }

    private static void ReplaceCategories(ObservableCollection<TransactionCategoryOption> target, IEnumerable<Suma.Application.Categories.CategoryResult> source)
    {
        target.Clear();
        foreach (var category in source)
        {
            target.Add(new(category.Id, category.Name, category.Kind));
        }
    }
}
