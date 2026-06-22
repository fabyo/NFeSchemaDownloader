using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace NFeSchemaDownloader;

/// <summary>
/// Normalized SEFAZ page node used by the release package parser.
/// </summary>
public sealed record SefazContentNode(
    string ClassName,
    string Text,
    string? Link,
    string? LinkText);

/// <summary>
/// Parses release package links from normalized SEFAZ content nodes.
/// </summary>
public interface ISefazPackageParser
{
    /// <summary>
    /// Parses release package links from normalized SEFAZ content nodes.
    /// </summary>
    /// <param name="nodes">Nodes extracted from the SEFAZ content area.</param>
    IReadOnlyList<ReleasePackage> Parse(IEnumerable<SefazContentNode> nodes);
}

/// <summary>
/// Default parser for SEFAZ release package content.
/// </summary>
public sealed class SefazPackageParser : ISefazPackageParser
{
    private static readonly Regex DateRegex = new(@"(\d{2}/\d{2}/\d{2,4})", RegexOptions.Compiled);
    private readonly string _baseUrl;
    private readonly ILogger<SefazPackageParser> _logger;

    /// <summary>
    /// Creates a parser using configured options and logger.
    /// </summary>
    /// <param name="options">Synchronization options.</param>
    /// <param name="logger">Logger used to report ignored and accepted links.</param>
    public SefazPackageParser(IOptions<NFeSchemaOptions> options, ILogger<SefazPackageParser>? logger = null)
    {
        _baseUrl = options.Value.BaseUrl;
        _logger = logger ?? NullLogger<SefazPackageParser>.Instance;
    }

    /// <inheritdoc />
    public IReadOnlyList<ReleasePackage> Parse(IEnumerable<SefazContentNode> nodes)
    {
        var packages = new List<ReleasePackage>();
        var currentSection = "";

        foreach (var node in nodes)
        {
            if (node.ClassName.Contains("tituloSessao", StringComparison.OrdinalIgnoreCase))
            {
                currentSection = GetSection(node.Text);
                continue;
            }

            if (currentSection != "OFICIAIS" && currentSection != "ANTERIORES")
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.Link))
            {
                continue;
            }

            var fullParagraphTextLower = node.Text.ToLowerInvariant();
            var linkText = node.LinkText ?? "";

            if (!fullParagraphTextLower.Contains("(zip)") &&
                !linkText.Contains("ZIP", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!fullParagraphTextLower.Contains("pacote de liberação") &&
                !fullParagraphTextLower.Contains("pacote de liberaÃ§Ã£o") &&
                !fullParagraphTextLower.Contains("esquema xml"))
            {
                continue;
            }

            var match = DateRegex.Match(node.Text);
            if (!match.Success)
            {
                _logger.LogWarning("Ignoring package link without date: {PackageText}", linkText);
                continue;
            }

            var dateStr = match.Groups[1].Value;
            if (dateStr.Length == 8)
            {
                dateStr = dateStr.Insert(6, "20");
            }

            if (!DateTime.TryParseExact(dateStr, "dd/MM/yyyy", null, DateTimeStyles.None, out var publishedAt))
            {
                _logger.LogWarning(
                    "Could not parse package date {PackageDate} for {PackageText}",
                    dateStr,
                    linkText);
                continue;
            }

            if (publishedAt.Year < 2017)
            {
                continue;
            }

            var link = node.Link.Trim().Replace(" ", "");
            var absoluteUrl = new Uri(new Uri(_baseUrl), link).ToString();

            var package = new ReleasePackage
            {
                Url = absoluteUrl,
                Date = publishedAt,
                Text = linkText
            };

            packages.Add(package);
            _logger.LogInformation(
                "Found package {PackageText} published at {PackageDate:dd/MM/yyyy}",
                package.Text,
                package.Date);
        }

        return packages;
    }

    private static string GetSection(string text)
    {
        var textLower = text.ToLowerInvariant();

        if (textLower.Contains("versões oficiais") || textLower.Contains("versÃµes oficiais"))
        {
            return "OFICIAIS";
        }

        if (textLower.Contains("versões anteriores") || textLower.Contains("versÃµes anteriores"))
        {
            return "ANTERIORES";
        }

        if (textLower.Contains("versões para testes") || textLower.Contains("versÃµes para testes"))
        {
            return "TESTES";
        }

        return "";
    }
}
