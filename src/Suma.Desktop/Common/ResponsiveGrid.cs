using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Suma.Desktop.Common;

/// <summary>Reflows explicitly opted-in, single-row grids using their own available width.</summary>
public static class ResponsiveGrid
{
    public static readonly DependencyProperty MinimumColumnWidthProperty = DependencyProperty.RegisterAttached(
        "MinimumColumnWidth", typeof(double), typeof(ResponsiveGrid), new PropertyMetadata(0d, OnChanged));

    public static double GetMinimumColumnWidth(DependencyObject element) => (double)element.GetValue(MinimumColumnWidthProperty);
    public static void SetMinimumColumnWidth(DependencyObject element, double value) => element.SetValue(MinimumColumnWidthProperty, value);

    private static void OnChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not Grid grid || (double)args.NewValue <= 0 || (double)args.OldValue > 0) return;
        bool initialized = false;
        GridLength[] widths = [];
        FrameworkElement[] children = [];
        int previousColumns = -1;

        void Reflow()
        {
            if (grid.ActualWidth <= 0) return;
            if (!initialized)
            {
                widths = grid.ColumnDefinitions.Select(column => column.Width).ToArray();
                children = grid.Children.Cast<FrameworkElement>().OrderBy(child => Grid.GetColumn(child)).ToArray();
                if (widths.Length == 0)
                    widths = children.Select(_ => new GridLength(1, GridUnitType.Star)).ToArray();
                initialized = true;
            }
            if (widths.Length == 0) return;
            double available = grid.ActualWidth - grid.Padding.Left - grid.Padding.Right;
            int columns = Math.Clamp((int)((available + grid.ColumnSpacing) /
                (GetMinimumColumnWidth(grid) + grid.ColumnSpacing)), 1, widths.Length);
            if (columns == previousColumns) return;
            previousColumns = columns;
            grid.ColumnDefinitions.Clear();
            for (int index = 0; index < columns; index++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = columns == widths.Length ? widths[index] : new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Clear();
            for (int index = 0; index < (children.Length + columns - 1) / columns; index++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowSpacing = Math.Max(12, grid.RowSpacing);
            for (int index = 0; index < children.Length; index++)
            {
                Grid.SetColumn(children[index], index % columns);
                Grid.SetRow(children[index], index / columns);
            }
        }

        grid.Loaded += (_, _) => Reflow();
        grid.SizeChanged += (_, _) => Reflow();
    }
}
