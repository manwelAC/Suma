using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Suma.Desktop.ViewModels;
using Suma.Desktop.Common;
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
        if (FindAccount(sender) is { } account)
        {
            var dialog = SumaDialog.CreateDestructive(
                XamlRoot,
                "Archive account?",
                $"{account.Name} will be moved to Archived accounts. You can view it anytime or restore it later.",
                "Archived accounts are excluded from available to spend and reports.",
                destructiveButtonText: "Archive");
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await AccountsViewModel.ArchiveCommand.ExecuteAsync(account.Id);
            }
        }
        else if (GetId(sender) is { } id)
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
        if (FindCategory(sender) is { } category)
        {
            var dialog = SumaDialog.CreateDestructive(
                XamlRoot,
                "Archive category?",
                $"{category.Name} will be moved to Archived categories. You can view it anytime or restore it later.",
                "Archived categories cannot be assigned to new transactions or budgets.",
                destructiveButtonText: "Archive");
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await CategoriesViewModel.ArchiveCommand.ExecuteAsync(category.Id);
            }
        }
        else if (GetId(sender) is { } id)
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
        var selectedType = account?.Type ?? AccountType.Bank;
        var typeSelector = SumaDialog.CreateAccountTypeSelector(selectedType, type => selectedType = type);
        var typeField = SumaDialog.CreateField("Account type", typeSelector);

        var nameBox = SumaDialog.CreateTextBox("e.g., Main Wallet", account?.Name ?? string.Empty);
        var nameField = SumaDialog.CreateField("Account name", nameBox);

        var currencyBox = SumaDialog.CreateTextBox("PHP", "PHP", maxLength: 3, casing: CharacterCasing.Upper);
        var currencyField = SumaDialog.CreateField("Currency", currencyBox);

        var openingBalanceBox = SumaDialog.CreateTextBox("0.00", "0.00", inputScope: InputScopeNameValue.CurrencyAmount);
        var balanceField = SumaDialog.CreateField("Opening balance", openingBalanceBox);

        var balanceGrid = new Grid { ColumnSpacing = 16 };
        balanceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        balanceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(balanceField, 0);
        Grid.SetColumn(currencyField, 1);
        balanceGrid.Children.Add(balanceField);
        balanceGrid.Children.Add(currencyField);
        balanceGrid.Visibility = account is null ? Visibility.Visible : Visibility.Collapsed;

        var inclusionSwitch = SumaDialog.CreateToggleSwitch(account?.IncludeInAvailableToSpend ?? true);

        var inclusionRow = new Grid();
        inclusionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inclusionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var inclusionTextStack = new StackPanel { Spacing = 2 };
        inclusionTextStack.Children.Add(new TextBlock { Text = "Include in available to spend", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13, Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SumaTextPrimaryBrush"] });
        inclusionTextStack.Children.Add(new TextBlock { Text = "Included accounts increase the amount you can spend.", FontSize = 12, Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SumaTextSecondaryBrush"] });
        inclusionRow.Children.Add(inclusionTextStack);
        Grid.SetColumn(inclusionSwitch, 1);
        inclusionRow.Children.Add(inclusionSwitch);

        var immutableNote = new TextBlock
        {
            Text = account is null ? string.Empty : "Currency and opening balance remain unchanged when editing.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SumaTextSecondaryBrush"],
            Visibility = account is null ? Visibility.Collapsed : Visibility.Visible
        };

        var errorText = SumaDialog.CreateErrorText();

        var content = new StackPanel { Spacing = 18, Padding = new Thickness(0, 4, 0, 8) };
        content.Children.Add(typeField);
        content.Children.Add(nameField);
        content.Children.Add(balanceGrid);
        content.Children.Add(inclusionRow);
        content.Children.Add(immutableNote);
        content.Children.Add(errorText);

        var title = account is null ? "Add account" : "Edit account";
        var primaryText = account is null ? "Add account" : "Save";
        var dialog = SumaDialog.Create(XamlRoot, title, content, primaryText, "Cancel", ModalSize.Medium);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                long openingMinor = 0;
                if (account is null && !MoneyText.TryParseMinor(openingBalanceBox.Text, out openingMinor))
                {
                    SumaDialog.SetError(errorText, "Enter an opening balance with no more than two decimal places.");
                    args.Cancel = true;
                    return;
                }

                var input = new AccountEditorInput(
                    nameBox.Text,
                    selectedType,
                    inclusionSwitch.IsOn,
                    openingMinor,
                    currencyBox.Text);
                var succeeded = account is null
                    ? await AccountsViewModel.CreateAsync(input)
                    : await AccountsViewModel.UpdateAsync(account.Id, input);
                if (!succeeded)
                {
                    SumaDialog.SetError(errorText, AccountsViewModel.ErrorMessage!);
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
        var nameBox = SumaDialog.CreateTextBox(kind == CategoryTransactionKind.Expense ? "e.g., Groceries" : "e.g., Salary", category?.Name ?? string.Empty);
        var nameField = SumaDialog.CreateField("Category name", nameBox);

        var kindText = new TextBlock
        {
            Text = kind == CategoryTransactionKind.Expense ? "Expense" : "Income",
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SumaTextPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };
        var kindBorder = new Border
        {
            Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SumaSurfaceSecondaryBrush"],
            BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SumaBorderBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = kindText
        };
        var kindField = SumaDialog.CreateField("Category kind", kindBorder, "Category kind cannot be changed after creation.");

        var parentBox = SumaDialog.CreateComboBox(CategoriesViewModel.GetParentOptions(category?.Id));
        parentBox.SelectedItem = ((IEnumerable<CategoryParentOption>)parentBox.ItemsSource)
            .First(option => option.Id == category?.ParentCategoryId);
        var parentField = SumaDialog.CreateField("Parent category (optional)", parentBox);

        var errorText = SumaDialog.CreateErrorText();

        var content = new StackPanel { Spacing = 18, Padding = new Thickness(0, 4, 0, 8) };
        content.Children.Add(kindField);
        content.Children.Add(nameField);
        content.Children.Add(parentField);
        content.Children.Add(errorText);

        var title = category is null ? "Add category" : "Edit category";
        var primaryText = category is null ? "Add category" : "Save";
        var dialog = SumaDialog.Create(XamlRoot, title, content, primaryText, "Cancel", ModalSize.Medium);
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
                    SumaDialog.SetError(errorText, CategoriesViewModel.ErrorMessage!);
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
