using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Suma.Desktop.Composition;
using Suma.Infrastructure.Runtime;
using Suma.Desktop.Operations.Settings;

namespace Suma.Desktop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    private readonly ServiceProvider _services;

    internal Window? MainWindow => _window;

    public App()
    {
        InitializeComponent();
        _services = ServiceConfiguration.Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var logger = _services.GetRequiredService<ILogger<App>>();

        try
        {
            var requiresPin = await _services.GetRequiredService<ISettingsOperations>().IsPinEnabledAsync();
            var restore = await _services.GetRequiredService<IPendingRestoreApplier>().ApplyAsync();
            var mainWindow = _services.GetRequiredService<MainWindow>();
            _window = mainWindow;
            var destination = StartupDestinationSelector.Select(restore, requiresPin);
            if (destination == StartupDestination.Recovery)
            {
                mainWindow.ShowRecovery(restore.UserMessage!); _window.Activate(); logger.LogCritical("Suma finance startup was blocked because restore recovery is required."); return;
            }
            await _services.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            if (destination == StartupDestination.Lock) mainWindow.ShowLock(); else mainWindow.ShowShell();
            if (!string.IsNullOrWhiteSpace(restore.UserMessage)) mainWindow.ShowStartupError(restore.UserMessage);
            _window.Activate();
            logger.LogInformation("Suma main window activated.");
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Suma startup failed before window activation.");
            throw;
        }
    }
}
