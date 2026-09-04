using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Suma.Desktop.ViewModels;
using Suma.Desktop.Common;
using Suma.Domain.Accounts;
using Suma.Domain.Categories;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Suma.Desktop.Pages.Accounts;

public sealed partial class AccountsPage : Page
{
    private readonly Suma.Desktop.Navigation.INavigationService? navigationService;

    public AccountsPage(
        AccountsViewModel accountsViewModel,
        CategoriesViewModel categoriesViewModel,
        Suma.Desktop.Navigation.INavigationService? navigationService = null)
    {
        AccountsViewModel = accountsViewModel;
        CategoriesViewModel = categoriesViewModel;
        this.navigationService = navigationService;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public AccountsViewModel AccountsViewModel { get; }

    public CategoriesViewModel CategoriesViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await Task.WhenAll(AccountsViewModel.LoadAsync(), CategoriesViewModel.LoadAsync());
    }

    private void OnThemeSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tagStr } && int.TryParse(tagStr, out var themeIdx))
        {
            AccountsViewModel.SetCardTheme(themeIdx);
        }
        else if (sender is FrameworkElement { Tag: int themeInt })
        {
            AccountsViewModel.SetCardTheme(themeInt);
        }
    }

    private void OnCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AccountRowViewModel account })
        {
            AccountsViewModel.SelectAccount(account);
        }
    }

    private async void OnAtsToggled(object sender, RoutedEventArgs e)
    {
        if (AccountsViewModel.SelectedAccount is { } account && sender is ToggleSwitch toggle)
        {
            if (toggle.IsOn != account.IncludeInAvailableToSpend)
            {
                await AccountsViewModel.ToggleAtsAsync(account);
            }
        }
    }

    private void OnViewAllActivityClick(object sender, RoutedEventArgs e)
    {
        navigationService?.Navigate(Suma.Desktop.Navigation.NavigationRoute.Activity);
    }

    private async void OnEditSelectedAccountClick(object sender, RoutedEventArgs e)
    {
        if (AccountsViewModel.SelectedAccount is { } account)
        {
            await ShowAccountEditorAsync(account);
        }
    }

    private async void OnArchiveSelectedAccountClick(object sender, RoutedEventArgs e)
    {
        if (AccountsViewModel.SelectedAccount is { } account)
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
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await AccountsViewModel.LoadAsync();
    }

    private async void OnAddAccountClick(object sender, RoutedEventArgs e)
    {
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

    private async void OnManageCategoriesClick(object sender, RoutedEventArgs e)
    {
        await ShowCategoryManagementDialogAsync();
    }

    private async Task ShowCategoryManagementDialogAsync()
    {
        await CategoriesViewModel.LoadAsync();

        var dialogContent = new StackPanel { Spacing = 14, MinWidth = 420 };

        var topBar = new Grid();
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var kindCombo = new ComboBox
        {
            ItemsSource = new[] { "Expense", "Income" },
            SelectedIndex = CategoriesViewModel.SelectedKind == CategoryTransactionKind.Expense ? 0 : 1,
            Height = 34,
            CornerRadius = new CornerRadius(8)
        };

        var addCategoryBtn = new Button
        {
            Content = "+ Add category",
            Style = (Style)XamlApp.Current.Resources["AccountPrimaryButtonStyle"],
            Height = 34,
            Padding = new Thickness(14, 0, 14, 0)
        };

        Grid.SetColumn(kindCombo, 0);
        Grid.SetColumn(addCategoryBtn, 1);
        topBar.Children.Add(kindCombo);
        topBar.Children.Add(addCategoryBtn);
        dialogContent.Children.Add(topBar);

        var listPanel = new StackPanel { Spacing = 4, MaxHeight = 320 };
        var scrollViewer = new ScrollViewer { Content = listPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        dialogContent.Children.Add(scrollViewer);

        Action refreshList = () =>
        {
            listPanel.Children.Clear();
            if (CategoriesViewModel.Items.Count == 0)
            {
                listPanel.Children.Add(new TextBlock
                {
                    Text = "No categories found.",
                    FontSize = 13,
                    Foreground = (Brush)XamlApp.Current.Resources["SumaTextSecondaryBrush"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 20)
                });
                return;
            }

            foreach (var item in CategoriesViewModel.Items)
            {
                var row = new Grid { Padding = new Thickness(8, 6, 8, 6), BorderBrush = (Brush)XamlApp.Current.Resources["SumaBorderBrush"], BorderThickness = new Thickness(0, 0, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nameStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
                nameStack.Children.Add(new TextBlock { Text = item.Name, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Brush)XamlApp.Current.Resources["SumaTextPrimaryBrush"] });
                if (!string.IsNullOrEmpty(item.ParentDisplay))
                {
                    nameStack.Children.Add(new TextBlock { Text = $"in {item.ParentDisplay}", FontSize = 11, Foreground = (Brush)XamlApp.Current.Resources["SumaTextSecondaryBrush"] });
                }

                var actionsStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                var editBtn = new Button { Content = "Edit", Height = 28, Padding = new Thickness(8, 0, 8, 0), Tag = item.Id };
                editBtn.Click += async (s, _) =>
                {
                    var cat = CategoriesViewModel.Items.FirstOrDefault(c => c.Id == (Guid)((Button)s).Tag);
                    if (cat != null)
                    {
                        await ShowCategoryEditorAsync(cat);
                        await CategoriesViewModel.LoadAsync();
                    }
                };

                var archiveBtn = new Button { Content = "Archive", Height = 28, Padding = new Thickness(8, 0, 8, 0), Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 199, 75, 62)), Tag = item.Id };
                archiveBtn.Click += async (s, _) =>
                {
                    var catId = (Guid)((Button)s).Tag;
                    await CategoriesViewModel.ArchiveCommand.ExecuteAsync(catId);
                };

                actionsStack.Children.Add(editBtn);
                actionsStack.Children.Add(archiveBtn);

                Grid.SetColumn(nameStack, 0);
                Grid.SetColumn(actionsStack, 1);
                row.Children.Add(nameStack);
                row.Children.Add(actionsStack);
                listPanel.Children.Add(row);
            }
        };

        refreshList();

        kindCombo.SelectionChanged += async (_, _) =>
        {
            var kind = kindCombo.SelectedIndex == 0 ? CategoryTransactionKind.Expense : CategoryTransactionKind.Income;
            await CategoriesViewModel.SetKindAsync(kind);
            refreshList();
        };

        addCategoryBtn.Click += async (_, _) =>
        {
            await ShowCategoryEditorAsync(category: null);
            await CategoriesViewModel.LoadAsync();
            refreshList();
        };

        var dialog = SumaDialog.Create(
            XamlRoot,
            "Manage Categories",
            dialogContent,
            primaryText: "Done",
            closeText: "Close",
            size: ModalSize.Medium);

        await dialog.ShowAsync();
    }

    private async Task ShowAccountEditorAsync(AccountRowViewModel? account)
    {
        var selectedType = account?.Type ?? AccountType.Bank;

        var nameBox = SumaDialog.CreateTextBox("e.g., Main Wallet", account?.Name ?? string.Empty);
        var nameField = SumaDialog.CreateField("Account name", nameBox);

        var numberPlaceholder = selectedType == AccountType.EWallet ? "e.g., 0917 123 4567" : "e.g., 1234 5678 9012";
        var numberBox = SumaDialog.CreateTextBox(numberPlaceholder, account?.AccountNumber ?? string.Empty);
        var numberField = SumaDialog.CreateField("Account or mobile number (optional)", numberBox);

        var typeSelector = SumaDialog.CreateAccountTypeSelector(selectedType, type =>
        {
            selectedType = type;
            numberBox.PlaceholderText = type == AccountType.EWallet ? "e.g., 0917 123 4567" : "e.g., 1234 5678 9012";
        });
        var typeField = SumaDialog.CreateField("Account type", typeSelector);

        var currencyBox = SumaDialog.CreateTextBox("PHP", account?.CurrencyCode ?? "PHP", maxLength: 3, casing: CharacterCasing.Upper);
        if (account is not null)
        {
            currencyBox.IsEnabled = false;
        }
        var currencyField = SumaDialog.CreateField("Currency", currencyBox);

        var initialBalanceStr = account is null ? "0.00" : (account.OpeningBalanceMinor / 100.0).ToString("F2");
        var openingBalanceBox = SumaDialog.CreateTextBox("0.00", initialBalanceStr, inputScope: InputScopeNameValue.CurrencyAmount);
        var balanceField = SumaDialog.CreateField("Opening balance", openingBalanceBox);

        var balanceGrid = new Grid { ColumnSpacing = 16 };
        balanceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        balanceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(balanceField, 0);
        Grid.SetColumn(currencyField, 1);
        balanceGrid.Children.Add(balanceField);
        balanceGrid.Children.Add(currencyField);
        balanceGrid.Visibility = Visibility.Visible;

        var inclusionSwitch = SumaDialog.CreateToggleSwitch(account?.IncludeInAvailableToSpend ?? true);

        var inclusionRow = new Grid();
        inclusionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inclusionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var inclusionTextStack = new StackPanel { Spacing = 2 };
        inclusionTextStack.Children.Add(new TextBlock { Text = "Include in available to spend", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13, Foreground = (Brush)XamlApp.Current.Resources["SumaTextPrimaryBrush"] });
        inclusionTextStack.Children.Add(new TextBlock { Text = "Included accounts increase the amount you can spend.", FontSize = 12, Foreground = (Brush)XamlApp.Current.Resources["SumaTextSecondaryBrush"] });
        inclusionRow.Children.Add(inclusionTextStack);
        Grid.SetColumn(inclusionSwitch, 1);
        inclusionRow.Children.Add(inclusionSwitch);

        var immutableNote = new TextBlock
        {
            Text = account is null ? string.Empty : "Currency cannot be changed after creation. Opening balance can be adjusted anytime.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)XamlApp.Current.Resources["SumaTextSecondaryBrush"],
            Visibility = account is null ? Visibility.Collapsed : Visibility.Visible
        };

        var errorText = SumaDialog.CreateErrorText();

        var content = new StackPanel { Spacing = 18, Padding = new Thickness(0, 4, 0, 8) };
        content.Children.Add(typeField);
        content.Children.Add(nameField);
        content.Children.Add(numberField);
        content.Children.Add(balanceGrid);
        content.Children.Add(inclusionRow);
        content.Children.Add(immutableNote);
        content.Children.Add(errorText);

        var title = account is null ? "Add account" : "Edit account";
        var primaryText = account is null ? "Add account" : "Save";
        var dialog = SumaDialog.Create(XamlRoot, title, content, primaryText, "Cancel", ModalSize.Large);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                if (!MoneyText.TryParseMinor(openingBalanceBox.Text, out var openingMinor))
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
                    currencyBox.Text,
                    numberBox.Text);
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
            Foreground = (Brush)XamlApp.Current.Resources["SumaTextPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };
        var kindBorder = new Border
        {
            Background = (Brush)XamlApp.Current.Resources["SumaSurfaceSecondaryBrush"],
            BorderBrush = (Brush)XamlApp.Current.Resources["SumaBorderBrush"],
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
        GetId(sender) is { } id
            ? AccountsViewModel.ActiveAccounts.Concat(AccountsViewModel.ArchivedAccounts).FirstOrDefault(item => item.Id == id)
            : null;

    private static Guid? GetId(object sender) => ((FrameworkElement)sender).Tag switch
    {
        Guid id => id,
        string text when Guid.TryParse(text, out var id) => id,
        _ => null
    };
}
