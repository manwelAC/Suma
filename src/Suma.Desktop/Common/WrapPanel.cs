using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Suma.Desktop.Common;

/// <summary>Wraps toolbar controls without shrinking their text or changing their behavior.</summary>
public sealed class WrapPanel : Panel
{
    public double Spacing { get; set; } = 8;

    protected override Size MeasureOverride(Size availableSize) => Layout(availableSize.Width, false);
    protected override Size ArrangeOverride(Size finalSize)
    {
        Layout(finalSize.Width, true);
        return finalSize;
    }

    private Size Layout(double width, bool arrange)
    {
        double x = 0, y = 0, rowHeight = 0, usedWidth = 0;
        foreach (UIElement child in Children)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            if (!arrange) child.Measure(new Size(width, double.PositiveInfinity));
            Size size = child.DesiredSize;
            if (x > 0 && x + size.Width > width)
            {
                x = 0;
                y += rowHeight + Spacing;
                rowHeight = 0;
            }
            if (arrange) child.Arrange(new Rect(x, y, Math.Min(size.Width, width), size.Height));
            usedWidth = Math.Max(usedWidth, x + size.Width);
            x += size.Width + Spacing;
            rowHeight = Math.Max(rowHeight, size.Height);
        }
        return new Size(usedWidth, y + rowHeight);
    }
}
