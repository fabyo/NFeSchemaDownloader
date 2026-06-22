using Xunit;

namespace NFeSchemaDownloader.Tests;

public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("NFESCHEMA_RUN_INTEGRATION_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set NFESCHEMA_RUN_INTEGRATION_TESTS=true to run SEFAZ/Playwright integration tests.";
        }
    }
}
