using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Suma.Application.Reports.GetReportOptions;
using Suma.Desktop.ViewModels;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;

namespace Suma.Desktop.Pages.Reports;

public sealed partial class ReportsPage : Page
{
    private static readonly SolidColorBrush GreenBrush = new(Color.FromArgb(255, 38, 78, 54));    // #264E36
    private static readonly SolidColorBrush RedBrush = new(Color.FromArgb(255, 198, 40, 40));      // #C62828
    private static readonly SolidColorBrush GrayBrush = new(Color.FromArgb(255, 117, 117, 117));   // #757575
    private static readonly SolidColorBrush GridlineBrush = new(Color.FromArgb(40, 0, 0, 0));

    private bool synchronizingControls;

    public ReportsPage(ReportsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += OnLoaded;
    }

    public ReportsViewModel ViewModel { get; }

    // KPI Values
    public string GrossIncomeValueText => ViewModel.Report is not null && !string.IsNullOrEmpty(ViewModel.Currency)
        ? MoneyText.Format(ViewModel.Report.CashFlow.GrossIncomeMinor, ViewModel.Currency) : "—";

    public string NetExpenseValueText => ViewModel.Report is not null && !string.IsNullOrEmpty(ViewModel.Currency)
        ? MoneyText.Format(ViewModel.Report.CashFlow.NetExpenseMinor, ViewModel.Currency) : "—";

    public string RefundsValueText => ViewModel.Report is not null && !string.IsNullOrEmpty(ViewModel.Currency)
        ? MoneyText.Format(ViewModel.Report.CashFlow.RefundMinor, ViewModel.Currency) : "—";

    public string NetCashFlowValueText => ViewModel.Report is not null && !string.IsNullOrEmpty(ViewModel.Currency)
        ? (ViewModel.Report.CashFlow.NetCashFlowMinor >= 0 ? "+" : "") + MoneyText.Format(ViewModel.Report.CashFlow.NetCashFlowMinor, ViewModel.Currency) : "—";

    // Comparison Delts
    public string GrossIncomeComparisonText => ViewModel.Comparison?.GrossIncomeComparisonText ?? "vs previous period";
    public SolidColorBrush GrossIncomeComparisonColor => ViewModel.Comparison is null ? GrayBrush : (ViewModel.Comparison.GrossIncomeIsPositive ? GreenBrush : RedBrush);

    public string NetExpenseComparisonText => ViewModel.Comparison?.NetExpenseComparisonText ?? "vs previous period";
    public SolidColorBrush NetExpenseComparisonColor => ViewModel.Comparison is null ? GrayBrush : (ViewModel.Comparison.NetExpenseIsPositive ? GreenBrush : RedBrush);

    public string RefundsComparisonText => ViewModel.Comparison?.RefundsComparisonText ?? "vs previous period";
    public string NetCashFlowComparisonText => ViewModel.Comparison?.NetCashFlowComparisonText ?? "vs previous period";

    // Tab Rows
    public IEnumerable<string> CategoryRows => ViewModel.CategoryMode == ReportCategoryMode.Expense
        ? ViewModel.Report?.ExpenseCategories.Select(item => $"{item.CategoryName}{Archived(item.CategoryArchived)}  •  Gross {MoneyText.Format(item.GrossExpenseMinor, ViewModel.Currency)}  •  Refunds {MoneyText.Format(item.RefundMinor, ViewModel.Currency)}  •  Net {MoneyText.Format(item.NetExpenseMinor, ViewModel.Currency)}") ?? []
        : ViewModel.Report?.IncomeCategories.Select(item => $"{item.CategoryName}{Archived(item.CategoryArchived)}  •  Income {MoneyText.Format(item.IncomeMinor, ViewModel.Currency)}") ?? [];

