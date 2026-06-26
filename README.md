![Logo](https://raw.githubusercontent.com/fabyo/NFeSchemaDownloader/main/logo-200.png)

[![NuGet](https://img.shields.io/nuget/v/NFeSchemaDownloader.svg)](https://www.nuget.org/packages/NFeSchemaDownloader)
[![Downloads](https://img.shields.io/nuget/dt/NFeSchemaDownloader.svg)](https://www.nuget.org/packages/NFeSchemaDownloader)
[![Build](https://github.com/fabyo/NFeSchemaDownloader/actions/workflows/ci.yml/badge.svg)](https://github.com/fabyo/NFeSchemaDownloader/actions/workflows/ci.yml)
[![Publish](https://github.com/fabyo/NFeSchemaDownloader/actions/workflows/publish.yml/badge.svg)](https://github.com/fabyo/NFeSchemaDownloader/actions/workflows/publish.yml)
[![Scorecard](https://github.com/fabyo/NFeSchemaDownloader/actions/workflows/scorecard.yml/badge.svg)](https://github.com/fabyo/NFeSchemaDownloader/actions/workflows/scorecard.yml)
[![GitHub stars](https://img.shields.io/github/stars/fabyo/NFeSchemaDownloader)](https://github.com/fabyo/NFeSchemaDownloader)
[![License](https://img.shields.io/github/license/fabyo/NFeSchemaDownloader)](https://github.com/fabyo/NFeSchemaDownloader)

**NFeSchemaDownloader** é uma biblioteca .NET projetada para baixar e organizar os Schemas XML (XSD) oficiais e manter atualizados nos seus projetos.

Biblioteca e CLI em .NET utilizando Playwright.

O projeto tem dois formatos de uso:

- `NFeSchemaDownloader`: biblioteca para integração direta em sistemas .NET, workers e ERPs.
- `NFeSchemaDownloader.Cli`: ferramenta de linha de comando para uso em pipelines de CI/CD (GitHub Actions, etc).

## Por que usar o NFeSchemaDownloader?

Todo sistema que faz emissão, consultas ou validação de Documentos Fiscais Eletrônicos (NF-e, NFC-e) precisa manter os arquivos XSD (Schemas XML) atualizados para garantir que o XML gerado está nas regras vigentes da SEFAZ.

**O problema:** Tradicionalmente, os desenvolvedores precisam acessar o portal da SEFAZ, procurar manualmente pelas atualizações, baixar e procurar dentro de zip um por um etc. Isso é chato, propenso a erros e fácil de esquecer.

**A solução:** Com a nossa biblioteca, você simplesmente instala o pacote e ele faz todo esse trabalho para você! O `NFeSchemaDownloader` usa um navegador headless (Playwright) para lidar de forma transparente com as políticas de acesso e validações do portal, identifica automaticamente as versões oficiais desde 2017 e extrai os novos Schemas direto para a sua pasta `schemas/v4/`. Sem dor de cabeça, sem downloads manuais.

## Recursos

- Compatível com .NET 10.
- Download inteligente com bypass utilizando `Microsoft.Playwright`.
- Download performático e paralelo via `HttpClient` herdando cookies validados do navegador.
- Extração de arquivos `.xsd` diretamente em memória a partir dos arquivos `.zip`.
- Suporte para execução isolada como CLI e forte tipagem para uso como Biblioteca C#.

## Instalação Como Biblioteca

Quando publicado no NuGet:

```powershell
dotnet add package NFeSchemaDownloader
```

Uso básico para baixar e sincronizar os schemas:

```csharp
using NFeSchemaDownloader;

// Executa a automação completa: abre o Playwright, varre a SEFAZ e baixa para a pasta local
await NFeSchemaManager.SyncSchemasAsync();
```

> Isso criará automaticamente a pasta `schemas/v4` na raiz de execução e colocará todos os XSDs sempre em sua versão mais atualizada!

Também é possível cancelar a sincronização de forma cooperativa:

```csharp
using NFeSchemaDownloader;

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
await NFeSchemaManager.SyncSchemasAsync(cts.Token);
```

## Uso com Dependency Injection

Para workers, APIs e aplicações com `IServiceCollection`, registre os serviços com `AddNFeSchemaDownloader`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using NFeSchemaDownloader;

var services = new ServiceCollection();

services.AddNFeSchemaDownloader(options =>
{
    options.ExtractionDirectory = "schemas/v4";
    options.MaxDownloadConcurrency = 2;
    options.HttpTimeout = TimeSpan.FromMinutes(2);
    options.PlaywrightNavigationTimeout = TimeSpan.FromSeconds(90);
    options.RetryCount = 3;
    options.RetryBaseDelay = TimeSpan.FromSeconds(1);
    options.ValidateExtractedSchemas = false;
});

using var serviceProvider = services.BuildServiceProvider();
var syncService = serviceProvider.GetRequiredService<INFeSchemaSyncService>();

await syncService.SyncSchemasAsync();
```

Para acompanhar progresso em uma UI, worker ou pipeline, registre `IProgress<NFeSchemaSyncProgress>`:

```csharp
services.AddSingleton<IProgress<NFeSchemaSyncProgress>>(
    new Progress<NFeSchemaSyncProgress>(progress =>
    {
        Console.WriteLine(progress.Message);
    }));
```

### Opções Disponíveis

| Opção | Padrão | Descrição |
|---|---:|---|
| `BaseUrl` | Portal SEFAZ NFe | Página usada para descobrir pacotes de schemas. |
| `ExtractionDirectory` | `schemas/v4` | Diretório onde os XSDs serão extraídos. |
| `MaxDownloadConcurrency` | `1` | Quantidade máxima de downloads simultâneos. |
| `HttpTimeout` | `00:02:00` | Timeout usado nos downloads HTTP. |
| `PlaywrightNavigationTimeout` | `00:01:30` | Timeout usado na navegação do Playwright. |
| `DryRun` | `false` | Lista pacotes descobertos sem baixar. |
| `OverwriteExistingFiles` | `true` | Permite sobrescrever XSDs existentes. |
| `ManifestFileName` | `.nfe-schema-manifest.json` | Arquivo de manifesto incremental. |
| `RetryCount` | `3` | Número de tentativas extras para falhas HTTP transientes. |
| `RetryBaseDelay` | `00:00:01` | Delay base do backoff exponencial. |
| `ValidateExtractedSchemas` | `false` | Valida os arquivos XSD após a extração. |

## Uso do CLI

Execute o projeto CLI diretamente durante desenvolvimento:

```powershell
dotnet run --project .\NFeSchemaDownloader.Cli\NFeSchemaDownloader.Cli.csproj -- --output-dir schemas/v4
```

Exemplos:

```powershell
dotnet run --project .\NFeSchemaDownloader.Cli\NFeSchemaDownloader.Cli.csproj -- --dry-run
dotnet run --project .\NFeSchemaDownloader.Cli\NFeSchemaDownloader.Cli.csproj -- --output-dir C:\schemas\nfe --concurrency 3
dotnet run --project .\NFeSchemaDownloader.Cli\NFeSchemaDownloader.Cli.csproj -- --timeout 120 --retry-count 5 --retry-delay 2
dotnet run --project .\NFeSchemaDownloader.Cli\NFeSchemaDownloader.Cli.csproj -- --playwright-timeout 180
dotnet run --project .\NFeSchemaDownloader.Cli\NFeSchemaDownloader.Cli.csproj -- --force
dotnet run --project .\NFeSchemaDownloader.Cli\NFeSchemaDownloader.Cli.csproj -- --validate-schemas
dotnet run --project .\NFeSchemaDownloader.Cli\NFeSchemaDownloader.Cli.csproj -- --json-logs
```

Flags disponíveis:

| Flag | Descrição |
|---|---|
| `--output-dir <path>` | Diretório onde os XSDs serão extraídos. |
| `--timeout <seconds|TimeSpan>` | Timeout HTTP, por exemplo `120` ou `00:02:00`. |
| `--playwright-timeout <seconds|TimeSpan>` | Timeout da navegação Playwright, por exemplo `180` ou `00:03:00`. |
| `--concurrency <number>` | Máximo de downloads simultâneos. |
| `--dry-run` | Lista pacotes encontrados sem baixar. |
| `--force` | Sobrescreve arquivos existentes e reprocessa pacotes. |
| `--retry-count <number>` | Número de retries para falhas HTTP transientes. |
| `--retry-delay <seconds|TimeSpan>` | Delay base do backoff exponencial. |
| `--validate-schemas` | Valida os arquivos XSD após a extração. |
| `--json-logs` | Emite logs estruturados em JSON para uso em CI. |
| `--help` | Mostra ajuda do CLI. |

## Manifesto Incremental

O downloader cria um manifesto local chamado `.nfe-schema-manifest.json` dentro do diretório de extração. Ele registra os pacotes processados, os metadados HTTP observados no download (`Content-Type`, `Content-Length`, `ETag`, `Last-Modified` e filename remoto) e os XSDs extraídos com tamanho e SHA-256.

Quando overwrite está desativado, pacotes já registrados no manifesto são ignorados para evitar downloads redundantes. No CLI, use `--force` quando quiser baixar e sobrescrever novamente.

## Extração Transacional

Os XSDs são extraídos primeiro para um diretório temporário dentro da pasta de saída. A biblioteca só promove os arquivos para o destino final depois que a extração e as validações opcionais terminam com sucesso. Se algo falhar no meio do processo, os arquivos finais existentes são preservados.

## Playwright e Testes de Integração

O scraper usa Playwright para abrir o portal da SEFAZ em navegador headless. Em ambientes novos, instale os browsers do Playwright antes de rodar a automação real:

```powershell
pwsh .\NFeSchemaDownloader\bin\Debug\net10.0\playwright.ps1 install
```

Os testes de integração que acessam a SEFAZ real ficam desativados por padrão. Para executá-los:

```powershell
$env:NFESCHEMA_RUN_INTEGRATION_TESTS='true'
dotnet test --filter Category=Integration
```

## Uso Automático no GitHub (CI/CD)

O poder real dessa ferramenta é deixar rodando em nuvem. Se você olhar o código fonte deste repositório, verá que utilizamos o projeto `NFeSchemaDownloader.Cli` em um arquivo `.github/workflows/publish.yml`.

Toda segunda-feira, de forma automática, o robô roda esse projeto na nuvem, acessa a SEFAZ, baixa novos XSDs se existirem e faz o _commit_ direto na branch principal do repositório, além de **bump de versão e publicação no NuGet automatizada!**

## Estrutura

- `NFeSchemaDownloader`: biblioteca reutilizável e pacote NuGet.
- `NFeSchemaDownloader.Cli`: CLI para rodar a automação no GitHub Actions.
- `schemas`: pasta oficial mantida sempre atualizada pelo robô onde ficam todos os XSDs extraídos.

## Troubleshooting

| Sintoma | Causa provável | Ação sugerida |
|---|---|---|
| Playwright falha ao abrir o navegador | Browsers do Playwright não instalados | Execute `pwsh .\NFeSchemaDownloader\bin\Debug\net10.0\playwright.ps1 install`. |
| Timeout ao carregar a SEFAZ | Portal lento ou rede instável | Aumente `PlaywrightNavigationTimeout` ou use `--playwright-timeout 180`. |
| Timeout durante download | ZIP grande, SEFAZ lenta ou proxy | Aumente `HttpTimeout` ou use `--timeout 300`. |
| Erro de proxy corporativo | Ambiente exige proxy HTTP/HTTPS | Configure `HTTP_PROXY` e `HTTPS_PROXY` no ambiente antes da execução. |
| Permissão negada ao salvar XSD | Diretório de saída sem permissão de escrita | Ajuste `--output-dir` para um caminho gravável. |
| `--validate-schemas` falha | XSD inválido ou imports/includes indisponíveis | Rode sem validação para baixar, ou valide o pacote manualmente com os XSDs dependentes disponíveis. |

## 🔗 Projetos relacionados

O ecossistema fiscal open-source é gigante! Conheça também:

| Projeto | Descrição |
|---|---|
| [NFEEmissor](https://github.com/fabyo/NFEEmissor) | Emissor completo de Nota Fiscal Eletrônica (NF-e). |
| [NFEDanfe](https://github.com/fabyo/NFEDanfe) | Gera PDFs oficiais e bem feitos de DANFE a partir de XMLs da NF-e autorizada. |
| [NFEConsulta](https://github.com/fabyo/NFEConsulta) | Consulta NF-e, valida XML e verifica status oficial da SEFAZ. |

### Fluxo recomendado

```text
NFeSchemaDownloader (Mantém os arquivos XSD atualizados para validação prévia)
   │
   ▼
NFEEmissor (Gera o XML da NF-e)
   │
   ▼
NF-e XML
   │
   ▼
NFEConsulta (Valida XML e consulta autorização)
   │
   ▼
NFEDanfe (Gera o PDF final)
```

## 👨‍💻 Autor

Fabyo Guimarães Oliveira

- LinkedIn: [https://www.linkedin.com/in/fabyo-guimaraes/](https://www.linkedin.com/in/fabyo-guimaraes/)
- GitHub: https://github.com/fabyo

## Licença

MIT.
