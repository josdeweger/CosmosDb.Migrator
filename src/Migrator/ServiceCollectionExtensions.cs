using Microsoft.Extensions.DependencyInjection;

namespace CosmosDb.Migrator;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddMigrationRunner(this IServiceCollection services)
    {
        return services.AddScoped<IProvideCurrentVersion, CurrentVersionProvider>();
    }

}