    public IEnumerable<string> AccountRows => ViewModel.Report?.AccountMovements.Select(item =>
        $"{item.AccountName}{Archived(item.AccountArchived)}  •  Inflow {MoneyText.Format(item.TotalInflowMinor, ViewModel.Currency)}  •  Outflow {MoneyText.Format(item.TotalOutflowMinor, ViewModel.Currency)}  •  Net {MoneyText.Format(item.NetMovementMinor, ViewModel.Currency)}") ?? [];

    public IEnumerable<string> DetailRows => ViewModel.AccountDetails.Select(item =>
        $"{item.TransactionDate:MMM d, yyyy}  •  {item.AccountName}  •  {item.Direction}  •  {item.Category ?? "Uncategorized"}  •  {MoneyText.Format(item.AmountMinor, item.CurrencyCode)}");

    public IEnumerable<string> BudgetRows => ViewModel.Budget?.Allocations.Select(item =>
        $"{item.CategoryName}{Archived(item.CategoryArchived)}  •  Allocated {MoneyText.Format(item.AmountMinor, ViewModel.Budget.Summary.CurrencyCode)}  •  Spent {MoneyText.Format(item.SpentMinor, ViewModel.Budget.Summary.CurrencyCode)}  •  Remaining {MoneyText.Format(item.RemainingMinor, ViewModel.Budget.Summary.CurrencyCode)}  •  {item.UtilizationPercent:0.##}% Used") ?? [];

