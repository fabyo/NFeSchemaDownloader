using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NFeSchemaDownloader.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNFeSchemaDownloader_ShouldRegisterServicesAndOptions()
    {
        var services = new ServiceCollection();

        services.AddNFeSchemaDownloader(options =>
        {
            options.ExtractionDirectory = "custom-schemas";
            options.MaxDownloadConcurrency = 4;
            options.DryRun = true;
            options.OverwriteExistingFiles = false;
            options.RetryCount = 5;
            options.RetryBaseDelay = TimeSpan.FromMilliseconds(250);
            options.ValidateExtractedSchemas = true;
        });

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<INFeSchemaSyncService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISefazPackageParser>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISefazScraper>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISchemaDownloader>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISchemaExtractor>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISchemaManifestStore>());
        Assert.NotNull(serviceProvider.GetRequiredService<IProgress<NFeSchemaSyncProgress>>());

        var options = serviceProvider.GetRequiredService<IOptions<NFeSchemaOptions>>().Value;
        Assert.Equal("custom-schemas", options.ExtractionDirectory);
        Assert.Equal(4, options.MaxDownloadConcurrency);
        Assert.True(options.DryRun);
        Assert.False(options.OverwriteExistingFiles);
        Assert.Equal(5, options.RetryCount);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.RetryBaseDelay);
        Assert.True(options.ValidateExtractedSchemas);
    }
}
