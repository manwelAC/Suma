using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Suma.Desktop.ViewModels;
using Suma.Domain.Accounts;
using Suma.Domain.Categories;

namespace Suma.Desktop.Pages.Accounts;

public sealed partial class AccountsPage : Page
{
    public AccountsPage(
        AccountsViewModel accountsViewModel,
        CategoriesViewModel categoriesViewModel)
    {
        AccountsViewModel = accountsViewModel;
        CategoriesViewModel = categoriesViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public AccountsViewModel AccountsViewModel { get; }

    public CategoriesViewModel CategoriesViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await Task.WhenAll(AccountsViewModel.LoadAsync(), CategoriesViewModel.LoadAsync());
    }

    private void OnAccountsSectionClick(object sender, RoutedEventArgs e)
    {
        AccountsSection.Visibility = Visibility.Visible;
        CategoriesSection.Visibility = Visibility.Collapsed;
        SetToggleState(AccountsSectionButton, true);
        SetToggleState(CategoriesSectionButton, false);
    }

    private void OnCategoriesSectionClick(object sender, RoutedEventArgs e)
    {
        AccountsSection.Visibility = Visibility.Collapsed;
        CategoriesSection.Visibility = Visibility.Visible;
        SetToggleState(AccountsSectionButton, false);
        SetToggleState(CategoriesSectionButton, true);
    }

