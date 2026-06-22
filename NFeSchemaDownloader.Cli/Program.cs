using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NFeSchemaDownloader;

namespace NFeSchemaDownloader.Cli
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (!CliOptions.TryParse(args, out var cliOptions, out var errorMessage))
            {
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    Console.Error.WriteLine(errorMessage);
                }

                CliOptions.WriteUsage();
                Environment.Exit(string.IsNullOrWhiteSpace(errorMessage) ? 0 : 2);
                return;
            }

            using var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            try
            {
                await using var serviceProvider = new ServiceCollection()
                    .AddNFeSchemaDownloader(options =>
                    {
                        options.ExtractionDirectory = cliOptions.OutputDirectory;
                        options.HttpTimeout = cliOptions.Timeout;
                        options.MaxDownloadConcurrency = cliOptions.Concurrency;
                        options.DryRun = cliOptions.DryRun;
                        options.OverwriteExistingFiles = cliOptions.Force;
                        options.RetryCount = cliOptions.RetryCount;
                        options.RetryBaseDelay = cliOptions.RetryBaseDelay;
                    })
                    .AddLogging(builder =>
                    {
                        builder.ClearProviders();
                        builder.AddSimpleConsole(options =>
                        {
                            options.SingleLine = true;
                            options.TimestampFormat = "HH:mm:ss ";
                        });
                    })
                    .AddSingleton<IProgress<NFeSchemaSyncProgress>, ConsoleNFeSchemaProgress>()
                    .BuildServiceProvider();

                var syncService = serviceProvider.GetRequiredService<INFeSchemaSyncService>();
                await syncService.SyncSchemasAsync(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operação cancelada.");
                Environment.Exit(130);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro crítico: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }

    internal sealed class ConsoleNFeSchemaProgress : IProgress<NFeSchemaSyncProgress>
    {
        private int _completedPackages;

        public void Report(NFeSchemaSyncProgress value)
        {
            switch (value.Kind)
            {
                case NFeSchemaSyncProgressKind.PackagesDiscovered:
                    Console.WriteLine($"Pacotes encontrados: {value.TotalCount ?? 0}");
                    break;

                case NFeSchemaSyncProgressKind.PackageCompleted:
                    var completed = Interlocked.Increment(ref _completedPackages);
                    Console.WriteLine($"[{completed}/{value.TotalCount}] {value.Message}");
                    break;

                case NFeSchemaSyncProgressKind.PackageSkipped:
                case NFeSchemaSyncProgressKind.DryRunPackage:
                    Console.WriteLine(value.Message);
                    break;

                case NFeSchemaSyncProgressKind.Completed:
                    Console.WriteLine(value.Message);
                    break;
            }
        }
    }

    internal sealed class CliOptions
    {
        public string OutputDirectory { get; private init; } = "schemas/v4";

        public TimeSpan Timeout { get; private init; } = TimeSpan.FromMinutes(2);

        public int Concurrency { get; private init; } = 1;

        public bool DryRun { get; private init; }

        public bool Force { get; private init; }

        public int RetryCount { get; private init; } = 3;

        public TimeSpan RetryBaseDelay { get; private init; } = TimeSpan.FromSeconds(1);

        public static bool TryParse(string[] args, out CliOptions options, out string? errorMessage)
        {
            var outputDirectory = "schemas/v4";
            var timeout = TimeSpan.FromMinutes(2);
            var concurrency = 1;
            var dryRun = false;
            var force = false;
            var retryCount = 3;
            var retryBaseDelay = TimeSpan.FromSeconds(1);

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                var value = GetInlineValue(arg);
                var name = value is null ? arg : arg[..arg.IndexOf('=')];

                switch (name)
                {
                    case "-h":
                    case "--help":
                        options = new CliOptions();
                        errorMessage = null;
                        return false;

                    case "--output-dir":
                        if (!TryReadValue(args, ref i, value, name, out outputDirectory, out errorMessage))
                        {
                            options = new CliOptions();
                            return false;
                        }

                        break;

                    case "--timeout":
                        if (!TryReadValue(args, ref i, value, name, out var timeoutValue, out errorMessage) ||
                            !TryParseTimeout(timeoutValue, out timeout))
                        {
                            options = new CliOptions();
                            errorMessage ??= "Invalid --timeout value. Use seconds, for example 120, or a TimeSpan, for example 00:02:00.";
                            return false;
                        }

                        break;

                    case "--concurrency":
                        if (!TryReadValue(args, ref i, value, name, out var concurrencyValue, out errorMessage) ||
                            !int.TryParse(concurrencyValue, out concurrency) ||
                            concurrency < 1)
                        {
                            options = new CliOptions();
                            errorMessage ??= "Invalid --concurrency value. Use an integer greater than zero.";
                            return false;
                        }

                        break;

                    case "--dry-run":
                        dryRun = true;
                        break;

                    case "--force":
                        force = true;
                        break;

                    case "--retry-count":
                        if (!TryReadValue(args, ref i, value, name, out var retryCountValue, out errorMessage) ||
                            !int.TryParse(retryCountValue, out retryCount) ||
                            retryCount < 0)
                        {
                            options = new CliOptions();
                            errorMessage ??= "Invalid --retry-count value. Use zero or a positive integer.";
                            return false;
                        }

                        break;

                    case "--retry-delay":
                        if (!TryReadValue(args, ref i, value, name, out var retryDelayValue, out errorMessage) ||
                            !TryParseTimeout(retryDelayValue, out retryBaseDelay))
                        {
                            options = new CliOptions();
                            errorMessage ??= "Invalid --retry-delay value. Use seconds, for example 1, or a TimeSpan, for example 00:00:01.";
                            return false;
                        }

                        break;

                    default:
                        options = new CliOptions();
                        errorMessage = $"Unknown option: {arg}";
                        return false;
                }
            }

            options = new CliOptions
            {
                OutputDirectory = outputDirectory,
                Timeout = timeout,
                Concurrency = concurrency,
                DryRun = dryRun,
                Force = force,
                RetryCount = retryCount,
                RetryBaseDelay = retryBaseDelay
            };
            errorMessage = null;
            return true;
        }

        public static void WriteUsage()
        {
            Console.WriteLine("Usage: NFeSchemaDownloader.Cli [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --output-dir <path>       Directory where XSD files are extracted. Default: schemas/v4");
            Console.WriteLine("  --timeout <value>         HTTP timeout as seconds or TimeSpan. Default: 00:02:00");
            Console.WriteLine("  --concurrency <number>    Maximum concurrent downloads. Default: 1");
            Console.WriteLine("  --dry-run                 List discovered packages without downloading.");
            Console.WriteLine("  --force                   Overwrite existing XSD files.");
            Console.WriteLine("  --retry-count <number>    Retry count for transient HTTP failures. Default: 3");
            Console.WriteLine("  --retry-delay <value>     Base retry delay as seconds or TimeSpan. Default: 00:00:01");
            Console.WriteLine("  -h, --help                Show this help.");
        }

        private static string? GetInlineValue(string arg)
        {
            var separatorIndex = arg.IndexOf('=');
            return separatorIndex < 0 ? null : arg[(separatorIndex + 1)..];
        }

        private static bool TryReadValue(
            string[] args,
            ref int index,
            string? inlineValue,
            string optionName,
            out string value,
            out string? errorMessage)
        {
            if (inlineValue is not null)
            {
                value = inlineValue;
                errorMessage = null;
                return true;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = "";
                errorMessage = $"Missing value for {optionName}.";
                return false;
            }

            index++;
            value = args[index];
            errorMessage = null;
            return true;
        }

        private static bool TryParseTimeout(string value, out TimeSpan timeout)
        {
            if (int.TryParse(value, out var timeoutSeconds) && timeoutSeconds > 0)
            {
                timeout = TimeSpan.FromSeconds(timeoutSeconds);
                return true;
            }

            return TimeSpan.TryParse(value, out timeout) && timeout > TimeSpan.Zero;
        }
    }
}
