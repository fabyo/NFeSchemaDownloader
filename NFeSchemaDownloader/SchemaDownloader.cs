using System.IO.Compression;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace NFeSchemaDownloader;

public class SchemaDownloader : ISchemaDownloader
{
    private readonly string _extractionDir;
    private readonly HttpClient _httpClient;
    private readonly ISchemaExtractor _extractor;
    private readonly ISchemaManifestStore _manifestStore;
    private readonly NFeSchemaOptions _options;
    private readonly IProgress<NFeSchemaSyncProgress> _progress;
    private readonly ILogger<SchemaDownloader> _logger;

    public SchemaDownloader(IReadOnlyList<BrowserContextCookiesResult> cookies, string baseUrl, HttpMessageHandler? handler = null, string extractionDir = "schemas/v4")
    {
        _extractionDir = extractionDir;
        _httpClient = new HttpClient(handler ?? new HttpClientHandler { UseCookies = false });
        _extractor = new SchemaExtractor(extractionDir);
        _manifestStore = new SchemaManifestStore(Path.Combine(extractionDir, ".nfe-schema-manifest.json"));
        _options = new NFeSchemaOptions
        {
            BaseUrl = baseUrl,
            ExtractionDirectory = extractionDir
        };
        _progress = new NullProgress<NFeSchemaSyncProgress>();
        _logger = NullLogger<SchemaDownloader>.Instance;
        
        var cookieString = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
        _httpClient.DefaultRequestHeaders.Add("Cookie", cookieString);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Referer", baseUrl);
    }

    public SchemaDownloader(
        HttpClient httpClient,
        ISchemaExtractor extractor,
        ISchemaManifestStore manifestStore,
        IOptions<NFeSchemaOptions> options,
        IProgress<NFeSchemaSyncProgress>? progress = null,
        ILogger<SchemaDownloader>? logger = null)
    {
        _httpClient = httpClient;
        _extractor = extractor;
        _manifestStore = manifestStore;
        _options = options.Value;
        _extractionDir = _options.ExtractionDirectory;
        _progress = progress ?? new NullProgress<NFeSchemaSyncProgress>();
        _logger = logger ?? NullLogger<SchemaDownloader>.Instance;
        _httpClient.Timeout = _options.HttpTimeout;
    }

    public async Task DownloadAndExtractAsync(IEnumerable<ReleasePackage> packages, CancellationToken cancellationToken = default)
    {
        await DownloadAndExtractAsync(packages, [], cancellationToken);
    }

    /// <inheritdoc />
    public async Task DownloadAndExtractAsync(
        IEnumerable<ReleasePackage> packages,
        IReadOnlyList<BrowserContextCookiesResult> cookies,
        CancellationToken cancellationToken = default)
    {
        var sortedPackages = packages.OrderBy(p => p.Date).ToList();

        if (sortedPackages.Count == 0)
        {
            _logger.LogInformation("No schema packages found for download");
            return;
        }

        ApplyBrowserHeaders(cookies);

        _logger.LogInformation(
            "Starting download of {PackageCount} schema packages with concurrency {Concurrency}",
            sortedPackages.Count,
            _options.MaxDownloadConcurrency);

        Directory.CreateDirectory(_extractionDir);
        var manifest = await _manifestStore.LoadAsync(cancellationToken);

        var maxConcurrency = Math.Max(1, _options.MaxDownloadConcurrency);
        using var downloadGate = new SemaphoreSlim(maxConcurrency);
        using var extractionGate = new SemaphoreSlim(1);
        using var manifestGate = new SemaphoreSlim(1);

        var downloadTasks = sortedPackages.Select(pkg => ProcessPackageAsync(
            pkg,
            manifest,
            downloadGate,
            extractionGate,
            manifestGate,
            sortedPackages.Count,
            cancellationToken));

        await Task.WhenAll(downloadTasks);

        _logger.LogInformation("Finished processing all schema packages");
    }

