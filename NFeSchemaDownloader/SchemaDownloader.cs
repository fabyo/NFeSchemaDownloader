using System.IO.Compression;
using System.Net;
using Microsoft.Playwright;

namespace NFeSchemaDownloader;

public class SchemaDownloader
{
    private const string EXTRACTION_DIR = "schemas/v4";
    private readonly HttpClient _httpClient;

    public SchemaDownloader(IReadOnlyList<BrowserContextCookiesResult> cookies, string baseUrl)
    {
        var handler = new HttpClientHandler { UseCookies = false };
        _httpClient = new HttpClient(handler);
        
        var cookieString = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
        _httpClient.DefaultRequestHeaders.Add("Cookie", cookieString);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Referer", baseUrl);
    }

    public async Task DownloadAndExtractAsync(IEnumerable<ReleasePackage> packages)
    {
        var sortedPackages = packages.OrderBy(p => p.Date).ToList();

        if (sortedPackages.Count == 0)
        {
            Console.WriteLine("Nenhum pacote encontrado para download.");
            return;
        }

        Console.WriteLine($"Total de {sortedPackages.Count} pacotes relevantes encontrados. Iniciando downloads em ordem...");
        Console.WriteLine("-----------------------------------------------------");

        Directory.CreateDirectory(EXTRACTION_DIR);

        foreach (var pkg in sortedPackages)
        {
            Console.WriteLine($"🚀 Processando (Data: {pkg.Date:yyyy-MM-dd}): {pkg.Text}");

            try
            {
                using var response = await _httpClient.GetAsync(pkg.Url);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Erro ao BAIXAR {pkg.Url}: Status {response.StatusCode}");
                    continue;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                if (!contentType.Contains("zip") && !contentType.Contains("octet-stream"))
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                ExtractXsdFiles(stream);
                Console.WriteLine($"✅ Sucesso ao processar {pkg.Url}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar {pkg.Url}: {ex.Message}");
            }
        }

        Console.WriteLine("--- ✅ Processamento de todos os pacotes concluído! ---");
    }

    private void ExtractXsdFiles(Stream zipStream)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase))
                continue;

            var fileName = Path.GetFileName(entry.FullName);
            var destPath = Path.Combine(EXTRACTION_DIR, fileName);

            Console.WriteLine($"📦 Extraindo XSD: {destPath}");

            using var entryStream = entry.Open();
            using var outFile = new FileStream(destPath, FileMode.Create, FileAccess.Write);
            entryStream.CopyTo(outFile);
        }
    }
}
