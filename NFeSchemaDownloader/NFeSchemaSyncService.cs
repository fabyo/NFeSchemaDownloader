using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace NFeSchemaDownloader;

/// <summary>
/// Default implementation of the NFe schema synchronization workflow.
/// </summary>
public sealed class NFeSchemaSyncService : INFeSchemaSyncService
{
    private readonly ISefazScraper _scraper;
    private readonly ISchemaDownloader _downloader;
    private readonly NFeSchemaOptions _options;
    private readonly IProgress<NFeSchemaSyncProgress> _progress;
    private readonly ILogger<NFeSchemaSyncService> _logger;

    /// <summary>
    /// Creates a synchronization service.
    /// </summary>
    /// <param name="scraper">Scraper used to discover release packages.</param>
    /// <param name="downloader">Downloader used to fetch and extract release packages.</param>
    /// <param name="logger">Logger used to report synchronization progress.</param>
    public NFeSchemaSyncService(
        ISefazScraper scraper,
        ISchemaDownloader downloader,
        IOptions<NFeSchemaOptions> options,
        IProgress<NFeSchemaSyncProgress>? progress = null,
        ILogger<NFeSchemaSyncService>? logger = null)
    {
        _scraper = scraper;
        _downloader = downloader;
        _options = options.Value;
        _progress = progress ?? new NullProgress<NFeSchemaSyncProgress>();
        _logger = logger ?? NullLogger<NFeSchemaSyncService>.Instance;
    }

    /// <inheritdoc />
    public async Task SyncSchemasAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting NFe schema synchronization");
        _progress.Report(new NFeSchemaSyncProgress(
            NFeSchemaSyncProgressKind.Started,
            "Starting NFe schema synchronization"));

        var (packages, cookies) = await _scraper.ScrapeAsync(cancellationToken);
        _progress.Report(new NFeSchemaSyncProgress(
            NFeSchemaSyncProgressKind.PackagesDiscovered,
            $"{packages.Count} packages discovered",
            TotalCount: packages.Count));

        if (packages.Count == 0)
        {
            _logger.LogInformation("No schema packages found");
            _progress.Report(new NFeSchemaSyncProgress(
                NFeSchemaSyncProgressKind.Completed,
                "No schema packages found",
                CompletedCount: 0,
                TotalCount: 0));
            return;
        }

        if (_options.DryRun)
        {
            _logger.LogInformation("Dry run enabled. {PackageCount} packages were discovered and will not be downloaded", packages.Count);
            foreach (var package in packages.OrderBy(package => package.Date))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation(
                    "Dry run package: {PackageText} published at {PackageDate:yyyy-MM-dd} from {PackageUrl}",
                    package.Text,
                    package.Date,
                    package.Url);
                _progress.Report(new NFeSchemaSyncProgress(
                    NFeSchemaSyncProgressKind.DryRunPackage,
                    $"Dry run package: {package.Text}",
                    package.Url,
                    package.Text,
                    TotalCount: packages.Count));
            }

            _progress.Report(new NFeSchemaSyncProgress(
                NFeSchemaSyncProgressKind.Completed,
                "Dry run completed",
                CompletedCount: packages.Count,
                TotalCount: packages.Count));
            return;
        }

        await _downloader.DownloadAndExtractAsync(packages, cookies, cancellationToken);

        _logger.LogInformation("NFe schema synchronization finished");
        _progress.Report(new NFeSchemaSyncProgress(
            NFeSchemaSyncProgressKind.Completed,
            "NFe schema synchronization finished",
            CompletedCount: packages.Count,
            TotalCount: packages.Count));
    }
}
