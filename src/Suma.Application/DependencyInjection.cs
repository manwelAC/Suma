using Microsoft.Extensions.DependencyInjection;

namespace Suma.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