    public string BudgetTitleText => ViewModel.Budget?.Summary.Name ?? "No Budget Selected";
    public string BudgetPeriodText => ViewModel.Budget is null ? string.Empty : $"Period {ViewModel.Budget.Summary.PeriodStart:MMM d, yyyy} – {ViewModel.Budget.Summary.PeriodEnd:MMM d, yyyy}  •  {ViewModel.Budget.Summary.CurrencyCode}";
    public string BudgetExpectedIncomeText => ViewModel.Budget is null ? string.Empty : $"Expected Income: {MoneyText.Format(ViewModel.Budget.Summary.ExpectedIncomeMinor, ViewModel.Budget.Summary.CurrencyCode)}";
    public string BudgetTotalsText => ViewModel.Budget is null ? string.Empty : $"Total Allocated: {MoneyText.Format(ViewModel.Budget.AllocatedMinor, ViewModel.Budget.Summary.CurrencyCode)}  •  Total Spent: {MoneyText.Format(ViewModel.Budget.SpentMinor, ViewModel.Budget.Summary.CurrencyCode)}  •  Remaining: {MoneyText.Format(ViewModel.Budget.RemainingMinor, ViewModel.Budget.Summary.CurrencyCode)}";

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        synchronizingControls = true;
        try
        {
            await ViewModel.InitializeAsync();
            CurrencyBox.SelectedItem = ViewModel.Currency;
            SyncDates();
            BudgetBox.SelectedItem = ViewModel.Budgets.FirstOrDefault(item => item.Id == ViewModel.Budget?.Summary.Id);
            await ViewModel.LoadDashboardDetailsAsync();
        }
        finally
        {
            synchronizingControls = false;
            RefreshAll();
        }
    }

    private async void OnPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string tag && Enum.TryParse<ReportDatePreset>(tag, out var preset))
        {
            await ViewModel.SetPresetAsync(preset);
            synchronizingControls = true;
            SyncDates();
            synchronizingControls = false;
            await ViewModel.LoadDashboardDetailsAsync();
            RefreshAll();
        }
    }

    private async void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (CurrencyBox.SelectedItem is string currency)
        {
            await ViewModel.SetSelectionAsync(
                currency,
                DateOnly.FromDateTime(StartBox.Date.DateTime),
                DateOnly.FromDateTime(EndBox.Date.DateTime));
            await ViewModel.LoadDashboardDetailsAsync();
            RefreshAll();
        }
    }

    private void OnReportSelectionChanged(object sender, SelectionChangedEventArgs e) => MarkDraftDirty();
    private void OnReportDateChanged(object sender, DatePickerValueChangedEventArgs args) => MarkDraftDirty();

    private void MarkDraftDirty()
    {
        if (!synchronizingControls && CurrencyBox.SelectedItem is string currency)
        {
            ViewModel.SetDraftSelection(
                currency,
                DateOnly.FromDateTime(StartBox.Date.DateTime),
                DateOnly.FromDateTime(EndBox.Date.DateTime));
        }
    }

    private async void OnSectionClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string tag && Enum.TryParse<ReportSection>(tag, out var section))
        {
            await ViewModel.SetSectionAsync(section);
            UpdateTabStyles(section);
            CashFlowPanel.Visibility = section == ReportSection.CashFlow ? Visibility.Visible : Visibility.Collapsed;
            CategoriesPanel.Visibility = section == ReportSection.Categories ? Visibility.Visible : Visibility.Collapsed;
            AccountsPanel.Visibility = section == ReportSection.Accounts ? Visibility.Visible : Visibility.Collapsed;
            BudgetPanel.Visibility = section == ReportSection.Budget ? Visibility.Visible : Visibility.Collapsed;
            RefreshAll();
        }
    }

    private void UpdateTabStyles(ReportSection active)
    {
        TabBtnCashFlow.Style = (Style)Resources[active == ReportSection.CashFlow ? "ReportActivePillButtonStyle" : "ReportPillButtonStyle"];
        TabBtnCategories.Style = (Style)Resources[active == ReportSection.Categories ? "ReportActivePillButtonStyle" : "ReportPillButtonStyle"];
        TabBtnAccounts.Style = (Style)Resources[active == ReportSection.Accounts ? "ReportActivePillButtonStyle" : "ReportPillButtonStyle"];
        TabBtnBudget.Style = (Style)Resources[active == ReportSection.Budget ? "ReportActivePillButtonStyle" : "ReportPillButtonStyle"];
    }

    private void OnCategoryModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string tag && Enum.TryParse<ReportCategoryMode>(tag, out var mode))
        {
            ViewModel.SetCategoryMode(mode);
            CategoryExpenseBtn.Style = (Style)Resources[mode == ReportCategoryMode.Expense ? "ReportActivePillButtonStyle" : "ReportPillButtonStyle"];
            CategoryIncomeBtn.Style = (Style)Resources[mode == ReportCategoryMode.Income ? "ReportActivePillButtonStyle" : "ReportPillButtonStyle"];
            RefreshBindings();
        }
    }

    private void OnGranularityClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string tag && Enum.TryParse<CashFlowGranularity>(tag, out var gran))
        {
            ViewModel.SetGranularity(gran);
            UpdateGranularityButtons(gran);
            DrawCashFlowChart();
        }
    }

    private void UpdateGranularityButtons(CashFlowGranularity active)
    {
        GranularityDailyBtn.Background = active == CashFlowGranularity.Daily ? GreenBrush : new SolidColorBrush(Colors.Transparent);
        GranularityDailyBtn.Foreground = active == CashFlowGranularity.Daily ? new SolidColorBrush(Colors.White) : GrayBrush;

        GranularityWeeklyBtn.Background = active == CashFlowGranularity.Weekly ? GreenBrush : new SolidColorBrush(Colors.Transparent);
        GranularityWeeklyBtn.Foreground = active == CashFlowGranularity.Weekly ? new SolidColorBrush(Colors.White) : GrayBrush;

        GranularityMonthlyBtn.Background = active == CashFlowGranularity.Monthly ? GreenBrush : new SolidColorBrush(Colors.Transparent);
        GranularityMonthlyBtn.Foreground = active == CashFlowGranularity.Monthly ? new SolidColorBrush(Colors.White) : GrayBrush;
    }

    private void OnBreakdownModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string tag && Enum.TryParse<CashFlowBreakdownMode>(tag, out var mode))
        {
            ViewModel.SetBreakdownMode(mode);
            BreakdownCategoryBtn.Background = mode == CashFlowBreakdownMode.ByCategory ? GreenBrush : new SolidColorBrush(Colors.Transparent);
            BreakdownCategoryBtn.Foreground = mode == CashFlowBreakdownMode.ByCategory ? new SolidColorBrush(Colors.White) : GrayBrush;

            BreakdownAccountBtn.Background = mode == CashFlowBreakdownMode.ByAccount ? GreenBrush : new SolidColorBrush(Colors.Transparent);
            BreakdownAccountBtn.Foreground = mode == CashFlowBreakdownMode.ByAccount ? new SolidColorBrush(Colors.White) : GrayBrush;
        }
    }

    private async void OnBudgetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BudgetBox.SelectedItem is ReportBudgetOption item && ViewModel.Budget?.Summary.Id != item.Id)
        {
            await ViewModel.SelectBudgetAsync(item.Id);
            RefreshBindings();
        }
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RunExportInteractionAsync(SaveExportAsync);
        RefreshBindings();
    }

    private static async Task SaveExportAsync(ReportExport export, CancellationToken cancellationToken)
    {
        var picker = new FileSavePicker { SuggestedFileName = System.IO.Path.GetFileNameWithoutExtension(export.FileName) };
        picker.FileTypeChoices.Add("CSV", [".csv"]);
        var window = ((App)Microsoft.UI.Xaml.Application.Current).MainWindow;
        if (window is null) throw new InvalidOperationException("The Suma window is unavailable.");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        var file = await picker.PickSaveFileAsync();
        if (file is not null) await FileIO.WriteBytesAsync(file, export.Content);
    }

    private void SyncDates()
    {
        StartBox.Date = new DateTimeOffset(ViewModel.StartDate.ToDateTime(TimeOnly.MinValue));
        EndBox.Date = new DateTimeOffset(ViewModel.EndDate.ToDateTime(TimeOnly.MinValue));
    }

    private void RefreshAll()
    {
        RefreshBindings();
        DrawCashFlowChart();
        DrawDonutChart();
    }

    private void RefreshBindings() => Bindings.Update();

    private void OnChartCanvasSizeChanged(object sender, SizeChangedEventArgs e) => DrawCashFlowChart();
    private void OnDonutCanvasSizeChanged(object sender, SizeChangedEventArgs e) => DrawDonutChart();

    // ==========================================
    // CHART 1: CASH FLOW OVER TIME COMBO CHART
    // ==========================================
    private void DrawCashFlowChart()
    {
        if (CashFlowChartCanvas is null) return;
        CashFlowChartCanvas.Children.Clear();

        var points = ViewModel.TimelinePoints;
        if (points.Count == 0)
        {
            ChartEmptyState.Visibility = Visibility.Visible;
            return;
        }

        double width = CashFlowChartCanvas.ActualWidth;
        double height = CashFlowChartCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        ChartEmptyState.Visibility = Visibility.Collapsed;

        double padLeft = 64;
        double padRight = 16;
        double padTop = 20;
        double padBottom = 32;

        double plotW = width - padLeft - padRight;
        double plotH = height - padTop - padBottom;
        if (plotW <= 0 || plotH <= 0) return;

        // Calculate Scale
        long maxIncome = points.Max(p => p.IncomeMinor);
        long maxExpense = points.Max(p => p.ExpenseMinor);
        long maxVal = Math.Max(1000, Math.Max(maxIncome, maxExpense));
        long minNet = points.Min(p => p.NetMinor);
        long minVal = Math.Min(0, minNet);

        // If all 0, default nice ceiling
        if (maxVal == 0 && minVal == 0) maxVal = 10000;

        long range = maxVal - minVal;
        if (range <= 0) range = 10000;

        // Draw 4 Horizontal Reference Gridlines + Y-axis labels
        int gridSteps = 4;
        for (int i = 0; i <= gridSteps; i++)
        {
            double ratio = (double)i / gridSteps;
            double y = padTop + plotH * (1.0 - ratio);
            long val = minVal + (long)(range * ratio);

            var line = new Line
            {
                X1 = padLeft,
                Y1 = y,
                X2 = width - padRight,
                Y2 = y,
                Stroke = GridlineBrush,
                StrokeThickness = 1,
                StrokeDashArray = [4, 4]
            };
            CashFlowChartCanvas.Children.Add(line);

            var label = new TextBlock
            {
                Text = MoneyText.Format(val, ViewModel.Currency),
                FontSize = 10,
                Foreground = GrayBrush,
                TextAlignment = TextAlignment.Right,
                Width = padLeft - 8
            };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, y - 7);
            CashFlowChartCanvas.Children.Add(label);
        }

        // Draw Zero Baseline
        double zeroY = padTop + plotH * (1.0 - ((double)(0 - minVal) / range));
        var zeroLine = new Line
        {
            X1 = padLeft,
            Y1 = zeroY,
            X2 = width - padRight,
            Y2 = zeroY,
            Stroke = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)),
            StrokeThickness = 1
        };
        CashFlowChartCanvas.Children.Add(zeroLine);

        // Bar Layout Math
        int n = points.Count;
        double slotW = plotW / n;
        double groupW = Math.Min(slotW * 0.7, 36);
        double barW = (groupW - 4) / 2.0;

        var netPolyline = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromArgb(255, 30, 63, 43)),
            StrokeThickness = 2.5
        };

        var nodes = new List<(Point Pt, CashFlowTimelinePoint Data)>();

        int labelInterval = n > 20 ? (int)Math.Ceiling(n / 10.0) : 1;

        for (int i = 0; i < n; i++)
        {
            var p = points[i];
            double slotCenter = padLeft + i * slotW + (slotW / 2.0);

            // Income Bar (Left in group)
            double incH = ((double)p.IncomeMinor / range) * plotH;
            if (incH > 0)
            {
                var incRect = new Rectangle
                {
                    Width = barW,
                    Height = Math.Max(2, incH),
                    Fill = GreenBrush,
                    RadiusX = 3,
                    RadiusY = 3
                };
                Canvas.SetLeft(incRect, slotCenter - groupW / 2.0);
                Canvas.SetTop(incRect, zeroY - incH);
                ToolTipService.SetToolTip(incRect, p.Tooltip);
                CashFlowChartCanvas.Children.Add(incRect);
            }

            // Expense Bar (Right in group)
            double expH = ((double)p.ExpenseMinor / range) * plotH;
            if (expH > 0)
            {
                var expRect = new Rectangle
                {
                    Width = barW,
                    Height = Math.Max(2, expH),
                    Fill = new SolidColorBrush(Color.FromArgb(255, 231, 111, 81)), // #E76F51
                    RadiusX = 3,
                    RadiusY = 3
                };
                Canvas.SetLeft(expRect, slotCenter - groupW / 2.0 + barW + 4);
                Canvas.SetTop(expRect, zeroY - expH);
                ToolTipService.SetToolTip(expRect, p.Tooltip);
                CashFlowChartCanvas.Children.Add(expRect);
            }

            // Net Cash Flow Point
            double netRatio = (double)(p.NetMinor - minVal) / range;
            double netY = padTop + plotH * (1.0 - netRatio);
            Point netPt = new(slotCenter, netY);
            netPolyline.Points.Add(netPt);
            nodes.Add((netPt, p));

            // X-axis label
            if (i % labelInterval == 0 || i == n - 1)
            {
                var xLabel = new TextBlock
                {
                    Text = p.Label,
                    FontSize = 10,
                    Foreground = GrayBrush,
                    TextAlignment = TextAlignment.Center,
                    Width = slotW
                };
                Canvas.SetLeft(xLabel, slotCenter - slotW / 2.0);
                Canvas.SetTop(xLabel, height - padBottom + 6);
                CashFlowChartCanvas.Children.Add(xLabel);
            }
        }

        // Add Net Polyline
        CashFlowChartCanvas.Children.Add(netPolyline);

        // Add Node Circles
        foreach (var (pt, data) in nodes)
        {
            var ellipse = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = new SolidColorBrush(Colors.White),
                Stroke = new SolidColorBrush(Color.FromArgb(255, 30, 63, 43)),
                StrokeThickness = 2
            };
            Canvas.SetLeft(ellipse, pt.X - 3.5);
            Canvas.SetTop(ellipse, pt.Y - 3.5);
            ToolTipService.SetToolTip(ellipse, data.Tooltip);
            CashFlowChartCanvas.Children.Add(ellipse);
        }
    }

    // ==========================================
    // CHART 2: TOP EXPENSE CATEGORIES DONUT
    // ==========================================
    private void DrawDonutChart()
    {
        if (DonutChartCanvas is null) return;
        DonutChartCanvas.Children.Clear();

        var items = ViewModel.TopExpenseDonutItems;
        if (items.Count == 0 || !ViewModel.HasExpenseCategories)
        {
            DonutEmptyState.Visibility = Visibility.Visible;
            return;
        }

        DonutEmptyState.Visibility = Visibility.Collapsed;

        double size = Math.Min(DonutChartCanvas.ActualWidth, DonutChartCanvas.ActualHeight);
        if (size <= 0) size = 200;

        Point center = new(size / 2.0, size / 2.0);
        double outerRadius = (size / 2.0) - 8;
        double innerRadius = outerRadius * 0.65;

        double currentAngle = 0.0;

        foreach (var item in items)
        {
            double sweep = (item.Percentage / 100.0) * 360.0;
            if (sweep <= 0.05) continue;

            var slice = CreateDonutSlice(center, innerRadius, outerRadius, currentAngle, sweep, item.ColorBrush);
            ToolTipService.SetToolTip(slice, $"{item.CategoryName}: {item.FormattedAmount} ({item.FormattedPercentage})");
            DonutChartCanvas.Children.Add(slice);

            currentAngle += sweep;
        }
    }

    private static Microsoft.UI.Xaml.Shapes.Path CreateDonutSlice(Point center, double innerRadius, double outerRadius, double startAngle, double sweepAngle, Brush fill)
    {
        if (sweepAngle >= 360.0) sweepAngle = 359.99;
        if (sweepAngle <= 0.0) return new Microsoft.UI.Xaml.Shapes.Path();

        double startRad = (startAngle - 90) * Math.PI / 180.0;
        double endRad = (startAngle + sweepAngle - 90) * Math.PI / 180.0;

        Point p0 = new(center.X + outerRadius * Math.Cos(startRad), center.Y + outerRadius * Math.Sin(startRad));
        Point p1 = new(center.X + outerRadius * Math.Cos(endRad), center.Y + outerRadius * Math.Sin(endRad));
        Point p2 = new(center.X + innerRadius * Math.Cos(endRad), center.Y + innerRadius * Math.Sin(endRad));
        Point p3 = new(center.X + innerRadius * Math.Cos(startRad), center.Y + innerRadius * Math.Sin(startRad));

        bool isLargeArc = sweepAngle > 180.0;

        var fig = new PathFigure { StartPoint = p0, IsClosed = true };
        fig.Segments.Add(new ArcSegment { Point = p1, Size = new Size(outerRadius, outerRadius), IsLargeArc = isLargeArc, SweepDirection = SweepDirection.Clockwise });
        fig.Segments.Add(new LineSegment { Point = p2 });
        fig.Segments.Add(new ArcSegment { Point = p3, Size = new Size(innerRadius, innerRadius), IsLargeArc = isLargeArc, SweepDirection = SweepDirection.Counterclockwise });

        var geom = new PathGeometry();
        geom.Figures.Add(fig);

        return new Microsoft.UI.Xaml.Shapes.Path { Data = geom, Fill = fill };
    }

    private static string Archived(bool value) => value ? " (Archived)" : string.Empty;
}
