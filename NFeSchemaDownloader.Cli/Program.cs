using System;
using System.Threading.Tasks;
using NFeSchemaDownloader;

namespace NFeSchemaDownloader.Cli
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                await NFeSchemaManager.SyncSchemasAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro crítico: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }
}
