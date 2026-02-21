using System.Globalization;
using Microsoft.Extensions.Logging;
using RcBridge.App.Cli;
using Serilog;

namespace RcBridge.App;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        ConfigureLogging();

        try
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(Log.Logger, dispose: false);
            });

            CommandHandlers handlers = new(loggerFactory);
            return await CliRootCommand.Create(handlers).Parse(args).InvokeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal application error");
            Console.Error.WriteLine($"Fatal: {ex.Message}");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureLogging()
    {
        Directory.CreateDirectory("logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(
                path: Path.Combine("logs", "rcbridge-.log"),
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();
    }
}
