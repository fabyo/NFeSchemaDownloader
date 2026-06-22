using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace NFeSchemaDownloader;

/// <summary>
/// Extracts XSD files from ZIP package streams.
/// </summary>
public sealed class SchemaExtractor : ISchemaExtractor
{
    private readonly string _extractionDir;
    private readonly bool _overwriteExistingFiles;
    private readonly IProgress<NFeSchemaSyncProgress> _progress;
    private readonly ILogger<SchemaExtractor> _logger;

    /// <summary>
    /// Creates a schema extractor using configured options and logger.
    /// </summary>
    /// <param name="options">Synchronization options.</param>
    /// <param name="logger">Logger used to report extraction progress.</param>
    public SchemaExtractor(
        IOptions<NFeSchemaOptions> options,
        IProgress<NFeSchemaSyncProgress>? progress = null,
        ILogger<SchemaExtractor>? logger = null)
        : this(options.Value.ExtractionDirectory, options.Value.OverwriteExistingFiles, progress, logger)
    {
    }

    internal SchemaExtractor(
        string extractionDir,
        bool overwriteExistingFiles = true,
        IProgress<NFeSchemaSyncProgress>? progress = null,
        ILogger<SchemaExtractor>? logger = null)
    {
        _extractionDir = extractionDir;
        _overwriteExistingFiles = overwriteExistingFiles;
        _progress = progress ?? new NullProgress<NFeSchemaSyncProgress>();
        _logger = logger ?? NullLogger<SchemaExtractor>.Instance;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExtractedSchemaFile>> ExtractXsdFilesAsync(Stream zipStream, CancellationToken cancellationToken = default)
    {
        var extractedFiles = new List<ExtractedSchemaFile>();
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!entry.FullName.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = Path.GetFileName(entry.FullName);
            var destPath = Path.Combine(_extractionDir, fileName);

            if (!_overwriteExistingFiles && File.Exists(destPath))
            {
                _logger.LogInformation("Skipping existing XSD file {DestinationPath}", destPath);
                var existingFile = await CreateFileMetadataAsync(destPath, cancellationToken);
                extractedFiles.Add(existingFile);
                ReportFile(existingFile, skipped: true);
                continue;
            }

            _logger.LogInformation("Extracting XSD file to {DestinationPath}", destPath);

            using var entryStream = entry.Open();
            var fileMode = _overwriteExistingFiles ? FileMode.Create : FileMode.CreateNew;
            await using (var outFile = new FileStream(destPath, fileMode, FileAccess.Write))
            {
                await entryStream.CopyToAsync(outFile, cancellationToken);
            }

            var extractedFile = await CreateFileMetadataAsync(destPath, cancellationToken);
            extractedFiles.Add(extractedFile);
            ReportFile(extractedFile, skipped: false);
        }

        return extractedFiles;
    }

    private void ReportFile(ExtractedSchemaFile file, bool skipped)
    {
        _progress.Report(new NFeSchemaSyncProgress(
            NFeSchemaSyncProgressKind.FileExtracted,
            skipped ? $"Existing XSD kept: {file.FileName}" : $"Extracted XSD: {file.FileName}",
            FileName: file.FileName));
    }

    private static async Task<ExtractedSchemaFile> CreateFileMetadataAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return new ExtractedSchemaFile
        {
            FileName = Path.GetFileName(path),
            Length = stream.Length,
            Sha256 = Convert.ToHexString(hash).ToLowerInvariant()
        };
    }
}
