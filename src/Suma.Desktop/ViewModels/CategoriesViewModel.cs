using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Suma.Application.Categories.CreateCategory;
using Suma.Application.Categories.GetCategories;
using Suma.Application.Categories.UpdateCategory;
using Suma.Application.Common.Exceptions;
using Suma.Desktop.Operations.Categories;
using Suma.Domain.Categories;

namespace Suma.Desktop.ViewModels;

public sealed partial class CategoriesViewModel(ICategoryOperations operations) : ViewModelBase
{
    private bool isLoading;
    private bool showArchived;
    private string? errorMessage;
    private CategoryTransactionKind selectedKind = CategoryTransactionKind.Expense;

    public ObservableCollection<CategoryRowViewModel> Items { get; } = [];

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (SetProperty(ref isLoading, value))
            {
                OnPropertyChanged(nameof(LoadingVisibility));
                OnPropertyChanged(nameof(EmptyVisibility));
            }
        }
    }

    public bool ShowArchived
    {
        get => showArchived;
        private set
        {
            if (SetProperty(ref showArchived, value))
            {
                OnPropertyChanged(nameof(FilterLabel));
            }
        }
    }

    public CategoryTransactionKind SelectedKind
    {
        get => selectedKind;
        private set
        {
            if (SetProperty(ref selectedKind, value))
            {
                OnPropertyChanged(nameof(EmptyTitle));
                OnPropertyChanged(nameof(FilterLabel));
            }
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetProperty(ref errorMessage, value))
            {
                OnPropertyChanged(nameof(ErrorVisibility));
            }
        }
    }

    public string EmptyTitle => $"No {SelectedKind.ToString().ToLowerInvariant()} categories yet";

    public string FilterLabel => $"{(ShowArchived ? "Archived" : "Active")} {SelectedKind.ToString().ToLowerInvariant()} categories";

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyVisibility => !IsLoading && Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility => string.IsNullOrEmpty(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var results = await operations.GetAsync(new GetCategoriesRequest(SelectedKind, ShowArchived), cancellationToken);
            Items.Clear();
            foreach (var category in results)
            {
                Items.Add(new CategoryRowViewModel(
                    category.Id,
                    category.Name,
                    category.Kind,
                    category.ParentCategoryId,
                    category.ParentCategoryName is null ? "No parent" : $"Under {category.ParentCategoryName}",
                    category.IsSystem,
                    category.IsArchived));
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = UserMessage(exception);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(EmptyVisibility));
        }
    }

    public async Task SetKindAsync(CategoryTransactionKind kind, CancellationToken cancellationToken = default)
    {
        SelectedKind = kind;
        await LoadAsync(cancellationToken);
    }

    public async Task SetArchivedViewAsync(bool archived, CancellationToken cancellationToken = default)
    {
        ShowArchived = archived;
        await LoadAsync(cancellationToken);
    }

    public IReadOnlyList<CategoryParentOption> GetParentOptions(Guid? excludedCategoryId = null)
    {
        return [
            new CategoryParentOption(null, "No parent"),
            .. Items
                .Where(item => !item.IsArchived && item.Id != excludedCategoryId)
                .Select(item => new CategoryParentOption(item.Id, item.Name))
        ];
    }

    public async Task<bool> CreateAsync(CategoryEditorInput input, CancellationToken cancellationToken = default)
    {
        return await ExecuteWriteAsync(
            () => operations.CreateAsync(
                new CreateCategoryRequest(input.Name, input.Kind, input.ParentCategoryId),
                cancellationToken),
            cancellationToken);
    }

    public async Task<bool> UpdateAsync(
        Guid categoryId,
        CategoryEditorInput input,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWriteAsync(
            () => operations.UpdateAsync(
                new UpdateCategoryRequest(categoryId, input.Name, input.ParentCategoryId),
                cancellationToken),
            cancellationToken);
    }

    [RelayCommand]
    private async Task ArchiveAsync(Guid categoryId)
    {
        await ExecuteWriteAsync(() => operations.ArchiveAsync(categoryId), CancellationToken.None);
    }

    [RelayCommand]
    private async Task RestoreAsync(Guid categoryId)
    {
        await ExecuteWriteAsync(() => operations.RestoreAsync(categoryId), CancellationToken.None);
    }

    private async Task<bool> ExecuteWriteAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        try
        {
            await operation();
            await LoadAsync(cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            ErrorMessage = UserMessage(exception);
            return false;
        }
    }

    private async Task<bool> ExecuteWriteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        return await ExecuteWriteAsync(async () => { _ = await operation(); }, cancellationToken);
    }

    private static string UserMessage(Exception exception) => exception switch
    {
        ApplicationValidationException or ConflictException or NotFoundException => exception.Message,
        _ => "Suma could not complete that category change. Try again."
    };
}