    private async void OnActiveAccountsClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(ActiveAccountsButton, true);
        SetToggleState(ArchivedAccountsButton, false);
        await AccountsViewModel.SetArchivedViewAsync(false);
    }

    private async void OnArchivedAccountsClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(ActiveAccountsButton, false);
        SetToggleState(ArchivedAccountsButton, true);
        await AccountsViewModel.SetArchivedViewAsync(true);
    }

    private async void OnExpenseCategoriesClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(ExpenseCategoriesButton, true);
        SetToggleState(IncomeCategoriesButton, false);
        await CategoriesViewModel.SetKindAsync(CategoryTransactionKind.Expense);
    }

    private async void OnIncomeCategoriesClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(ExpenseCategoriesButton, false);
        SetToggleState(IncomeCategoriesButton, true);
        await CategoriesViewModel.SetKindAsync(CategoryTransactionKind.Income);
    }

    private async void OnActiveCategoriesClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(ActiveCategoriesButton, true);
        SetToggleState(ArchivedCategoriesButton, false);
        await CategoriesViewModel.SetArchivedViewAsync(false);
    }

    private async void OnArchivedCategoriesClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(ActiveCategoriesButton, false);
        SetToggleState(ArchivedCategoriesButton, true);
        await CategoriesViewModel.SetArchivedViewAsync(true);
    }

    private async void OnAddAccountClick(object sender, RoutedEventArgs e)
    {
        if (AccountsViewModel.ShowArchived)
        {
            SetToggleState(ActiveAccountsButton, true);
            SetToggleState(ArchivedAccountsButton, false);
            await AccountsViewModel.SetArchivedViewAsync(false);
        }

        await ShowAccountEditorAsync(account: null);
    }

    private async void OnEditAccountClick(object sender, RoutedEventArgs e)
    {
        var account = FindAccount(sender);
        if (account is not null)
        {
            await ShowAccountEditorAsync(account);
        }
    }

    private async void OnArchiveAccountClick(object sender, RoutedEventArgs e)
    {
        if (GetId(sender) is { } id)
        {
            await AccountsViewModel.ArchiveCommand.ExecuteAsync(id);
        }
    }

    private async void OnRestoreAccountClick(object sender, RoutedEventArgs e)
    {
        if (GetId(sender) is { } id)
        {
            await AccountsViewModel.RestoreCommand.ExecuteAsync(id);
        }
    }

    private async void OnAddCategoryClick(object sender, RoutedEventArgs e)
    {
        if (CategoriesViewModel.ShowArchived)
        {
            SetToggleState(ActiveCategoriesButton, true);
            SetToggleState(ArchivedCategoriesButton, false);
            await CategoriesViewModel.SetArchivedViewAsync(false);
        }

        await ShowCategoryEditorAsync(category: null);
    }

    private async void OnEditCategoryClick(object sender, RoutedEventArgs e)
    {
        var category = FindCategory(sender);
        if (category is not null)
        {
            await ShowCategoryEditorAsync(category);
        }
    }

    private async void OnArchiveCategoryClick(object sender, RoutedEventArgs e)
    {
        if (GetId(sender) is { } id)
        {
            await CategoriesViewModel.ArchiveCommand.ExecuteAsync(id);
        }
    }

    private async void OnRestoreCategoryClick(object sender, RoutedEventArgs e)
    {
        if (GetId(sender) is { } id)
        {
            await CategoriesViewModel.RestoreCommand.ExecuteAsync(id);
        }
    }

    private async Task ShowAccountEditorAsync(AccountRowViewModel? account)
    {
        var nameBox = new TextBox
        {
            Header = "Name",
            PlaceholderText = "Everyday account",
            Text = account?.Name ?? string.Empty
        };
        var typeBox = new ComboBox
        {
            Header = "Type",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = Enum.GetValues<AccountType>(),
            SelectedItem = account?.Type ?? AccountType.Bank
        };
        var currencyBox = new TextBox
        {
            Header = "Currency code",
            CharacterCasing = CharacterCasing.Upper,
            MaxLength = 3,
            PlaceholderText = "PHP",
            Text = "PHP",
            Visibility = account is null ? Visibility.Visible : Visibility.Collapsed
        };
        var openingBalanceBox = new TextBox
        {
            Header = "Opening balance",
            InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.CurrencyAmount) } },
            PlaceholderText = "0.00",
            Text = "0.00",
            Visibility = account is null ? Visibility.Visible : Visibility.Collapsed
        };
        var inclusionBox = new CheckBox
        {
            Content = "Include in future Available-to-Spend",
            IsChecked = account?.IncludeInAvailableToSpend ?? true
        };
        var immutableNote = new TextBlock
        {
            Text = account is null ? string.Empty : "Currency and opening balance remain unchanged when editing.",
            TextWrapping = TextWrapping.Wrap,
            Visibility = account is null ? Visibility.Collapsed : Visibility.Visible
        };
        var errorText = EditorErrorText();
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(nameBox);
        content.Children.Add(typeBox);
        content.Children.Add(currencyBox);
        content.Children.Add(openingBalanceBox);
        content.Children.Add(inclusionBox);
        content.Children.Add(immutableNote);
        content.Children.Add(errorText);

        var dialog = EditorDialog(account is null ? "Add account" : "Edit account", content);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                if (typeBox.SelectedItem is not AccountType type)
                {
                    SetEditorError(errorText, "Choose an account type.");
                    args.Cancel = true;
                    return;
                }

                long openingMinor = 0;
                if (account is null && !MoneyText.TryParseMinor(openingBalanceBox.Text, out openingMinor))
                {
                    SetEditorError(errorText, "Enter an opening balance with no more than two decimal places.");
                    args.Cancel = true;
                    return;
                }

                var input = new AccountEditorInput(
                    nameBox.Text,
                    type,
                    inclusionBox.IsChecked == true,
                    openingMinor,
                    currencyBox.Text);
                var succeeded = account is null
                    ? await AccountsViewModel.CreateAsync(input)
                    : await AccountsViewModel.UpdateAsync(account.Id, input);
                if (!succeeded)
                {
                    SetEditorError(errorText, AccountsViewModel.ErrorMessage!);
                    args.Cancel = true;
                }
            }
            finally
            {
                deferral.Complete();
            }
        };

        _ = await dialog.ShowAsync();
    }

    private async Task ShowCategoryEditorAsync(CategoryRowViewModel? category)
    {
        var kind = category?.Kind ?? CategoriesViewModel.SelectedKind;
        var nameBox = new TextBox
        {
            Header = "Name",
            PlaceholderText = kind == CategoryTransactionKind.Expense ? "Groceries" : "Salary",
            Text = category?.Name ?? string.Empty
        };
        var kindBox = new ComboBox
        {
            Header = "Kind",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = Enum.GetValues<CategoryTransactionKind>(),
            SelectedItem = kind,
            IsEnabled = false
        };
        var parentBox = new ComboBox
        {
            Header = "Parent category (optional)",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = CategoriesViewModel.GetParentOptions(category?.Id)
        };
        parentBox.SelectedItem = ((IEnumerable<CategoryParentOption>)parentBox.ItemsSource)
            .First(option => option.Id == category?.ParentCategoryId);
        var kindNote = new TextBlock
        {
            Text = "Choose Expense or Income from the section selector before adding. Kind cannot be changed later.",
            TextWrapping = TextWrapping.Wrap
        };
        var errorText = EditorErrorText();
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(nameBox);
        content.Children.Add(kindBox);
        content.Children.Add(parentBox);
        content.Children.Add(kindNote);
        content.Children.Add(errorText);

        var dialog = EditorDialog(category is null ? "Add category" : "Edit category", content);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                var parentId = (parentBox.SelectedItem as CategoryParentOption)?.Id;
                var input = new CategoryEditorInput(nameBox.Text, kind, parentId);
                var succeeded = category is null
                    ? await CategoriesViewModel.CreateAsync(input)
                    : await CategoriesViewModel.UpdateAsync(category.Id, input);
                if (!succeeded)
                {
                    SetEditorError(errorText, CategoriesViewModel.ErrorMessage!);
                    args.Cancel = true;
                }
            }
            finally
            {
                deferral.Complete();
            }
        };

        _ = await dialog.ShowAsync();
    }

    private ContentDialog EditorDialog(string title, UIElement content) => new()
    {
        Title = title,
        Content = content,
        PrimaryButtonText = "Save",
        CloseButtonText = "Cancel",
        DefaultButton = ContentDialogButton.Primary,
        XamlRoot = XamlRoot
    };

    private static TextBlock EditorErrorText() => new()
    {
        TextWrapping = TextWrapping.Wrap,
        Visibility = Visibility.Collapsed
    };

    private static void SetEditorError(TextBlock textBlock, string message)
    {
        textBlock.Text = message;
        textBlock.Visibility = Visibility.Visible;
    }

    private AccountRowViewModel? FindAccount(object sender) =>
        GetId(sender) is { } id ? AccountsViewModel.Items.SingleOrDefault(item => item.Id == id) : null;

    private CategoryRowViewModel? FindCategory(object sender) =>
        GetId(sender) is { } id ? CategoriesViewModel.Items.SingleOrDefault(item => item.Id == id) : null;

    private static Guid? GetId(object sender) => ((FrameworkElement)sender).Tag switch
    {
        Guid id => id,
        string text when Guid.TryParse(text, out var id) => id,
        _ => null
    };

    private static void SetToggleState(ToggleButton button, bool selected)
    {
        button.IsChecked = selected;
        button.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources[
            selected ? "SumaNavigationItemSelectedStyle" : "SumaNavigationItemStyle"];
    }
}
