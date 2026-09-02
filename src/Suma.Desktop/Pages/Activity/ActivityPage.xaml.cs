using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Suma.Desktop.ViewModels;
using Suma.Domain.Transactions;

namespace Suma.Desktop.Pages.Activity;

public sealed partial class ActivityPage : Page
{
    public ActivityPage(ActivityViewModel viewModel, TransactionEditorViewModel editorViewModel)
    {
        ViewModel = viewModel;
        EditorViewModel = editorViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public ActivityViewModel ViewModel { get; }

    public TransactionEditorViewModel EditorViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private async void OnAllFilterClick(object sender, RoutedEventArgs e) => await ApplyFilterAsync(null, AllFilter);

    private async void OnExpenseFilterClick(object sender, RoutedEventArgs e) => await ApplyFilterAsync(TransactionType.Expense, ExpenseFilter);

    private async void OnIncomeFilterClick(object sender, RoutedEventArgs e) => await ApplyFilterAsync(TransactionType.Income, IncomeFilter);

    private async void OnTransferFilterClick(object sender, RoutedEventArgs e) => await ApplyFilterAsync(TransactionType.Transfer, TransferFilter);

    private async void OnRefundFilterClick(object sender, RoutedEventArgs e) => await ApplyFilterAsync(TransactionType.Refund, RefundFilter);

    private async Task ApplyFilterAsync(TransactionType? type, ToggleButton selected)
    {
        foreach (var button in new[] { AllFilter, ExpenseFilter, IncomeFilter, TransferFilter, RefundFilter })
        {
            SetToggleState(button, button == selected);
        }

        await ViewModel.SetFilterAsync(type);
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
        var chooser = Dialog("Add transaction", typeBox, "Continue");
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
        var dialog = Dialog($"Add {type.ToString().ToLowerInvariant()}", content);
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
        var dialog = Dialog("Add transfer", Stack(sourceBox, destinationBox, amountBox, datePicker, descriptionBox, notesBox, error));
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
        var dialog = Dialog("Add refund", Stack(expenseBox, destinationBox, amountBox, datePicker, descriptionBox, notesBox, error));
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

    private ContentDialog Dialog(string title, UIElement content, string primaryText = "Save") => new()
    {
        Title = title,
        Content = new ScrollViewer
        {
            Content = content,
            MaxHeight = 400,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        },
        PrimaryButtonText = primaryText,
        CloseButtonText = "Cancel",
        DefaultButton = ContentDialogButton.Primary,
        XamlRoot = XamlRoot
    };

    private static ComboBox Combo(string header, object items) => new() { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = items };

    private static TextBox AmountBox() => new() { Header = "Amount", PlaceholderText = "0.00", InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.CurrencyAmount) } } };

    private static DatePicker DatePicker() => new() { Header = "Date", HorizontalAlignment = HorizontalAlignment.Stretch, Date = DateTimeOffset.Now };

    private static TextBox TextBox(string header, string placeholder, bool acceptsReturn = false) => new() { Header = header, PlaceholderText = placeholder, AcceptsReturn = acceptsReturn, TextWrapping = TextWrapping.Wrap };

    private static TextBlock ErrorText() => new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

    private static StackPanel Stack(params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = 12 };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    private static bool TryAmount(TextBox amountBox, out long amount) => MoneyText.TryParseMinor(amountBox.Text, out amount) && amount > 0;

    private static void Reject(ContentDialogButtonClickEventArgs args, TextBlock error, string message)
    {
        args.Cancel = true;
        error.Text = message;
        error.Visibility = Visibility.Visible;
    }

    private static void SetToggleState(ToggleButton button, bool selected)
    {
        button.IsChecked = selected;
        button.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources[selected ? "SumaNavigationItemSelectedStyle" : "SumaNavigationItemStyle"];
    }
}
