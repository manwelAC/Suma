using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Suma.Desktop.ViewModels;
using Suma.Application.Recurring.CreateRecurringTransaction;
using Suma.Domain.Recurring;
using Suma.Domain.Transactions;

namespace Suma.Desktop.Pages.Planning;

public sealed partial class PlanningPage : Page
{
    public PlanningPage(PlanningViewModel viewModel, BudgetEditorViewModel editorViewModel, RecurringViewModel recurringViewModel, RecurringEditorViewModel recurringEditorViewModel)
    {
        ViewModel = viewModel;
        EditorViewModel = editorViewModel;
        RecurringViewModel = recurringViewModel;
        RecurringEditorViewModel = recurringEditorViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public PlanningViewModel ViewModel { get; }

    public BudgetEditorViewModel EditorViewModel { get; }

    public RecurringViewModel RecurringViewModel { get; }

    public RecurringEditorViewModel RecurringEditorViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        BudgetList.SelectedItem = ViewModel.SelectedBudget;
    }

    private async void OnActiveBudgetsClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(ActiveBudgetsButton, true);
        SetToggleState(ArchivedBudgetsButton, false);
        await ViewModel.SetArchivedViewAsync(false);
        BudgetList.SelectedItem = ViewModel.SelectedBudget;
    }

    private async void OnArchivedBudgetsClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(ActiveBudgetsButton, false);
        SetToggleState(ArchivedBudgetsButton, true);
        await ViewModel.SetArchivedViewAsync(true);
        BudgetList.SelectedItem = ViewModel.SelectedBudget;
    }

    private async void OnBudgetItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is BudgetRowViewModel budget)
        {
            BudgetList.SelectedItem = budget;
            await ViewModel.SelectBudgetAsync(budget.Id);
        }
    }

    private async void OnNewBudgetClick(object sender, RoutedEventArgs e) => await ShowBudgetEditorAsync();

    private void OnBudgetsSectionClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(BudgetsSectionButton, true);
        SetToggleState(RecurringSectionButton, false);
        BudgetSection.Visibility = Visibility.Visible;
        RecurringSection.Visibility = Visibility.Collapsed;
        NewBudgetButton.Visibility = ViewModel.NewBudgetVisibility;
    }

    private async void OnRecurringSectionClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(BudgetsSectionButton, false);
        SetToggleState(RecurringSectionButton, true);
        BudgetSection.Visibility = Visibility.Collapsed;
        RecurringSection.Visibility = Visibility.Visible;
        NewBudgetButton.Visibility = Visibility.Collapsed;
        await RecurringViewModel.LoadAsync();
    }

    private async void OnUpcomingOccurrencesClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(UpcomingOccurrencesButton, true);
        SetToggleState(HistoryOccurrencesButton, false);
        await RecurringViewModel.SetHistoryAsync(false);
    }

    private async void OnHistoryOccurrencesClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(UpcomingOccurrencesButton, false);
        SetToggleState(HistoryOccurrencesButton, true);
        await RecurringViewModel.SetHistoryAsync(true);
    }

    private async void OnMarkPaidClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RecurringOccurrenceRowViewModel occurrence }) await RecurringViewModel.MarkPaidAsync(occurrence);
    }

    private async void OnSkipOccurrenceClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RecurringOccurrenceRowViewModel occurrence }) await RecurringViewModel.SkipAsync(occurrence);
    }

    private async void OnNewRecurringClick(object sender, RoutedEventArgs e) => await ShowRecurringEditorAsync();

    private async Task ShowRecurringEditorAsync()
    {
        if (!await RecurringEditorViewModel.LoadAsync())
        {
            RecurringViewModel.SetError(RecurringEditorViewModel.ErrorMessage!);
            return;
        }

        if (RecurringEditorViewModel.Accounts.Count == 0)
        {
            RecurringViewModel.SetError("Create an active account before adding a recurring transaction.");
            return;
        }

        var typeBox = new ComboBox { Header = "Type", ItemsSource = new[] { TransactionType.Expense, TransactionType.Income, TransactionType.Transfer }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var sourceBox = new ComboBox { Header = "Source account", ItemsSource = RecurringEditorViewModel.Accounts, DisplayMemberPath = nameof(RecurringAccountOption.Display), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var destinationBox = new ComboBox { Header = "Destination account", ItemsSource = RecurringEditorViewModel.Accounts, DisplayMemberPath = nameof(RecurringAccountOption.Display), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, Visibility = Visibility.Collapsed };
        var categoryBox = new ComboBox { Header = "Expense category", ItemsSource = RecurringEditorViewModel.ExpenseCategories, DisplayMemberPath = nameof(RecurringCategoryOption.Display), SelectedIndex = RecurringEditorViewModel.ExpenseCategories.Count > 0 ? 0 : -1, HorizontalAlignment = HorizontalAlignment.Stretch };
        var amountBox = new TextBox { Header = "Amount", PlaceholderText = "0.00", InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.CurrencyAmount) } } };
        var descriptionBox = new TextBox { Header = "Description", PlaceholderText = "Optional schedule name" };
        var frequencyBox = new ComboBox { Header = "Frequency", ItemsSource = Enum.GetValues<RecurrenceFrequencyUnit>(), SelectedIndex = 2, HorizontalAlignment = HorizontalAlignment.Stretch };
        var intervalBox = new NumberBox { Header = "Repeat every", Minimum = 1, Maximum = 365, Value = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var dayOfWeekBox = new ComboBox { Header = "Day of week", ItemsSource = Enum.GetValues<DayOfWeek>(), SelectedItem = DayOfWeek.Monday, HorizontalAlignment = HorizontalAlignment.Stretch, Visibility = Visibility.Collapsed };
        var dayOfMonthBox = new NumberBox { Header = "Day of month", Minimum = 1, Maximum = 31, Value = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var monthOfYearBox = new NumberBox { Header = "Month of year", Minimum = 1, Maximum = 12, Value = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Visibility = Visibility.Collapsed };
        var startBox = new DatePicker { Header = "Start date", Date = DateTimeOffset.Now };
        var error = ErrorText();

        void ApplyType()
        {
            var type = (TransactionType)typeBox.SelectedItem;
            sourceBox.Visibility = type == TransactionType.Income ? Visibility.Collapsed : Visibility.Visible;
            destinationBox.Visibility = type == TransactionType.Expense ? Visibility.Collapsed : Visibility.Visible;
            categoryBox.Visibility = type == TransactionType.Transfer ? Visibility.Collapsed : Visibility.Visible;
            categoryBox.Header = type == TransactionType.Income ? "Income category" : "Expense category";
            categoryBox.ItemsSource = type == TransactionType.Income ? RecurringEditorViewModel.IncomeCategories : RecurringEditorViewModel.ExpenseCategories;
            categoryBox.SelectedIndex = categoryBox.Items.Count > 0 ? 0 : -1;
        }

        void ApplyFrequency()
        {
            var frequency = (RecurrenceFrequencyUnit)frequencyBox.SelectedItem;
            dayOfWeekBox.Visibility = frequency == RecurrenceFrequencyUnit.Week ? Visibility.Visible : Visibility.Collapsed;
            dayOfMonthBox.Visibility = frequency is RecurrenceFrequencyUnit.Month or RecurrenceFrequencyUnit.Year ? Visibility.Visible : Visibility.Collapsed;
            monthOfYearBox.Visibility = frequency == RecurrenceFrequencyUnit.Year ? Visibility.Visible : Visibility.Collapsed;
        }

        typeBox.SelectionChanged += (_, _) => ApplyType();
        frequencyBox.SelectionChanged += (_, _) => ApplyFrequency();
        ApplyType(); ApplyFrequency();
        var dialog = Dialog("New recurring transaction", DialogContent(typeBox, sourceBox, destinationBox, categoryBox, amountBox, descriptionBox, frequencyBox, intervalBox, dayOfWeekBox, dayOfMonthBox, monthOfYearBox, startBox, error));
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            dialog.IsPrimaryButtonEnabled = false;
            try
            {
                if (!MoneyText.TryParseMinor(amountBox.Text, out var amount) || amount <= 0)
                {
                    Reject(args, error, "Enter a valid positive amount and start date."); return;
                }
                var startValue = startBox.Date;
                var type = (TransactionType)typeBox.SelectedItem;
                var frequency = (RecurrenceFrequencyUnit)frequencyBox.SelectedItem;
                if (!TryGetWholeNumber(intervalBox.Value, 1, 365, out var interval))
                {
                    Reject(args, error, "Enter a valid repeat interval."); return;
                }
                var dayOfMonth = default(int);
                var monthOfYear = default(int);
                if (frequency is RecurrenceFrequencyUnit.Month or RecurrenceFrequencyUnit.Year
                    && !TryGetWholeNumber(dayOfMonthBox.Value, 1, 31, out dayOfMonth))
                {
                    Reject(args, error, "Enter a valid day of month."); return;
                }
                if (frequency == RecurrenceFrequencyUnit.Year
                    && !TryGetWholeNumber(monthOfYearBox.Value, 1, 12, out monthOfYear))
                {
                    Reject(args, error, "Enter a valid month of year."); return;
                }
                var schedule = new RecurringScheduleInput(amount, frequency, interval, DateOnly.FromDateTime(startValue.DateTime), null,
                    frequency == RecurrenceFrequencyUnit.Week ? (DayOfWeek?)dayOfWeekBox.SelectedItem : null,
                    frequency is RecurrenceFrequencyUnit.Month or RecurrenceFrequencyUnit.Year ? dayOfMonth : null,
                    frequency == RecurrenceFrequencyUnit.Year ? monthOfYear : null,
                    descriptionBox.Text, null);
                var saved = type switch
                {
                    TransactionType.Expense when sourceBox.SelectedItem is RecurringAccountOption source && categoryBox.SelectedItem is RecurringCategoryOption category => await RecurringViewModel.CreateExpenseAsync(new(source.Id, category.Id, schedule)),
                    TransactionType.Income when destinationBox.SelectedItem is RecurringAccountOption destination && categoryBox.SelectedItem is RecurringCategoryOption category => await RecurringViewModel.CreateIncomeAsync(new(destination.Id, category.Id, schedule)),
                    TransactionType.Transfer when sourceBox.SelectedItem is RecurringAccountOption source && destinationBox.SelectedItem is RecurringAccountOption destination => await RecurringViewModel.CreateTransferAsync(new(source.Id, destination.Id, schedule)),
                    _ => false
                };
                if (!saved) Reject(args, error, RecurringViewModel.ErrorMessage ?? "Choose the required account and category options.");
            }
            finally { dialog.IsPrimaryButtonEnabled = true; deferral.Complete(); }
        };
        _ = await dialog.ShowAsync();
    }

    private async void OnAddAllocationClick(object sender, RoutedEventArgs e) => await ShowAllocationEditorAsync();

    private async void OnArchiveBudgetClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ArchiveAsync();
        BudgetList.SelectedItem = ViewModel.SelectedBudget;
    }

    private async void OnRestoreBudgetClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RestoreAsync();
        if (!ViewModel.ShowArchived)
        {
            SetToggleState(ActiveBudgetsButton, true);
            SetToggleState(ArchivedBudgetsButton, false);
        }

        BudgetList.SelectedItem = ViewModel.SelectedBudget;
    }

    private async Task ShowBudgetEditorAsync()
    {
        var nameBox = new TextBox { Header = "Name", PlaceholderText = "September Budget" };
        var startPicker = new DatePicker
        {
            Header = "Period start",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Date = DateTimeOffset.Now
        };
        var endPicker = new DatePicker
        {
            Header = "Period end",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Date = DateTimeOffset.Now
        };
        var currencyBox = new TextBox
        {
            Header = "Currency",
            CharacterCasing = CharacterCasing.Upper,
            MaxLength = 3,
            PlaceholderText = "PHP",
            Text = "PHP"
        };
        var incomeBox = new TextBox
        {
            Header = "Expected income",
            InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.CurrencyAmount) } },
            PlaceholderText = "0.00",
            Text = "0.00"
        };
        var planningNote = new TextBlock
        {
            Text = "Expected income is planning context only. It does not create a transaction or change an account balance.",
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["SumaBodySecondaryTextStyle"]
        };
        var error = ErrorText();
        var content = DialogContent(nameBox, startPicker, endPicker, currencyBox, incomeBox, planningNote, error);
        var dialog = Dialog("New budget", content);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            dialog.IsPrimaryButtonEnabled = false;
            try
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    Reject(args, error, "Enter a budget name.");
                    return;
                }

                var start = DateOnly.FromDateTime(startPicker.Date.DateTime);
                var end = DateOnly.FromDateTime(endPicker.Date.DateTime);
                if (end < start)
                {
                    Reject(args, error, "End date must be on or after the start date.");
                    return;
                }

                var currency = currencyBox.Text.Trim().ToUpperInvariant();
                if (currency.Length != 3 || !currency.All(char.IsLetter))
                {
                    Reject(args, error, "Enter a three-letter currency code.");
                    return;
                }

                if (!MoneyText.TryParseMinor(incomeBox.Text, out var expectedIncome) || expectedIncome < 0)
                {
                    Reject(args, error, "Enter a valid expected income.");
                    return;
                }

                if (!await ViewModel.CreateAsync(new(nameBox.Text, start, end, expectedIncome, currency)))
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
        BudgetList.SelectedItem = ViewModel.SelectedBudget;
    }

    private async Task ShowAllocationEditorAsync()
    {
        if (ViewModel.SelectedBudget is null)
        {
            return;
        }

        if (!await EditorViewModel.LoadExpenseCategoriesAsync())
        {
            ViewModel.SetError(EditorViewModel.ErrorMessage!);
            return;
        }

        var allocatedIds = ViewModel.Allocations.Select(item => item.CategoryId).ToHashSet();
        var available = EditorViewModel.ExpenseCategories.Where(category => !allocatedIds.Contains(category.Id)).ToArray();
        if (available.Length == 0)
        {
            ViewModel.SetError("All active expense categories are already allocated to this budget.");
            return;
        }

        var categoryBox = new ComboBox
        {
            Header = "Expense category",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = available,
            DisplayMemberPath = nameof(BudgetCategoryOption.Display),
            SelectedIndex = 0
        };
        var amountBox = new TextBox
        {
            Header = $"Amount ({ViewModel.SelectedBudget.CurrencyCode})",
            InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.CurrencyAmount) } },
            PlaceholderText = "0.00"
        };
        var reserveBox = new CheckBox
        {
            Content = "Reserve this allocation from Available-to-Spend"
        };
        var reserveNote = new TextBlock
        {
            Text = "Marks this allocation as protected for future Available-to-Spend calculations. M13 does not calculate Available-to-Spend.",
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["SumaBodySecondaryTextStyle"]
        };
        var error = ErrorText();
        var dialog = Dialog("Add allocation", DialogContent(categoryBox, amountBox, reserveBox, reserveNote, error));
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            dialog.IsPrimaryButtonEnabled = false;
            try
            {
                if (categoryBox.SelectedItem is not BudgetCategoryOption category)
                {
                    Reject(args, error, "Choose an expense category.");
                    return;
                }

                if (!MoneyText.TryParseMinor(amountBox.Text, out var amount) || amount <= 0)
                {
                    Reject(args, error, "Enter a valid allocation amount.");
                    return;
                }

                if (!await ViewModel.AddAllocationAsync(new(category.Id, amount, reserveBox.IsChecked == true)))
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

    private ContentDialog Dialog(string title, UIElement content) => new()
    {
        Title = title,
        Content = content,
        PrimaryButtonText = "Save",
        CloseButtonText = "Cancel",
        DefaultButton = ContentDialogButton.Primary,
        XamlRoot = XamlRoot
    };

    private static ScrollViewer DialogContent(params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = 12 };
        foreach (var child in children) panel.Children.Add(child);
        return new ScrollViewer
        {
            MaxHeight = 420,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = panel
        };
    }

    private static TextBlock ErrorText() => new()
    {
        TextWrapping = TextWrapping.Wrap,
        Visibility = Visibility.Collapsed
    };

    private static void Reject(ContentDialogButtonClickEventArgs args, TextBlock error, string message)
    {
        args.Cancel = true;
        error.Text = message;
        error.Visibility = Visibility.Visible;
    }

    private static void SetToggleState(ToggleButton button, bool selected)
    {
        button.IsChecked = selected;
        button.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources[
            selected ? "SumaNavigationItemSelectedStyle" : "SumaNavigationItemStyle"];
    }

    private static bool TryGetWholeNumber(double value, int minimum, int maximum, out int result)
    {
        if (!double.IsFinite(value) || value != Math.Truncate(value) || value < minimum || value > maximum)
        {
            result = default;
            return false;
        }

        result = (int)value;
        return true;
    }
}
