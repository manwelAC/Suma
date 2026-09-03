using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Suma.Desktop.Shell;
using Suma.Desktop.Pages.Lock;
using Suma.Desktop.Pages.Recovery;
using Suma.Desktop.ViewModels;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Suma.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly Func<ShellPage> shellFactory;
    private readonly LockViewModel lockViewModel;
    private bool isFirstActivation = true;

    public MainWindow(Func<ShellPage> shellFactory, LockViewModel lockViewModel)
    {
        this.shellFactory = shellFactory;
        this.lockViewModel = lockViewModel;
        InitializeComponent();
        CustomizeTitleBar();
        AppWindow.Resize(new SizeInt32(1200, 760));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        Activated += (_, _) =>
        {
            if (isFirstActivation)
            {
                isFirstActivation = false;
                if (AppWindow.Presenter is OverlappedPresenter activePresenter)
                {
                    activePresenter.Maximize();
                }
            }
        };
    }

    public void ShowShell() => RootHost.Content = shellFactory();
    public void ShowLock() => RootHost.Content = new LockPage(lockViewModel, ShowShell);
    public void ShowRecovery(string message) => RootHost.Content = new RecoveryPage(message);
    public void ShowStartupError(string message) { StartupInfo.Message = message; StartupInfo.IsOpen = true; }

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
