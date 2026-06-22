using Microsoft.Playwright;

namespace NFeSchemaDownloader;

/// <summary>
/// Coordinates discovery, download, and extraction of official SEFAZ NFe schema packages.
/// </summary>
public interface INFeSchemaSyncService
{
    /// <summary>
    /// Synchronizes local XSD schemas with the release packages discovered on the SEFAZ portal.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the synchronization cooperatively.</param>
    Task SyncSchemasAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Discovers release packages and browser cookies from the SEFAZ portal.
/// </summary>
public interface ISefazScraper
{
    /// <summary>
    /// Scrapes the configured SEFAZ page and returns release packages with cookies required for downloads.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel scraping cooperatively.</param>
    Task<(List<ReleasePackage> Packages, IReadOnlyList<BrowserContextCookiesResult> Cookies)> ScrapeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Downloads release packages and delegates XSD extraction.
/// </summary>
public interface ISchemaDownloader
{
    /// <summary>
    /// Downloads and extracts the provided release packages.
    /// </summary>
    /// <param name="packages">Release packages to download.</param>
    /// <param name="cookies">Cookies captured from the browser session.</param>
    /// <param name="cancellationToken">Token used to cancel downloads cooperatively.</param>
    Task DownloadAndExtractAsync(
        IEnumerable<ReleasePackage> packages,
        IReadOnlyList<BrowserContextCookiesResult> cookies,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Extracts XSD files from ZIP package streams.
/// </summary>
public interface ISchemaExtractor
{
    /// <summary>
    /// Extracts XSD files from a ZIP stream into the configured output directory.
    /// </summary>
    /// <param name="zipStream">ZIP package stream.</param>
    /// <param name="cancellationToken">Token used to cancel extraction cooperatively.</param>
    Task<IReadOnlyList<ExtractedSchemaFile>> ExtractXsdFilesAsync(Stream zipStream, CancellationToken cancellationToken = default);
}
