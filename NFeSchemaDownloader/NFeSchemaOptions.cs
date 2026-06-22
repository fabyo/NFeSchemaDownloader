namespace NFeSchemaDownloader;

/// <summary>
/// Configuration options used by the NFe schema synchronization pipeline.
/// </summary>
public sealed class NFeSchemaOptions
{
    /// <summary>
    /// Default SEFAZ page used to discover schema release packages.
    /// </summary>
    public const string DefaultBaseUrl = "https://www.nfe.fazenda.gov.br/portal/listaConteudo.aspx?tipoConteudo=BMPFMBoln3w=";

    /// <summary>
    /// Gets or sets the SEFAZ page used to discover schema release packages.
    /// </summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>
    /// Gets or sets the directory where extracted XSD files are written.
    /// </summary>
    public string ExtractionDirectory { get; set; } = "schemas/v4";

    /// <summary>
    /// Gets or sets the maximum number of package downloads that may run concurrently.
    /// </summary>
    public int MaxDownloadConcurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets the timeout applied to HTTP package downloads.
    /// </summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets whether synchronization should only list discovered packages without downloading them.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets whether extracted XSD files may overwrite existing files.
    /// </summary>
    public bool OverwriteExistingFiles { get; set; } = true;

    /// <summary>
    /// Gets or sets the manifest file name stored in the extraction directory.
    /// </summary>
    public string ManifestFileName { get; set; } = ".nfe-schema-manifest.json";

    /// <summary>
    /// Gets or sets how many times transient HTTP download failures should be retried.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay used for exponential retry backoff.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
}
