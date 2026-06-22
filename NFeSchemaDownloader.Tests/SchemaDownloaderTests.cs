using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.Playwright;
using Moq;
using Moq.Protected;
using Xunit;

namespace NFeSchemaDownloader.Tests;

public class SchemaDownloaderTests
{
    private static Stream CreateMockZipStream()
    {
        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var xsdEntry = archive.CreateEntry("leiaute.xsd");
            using (var entryStream = xsdEntry.Open())
            {
                var bytes = Encoding.UTF8.GetBytes("<xs:schema></xs:schema>");
                entryStream.Write(bytes, 0, bytes.Length);
            }

            var txtEntry = archive.CreateEntry("leiaime.txt");
            using (var entryStream = txtEntry.Open())
            {
                var bytes = Encoding.UTF8.GetBytes("Leia-me");
                entryStream.Write(bytes, 0, bytes.Length);
            }
        }
        memoryStream.Position = 0;
        return memoryStream;
    }

    [Fact]
    public async Task DownloadAndExtractAsync_ShouldExtractOnlyXsdFiles()
    {
        // Arrange
        var tempExtractionDir = Path.Combine(Path.GetTempPath(), "schemas_test_" + Guid.NewGuid().ToString());
        
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StreamContent(CreateMockZipStream())
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip") }
                }
            });

        var cookies = new List<BrowserContextCookiesResult>
        {
            new BrowserContextCookiesResult { Name = "session", Value = "123" }
        };

        var downloader = new SchemaDownloader(cookies, "https://fake.sefaz.gov.br", mockHandler.Object, tempExtractionDir);

        var packages = new List<ReleasePackage>
        {
            new ReleasePackage { Date = DateTime.Now, Text = "Pacote Fake 1.00", Url = "https://fake.sefaz.gov.br/download.zip" }
        };

        // Act
        await downloader.DownloadAndExtractAsync(packages);

        // Assert
        Assert.True(Directory.Exists(tempExtractionDir));
        var files = Directory.GetFiles(tempExtractionDir);
        
        Assert.Single(files);
        Assert.EndsWith(".xsd", files[0]);
        Assert.Equal("leiaute.xsd", Path.GetFileName(files[0]));

        // Cleanup
        if (Directory.Exists(tempExtractionDir))
            Directory.Delete(tempExtractionDir, true);
    }
}
