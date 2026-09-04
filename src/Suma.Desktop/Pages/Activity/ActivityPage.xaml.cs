using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Suma.Desktop.ViewModels;
using Suma.Desktop.Common;
using Suma.Domain.Transactions;

namespace Suma.Desktop.Pages.Activity;

public sealed partial class ActivityPage : Page
{
    private bool isUpdatingFilterControls;

    public ActivityPage(ActivityViewModel viewModel, TransactionEditorViewModel editorViewModel)
    {
        ViewModel = viewModel;
        EditorViewModel = editorViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public ActivityViewModel ViewModel { get; }

    public TransactionEditorViewModel EditorViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        UpdateResponsiveLayout(ActualWidth);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateResponsiveLayout(e.NewSize.Width);

    private void UpdateResponsiveLayout(double availableWidth)
    {
        if (availableWidth <= 0) availableWidth = ActualWidth;
        if (availableWidth <= 0) return;

        var compact = availableWidth < 900;
        var standard = availableWidth is >= 900 and < 1250;

        Grid.SetColumn(HeaderActions, compact ? 0 : 1);
        Grid.SetRow(HeaderActions, compact ? 1 : 0);
        HeaderGrid.RowDefinitions.Clear();
        HeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (compact) HeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetColumn(IncomeCard, 0); Grid.SetRow(IncomeCard, 0);
        Grid.SetColumn(ExpenseCard, standard ? 1 : compact ? 0 : 1); Grid.SetRow(ExpenseCard, compact ? 1 : 0);
        Grid.SetColumn(NetCard, standard ? 0 : compact ? 0 : 2); Grid.SetRow(NetCard, compact ? 2 : standard ? 1 : 0);
        Grid.SetColumn(CountCard, standard ? 1 : compact ? 0 : 3); Grid.SetRow(CountCard, compact ? 3 : standard ? 1 : 0);
        SummaryGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        SummaryGrid.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        SummaryGrid.ColumnDefinitions[2].Width = standard || compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        SummaryGrid.ColumnDefinitions[3].Width = standard || compact ? new GridLength(0) : new GridLength(.72, GridUnitType.Star);
        SummaryGrid.RowDefinitions[1].Height = standard || compact ? GridLength.Auto : new GridLength(0);
        while (SummaryGrid.RowDefinitions.Count < 4) SummaryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        LayoutToolbar(compact, standard);

        Grid.SetColumn(DetailPanel, compact ? 0 : 1);
        Grid.SetRow(DetailPanel, compact ? 1 : 0);
        LedgerColumn.Width = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(3, GridUnitType.Star);
        DetailColumn.Width = compact ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
        CompactDetailRow.Height = compact ? GridLength.Auto : new GridLength(0);
    }

    private void LayoutToolbar(bool compact, bool standard)
    {
        if (!compact && !standard)
        {
            ToolbarGrid.ColumnDefinitions[0].Width = new GridLength(195);
            ToolbarGrid.ColumnDefinitions[1].Width = new GridLength(145);
            ToolbarGrid.ColumnDefinitions[2].Width = new GridLength(145);
            ToolbarGrid.ColumnDefinitions[3].Width = new GridLength(130);
            ToolbarGrid.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
            ToolbarGrid.ColumnDefinitions[5].Width = GridLength.Auto;
            Place(DateRangeButton, 0, 0); Place(CategoryFilter, 1, 0); Place(AccountFilter, 2, 0); Place(TypeFilter, 3, 0); Place(SearchContainer, 4, 0); Place(AddTransactionButton, 5, 0);
            ToolbarGrid.RowDefinitions[1].Height = new GridLength(0); ToolbarGrid.RowDefinitions[2].Height = new GridLength(0);
            return;
        }
        Place(DateRangeButton, 0, 0); Place(CategoryFilter, 1, 0); Place(AccountFilter, 2, 0); Place(TypeFilter, compact ? 0 : 3, compact ? 1 : 0);
        Place(SearchContainer, compact ? 0 : 0, compact ? 2 : 1); Grid.SetColumnSpan(SearchContainer, compact ? 5 : 5);
        Place(AddTransactionButton, compact ? 5 : 5, compact ? 2 : 1);
        ToolbarGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        ToolbarGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
        ToolbarGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
        ToolbarGrid.ColumnDefinitions[3].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        ToolbarGrid.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
        ToolbarGrid.ColumnDefinitions[5].Width = GridLength.Auto;
        ToolbarGrid.RowDefinitions[1].Height = GridLength.Auto; ToolbarGrid.RowDefinitions[2].Height = compact ? GridLength.Auto : new GridLength(0);
    }

    private static void Place(FrameworkElement element, int column, int row) { Grid.SetColumn(element, column); Grid.SetRow(element, row); Grid.SetColumnSpan(element, 1); }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActivityViewModel.SelectedCategory))
        {
            if (CategoryFilter.SelectedItem as string != ViewModel.SelectedCategory)
            {
                isUpdatingFilterControls = true;
                try
                {
                    CategoryFilter.SelectedItem = ViewModel.SelectedCategory;
                }
                finally
                {
                    isUpdatingFilterControls = false;
                }
            }
        }
        else if (e.PropertyName == nameof(ActivityViewModel.SelectedAccount))
        {
            if (AccountFilter.SelectedItem as string != ViewModel.SelectedAccount)
            {
                isUpdatingFilterControls = true;
                try
                {
                    AccountFilter.SelectedItem = ViewModel.SelectedAccount;
                }
                finally
                {
                    isUpdatingFilterControls = false;
                }
            }
        }
        else if (e.PropertyName == nameof(ActivityViewModel.SelectedType))
        {
            isUpdatingFilterControls = true;
            try
            {
                var targetTag = ViewModel.SelectedType?.ToString() ?? "All";
                for (int i = 0; i < TypeFilter.Items.Count; i++)
                {
                    if (TypeFilter.Items[i] is ComboBoxItem { Tag: string t } && t == targetTag)
                    {
                        TypeFilter.SelectedIndex = i;
                        break;
                    }
                }
            }
            finally
            {
                isUpdatingFilterControls = false;
            }
        }
    }

    private async void OnTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingFilterControls) return;
        if (TypeFilter.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        await ViewModel.SetFilterAsync(tag == "All" ? null : Enum.Parse<TransactionType>(tag));
    }

    private async void OnSearchButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SearchAsync(SearchBox.Text);
    }

    private async void OnSearchBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            await ViewModel.SearchAsync(SearchBox.Text);
        }
    }

    private void OnSearchBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SearchBox.Text) && !string.IsNullOrWhiteSpace(ViewModel.SearchText))
        {
            ViewModel.SetSearch(string.Empty);
        }
    }

    private void OnCurrencySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingFilterControls) return;
        if ((sender as ComboBox)?.SelectedItem is string currency)
        {
            ViewModel.SetCurrency(currency);
        }
    }

    private void OnAccountSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingFilterControls) return;
        if ((sender as ComboBox)?.SelectedItem is string account)
        {
            ViewModel.SetAccount(account);
        }
    }

    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingFilterControls) return;
        if ((sender as ComboBox)?.SelectedItem is string category)
        {
            ViewModel.SetCategory(category);
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private void OnDateRangeClick(object sender, RoutedEventArgs e)
    {
        if (DateRangeButton.Flyout is not null)
        {
            DateRangeButton.Flyout.ShowAt(DateRangeButton);
        }
    }

    private void OnDateRangePresetSelected(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag as string;
        if (!string.IsNullOrWhiteSpace(tag))
        {
            ViewModel.SetDateRange(tag);
            DateRangeFlyout?.Hide();
        }
    }

    private void OnApplyCustomDateRangeClick(object sender, RoutedEventArgs e)
    {
        if (StartDatePicker.Date.HasValue && EndDatePicker.Date.HasValue)
        {
            var start = DateOnly.FromDateTime(StartDatePicker.Date.Value.DateTime);
            var end = DateOnly.FromDateTime(EndDatePicker.Date.Value.DateTime);
            if (start > end)
            {
                (start, end) = (end, start);
            }
            ViewModel.SetDateRange(start, end);
            DateRangeFlyout?.Hide();
        }
        else if (StartDatePicker.Date.HasValue)
        {
            var start = DateOnly.FromDateTime(StartDatePicker.Date.Value.DateTime);
            ViewModel.SetDateRange(start, DateOnly.FromDateTime(DateTime.Today));
            DateRangeFlyout?.Hide();
        }
    }

    private async void OnResetFiltersClick(object sender, RoutedEventArgs e)
    {
        isUpdatingFilterControls = true;
        try
        {
            SearchBox.Text = string.Empty;
            TypeFilter.SelectedIndex = 0;
            CategoryFilter.SelectedItem = "All categories";
            AccountFilter.SelectedItem = "All accounts";
        }
        finally
        {
            isUpdatingFilterControls = false;
        }

        await ViewModel.ResetFiltersAsync();
    }

    private void OnToggleSortClick(object sender, RoutedEventArgs e) => ViewModel.ToggleSortOrder();

    private void OnViewMoreClick(object sender, RoutedEventArgs e)
    {
    }

    private async void OnEditTransactionClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem is null) return;
        var notesBox = TextBox("Notes", ViewModel.SelectedItem.NotesDisplay, acceptsReturn: true);
        var dialog = Dialog("Edit transaction", Stack(notesBox), "Save", ModalSize.Medium);
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.LoadAsync();
        }
    }

    private async void OnDuplicateTransactionClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem is null) return;
        await ViewModel.DuplicateSelectedAsync();
    }

    private void OnCopyIdClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem is null) return;
        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.SetText(ViewModel.SelectedItem.ReferenceCode);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
    }

    private async void OnDeleteTransactionClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem is null) return;
        var item = ViewModel.SelectedItem;
        var confirmText = new TextBlock
        {
            Text = $"Are you sure you want to delete \"{item.Title}\" ({item.AmountDisplay}) from the activity ledger? This action cannot be undone.",
            TextWrapping = TextWrapping.Wrap
        };
        var error = ErrorText();
        var dialog = Dialog("Delete transaction", Stack(confirmText, error), "Delete", ModalSize.Compact);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            dialog.IsPrimaryButtonEnabled = false;
            try
            {
                var succeeded = await ViewModel.DeleteTransactionAsync(item.Id);
                if (!succeeded)
                {
                    Reject(args, error, ViewModel.ErrorMessage ?? "Could not delete transaction.");
                }
            }
            finally
            {
                dialog.IsPrimaryButtonEnabled = true;
                deferral.Complete();
            }
        };
        _ = await dialog.ShowAsync();
    }

    private async void OnAddTagClick(object sender, RoutedEventArgs e)
    {
        var tagBox = TextBox("Tag name", "Personal");
        var dialog = Dialog("Add tag", Stack(tagBox), "Add", ModalSize.Compact);
        _ = await dialog.ShowAsync();
    }

    private async void OnQuickActionAddToBudget(object sender, RoutedEventArgs e)
    {
        var text = new TextBlock { Text = $"Transaction '{ViewModel.SelectedItem?.Title}' added to budget allocation." };
        var dialog = Dialog("Add to budget", text, "OK", ModalSize.Compact);
        _ = await dialog.ShowAsync();
    }

    private async void OnQuickActionSplit(object sender, RoutedEventArgs e)
    {
        var text = new TextBlock { Text = "Split this transaction across multiple categories or accounts." };
        var dialog = Dialog("Split transaction", text, "Continue", ModalSize.Compact);
        _ = await dialog.ShowAsync();
    }

    private async void OnQuickActionRecurring(object sender, RoutedEventArgs e)
    {
        var text = new TextBlock { Text = $"Mark '{ViewModel.SelectedItem?.Title}' as a recurring monthly bill?" };
        var dialog = Dialog("Mark as recurring", text, "Mark recurring", ModalSize.Compact);
        _ = await dialog.ShowAsync();
    }

    private async void OnQuickActionExportReceipt(object sender, RoutedEventArgs e)
    {
        var text = new TextBlock { Text = $"Receipt for {ViewModel.SelectedItem?.ReferenceCode} exported to clipboard." };
        if (ViewModel.SelectedItem is not null)
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText($"Suma Receipt\nRef: {ViewModel.SelectedItem.ReferenceCode}\nDate: {ViewModel.SelectedItem.DateTimeDisplay}\nMerchant: {ViewModel.SelectedItem.Title}\nAmount: {ViewModel.SelectedItem.FormattedDetailAmount}\nAccount: {ViewModel.SelectedItem.PrimaryAccountName}\nStatus: Cleared");
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        }
        var dialog = Dialog("Export receipt", text, "Done", ModalSize.Compact);
        _ = await dialog.ShowAsync();
    }

    private async void OnAddTransactionClick(object sender, RoutedEventArgs e)
    {
        if (!await EditorViewModel.LoadAsync())
        {
            ViewModel.SetError(EditorViewModel.ErrorMessage!);
            return;
        }

        var typeBox = Combo("Transaction type", Enum.GetValues<TransactionType>());
        typeBox.SelectedItem = TransactionType.Expense;
        var chooser = Dialog("Add transaction", Stack(typeBox), "Continue", ModalSize.Medium);
        if (await chooser.ShowAsync() != ContentDialogResult.Primary || typeBox.SelectedItem is not TransactionType type)
        {
            return;
        }

        switch (type)
        {
            case TransactionType.Expense:
            case TransactionType.Income:
                await ShowAccountTransactionEditorAsync(type);
                break;
            case TransactionType.Transfer:
                await ShowTransferEditorAsync();
                break;
            case TransactionType.Refund:
                await ShowRefundEditorAsync();
                break;
        }
    }

    private async Task ShowAccountTransactionEditorAsync(TransactionType type)
    {
        var accountBox = Combo("Account", EditorViewModel.Accounts);
        var categories = type == TransactionType.Expense ? EditorViewModel.ExpenseCategories : EditorViewModel.IncomeCategories;
        var categoryBox = Combo("Category", categories);
        accountBox.DisplayMemberPath = nameof(TransactionAccountOption.Display);
        categoryBox.DisplayMemberPath = nameof(TransactionCategoryOption.Display);
        accountBox.SelectedIndex = EditorViewModel.Accounts.Count > 0 ? 0 : -1;
        categoryBox.SelectedIndex = categories.Count > 0 ? 0 : -1;
        var amountBox = AmountBox();
        var datePicker = DatePicker();
        var descriptionBox = TextBox("Description (optional)", type == TransactionType.Expense ? "Groceries" : "Salary");
        var notesBox = TextBox("Notes (optional)", string.Empty, acceptsReturn: true);
        var error = ErrorText();
        var content = Stack(accountBox, categoryBox, amountBox, datePicker, descriptionBox, notesBox, error);
        var dialog = Dialog($"Add {type.ToString().ToLowerInvariant()}", content, size: ModalSize.Large);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            dialog.IsPrimaryButtonEnabled = false;
            try
            {
                if (accountBox.SelectedItem is not TransactionAccountOption account)
                {
                    Reject(args, error, "Choose an account.");
                    return;
                }

                if (categoryBox.SelectedItem is not TransactionCategoryOption category)
                {
                    Reject(args, error, $"Choose an {type.ToString().ToLowerInvariant()} category.");
                    return;
                }

                if (!TryAmount(amountBox, out var amount))
                {
                    Reject(args, error, "Enter a positive amount with no more than two decimal places.");
                    return;
                }

                var date = DateOnly.FromDateTime(datePicker.Date.DateTime);
                var succeeded = type == TransactionType.Expense
                    ? await ViewModel.CreateExpenseAsync(new(account.Id, category.Id, amount, account.CurrencyCode, date, descriptionBox.Text, notesBox.Text))
                    : await ViewModel.CreateIncomeAsync(new(account.Id, category.Id, amount, account.CurrencyCode, date, descriptionBox.Text, notesBox.Text));
                if (!succeeded)
                {
                    Reject(args, error, ViewModel.ErrorMessage!);
                }
            }
            finally
            {
                dialog.IsPrimaryButtonEnabled = true;
                deferral.Complete();
            }
        };
        _ = await dialog.ShowAsync();
    }

    private async Task ShowTransferEditorAsync()
    {
        var sourceBox = Combo("From account", EditorViewModel.Accounts);
        var destinationBox = Combo("To account", Array.Empty<TransactionAccountOption>());
        sourceBox.DisplayMemberPath = destinationBox.DisplayMemberPath = nameof(TransactionAccountOption.Display);
        sourceBox.SelectionChanged += (_, _) =>
        {
            var source = sourceBox.SelectedItem as TransactionAccountOption;
            destinationBox.ItemsSource = source is null
                ? Array.Empty<TransactionAccountOption>()
                : EditorViewModel.Accounts.Where(account => account.Id != source.Id && account.CurrencyCode == source.CurrencyCode).ToArray();
            destinationBox.SelectedIndex = ((IEnumerable<TransactionAccountOption>)destinationBox.ItemsSource).Any() ? 0 : -1;
        };
        sourceBox.SelectedIndex = EditorViewModel.Accounts.Count > 0 ? 0 : -1;
        var amountBox = AmountBox();
        var datePicker = DatePicker();
        var descriptionBox = TextBox("Description (optional)", "Transfer");
        var notesBox = TextBox("Notes (optional)", string.Empty, true);
        var error = ErrorText();
        var dialog = Dialog("Add transfer", Stack(sourceBox, destinationBox, amountBox, datePicker, descriptionBox, notesBox, error), size: ModalSize.Large);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            dialog.IsPrimaryButtonEnabled = false;
            try
            {
                if (sourceBox.SelectedItem is not TransactionAccountOption source || destinationBox.SelectedItem is not TransactionAccountOption destination)
                {
                    Reject(args, error, "Choose two different accounts that use the same currency.");
                    return;
                }

                if (!TryAmount(amountBox, out var amount))
                {
                    Reject(args, error, "Enter a positive amount with no more than two decimal places.");
                    return;
                }

                if (!await ViewModel.CreateTransferAsync(new(source.Id, destination.Id, amount, source.CurrencyCode, DateOnly.FromDateTime(datePicker.Date.DateTime), descriptionBox.Text, notesBox.Text)))
                {
                    Reject(args, error, ViewModel.ErrorMessage!);
                }
            }
            finally
            {
                dialog.IsPrimaryButtonEnabled = true;
                deferral.Complete();
            }
        };
        _ = await dialog.ShowAsync();
    }

    private async Task ShowRefundEditorAsync()
    {
        if (!await ViewModel.LoadRefundableExpensesAsync())
        {
            return;
        }

        var expenseBox = Combo("Original expense", ViewModel.RefundableExpenses);
        expenseBox.DisplayMemberPath = nameof(RefundableExpenseOption.Display);
        var destinationBox = Combo("Destination account", Array.Empty<TransactionAccountOption>());
        destinationBox.DisplayMemberPath = nameof(TransactionAccountOption.Display);
        expenseBox.SelectionChanged += (_, _) =>
        {
            var selected = (expenseBox.SelectedItem as RefundableExpenseOption)?.Expense;
            destinationBox.ItemsSource = selected is null
                ? Array.Empty<TransactionAccountOption>()
                : EditorViewModel.Accounts.Where(account => account.CurrencyCode == selected.CurrencyCode).ToArray();
            destinationBox.SelectedIndex = ((IEnumerable<TransactionAccountOption>)destinationBox.ItemsSource).Any() ? 0 : -1;
        };
        expenseBox.SelectedIndex = ViewModel.RefundableExpenses.Count > 0 ? 0 : -1;
        var amountBox = AmountBox();
        var datePicker = DatePicker();
        var descriptionBox = TextBox("Description (optional)", "Refund");
        var notesBox = TextBox("Notes (optional)", string.Empty, true);
        var error = ErrorText();
        var dialog = Dialog("Add refund", Stack(expenseBox, destinationBox, amountBox, datePicker, descriptionBox, notesBox, error), size: ModalSize.Large);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            dialog.IsPrimaryButtonEnabled = false;
            try
            {
                if (expenseBox.SelectedItem is not RefundableExpenseOption expense || destinationBox.SelectedItem is not TransactionAccountOption destination)
                {
                    Reject(args, error, "Choose a refundable expense and destination account.");
                    return;
                }

                if (!TryAmount(amountBox, out var amount))
                {
                    Reject(args, error, "Enter a positive amount with no more than two decimal places.");
                    return;
                }

                if (!await ViewModel.CreateRefundAsync(new(expense.Id, destination.Id, expense.Expense.CategoryId, amount, expense.Expense.CurrencyCode, DateOnly.FromDateTime(datePicker.Date.DateTime), descriptionBox.Text, notesBox.Text)))
                {
                    Reject(args, error, ViewModel.ErrorMessage!);
                }
            }
            finally
            {
                dialog.IsPrimaryButtonEnabled = true;
                deferral.Complete();
            }
        };
        _ = await dialog.ShowAsync();
    }

    private ContentDialog Dialog(string title, UIElement content, string primaryText = "Save", ModalSize size = ModalSize.Medium) =>
        SumaDialog.Create(XamlRoot, title, content, primaryText, "Cancel", size);

    private static ComboBox Combo(string header, object items)
    {
        var cb = SumaDialog.CreateComboBox(items);
        cb.Header = header;
        return cb;
    }

    private static TextBox AmountBox()
    {
        var tb = SumaDialog.CreateTextBox("0.00", inputScope: InputScopeNameValue.CurrencyAmount);
        tb.Header = "Amount";
        return tb;
    }

    private static DatePicker DatePicker()
    {
        var dp = SumaDialog.CreateDatePicker();
        dp.Header = "Date";
        return dp;
    }

    private static TextBox TextBox(string header, string placeholder, bool acceptsReturn = false)
    {
        var tb = SumaDialog.CreateTextBox(placeholder, acceptsReturn: acceptsReturn);
        tb.Header = header;
        return tb;
    }

    private static TextBlock ErrorText() => SumaDialog.CreateErrorText();

    private static StackPanel Stack(params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = 16, Padding = new Thickness(0, 4, 0, 8) };
        foreach (var child in children)
        {
            if (child is Control c)
            {
                c.RequestedTheme = ElementTheme.Light;
            }
            panel.Children.Add(child);
        }
        return panel;
    }

    private static bool TryAmount(TextBox amountBox, out long amount) => MoneyText.TryParseMinor(amountBox.Text, out amount) && amount > 0;

    private static void Reject(ContentDialogButtonClickEventArgs args, TextBlock error, string message)
    {
        args.Cancel = true;
        SumaDialog.SetError(error, message);
    }

}
