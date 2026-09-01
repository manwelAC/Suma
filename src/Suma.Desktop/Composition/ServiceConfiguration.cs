using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Suma.Application;
using Suma.Infrastructure;
using Suma.Infrastructure.Runtime;

namespace Suma.Desktop.Composition;

internal static class ServiceConfiguration
{
    public static ServiceProvider Build()
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(new DebugLogEventSink())
            .CreateLogger();

        try
        {
            logger.Information("Starting Suma application.");
            var databasePath = LocalDataPaths.GetRuntimeDatabasePath();
            logger.Information("Resolved Suma database path: {DatabasePath}", databasePath);

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddSerilog(logger, dispose: true));
            services.AddApplication();
            services.AddInfrastructure(databasePath);
            services.AddDesktop();

            return services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateScopes = true,
                    ValidateOnBuild = true
                });
        }
        catch (Exception exception)
        {
            logger.Fatal(exception, "Suma runtime composition failed. Application startup cannot continue.");
            logger.Dispose();
            throw;
        }
    }
}
