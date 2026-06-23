using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace NFeSchemaDownloader;

public class ReleasePackage
{
    public required string Url { get; set; }
    public DateTime Date { get; set; }
    public required string Text { get; set; }
}

public class SefazScraper : ISefazScraper
{
    private readonly string _baseUrl;
    private readonly TimeSpan _navigationTimeout;
    private readonly ISefazPackageParser _parser;
    private readonly ILogger<SefazScraper> _logger;

    public SefazScraper()
        : this(
            Options.Create(new NFeSchemaOptions()),
            new SefazPackageParser(Options.Create(new NFeSchemaOptions())),
            NullLogger<SefazScraper>.Instance)
    {
    }

    public SefazScraper(
        IOptions<NFeSchemaOptions> options,
        ISefazPackageParser parser,
        ILogger<SefazScraper>? logger = null)
    {
        _baseUrl = options.Value.BaseUrl;
        _navigationTimeout = options.Value.PlaywrightNavigationTimeout;
        _parser = parser;
        _logger = logger ?? NullLogger<SefazScraper>.Instance;
    }

    /// <inheritdoc />
    public async Task<(List<ReleasePackage> Packages, IReadOnlyList<BrowserContextCookiesResult> Cookies)> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Playwright headless browser");

        cancellationToken.ThrowIfCancellationRequested();
        using var playwright = await Playwright.CreateAsync();
        cancellationToken.ThrowIfCancellationRequested();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        cancellationToken.ThrowIfCancellationRequested();

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            Locale = "pt-BR"
        });
        cancellationToken.ThrowIfCancellationRequested();

        var page = await context.NewPageAsync();
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Navigating to {BaseUrl}", _baseUrl);
        await page.GotoAsync(_baseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = (float)_navigationTimeout.TotalMilliseconds
        });
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("SEFAZ page loaded. Reading HTML");

        var elements = await page.QuerySelectorAllAsync("#conteudoDinamico .tituloSessao, #conteudoDinamico .indentacaoNormal p");
        cancellationToken.ThrowIfCancellationRequested();

        var nodes = new List<SefazContentNode>();
        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var className = await element.GetAttributeAsync("class") ?? "";
            var text = await element.InnerTextAsync();
            var linkElement = await element.QuerySelectorAsync("a");
            var link = linkElement is null ? null : await linkElement.GetAttributeAsync("href");
            var linkText = linkElement is null ? null : await linkElement.InnerTextAsync();

            nodes.Add(new SefazContentNode(className, text, link, linkText));
        }

        var packages = _parser.Parse(nodes).ToList();

        cancellationToken.ThrowIfCancellationRequested();
        var cookies = await context.CookiesAsync();
        return (packages, cookies);
    }
}
