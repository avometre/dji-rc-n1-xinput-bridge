using Microsoft.Extensions.Logging;
using RcBridge.Core.Config;
using RcBridge.Core.Mapping;
using RcBridge.Input.Dji.Capture;
using RcBridge.Input.Dji.Decoder;
using RcBridge.Input.Dji.Serial;
using RcBridge.Output.XInput.XInput;

namespace RcBridge.App.Cli;

public sealed partial class CommandHandlers
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<CommandHandlers> _logger;

    public CommandHandlers(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<CommandHandlers>();
    }

    public static void ListPorts()
    {
        IReadOnlyList<string> ports = SerialPortDiscovery.ListPorts();
        if (ports.Count == 0)
        {
            Console.WriteLine("No COM ports found.");
            return;
        }

        Console.WriteLine("Detected COM ports:");
        foreach (string port in ports)
        {
            Console.WriteLine($"- {port}");
        }
    }

    public static void Diagnose()
    {
        IReadOnlyList<string> ports = SerialPortDiscovery.ListPorts();
        ViGEmProbeResult probe;
        if (OperatingSystem.IsWindows())
        {
            probe = ViGEmAvailabilityProbe.Probe();
        }
        else
        {
            probe = new ViGEmProbeResult(false, "ViGEm probe is only supported on Windows 10/11.");
        }

        Console.WriteLine("== rcbridge diagnostics ==");
        Console.WriteLine();

        Console.WriteLine("COM ports:");
        if (ports.Count == 0)
        {
            Console.WriteLine("- none detected");
        }
        else
        {
            foreach (string port in ports)
            {
                Console.WriteLine($"- {port}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("ViGEmBus:");
        Console.WriteLine($"- {(probe.IsAvailable ? "OK" : "NOT AVAILABLE")}: {probe.Message}");

        Console.WriteLine();
        Console.WriteLine("Recommended next steps:");
        if (!probe.IsAvailable)
        {
            Console.WriteLine("1. Install ViGEmBus, then reboot if requested.");
        }

        if (ports.Count == 0)
        {
            Console.WriteLine("2. Install DJI USB/VCOM driver (DJI Assistant 2 may install it), then reconnect RC-N1.");
        }
        else
        {
            Console.WriteLine("2. Run `rcbridge capture --port COMx --baud 115200 --out captures/session.bin --seconds 20`.");
            Console.WriteLine("3. Tune `config.json`, then run `rcbridge run --port COMx --baud 115200 --config config.json`.");
        }
    }

    public async Task CaptureAsync(string port, int baud, string outputPath, int seconds, CancellationToken cancellationToken)
    {
        using CancellationTokenSource cts = SetupCtrlC(cancellationToken);
        ILogger<SerialFrameSource> sourceLogger = _loggerFactory.CreateLogger<SerialFrameSource>();

        try
        {
            using SerialFrameSource source = new(port, baud, sourceLogger);
            await using BinaryCaptureWriter writer = new(outputPath);

            DateTimeOffset stopAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, seconds));
            int frameCount = 0;
            int totalBytes = 0;

            Console.WriteLine($"Capturing on {port} @ {baud} for {seconds} second(s)... Press Ctrl+C to stop.");

            await foreach (var frame in source.ReadFramesAsync(cts.Token))
            {
                await writer.WriteFrameAsync(frame, cts.Token).ConfigureAwait(false);
                frameCount++;
                totalBytes += frame.Data.Length;

                if (DateTimeOffset.UtcNow >= stopAt)
                {
                    break;
                }
            }

            await writer.FlushAsync(cts.Token).ConfigureAwait(false);

            Console.WriteLine($"Capture complete: {frameCount} frame(s), {totalBytes} byte(s) -> {outputPath}");
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Cannot open {port}. It may be in use by another app (e.g., DJI Assistant 2).");
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Serial I/O error while capturing from {port}: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Capture canceled.");
        }
    }

    public async Task RunAsync(string port, int baud, string configPath, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("`run` is only supported on Windows 10/11 because ViGEmBus is Windows-only.");
            return;
        }

        using CancellationTokenSource cts = SetupCtrlC(cancellationToken);

        try
        {
            ConfigRoot config = ConfigLoader.LoadAndValidate(configPath);

            ILogger<SerialFrameSource> sourceLogger = _loggerFactory.CreateLogger<SerialFrameSource>();
            ILogger<DiagnosticDjiDecoder> decoderLogger = _loggerFactory.CreateLogger<DiagnosticDjiDecoder>();
            ILogger<XInputSink> sinkLogger = _loggerFactory.CreateLogger<XInputSink>();

            using SerialFrameSource source = new(port, baud, sourceLogger);
            DiagnosticDjiDecoder decoder = new(
                new DjiDecoderOptions
                {
                    DiagnosticMode = config.Decoder.DiagnosticMode,
                    HexDumpFrames = config.Decoder.HexDumpFrames,
                    MaxChannels = config.Decoder.MaxChannels,
                },
                decoderLogger);

            AxisMapper mapper = new(config);
            await using XInputSink sink = new(sinkLogger);
            await sink.ConnectAsync(cts.Token).ConfigureAwait(false);

            TimeSpan minInterval = TimeSpan.FromSeconds(1.0d / config.UpdateRateHz);
            DateTimeOffset nextWriteAt = DateTimeOffset.MinValue;

            Console.WriteLine("Bridge running. Press Ctrl+C to stop.");

            await foreach (var frame in source.ReadFramesAsync(cts.Token))
            {
                if (!decoder.TryDecode(frame, out var decoded))
                {
                    continue;
                }

                DateTimeOffset now = DateTimeOffset.UtcNow;
                if (now < nextWriteAt)
                {
                    continue;
                }

                var state = mapper.Map(decoded);
                await sink.SendAsync(state, cts.Token).ConfigureAwait(false);
                nextWriteAt = now.Add(minInterval);
            }
        }
        catch (ConfigValidationException ex)
        {
            Console.Error.WriteLine(ex.Message);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Cannot open {port}. It may be in use by another app or require reconnect.");
        }
        catch (ViGEmUnavailableException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Install ViGEmBus and rerun `rcbridge diagnose`.");
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Serial I/O error on {port}: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Run canceled.");
        }
        catch (Exception ex)
        {
            LogMessages.RunUnhandledError(_logger, ex);
            Console.Error.WriteLine($"Run failed: {ex.Message}");
        }
    }

    private static CancellationTokenSource SetupCtrlC(CancellationToken parentToken)
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);

        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        Console.CancelKeyPress += handler;
        cts.Token.Register(() => Console.CancelKeyPress -= handler);

        return cts;
    }

    private static partial class LogMessages
    {
        [LoggerMessage(EventId = 1301, Level = LogLevel.Error, Message = "Unhandled error in run command.")]
        public static partial void RunUnhandledError(ILogger logger, Exception exception);
    }
}
