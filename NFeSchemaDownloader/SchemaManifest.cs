using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace NFeSchemaDownloader;

/// <summary>
/// Metadata for an extracted schema file.
/// </summary>
public sealed class ExtractedSchemaFile
{
    /// <summary>
    /// Gets or sets the extracted file name.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 checksum encoded as lowercase hexadecimal.
    /// </summary>
    public required string Sha256 { get; set; }
}

/// <summary>
/// HTTP metadata observed while downloading a release package.
/// </summary>
public sealed class ReleasePackageHttpMetadata
{
    /// <summary>
    /// Gets or sets the filename reported by Content-Disposition, when available.
    /// </summary>
    public string? RemoteFileName { get; set; }

    /// <summary>
    /// Gets or sets the response Content-Type.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the response Content-Length.
    /// </summary>
    public long? ContentLength { get; set; }

    /// <summary>
    /// Gets or sets the response ETag.
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Gets or sets the response Last-Modified timestamp.
    /// </summary>
    public DateTimeOffset? LastModified { get; set; }
}

/// <summary>
/// Local manifest used to skip packages that were already processed.
/// </summary>
public sealed class SchemaManifest
{
    /// <summary>
    /// Gets or sets the manifest schema version.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets processed package entries.
    /// </summary>
    public List<SchemaManifestPackage> Packages { get; set; } = [];

    /// <summary>
    /// Returns whether the package was already processed with the same discovered metadata.
    /// </summary>
    /// <param name="package">Package discovered from SEFAZ.</param>
    public bool ContainsProcessedPackage(ReleasePackage package)
    {
        return Packages.Any(entry => string.Equals(entry.Url, package.Url, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds or replaces a processed package entry.
    /// </summary>
    /// <param name="package">Package discovered from SEFAZ.</param>
    /// <param name="files">Files extracted from the package.</param>
    /// <param name="httpMetadata">HTTP metadata observed while downloading the package.</param>
    public void UpsertPackage(
        ReleasePackage package,
        IReadOnlyList<ExtractedSchemaFile> files,
        ReleasePackageHttpMetadata? httpMetadata = null)
    {
        Packages.RemoveAll(entry => string.Equals(entry.Url, package.Url, StringComparison.OrdinalIgnoreCase));
        Packages.Add(new SchemaManifestPackage
        {
            Url = package.Url,
            Text = package.Text,
            PublishedAt = package.Date,
            ProcessedAt = DateTimeOffset.UtcNow,
            HttpMetadata = httpMetadata,
            Files = files.ToList()
        });
    }
}

/// <summary>
/// Metadata for one processed release package.
/// </summary>
public sealed class SchemaManifestPackage
{
    /// <summary>
    /// Gets or sets the package URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Gets or sets the package display text discovered on SEFAZ.
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Gets or sets the package publication date discovered on SEFAZ.
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets when this package was processed locally.
    /// </summary>
    public DateTimeOffset ProcessedAt { get; set; }

    /// <summary>
    /// Gets or sets HTTP metadata observed while downloading this package.
    /// </summary>
    public ReleasePackageHttpMetadata? HttpMetadata { get; set; }

    /// <summary>
    /// Gets or sets files extracted from this package.
    /// </summary>
    public List<ExtractedSchemaFile> Files { get; set; } = [];
}

/// <summary>
/// Persists the local schema manifest.
/// </summary>
public interface ISchemaManifestStore
{
    /// <summary>
    /// Loads the local manifest, or returns an empty manifest when none exists.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel file I/O cooperatively.</param>
    Task<SchemaManifest> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the local manifest.
    /// </summary>
    /// <param name="manifest">Manifest to persist.</param>
    /// <param name="cancellationToken">Token used to cancel file I/O cooperatively.</param>
    Task SaveAsync(SchemaManifest manifest, CancellationToken cancellationToken = default);
}

/// <summary>
/// JSON file implementation of the schema manifest store.
/// </summary>
public sealed class SchemaManifestStore : ISchemaManifestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _manifestPath;

    /// <summary>
    /// Creates a manifest store using configured output directory.
    /// </summary>
    /// <param name="options">Synchronization options.</param>
    public SchemaManifestStore(IOptions<NFeSchemaOptions> options)
    {
        _manifestPath = Path.Combine(options.Value.ExtractionDirectory, options.Value.ManifestFileName);
    }

    internal SchemaManifestStore(string manifestPath)
    {
        _manifestPath = manifestPath;
    }

    /// <inheritdoc />
    public async Task<SchemaManifest> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_manifestPath))
        {
            return new SchemaManifest();
        }

        await using var stream = new FileStream(_manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<SchemaManifest>(stream, JsonOptions, cancellationToken) ?? new SchemaManifest();
    }

    /// <inheritdoc />
    public async Task SaveAsync(SchemaManifest manifest, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_manifestPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(_manifestPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
    }
}
