namespace NFeSchemaDownloader;

public class NFeSchemaManager
{
    public static async Task SyncSchemasAsync()
    {
        Console.WriteLine("🚀 Iniciando NFeSchemaDownloader...");

        var scraper = new SefazScraper();
        var (packages, cookies) = await scraper.ScrapeAsync();

        if (packages.Count == 0)
        {
            Console.WriteLine("Nenhum pacote encontrado. Encerrando.");
            return;
        }

        var downloader = new SchemaDownloader(cookies, "https://www.nfe.fazenda.gov.br/portal/listaConteudo.aspx?tipoConteudo=BMPFMBoln3w=");
        await downloader.DownloadAndExtractAsync(packages);

        Console.WriteLine("🏁 Sincronização finalizada.");
    }
}
