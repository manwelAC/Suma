using System.Collections.ObjectModel;
using Suma.Application.Categories.GetCategories;
using Suma.Application.Common.Exceptions;
using Suma.Desktop.Operations.Categories;
using Suma.Domain.Categories;

namespace Suma.Desktop.ViewModels;

public sealed record BudgetCategoryOption(Guid Id, string Name)
{
    public string Display => Name;
}

public sealed class BudgetEditorViewModel(ICategoryOperations categories) : ViewModelBase
{
    private string? errorMessage;

    public ObservableCollection<BudgetCategoryOption> ExpenseCategories { get; } = [];

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public async Task<bool> LoadExpenseCategoriesAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        try
        {
            var results = await categories.GetAsync(
                new GetCategoriesRequest(CategoryTransactionKind.Expense, Archived: false),
                cancellationToken);
            ExpenseCategories.Clear();
            foreach (var category in results)
            {
                ExpenseCategories.Add(new(category.Id, category.Name));
            }

            return true;
        }
        catch (Exception exception)
        {
            ExpenseCategories.Clear();
            ErrorMessage = exception switch
            {
                ApplicationValidationException or ConflictException or NotFoundException => exception.Message,
                _ => "Suma could not load expense categories. Try again."
            };
            return false;
        }
    }
}
