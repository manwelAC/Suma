using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Suma.Desktop.ViewModels;

namespace Suma.Desktop.Pages.Planning;

public sealed partial class PlanningPage : Page
{
    public PlanningPage(PlanningViewModel viewModel, BudgetEditorViewModel editorViewModel)
    {
        ViewModel = viewModel;
        EditorViewModel = editorViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public PlanningViewModel ViewModel { get; }

    public BudgetEditorViewModel EditorViewModel { get; }

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
}
