using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Moq;
using Moq.Protected;
using Xunit;

namespace NFeSchemaDownloader.Tests;

public class SchemaDownloaderTests
{
    private sealed class ListProgress : IProgress<NFeSchemaSyncProgress>
    {
        public List<NFeSchemaSyncProgress> Events { get; } = [];

        public void Report(NFeSchemaSyncProgress value)
        {
            Events.Add(value);
        }
    }

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

    private static SchemaDownloader CreateDownloader(
        HttpMessageHandler handler,
        string extractionDir,
        Action<NFeSchemaOptions>? configureOptions = null,
        IProgress<NFeSchemaSyncProgress>? progress = null)
    {
        var schemaOptions = new NFeSchemaOptions
        {
            BaseUrl = "https://fake.sefaz.gov.br",
            ExtractionDirectory = extractionDir,
            RetryBaseDelay = TimeSpan.Zero
        };
        configureOptions?.Invoke(schemaOptions);

        var options = Options.Create(schemaOptions);
        return new SchemaDownloader(
            new HttpClient(handler),
            new SchemaExtractor(options),
            new SchemaManifestStore(options),
            options,
            progress);
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
        var files = Directory.GetFiles(tempExtractionDir, "*.xsd");
        
        Assert.Single(files);
        Assert.EndsWith(".xsd", files[0]);
        Assert.Equal("leiaute.xsd", Path.GetFileName(files[0]));

        // Cleanup
        if (Directory.Exists(tempExtractionDir))
            Directory.Delete(tempExtractionDir, true);
    }

