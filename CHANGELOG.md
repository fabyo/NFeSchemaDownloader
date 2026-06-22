# Changelog

Todas as mudanças relevantes deste projeto serão documentadas neste arquivo.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
e este projeto segue [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Não lançado]

## [0.2.1] - 2026-06-22
### Atualizado
- Schemas XSD atualizados automaticamente.



## [0.2.0] - 2026-06-22

### Adicionado
- Estrutura inicial do projeto.
- Downloader de schemas XML da SEFAZ baseado em Playwright.
- Extração automática para o diretório `schemas/v4`.
- Aplicação CLI para execução em pipelines de CI/CD.
- Interfaces públicas para scraper, downloader, extractor e serviço de sincronização.
- Registro via `IServiceCollection` usando `AddNFeSchemaDownloader`.
- `NFeSchemaOptions` configurável para URL base, diretório de saída, timeout HTTP e concorrência de download.
- Documentação XML para os principais contratos públicos de sincronização.
- Concorrência configurável para downloads, com extração serializada para evitar escrita simultânea no mesmo diretório.
- Opções de CLI para `--output-dir`, `--timeout`, `--concurrency`, `--dry-run`, `--force` e `--help`.
- Modo dry-run para listar pacotes encontrados sem baixá-los.
- Comportamento configurável para sobrescrita de arquivos XSD extraídos.
- Manifesto incremental local salvo como `.nfe-schema-manifest.json` no diretório de extração.
- Rastreamento de SHA-256, tamanho e metadados do pacote para arquivos XSD extraídos.
- Opções configuráveis de retry/backoff para falhas HTTP transitórias.
- Opções de CLI para `--retry-count` e `--retry-delay`.
- Detecção de pacotes ZIP usando `Content-Type`, filename de `Content-Disposition` e URL do pacote.
- Documentação no README para cancelamento, dependency injection, opções de CLI, manifesto incremental, configuração do Playwright e testes de integração opt-in.
- Parser testável de pacotes SEFAZ para seções de release, filtros ZIP, parsing de data e normalização de URL.
- Relato de progresso via `IProgress<NFeSchemaSyncProgress>` com eventos de sincronização, pacote e arquivo.

### Alterado
- CLI agora propaga Ctrl+C para cancelamento da sincronização de schemas.
- Extração de schemas agora é feita por uma implementação dedicada de `ISchemaExtractor`.
- Fluxo principal agora usa `ILogger<T>` em vez de chamadas diretas a `Console.WriteLine`.
- CLI configura logging estruturado de console através do container de dependency injection.
- Downloads de schemas agora ignoram pacotes já presentes no manifesto local quando sobrescrita está desativada.
- Downloads HTTP agora fazem retry para falhas transitórias como 408, 429, 5xx, exceções de requisição e timeouts.
- Testes de integração SEFAZ/Playwright agora são opt-in via `NFESCHEMA_RUN_INTEGRATION_TESTS=true`, mantendo a suíte padrão offline e determinística.
- Respostas que não parecem pacotes ZIP agora são ignoradas com warning estruturado em vez de depender apenas de `Content-Type`.
- `SefazScraper` agora delega o parsing de pacotes para `ISefazPackageParser`, reduzindo acoplamento com Playwright e permitindo testes unitários sem rede.
- CLI agora registra um reporter de progresso no console para pacotes encontrados, ignorados, concluídos e dry-run.

### Corrigido
- Corrigido `NFeSchemaManager.SyncSchemasAsync`, que não aceitava `CancellationToken`; agora ele aceita e propaga cancelamento pelo scraper e downloader.
