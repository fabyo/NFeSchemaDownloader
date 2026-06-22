namespace NFeSchemaDownloader;

/// <summary>
/// Describes the current stage of schema synchronization.
/// </summary>
public enum NFeSchemaSyncProgressKind
{
    /// <summary>Synchronization has started.</summary>
    Started,

    /// <summary>Packages were discovered on the SEFAZ portal.</summary>
    PackagesDiscovered,

    /// <summary>A package was listed during dry-run mode.</summary>
    DryRunPackage,

    /// <summary>A package download is starting.</summary>
    PackageDownloading,

    /// <summary>A package was skipped.</summary>
    PackageSkipped,

    /// <summary>A remote package filename was found in response headers.</summary>
    PackageFileNameDetected,

    /// <summary>A schema file was extracted or observed.</summary>
    FileExtracted,

    /// <summary>A package was processed successfully.</summary>
    PackageCompleted,

    /// <summary>Synchronization has completed.</summary>
    Completed
}

/// <summary>
/// Progress event emitted by schema synchronization services.
/// </summary>
/// <param name="Kind">Progress event kind.</param>
/// <param name="Message">Human-readable progress message.</param>
/// <param name="PackageUrl">Related package URL, when available.</param>
/// <param name="PackageText">Related package text, when available.</param>
/// <param name="FileName">Related extracted file name, when available.</param>
/// <param name="CompletedCount">Completed package count, when available.</param>
/// <param name="TotalCount">Total package count, when available.</param>
public sealed record NFeSchemaSyncProgress(
    NFeSchemaSyncProgressKind Kind,
    string Message,
    string? PackageUrl = null,
    string? PackageText = null,
    string? FileName = null,
    int? CompletedCount = null,
    int? TotalCount = null);

internal sealed class NullProgress<T> : IProgress<T>
{
    public void Report(T value)
    {
    }
}
