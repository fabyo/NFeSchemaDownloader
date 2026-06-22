using Xunit;
using NFeSchemaDownloader;

namespace NFeSchemaDownloader.Tests;

public class SefazScraperTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ScrapeAsync_ShouldReturnPackagesAndCookies_WhenAccessingSefaz()
    {
        // Arrange
        var scraper = new SefazScraper();

        // Act
        // This hits the real SEFAZ website. Playwright must be installed locally.
        var result = await scraper.ScrapeAsync();

        // Assert
        Assert.NotNull(result);
        
        // Deve retornar pelo menos 1 pacote e pelo menos 1 cookie
        Assert.NotEmpty(result.Packages);
        Assert.NotEmpty(result.Cookies);

        // Validar que os pacotes tem links validos
        foreach (var pkg in result.Packages)
        {
            Assert.False(string.IsNullOrWhiteSpace(pkg.Url));
            Assert.StartsWith("http", pkg.Url);
            Assert.True(pkg.Date > DateTime.MinValue);
            Assert.False(string.IsNullOrWhiteSpace(pkg.Text));
        }

        // Validar que pegamos cookies importantes de sessao ou load balancer
        var hasRelevantCookies = result.Cookies.Any(c => !string.IsNullOrWhiteSpace(c.Name));
        Assert.True(hasRelevantCookies, "Deve retornar cookies preenchidos.");
    }
}
