using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NFeSchemaDownloader;

/// <summary>
/// Dependency injection extensions for NFe schema synchronization services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services required to discover, download, and extract NFe schemas.
    /// </summary>
    /// <param name="services">Service collection that receives the registrations.</param>
    /// <param name="configureOptions">Optional callback used to configure synchronization options.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddNFeSchemaDownloader(
        this IServiceCollection services,
        Action<NFeSchemaOptions>? configureOptions = null)
    {
        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<NFeSchemaOptions>(_ => { });
        }

        services.AddLogging();
        services.TryAddSingleton<IProgress<NFeSchemaSyncProgress>, NullProgress<NFeSchemaSyncProgress>>();
        services.AddHttpClient<SchemaDownloader>();
        services.AddSingleton<ISefazPackageParser, SefazPackageParser>();
        services.AddSingleton<ISefazScraper, SefazScraper>();
        services.AddSingleton<ISchemaExtractor, SchemaExtractor>();
        services.AddSingleton<ISchemaManifestStore, SchemaManifestStore>();
        services.AddTransient<ISchemaDownloader>(serviceProvider => serviceProvider.GetRequiredService<SchemaDownloader>());
        services.AddTransient<INFeSchemaSyncService, NFeSchemaSyncService>();

        return services;
    }
}
