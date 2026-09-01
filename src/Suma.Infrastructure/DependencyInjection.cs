using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Suma.Infrastructure.Persistence;

namespace Suma.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<SumaDbContext>(options => options.UseSqlite(connectionString));

        return services;
    }
}
