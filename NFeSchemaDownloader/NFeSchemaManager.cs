using Microsoft.Extensions.DependencyInjection;

namespace NFeSchemaDownloader;

/// <summary>
/// Static facade for running NFe schema synchronization with default services.
/// </summary>
public class NFeSchemaManager
{
    /// <summary>
    /// Synchronizes local XSD schemas with release packages discovered on the SEFAZ portal.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel synchronization cooperatively.</param>
    public static async Task SyncSchemasAsync(CancellationToken cancellationToken = default)
    {
        await using var serviceProvider = new ServiceCollection()
            .AddNFeSchemaDownloader()
            .BuildServiceProvider();

        var syncService = serviceProvider.GetRequiredService<INFeSchemaSyncService>();
        await syncService.SyncSchemasAsync(cancellationToken);
    }
}
