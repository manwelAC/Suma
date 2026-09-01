using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Suma.Desktop.Shell;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Suma.Desktop;

public sealed partial class MainWindow : Window
{
    public MainWindow(ShellPage shellPage)
    {
        InitializeComponent();
        CustomizeTitleBar();
        ShellHost.Content = shellPage;
        AppWindow.Resize(new SizeInt32(1200, 760));
    }

    private void CustomizeTitleBar()
    {
        if (!Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported()
            || new AccessibilitySettings().HighContrast)
        {
            return;
        }

        var titleBar = AppWindow.TitleBar;
        var background = GetResourceColor("SumaBackgroundBrush");
        var foreground = GetResourceColor("SumaTextPrimaryBrush");
        var secondaryForeground = GetResourceColor("SumaTextSecondaryBrush");

        titleBar.BackgroundColor = background;
        titleBar.ForegroundColor = foreground;
        titleBar.InactiveBackgroundColor = background;
        titleBar.InactiveForegroundColor = secondaryForeground;
        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverBackgroundColor = GetResourceColor("SumaHoverSurfaceBrush");
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = GetResourceColor("SumaPressedSurfaceBrush");
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonInactiveBackgroundColor = background;
        titleBar.ButtonInactiveForegroundColor = secondaryForeground;
    }

    private static Color GetResourceColor(string resourceKey)
    {
        return ((SolidColorBrush)Microsoft.UI.Xaml.Application.Current.Resources[resourceKey]).Color;
    }
}
