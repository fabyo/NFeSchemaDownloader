![Logo](https://raw.githubusercontent.com/fabyo/NFEDanfe/main/logo-200.png)

[![NuGet](https://img.shields.io/nuget/v/NFeSchemaDownloader.svg)](https://www.nuget.org/packages/NFeSchemaDownloader)
[![Build](https://github.com/fabyo/NFeSchemaDownloader/actions/workflows/ci.yml/badge.svg)](https://github.com/fabyo/NFeSchemaDownloader/actions/workflows/ci.yml)
[![Scorecard](https://github.com/fabyo/NFeSchemaDownloader/actions/workflows/scorecard.yml/badge.svg)](https://github.com/fabyo/NFeSchemaDownloader/actions/workflows/scorecard.yml)
[![Downloads](https://img.shields.io/nuget/dt/NFeSchemaDownloader.svg)](https://www.nuget.org/packages/NFeSchemaDownloader)
[![GitHub stars](https://img.shields.io/github/stars/fabyo/NFeSchemaDownloader)](https://github.com/fabyo/NFeSchemaDownloader)
[![License](https://img.shields.io/github/license/fabyo/NFeSchemaDownloader)](https://github.com/fabyo/NFeSchemaDownloader)

**NFeSchemaDownloader** é uma biblioteca .NET projetada para resolver a dor de cabeça de manter os arquivos XSD da SEFAZ atualizados nos seus projetos de NF-e/NFC-e.

Biblioteca e CLI em .NET para realizar o download automático e a manutenção dos Schemas XML (XSD) oficiais da SEFAZ utilizando Playwright.

O projeto tem dois formatos de uso:

- `NFeSchemaDownloader`: biblioteca para integração direta em sistemas .NET, workers e ERPs.
- `NFeSchemaDownloader.Cli`: ferramenta de linha de comando para uso em pipelines de CI/CD (GitHub Actions, etc).

## Por que usar o NFeSchemaDownloader?

Todo sistema que faz emissão ou validação de Documentos Fiscais Eletrônicos (NF-e, NFC-e) precisa manter os arquivos XSD (Schemas XML) atualizados para garantir que o XML gerado está nas regras vigentes da SEFAZ.

**O problema:** Tradicionalmente, os desenvolvedores precisam acessar o portal da SEFAZ, procurar manualmente pelas atualizações, procurar e baixar um por um, ficar procurando, etc. Isso é chato, propenso a erros e fácil de esquecer.

**A solução:** Com a nossa biblioteca, você simplesmente instala o pacote e ele faz todo esse trabalho pesado para você! O `NFeSchemaDownloader` usa um navegador headless (Playwright) para lidar de forma transparente com as políticas de acesso e validações do portal, identifica automaticamente as versões oficiais desde 2017 e extrai os novos Schemas direto para a sua pasta `schemas/v4/`. Sem dor de cabeça, sem downloads manuais.

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

## Uso Automático no GitHub (CI/CD)

O poder real dessa ferramenta é deixar rodando em nuvem. Se você olhar o código fonte deste repositório, verá que utilizamos o projeto `NFeSchemaDownloader.Cli` em um arquivo `.github/workflows/publish.yml`.

Toda segunda-feira, de forma automática, o robô roda esse projeto na nuvem, acessa a SEFAZ, baixa novos XSDs se existirem e faz o _commit_ direto na branch principal do repositório, além de **bump de versão e publicação no NuGet automatizada!**

## Estrutura

- `NFeSchemaDownloader`: biblioteca reutilizável e pacote NuGet.
- `NFeSchemaDownloader.Cli`: CLI para rodar a automação no GitHub Actions.
- `schemas`: pasta oficial mantida sempre atualizada pelo robô onde ficam todos os XSDs extraídos.

## 🔗 Projetos relacionados

O ecossistema fiscal open-source é gigante! Conheça também:

| Projeto | Descrição |
|---|---|
| [NFEDanfe](https://github.com/fabyo/NFEDanfe) | Gera PDFs oficiais e bem feitos de DANFE a partir de XMLs da NF-e autorizada. |
| [NFEConsulta](https://github.com/fabyo/NFEConsulta) | Consulta NF-e, valida XML e verifica status oficial da SEFAZ. |

### Fluxo recomendado

```text
NFeSchemaDownloader (Mantém os arquivos XSD atualizados para validação prévia)
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
