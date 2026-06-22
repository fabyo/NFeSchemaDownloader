# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-06-22

### Added
- Initial project structure.
- Playwright-based SEFAZ XML Schema downloader.
- Automatic extraction to `schemas/v4` directory.
- CLI application to run in CI/CD pipelines.
- Public interfaces for scraper, downloader, extractor, and synchronization services.
- `IServiceCollection` registration through `AddNFeSchemaDownloader`.
- Configurable `NFeSchemaOptions` for base URL, output directory, HTTP timeout, and download concurrency.
- XML documentation for the main public synchronization contracts.
- Configurable download concurrency with serialized extraction to avoid concurrent writes to the same output directory.
- CLI options for `--output-dir`, `--timeout`, `--concurrency`, `--dry-run`, `--force`, and `--help`.
- Dry-run mode to list discovered packages without downloading them.
- Configurable overwrite behavior for extracted XSD files.
- Incremental local manifest stored as `.nfe-schema-manifest.json` in the extraction directory.
- SHA-256, size, and package metadata tracking for extracted XSD files.
- Configurable retry/backoff options for transient HTTP download failures.
- CLI options for `--retry-count` and `--retry-delay`.
- ZIP package detection using `Content-Type`, `Content-Disposition` filename, and package URL.
- README documentation for cancellation, dependency injection, CLI options, incremental manifest, Playwright setup, and opt-in integration tests.
- Testable SEFAZ package parser for release sections, ZIP filters, date parsing, and URL normalization.
- Progress reporting through `IProgress<NFeSchemaSyncProgress>` with synchronization, package, and file-level events.

### Changed
- CLI now propagates Ctrl+C cancellation to schema synchronization.
- Schema extraction is now handled by a dedicated `ISchemaExtractor` implementation.
- Core workflow now uses `ILogger<T>` instead of direct `Console.WriteLine` calls.
- CLI configures structured console logging through the dependency injection container.
- Schema downloads now skip packages already present in the local manifest when overwrite is disabled.
- HTTP downloads now retry transient failures such as 408, 429, 5xx, request exceptions, and timeouts.
- SEFAZ/Playwright integration tests are now opt-in via `NFESCHEMA_RUN_INTEGRATION_TESTS=true`, keeping the default test suite offline and deterministic.
- Responses that do not look like ZIP packages are now skipped with a structured warning instead of relying only on `Content-Type`.
- `SefazScraper` now delegates release-package parsing to `ISefazPackageParser`, reducing Playwright coupling and enabling unit tests without network access.
- CLI now registers a console progress reporter for discovered, skipped, completed, and dry-run packages.

### Fixed
- Fixed `NFeSchemaManager.SyncSchemasAsync` not accepting `CancellationToken`; it now accepts and propagates cancellation through scraper and downloader flows.
