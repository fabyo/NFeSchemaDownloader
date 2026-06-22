using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace NFeSchemaDownloader;

public class ReleasePackage
{
    public required string Url { get; set; }
    public DateTime Date { get; set; }
    public required string Text { get; set; }
}

public class SefazScraper
{
    private const string SEFAZ_URL = "https://www.nfe.fazenda.gov.br/portal/listaConteudo.aspx?tipoConteudo=BMPFMBoln3w=";
    private static readonly Regex DateRegex = new Regex(@"(\d{2}/\d{2}/\d{2,4})", RegexOptions.Compiled);

    public async Task<(List<ReleasePackage> Packages, IReadOnlyList<BrowserContextCookiesResult> Cookies)> ScrapeAsync()
    {
        Console.WriteLine("--- 🤖 Iniciando Playwright (Navegador Headless) ---");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            Locale = "pt-BR"
        });

        var page = await context.NewPageAsync();

        Console.WriteLine($"Navegando para {SEFAZ_URL}...");
        await page.GotoAsync(SEFAZ_URL, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        Console.WriteLine("--- ✅ Página carregada. Lendo HTML... ---");
        
        var packages = new List<ReleasePackage>();
        var currentSection = "";

        var elements = await page.QuerySelectorAllAsync("#conteudoDinamico .tituloSessao, #conteudoDinamico .indentacaoNormal p");

        foreach (var element in elements)
        {
            var className = await element.GetAttributeAsync("class") ?? "";
            
            if (className.Contains("tituloSessao"))
            {
                var text = await element.InnerTextAsync();
                var textLower = text.ToLowerInvariant();

                if (textLower.Contains("versões oficiais"))
                {
                    Console.WriteLine("--- 🟢 Entrando na seção 'Versões Oficiais' ---");
                    currentSection = "OFICIAIS";
                }
                else if (textLower.Contains("versões anteriores"))
                {
                    Console.WriteLine("--- 🟡 Entrando na seção 'Versões Anteriores' ---");
                    currentSection = "ANTERIORES";
                }
                else if (textLower.Contains("versões para testes"))
                {
                    Console.WriteLine("--- 🔴 Entrando na seção 'Testes' (Ignorando) ---");
                    currentSection = "TESTES";
                }
                continue;
            }

            if (currentSection != "OFICIAIS" && currentSection != "ANTERIORES")
            {
                continue;
            }

            var aTag = await element.QuerySelectorAsync("a");
            if (aTag == null) continue;

            var link = await aTag.GetAttributeAsync("href");
            if (string.IsNullOrWhiteSpace(link)) continue;

            var fullParagraphText = await element.InnerTextAsync();
            var fullParagraphTextLower = fullParagraphText.ToLowerInvariant();
            var aTagText = await aTag.InnerTextAsync();

            if (!fullParagraphTextLower.Contains("(zip)") && !aTagText.Contains("ZIP", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!fullParagraphTextLower.Contains("pacote de liberação") && !fullParagraphTextLower.Contains("esquema xml"))
            {
                continue;
            }

            var match = DateRegex.Match(fullParagraphText);
            if (!match.Success)
            {
                Console.WriteLine($"⚠️ Link ignorado (sem data): {aTagText}");
                continue;
            }

            var dateStr = match.Groups[1].Value;
            if (dateStr.Length == 8) // e.g. 01/01/17
            {
                dateStr = dateStr.Insert(6, "20");
            }

            if (!DateTime.TryParseExact(dateStr, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var pubDate))
            {
                Console.WriteLine($"⚠️ Erro ao parsear data '{dateStr}' para: {aTagText}");
                continue;
            }

            if (pubDate.Year < 2017)
            {
                continue; // Ignores old packages (pre-v4.00)
            }

            link = link.Trim().Replace(" ", "");
            var absoluteUrl = new Uri(new Uri(SEFAZ_URL), link).ToString();

            var pkg = new ReleasePackage
            {
                Url = absoluteUrl,
                Date = pubDate,
                Text = aTagText
            };

            packages.Add(pkg);
            Console.WriteLine($"📝 Encontrado: {pkg.Text} (Data: {pubDate:dd/MM/yyyy})");
        }

        var cookies = await context.CookiesAsync();
        return (packages, cookies);
    }
}