    private async Task ProcessPackageAsync(
        ReleasePackage pkg,
        SchemaManifest manifest,
        SemaphoreSlim downloadGate,
        SemaphoreSlim extractionGate,
        SemaphoreSlim manifestGate,
        int totalCount,
        CancellationToken cancellationToken)
    {
        await downloadGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            await manifestGate.WaitAsync(cancellationToken);
            try
            {
                if (!_options.OverwriteExistingFiles && manifest.ContainsProcessedPackage(pkg))
                {
                    _logger.LogInformation(
                        "Skipping package {PackageText} because it is already present in the local manifest",
                        pkg.Text);
                    _progress.Report(new NFeSchemaSyncProgress(
                        NFeSchemaSyncProgressKind.PackageSkipped,
                        $"Package already processed: {pkg.Text}",
                        pkg.Url,
                        pkg.Text,
                        TotalCount: totalCount));
                    return;
                }
            }
            finally
            {
                manifestGate.Release();
            }

            _logger.LogInformation(
                "Processing package {PackageText} published at {PackageDate:yyyy-MM-dd}",
                pkg.Text,
                pkg.Date);
            _progress.Report(new NFeSchemaSyncProgress(
                NFeSchemaSyncProgressKind.PackageDownloading,
                $"Downloading package: {pkg.Text}",
                pkg.Url,
                pkg.Text,
                TotalCount: totalCount));

            try
            {
                using var response = await GetWithRetryAsync(pkg.Url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Could not download package {PackageUrl}. Status code: {StatusCode}",
                        pkg.Url,
                        response.StatusCode);
                    _progress.Report(new NFeSchemaSyncProgress(
                        NFeSchemaSyncProgressKind.PackageSkipped,
                        $"Package download failed with status {response.StatusCode}: {pkg.Text}",
                        pkg.Url,
                        pkg.Text,
                        TotalCount: totalCount));
                    return;
                }

                if (!LooksLikeZipPackage(response, pkg.Url, out var packageFileName))
                {
                    _logger.LogWarning(
                        "Skipping package {PackageUrl} because response headers do not indicate a ZIP package. Content-Type: {ContentType}, Content-Disposition filename: {FileName}",
                        pkg.Url,
                        response.Content.Headers.ContentType?.MediaType,
                        packageFileName);
                    _progress.Report(new NFeSchemaSyncProgress(
                        NFeSchemaSyncProgressKind.PackageSkipped,
                        $"Package does not look like ZIP: {pkg.Text}",
                        pkg.Url,
                        pkg.Text,
                        TotalCount: totalCount));
                    return;
                }

                if (!string.IsNullOrWhiteSpace(packageFileName))
                {
                    _logger.LogInformation("Remote package filename reported as {PackageFileName}", packageFileName);
                    _progress.Report(new NFeSchemaSyncProgress(
                        NFeSchemaSyncProgressKind.PackageFileNameDetected,
                        $"Remote package filename: {packageFileName}",
                        pkg.Url,
                        pkg.Text,
                        packageFileName,
                        TotalCount: totalCount));
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                await extractionGate.WaitAsync(cancellationToken);
                try
                {
                    var extractedFiles = await _extractor.ExtractXsdFilesAsync(stream, cancellationToken);

                    await manifestGate.WaitAsync(cancellationToken);
                    try
                    {
                        manifest.UpsertPackage(pkg, extractedFiles);
                        await _manifestStore.SaveAsync(manifest, cancellationToken);
                    }
                    finally
                    {
                        manifestGate.Release();
                    }
                }
                finally
                {
                    extractionGate.Release();
                }

                _logger.LogInformation("Successfully processed package {PackageUrl}", pkg.Url);
                _progress.Report(new NFeSchemaSyncProgress(
                    NFeSchemaSyncProgressKind.PackageCompleted,
                    $"Package processed: {pkg.Text}",
                    pkg.Url,
                    pkg.Text,
                    CompletedCount: 1,
                    TotalCount: totalCount));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not process package {PackageUrl}", pkg.Url);
            }
        }
        finally
        {
            downloadGate.Release();
        }
    }

    private async Task<HttpResponseMessage> GetWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, _options.RetryCount + 1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!IsTransientStatusCode(response.StatusCode) || attempt == maxAttempts)
                {
                    return response;
                }

                _logger.LogWarning(
                    "Transient HTTP status {StatusCode} while downloading {PackageUrl}. Retrying attempt {Attempt} of {MaxAttempts}",
                    response.StatusCode,
                    url,
                    attempt + 1,
                    maxAttempts);
                response.Dispose();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < maxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Transient HTTP exception while downloading {PackageUrl}. Retrying attempt {Attempt} of {MaxAttempts}",
                    url,
                    attempt + 1,
                    maxAttempts);
            }

            await Task.Delay(GetRetryDelay(attempt), cancellationToken);
        }

        throw new InvalidOperationException("Retry loop ended without returning a response.");
    }

    private TimeSpan GetRetryDelay(int attempt)
    {
        var baseDelay = _options.RetryBaseDelay <= TimeSpan.Zero
            ? TimeSpan.Zero
            : _options.RetryBaseDelay;
        var multiplier = Math.Pow(2, attempt - 1);
        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * multiplier);
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        var statusCodeNumber = (int)statusCode;
        return statusCode == HttpStatusCode.RequestTimeout ||
            statusCode == HttpStatusCode.TooManyRequests ||
            statusCodeNumber >= 500;
    }

    private static bool IsTransientException(Exception exception)
    {
        return exception is HttpRequestException or TimeoutException or TaskCanceledException;
    }

    private static bool LooksLikeZipPackage(HttpResponseMessage response, string url, out string? packageFileName)
    {
        packageFileName = GetContentDispositionFileName(response);
        return ContentTypeLooksLikeZip(response) ||
            FileNameLooksLikeZip(packageFileName) ||
            UrlLooksLikeZip(url);
    }

    private static string? GetContentDispositionFileName(HttpResponseMessage response)
    {
        var contentDisposition = response.Content.Headers.ContentDisposition;
        return contentDisposition?.FileNameStar?.Trim('"') ??
            contentDisposition?.FileName?.Trim('"');
    }

    private static bool ContentTypeLooksLikeZip(HttpResponseMessage response)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;
        return !string.IsNullOrWhiteSpace(contentType) &&
            (contentType.Contains("zip", StringComparison.OrdinalIgnoreCase) ||
             contentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase));
    }

    private static bool FileNameLooksLikeZip(string? fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName) &&
            fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static bool UrlLooksLikeZip(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyBrowserHeaders(IReadOnlyList<BrowserContextCookiesResult> cookies)
    {
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        if (!_httpClient.DefaultRequestHeaders.Contains("Referer"))
        {
            _httpClient.DefaultRequestHeaders.Add("Referer", _options.BaseUrl);
        }

        if (cookies.Count > 0)
        {
            _httpClient.DefaultRequestHeaders.Remove("Cookie");
            var cookieString = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
            _httpClient.DefaultRequestHeaders.Add("Cookie", cookieString);
        }
    }
}
