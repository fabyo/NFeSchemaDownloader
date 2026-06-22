using Microsoft.Extensions.Options;

namespace NFeSchemaDownloader.Tests;

public class SefazPackageParserTests
{
    private static SefazPackageParser CreateParser()
    {
        return new SefazPackageParser(Options.Create(new NFeSchemaOptions
        {
            BaseUrl = "https://www.nfe.fazenda.gov.br/portal/listaConteudo.aspx?tipoConteudo=BMPFMBoln3w="
        }));
    }

    [Fact]
    public void Parse_ShouldReturnPackagesFromOfficialAndPreviousSections()
    {
        var parser = CreateParser();
        var nodes = new[]
        {
            new SefazContentNode("tituloSessao", "Versões Oficiais", null, null),
            new SefazContentNode("indentacaoNormal", "Pacote de liberação 01/02/2024 (ZIP)", "download oficial.zip", "Pacote Oficial ZIP"),
            new SefazContentNode("tituloSessao", "Versões Anteriores", null, null),
            new SefazContentNode("indentacaoNormal", "Esquema XML 02/03/24", "anteriores/pacote.zip", "Pacote Anterior ZIP")
        };

        var packages = parser.Parse(nodes);

        Assert.Equal(2, packages.Count);
        Assert.Equal(new DateTime(2024, 2, 1), packages[0].Date);
        Assert.Equal("Pacote Oficial ZIP", packages[0].Text);
        Assert.DoesNotContain(" ", packages[0].Url);
        Assert.Equal(new DateTime(2024, 3, 2), packages[1].Date);
        Assert.StartsWith("https://www.nfe.fazenda.gov.br/portal/anteriores/pacote.zip", packages[1].Url);
    }

    [Fact]
    public void Parse_ShouldIgnoreTestSectionAndOldPackages()
    {
        var parser = CreateParser();
        var nodes = new[]
        {
            new SefazContentNode("tituloSessao", "Versões para testes", null, null),
            new SefazContentNode("indentacaoNormal", "Pacote de liberação 01/02/2024 (ZIP)", "teste.zip", "Pacote Teste ZIP"),
            new SefazContentNode("tituloSessao", "Versões Oficiais", null, null),
            new SefazContentNode("indentacaoNormal", "Pacote de liberação 01/02/2016 (ZIP)", "antigo.zip", "Pacote Antigo ZIP")
        };

        var packages = parser.Parse(nodes);

        Assert.Empty(packages);
    }

    [Fact]
    public void Parse_ShouldIgnoreNonZipAndNonSchemaLinks()
    {
        var parser = CreateParser();
        var nodes = new[]
        {
            new SefazContentNode("tituloSessao", "Versões Oficiais", null, null),
            new SefazContentNode("indentacaoNormal", "Manual 01/02/2024 (PDF)", "manual.pdf", "Manual PDF"),
            new SefazContentNode("indentacaoNormal", "Comunicado 01/02/2024 (ZIP)", "comunicado.zip", "Comunicado ZIP")
        };

        var packages = parser.Parse(nodes);

        Assert.Empty(packages);
    }
}