    [Fact]
    public async Task DownloadAndExtractAsync_ShouldSkipPackageAlreadyRecordedInManifest_WhenOverwriteIsDisabled()
    {
        // Arrange
        var tempExtractionDir = Path.Combine(Path.GetTempPath(), "schemas_incremental_test_" + Guid.NewGuid());
        var requestCount = 0;

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback(() => requestCount++)
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StreamContent(CreateMockZipStream())
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip") }
                }
            });

        var options = Options.Create(new NFeSchemaOptions
        {
            BaseUrl = "https://fake.sefaz.gov.br",
            ExtractionDirectory = tempExtractionDir,
            OverwriteExistingFiles = false
        });

        var downloader = new SchemaDownloader(
            new HttpClient(mockHandler.Object),
            new SchemaExtractor(options),
            new SchemaManifestStore(options),
            options);

        var packages = new List<ReleasePackage>
        {
            new ReleasePackage
            {
                Date = new DateTime(2026, 1, 1),
                Text = "Pacote Fake 1.00",
                Url = "https://fake.sefaz.gov.br/download.zip"
            }
        };

        try
        {
            // Act
            await downloader.DownloadAndExtractAsync(packages, []);
            await downloader.DownloadAndExtractAsync(packages, []);

            // Assert
            Assert.Equal(1, requestCount);
            Assert.True(File.Exists(Path.Combine(tempExtractionDir, ".nfe-schema-manifest.json")));
        }
        finally
        {
            if (Directory.Exists(tempExtractionDir))
            {
                Directory.Delete(tempExtractionDir, true);
            }
        }
    }

    [Fact]
    public async Task DownloadAndExtractAsync_ShouldRetryTransientHttpStatus()
    {
        // Arrange
        var tempExtractionDir = Path.Combine(Path.GetTempPath(), "schemas_retry_test_" + Guid.NewGuid());
        var requestCount = 0;

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() =>
            {
                requestCount++;
                if (requestCount == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StreamContent(CreateMockZipStream())
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip") }
                    }
                };
            });

        var options = Options.Create(new NFeSchemaOptions
        {
            BaseUrl = "https://fake.sefaz.gov.br",
            ExtractionDirectory = tempExtractionDir,
            RetryCount = 1,
            RetryBaseDelay = TimeSpan.Zero
        });

        var downloader = new SchemaDownloader(
            new HttpClient(mockHandler.Object),
            new SchemaExtractor(options),
            new SchemaManifestStore(options),
            options);

        var packages = new List<ReleasePackage>
        {
            new ReleasePackage
            {
                Date = new DateTime(2026, 1, 1),
                Text = "Pacote Fake 1.00",
                Url = "https://fake.sefaz.gov.br/download.zip"
            }
        };

        try
        {
            // Act
            await downloader.DownloadAndExtractAsync(packages, []);

            // Assert
            Assert.Equal(2, requestCount);
            Assert.True(File.Exists(Path.Combine(tempExtractionDir, "leiaute.xsd")));
        }
        finally
        {
            if (Directory.Exists(tempExtractionDir))
            {
                Directory.Delete(tempExtractionDir, true);
            }
        }
    }

    [Fact]
    public async Task DownloadAndExtractAsync_ShouldAcceptZipFromContentDispositionFileName()
    {
        // Arrange
        var tempExtractionDir = Path.Combine(Path.GetTempPath(), "schemas_content_disposition_test_" + Guid.NewGuid());

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
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/download"),
                        ContentDisposition = new ContentDispositionHeaderValue("attachment")
                        {
                            FileName = "schema-package.zip"
                        }
                    }
                }
            });

        var downloader = CreateDownloader(mockHandler.Object, tempExtractionDir);
        var packages = new List<ReleasePackage>
        {
            new ReleasePackage
            {
                Date = new DateTime(2026, 1, 1),
                Text = "Pacote Fake 1.00",
                Url = "https://fake.sefaz.gov.br/download"
            }
        };

        try
        {
            // Act
            await downloader.DownloadAndExtractAsync(packages, []);

            // Assert
            Assert.True(File.Exists(Path.Combine(tempExtractionDir, "leiaute.xsd")));
        }
        finally
        {
            if (Directory.Exists(tempExtractionDir))
            {
                Directory.Delete(tempExtractionDir, true);
            }
        }
    }

    [Fact]
    public async Task DownloadAndExtractAsync_ShouldRecordHttpMetadataInManifest()
    {
        // Arrange
        var tempExtractionDir = Path.Combine(Path.GetTempPath(), "schemas_http_metadata_test_" + Guid.NewGuid());
        var lastModified = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() =>
            {
                var zipStream = CreateMockZipStream();
                var content = new StreamContent(zipStream)
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/download"),
                        ContentDisposition = new ContentDispositionHeaderValue("attachment")
                        {
                            FileName = "schema-package.zip"
                        },
                        ContentLength = zipStream.Length,
                        LastModified = lastModified
                    }
                };

                var response = new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = content
                };
                response.Headers.ETag = new EntityTagHeaderValue("\"abc123\"");

                return response;
            });

        var downloader = CreateDownloader(mockHandler.Object, tempExtractionDir);
        var packages = new List<ReleasePackage>
        {
            new ReleasePackage
            {
                Date = new DateTime(2026, 1, 1),
                Text = "Pacote Fake 1.00",
                Url = "https://fake.sefaz.gov.br/download"
            }
        };

        try
        {
            // Act
            await downloader.DownloadAndExtractAsync(packages, []);

            // Assert
            var manifestPath = Path.Combine(tempExtractionDir, ".nfe-schema-manifest.json");
            await using var manifestStream = File.OpenRead(manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<SchemaManifest>(manifestStream);
            var httpMetadata = Assert.Single(manifest!.Packages).HttpMetadata;

            Assert.NotNull(httpMetadata);
            Assert.Equal("schema-package.zip", httpMetadata.RemoteFileName);
            Assert.Equal("application/download", httpMetadata.ContentType);
            Assert.True(httpMetadata.ContentLength > 0);
            Assert.Equal("\"abc123\"", httpMetadata.ETag);
            Assert.Equal(lastModified, httpMetadata.LastModified);
        }
        finally
        {
            if (Directory.Exists(tempExtractionDir))
            {
                Directory.Delete(tempExtractionDir, true);
            }
        }
    }

    [Fact]
    public async Task DownloadAndExtractAsync_ShouldIgnoreResponseThatDoesNotLookLikeZip()
    {
        // Arrange
        var tempExtractionDir = Path.Combine(Path.GetTempPath(), "schemas_non_zip_test_" + Guid.NewGuid());

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
                Content = new StringContent("not a zip")
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("text/html") }
                }
            });

        var downloader = CreateDownloader(mockHandler.Object, tempExtractionDir);
        var packages = new List<ReleasePackage>
        {
            new ReleasePackage
            {
                Date = new DateTime(2026, 1, 1),
                Text = "Pacote Fake 1.00",
                Url = "https://fake.sefaz.gov.br/download"
            }
        };

        try
        {
            // Act
            await downloader.DownloadAndExtractAsync(packages, []);

            // Assert
            Assert.Empty(Directory.GetFiles(tempExtractionDir, "*.xsd"));
        }
        finally
        {
            if (Directory.Exists(tempExtractionDir))
            {
                Directory.Delete(tempExtractionDir, true);
            }
        }
    }

    [Fact]
    public async Task DownloadAndExtractAsync_ShouldReportPackageProgress()
    {
        // Arrange
        var tempExtractionDir = Path.Combine(Path.GetTempPath(), "schemas_progress_test_" + Guid.NewGuid());
        var progress = new ListProgress();

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
                    Headers = { ContentType = new MediaTypeHeaderValue("application/zip") }
                }
            });

        var downloader = CreateDownloader(mockHandler.Object, tempExtractionDir, progress: progress);
        var packages = new List<ReleasePackage>
        {
            new ReleasePackage
            {
                Date = new DateTime(2026, 1, 1),
                Text = "Pacote Fake 1.00",
                Url = "https://fake.sefaz.gov.br/download.zip"
            }
        };

        try
        {
            // Act
            await downloader.DownloadAndExtractAsync(packages, []);

            // Assert
            Assert.Contains(progress.Events, progressEvent => progressEvent.Kind == NFeSchemaSyncProgressKind.PackageDownloading);
            Assert.Contains(progress.Events, progressEvent => progressEvent.Kind == NFeSchemaSyncProgressKind.PackageCompleted);
        }
        finally
        {
            if (Directory.Exists(tempExtractionDir))
            {
                Directory.Delete(tempExtractionDir, true);
            }
        }
    }
}
