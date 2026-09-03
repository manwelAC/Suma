using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Suma.Desktop.ViewModels;
using Suma.Desktop.Common;
using Suma.Application.Recurring.CreateRecurringTransaction;
using Suma.Domain.Recurring;
using Suma.Domain.Transactions;
using Suma.Application.Savings.CreateSavingsGoal;
using Suma.Domain.Savings;

namespace Suma.Desktop.Pages.Planning;

public sealed partial class PlanningPage : Page
{
    public PlanningPage(PlanningViewModel viewModel, BudgetEditorViewModel editorViewModel, RecurringViewModel recurringViewModel, RecurringEditorViewModel recurringEditorViewModel, SavingsViewModel savingsViewModel, SavingsGoalEditorViewModel savingsGoalEditorViewModel)
    {
        ViewModel = viewModel;
        EditorViewModel = editorViewModel;
        RecurringViewModel = recurringViewModel;
        RecurringEditorViewModel = recurringEditorViewModel;
        SavingsViewModel = savingsViewModel;
        SavingsGoalEditorViewModel = savingsGoalEditorViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnPageSizeChanged;
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ViewModel.SelectedBudget) or nameof(ViewModel.SelectedBudgetVisibility))
            {
                UpdateResponsiveLayout(ActualWidth);
            }
        };
        SavingsViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(SavingsViewModel.SelectedGoal) or nameof(SavingsViewModel.DetailsVisibility))
            {
                UpdateResponsiveLayout(ActualWidth);
            }
        };
    }

    public PlanningViewModel ViewModel { get; }

    public BudgetEditorViewModel EditorViewModel { get; }

    public RecurringViewModel RecurringViewModel { get; }

    public RecurringEditorViewModel RecurringEditorViewModel { get; }

    public SavingsViewModel SavingsViewModel { get; }

    public SavingsGoalEditorViewModel SavingsGoalEditorViewModel { get; }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e) => UpdateResponsiveLayout(e.NewSize.Width);

    private void UpdateResponsiveLayout(double availableWidth)
    {
        if (availableWidth <= 0) availableWidth = ActualWidth;
        if (availableWidth <= 0) return;

        var isWide = availableWidth >= 960;

        // 1. Budgets Section
        if (isWide && ViewModel.SelectedBudget != null)
        {
            BudgetListColDef.Width = new GridLength(380);
            BudgetDetailColDef.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(BudgetDetailPanel, 1);
            Grid.SetRow(BudgetDetailPanel, 0);
        }
        else
        {
            BudgetListColDef.Width = new GridLength(1, GridUnitType.Star);
            BudgetDetailColDef.Width = new GridLength(0);
            Grid.SetColumn(BudgetDetailPanel, 0);
            Grid.SetRow(BudgetDetailPanel, 1);
        }

        // 2. Recurring Section
        if (isWide)
        {
            RecurringOccurrencesColDef.Width = new GridLength(1.1, GridUnitType.Star);
            RecurringSchedulesColDef.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(RecurringSchedulesPanel, 1);
            Grid.SetRow(RecurringSchedulesPanel, 0);
        }
        else
        {
            RecurringOccurrencesColDef.Width = new GridLength(1, GridUnitType.Star);
            RecurringSchedulesColDef.Width = new GridLength(0);
            Grid.SetColumn(RecurringSchedulesPanel, 0);
            Grid.SetRow(RecurringSchedulesPanel, 1);
        }

        // 3. Savings Section
        if (isWide && SavingsViewModel.SelectedGoal != null)
        {
            SavingsListColDef.Width = new GridLength(380);
            SavingsDetailColDef.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(SavingsDetailPanel, 1);
            Grid.SetRow(SavingsDetailPanel, 0);
        }
        else
        {
            SavingsListColDef.Width = new GridLength(1, GridUnitType.Star);
            SavingsDetailColDef.Width = new GridLength(0);
            Grid.SetColumn(SavingsDetailPanel, 0);
            Grid.SetRow(SavingsDetailPanel, 1);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        BudgetList.SelectedItem = ViewModel.SelectedBudget;
        UpdateResponsiveLayout(ActualWidth);
    }

    private async void OnActiveBudgetsClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(ActiveBudgetsButton, true);
        SetToggleState(ArchivedBudgetsButton, false);
        await ViewModel.SetArchivedViewAsync(false);
        BudgetList.SelectedItem = ViewModel.SelectedBudget;
        UpdateResponsiveLayout(ActualWidth);
    }

    private async void OnArchivedBudgetsClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(ActiveBudgetsButton, false);
        SetToggleState(ArchivedBudgetsButton, true);
        await ViewModel.SetArchivedViewAsync(true);
        BudgetList.SelectedItem = ViewModel.SelectedBudget;
        UpdateResponsiveLayout(ActualWidth);
    }

    private async void OnBudgetItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is BudgetRowViewModel budget)
        {
            BudgetList.SelectedItem = budget;
            await ViewModel.SelectBudgetAsync(budget.Id);
            UpdateResponsiveLayout(ActualWidth);
        }
    }

    private async void OnNewBudgetClick(object sender, RoutedEventArgs e) => await ShowBudgetEditorAsync();

    private void OnBudgetsSectionClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(BudgetsSectionButton, true);
        SetToggleState(RecurringSectionButton, false);
        SetToggleState(SavingsSectionButton, false);
        BudgetSection.Visibility = Visibility.Visible;
        RecurringSection.Visibility = Visibility.Collapsed;
        SavingsSection.Visibility = Visibility.Collapsed;
        NewBudgetButton.Visibility = ViewModel.NewBudgetVisibility;
        UpdateResponsiveLayout(ActualWidth);
    }

    private async void OnRecurringSectionClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(BudgetsSectionButton, false);
        SetToggleState(RecurringSectionButton, true);
        SetToggleState(SavingsSectionButton, false);
        BudgetSection.Visibility = Visibility.Collapsed;
        RecurringSection.Visibility = Visibility.Visible;
        SavingsSection.Visibility = Visibility.Collapsed;
        NewBudgetButton.Visibility = Visibility.Collapsed;
        await RecurringViewModel.LoadAsync();
        UpdateResponsiveLayout(ActualWidth);
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

    private async void OnSavingsSectionClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(BudgetsSectionButton, false); SetToggleState(RecurringSectionButton, false); SetToggleState(SavingsSectionButton, true);
        BudgetSection.Visibility = Visibility.Collapsed; RecurringSection.Visibility = Visibility.Collapsed; SavingsSection.Visibility = Visibility.Visible;
        NewBudgetButton.Visibility = Visibility.Collapsed; await SavingsViewModel.LoadAsync(); SavingsGoalList.SelectedItem = SavingsViewModel.SelectedGoal;
        UpdateResponsiveLayout(ActualWidth);
    }

    private async void OnActiveSavingsClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(ActiveSavingsButton, true); SetToggleState(ArchivedSavingsButton, false);
        await SavingsViewModel.SetArchivedAsync(false); SavingsGoalList.SelectedItem = SavingsViewModel.SelectedGoal;
        UpdateResponsiveLayout(ActualWidth);
    }

    private async void OnArchivedSavingsClick(object sender, RoutedEventArgs e)
    {
        SetToggleState(ActiveSavingsButton, false); SetToggleState(ArchivedSavingsButton, true);
        await SavingsViewModel.SetArchivedAsync(true); SavingsGoalList.SelectedItem = SavingsViewModel.SelectedGoal;
        UpdateResponsiveLayout(ActualWidth);
    }

    private async void OnSavingsGoalClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SavingsGoalRowViewModel goal) { SavingsGoalList.SelectedItem = goal; await SavingsViewModel.SelectGoalAsync(goal.Id); UpdateResponsiveLayout(ActualWidth); }
    }

    private async void OnNewSavingsGoalClick(object sender, RoutedEventArgs e) => await ShowSavingsGoalEditorAsync();
    private async void OnAddSavingsContributionClick(object sender, RoutedEventArgs e) => await ShowSavingsContributionEditorAsync();
    private async void OnArchiveSavingsClick(object sender, RoutedEventArgs e)
    {
        if (SavingsViewModel.SelectedGoal is { } goal)
        {
            var dialog = SumaDialog.CreateDestructive(
                XamlRoot,
                "Archive savings goal?",
                $"{goal.Name} will be moved to Archived goals. You can view it anytime or restore it later.",
                "Archived goals do not accept new contributions until restored.",
                destructiveButtonText: "Archive");
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await SavingsViewModel.ArchiveAsync();
                SavingsGoalList.SelectedItem = SavingsViewModel.SelectedGoal;
                UpdateResponsiveLayout(ActualWidth);
            }
        }
    }
    private async void OnRestoreSavingsClick(object sender, RoutedEventArgs e)
    {
        await SavingsViewModel.RestoreAsync();
        SetToggleState(ActiveSavingsButton, !SavingsViewModel.ShowArchived);
        SetToggleState(ArchivedSavingsButton, SavingsViewModel.ShowArchived);
        SavingsGoalList.SelectedItem = SavingsViewModel.SelectedGoal;
        UpdateResponsiveLayout(ActualWidth);
    }

    private async Task ShowSavingsGoalEditorAsync()
    {
        if (!await SavingsGoalEditorViewModel.LoadAsync()) { SavingsViewModel.SetError(SavingsGoalEditorViewModel.ErrorMessage!); return; }
        var nameBox = new TextBox { Header = "Name", PlaceholderText = "Emergency Fund" };
        var targetBox = new TextBox { Header = "Target amount", PlaceholderText = "0.00", InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.CurrencyAmount) } } };
        var currencyBox = new TextBox { Header = "Currency", Text = "PHP", MaxLength = 3, CharacterCasing = CharacterCasing.Upper };
        var accountBox = new ComboBox { Header = "Destination account", ItemsSource = SavingsGoalEditorViewModel.Accounts, DisplayMemberPath = nameof(SavingsAccountOption.Display), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        accountBox.SelectionChanged += (_, _) => { if (accountBox.SelectedItem is SavingsAccountOption { Id: not null } option) { currencyBox.Text = option.CurrencyCode; currencyBox.IsEnabled = false; } else currencyBox.IsEnabled = true; };
        var targetDateCheck = new CheckBox { Content = "Set a target date" };
        var targetDateBox = new DatePicker { Header = "Target date", Date = DateTimeOffset.Now.AddMonths(6), Visibility = Visibility.Collapsed };
        targetDateCheck.Checked += (_, _) => targetDateBox.Visibility = Visibility.Visible; targetDateCheck.Unchecked += (_, _) => targetDateBox.Visibility = Visibility.Collapsed;
        var error = ErrorText(); var dialog = Dialog("New savings goal", DialogContent(nameBox, targetBox, currencyBox, accountBox, targetDateCheck, targetDateBox, error));
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral(); dialog.IsPrimaryButtonEnabled = false;
            try
            {
                if (!MoneyText.TryParseMinor(targetBox.Text, out var target) || target <= 0) { Reject(args, error, "Enter a valid target amount."); return; }
                var account = accountBox.SelectedItem as SavingsAccountOption;
                var request = new CreateSavingsGoalRequest(nameBox.Text, target, currencyBox.Text,
                    targetDateCheck.IsChecked == true ? DateOnly.FromDateTime(targetDateBox.Date.DateTime) : null, account?.Id);
                if (!await SavingsViewModel.CreateAsync(request)) Reject(args, error, SavingsViewModel.ErrorMessage!);
            }
            finally { dialog.IsPrimaryButtonEnabled = true; deferral.Complete(); }
        };
        _ = await dialog.ShowAsync(); SavingsGoalList.SelectedItem = SavingsViewModel.SelectedGoal;
    }

    private async Task ShowSavingsContributionEditorAsync()
    {
        if (!await SavingsViewModel.LoadCandidatesAsync()) return;
        if (SavingsViewModel.Candidates.Count == 0) { SavingsViewModel.SetError("No transactions have remaining capacity for this goal."); return; }
        var candidateBox = new ComboBox { Header = "Existing transaction", ItemsSource = SavingsViewModel.Candidates, DisplayMemberPath = nameof(GoalCandidateRowViewModel.Display), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var typeBox = new ComboBox { Header = "Contribution type", ItemsSource = Enum.GetValues<GoalContributionType>(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var amountBox = new TextBox { Header = "Amount", PlaceholderText = "0.00", InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.CurrencyAmount) } } };
        var error = ErrorText(); var dialog = Dialog("Add contribution", DialogContent(candidateBox, typeBox, amountBox, error));
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral(); dialog.IsPrimaryButtonEnabled = false;
            try
            {
                if (candidateBox.SelectedItem is not GoalCandidateRowViewModel candidate || !MoneyText.TryParseMinor(amountBox.Text, out var amount) || amount <= 0) { Reject(args, error, "Choose a transaction and enter a valid amount."); return; }
                if (!await SavingsViewModel.AddContributionAsync(candidate.Value.TransactionId, (GoalContributionType)typeBox.SelectedItem, amount)) Reject(args, error, SavingsViewModel.ErrorMessage!);
            }
            finally { dialog.IsPrimaryButtonEnabled = true; deferral.Complete(); }
        };
        _ = await dialog.ShowAsync();
    }

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
        if (ViewModel.SelectedBudget is { } budget)
        {
            var dialog = SumaDialog.CreateDestructive(
                XamlRoot,
                "Archive budget?",
                $"{budget.Name} will be moved to Archived budgets. Active allocations will no longer be tracked in Overview.",
                "Archived budgets can be viewed and restored anytime from the Archived view.",
                destructiveButtonText: "Archive");
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.ArchiveAsync();
                BudgetList.SelectedItem = ViewModel.SelectedBudget;
                UpdateResponsiveLayout(ActualWidth);
            }
        }
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

    private ContentDialog Dialog(string title, UIElement content, string primaryText = "Save", ModalSize size = ModalSize.Medium) =>
        SumaDialog.Create(XamlRoot, title, content, primaryText, "Cancel", size);

    private static StackPanel DialogContent(params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = 16, Padding = new Thickness(0, 4, 0, 8) };
        foreach (var child in children)
        {
            if (child is Control c)
            {
                c.RequestedTheme = ElementTheme.Light;
            }

            if (child is TextBox tb)
            {
                tb.MinHeight = tb.AcceptsReturn ? 80 : 44;
                tb.CornerRadius = new CornerRadius(10);
                tb.BorderThickness = new Thickness(1);
                tb.Padding = new Thickness(14, 10, 14, 10);
                if (Microsoft.UI.Xaml.Application.Current.Resources["SumaSurfaceBrush"] is Brush s) tb.Background = s;
                if (Microsoft.UI.Xaml.Application.Current.Resources["SumaBorderBrush"] is Brush b) tb.BorderBrush = b;
                if (Microsoft.UI.Xaml.Application.Current.Resources["SumaTextPrimaryBrush"] is Brush p) tb.Foreground = p;
            }
            else if (child is ComboBox cb)
            {
                cb.MinHeight = 44;
                cb.CornerRadius = new CornerRadius(10);
                cb.BorderThickness = new Thickness(1);
                cb.Padding = new Thickness(14, 0, 14, 0);
                if (Microsoft.UI.Xaml.Application.Current.Resources["SumaSurfaceBrush"] is Brush s) cb.Background = s;
                if (Microsoft.UI.Xaml.Application.Current.Resources["SumaBorderBrush"] is Brush b) cb.BorderBrush = b;
                if (Microsoft.UI.Xaml.Application.Current.Resources["SumaTextPrimaryBrush"] is Brush p) cb.Foreground = p;
            }
            else if (child is DatePicker dp)
            {
                dp.MinHeight = 44;
                dp.CornerRadius = new CornerRadius(10);
                dp.BorderThickness = new Thickness(1);
                if (Microsoft.UI.Xaml.Application.Current.Resources["SumaSurfaceBrush"] is Brush s) dp.Background = s;
                if (Microsoft.UI.Xaml.Application.Current.Resources["SumaBorderBrush"] is Brush b) dp.BorderBrush = b;
                if (Microsoft.UI.Xaml.Application.Current.Resources["SumaTextPrimaryBrush"] is Brush p) dp.Foreground = p;
            }
            else if (child is NumberBox nb)
            {
                nb.MinHeight = 44;
                nb.CornerRadius = new CornerRadius(10);
                nb.BorderThickness = new Thickness(1);
                if (Microsoft.UI.Xaml.Application.Current.Resources["SumaSurfaceBrush"] is Brush s) nb.Background = s;
                if (Microsoft.UI.Xaml.Application.Current.Resources["SumaBorderBrush"] is Brush b) nb.BorderBrush = b;
                if (Microsoft.UI.Xaml.Application.Current.Resources["SumaTextPrimaryBrush"] is Brush p) nb.Foreground = p;
            }

            panel.Children.Add(child);
        }
        return panel;
    }

    private static TextBlock ErrorText() => SumaDialog.CreateErrorText();

    private static void Reject(ContentDialogButtonClickEventArgs args, TextBlock error, string message)
    {
        args.Cancel = true;
        SumaDialog.SetError(error, message);
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
