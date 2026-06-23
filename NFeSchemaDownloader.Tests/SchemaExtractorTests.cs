using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Options;

namespace NFeSchemaDownloader.Tests;

public class SchemaExtractorTests
{
    private static Stream CreateZipWithFile(string fileName, string content)
    {
        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry(fileName);
            using var entryStream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            entryStream.Write(bytes, 0, bytes.Length);
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    [Fact]
    public async Task ExtractXsdFilesAsync_ShouldValidateValidSchema_WhenEnabled()
    {
        var tempExtractionDir = Path.Combine(Path.GetTempPath(), "schemas_validate_ok_" + Guid.NewGuid());
        var options = Options.Create(new NFeSchemaOptions
        {
            ExtractionDirectory = tempExtractionDir,
            ValidateExtractedSchemas = true
        });
        var extractor = new SchemaExtractor(options);

        try
        {
            await using var zipStream = CreateZipWithFile(
                "valid.xsd",
                """
                <?xml version="1.0" encoding="utf-8"?>
                <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
                  <xs:element name="root" type="xs:string" />
                </xs:schema>
                """);

            var files = await extractor.ExtractXsdFilesAsync(zipStream);

            Assert.Single(files);
            Assert.Equal("valid.xsd", files[0].FileName);
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
    public async Task ExtractXsdFilesAsync_ShouldThrowForInvalidSchema_WhenValidationIsEnabled()
    {
        var tempExtractionDir = Path.Combine(Path.GetTempPath(), "schemas_validate_fail_" + Guid.NewGuid());
        var options = Options.Create(new NFeSchemaOptions
        {
            ExtractionDirectory = tempExtractionDir,
            ValidateExtractedSchemas = true
        });
        var extractor = new SchemaExtractor(options);

        try
        {
            await using var zipStream = CreateZipWithFile(
                "invalid.xsd",
                """
                <?xml version="1.0" encoding="utf-8"?>
                <not-a-schema />
                """);

            await Assert.ThrowsAsync<InvalidDataException>(() => extractor.ExtractXsdFilesAsync(zipStream));
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
