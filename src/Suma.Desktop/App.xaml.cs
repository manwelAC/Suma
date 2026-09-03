using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Suma.Desktop.Composition;
using Suma.Infrastructure.Runtime;

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
            await _services.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            _window = _services.GetRequiredService<MainWindow>();
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
