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
        IReadOnlyList<SerialPortInfo> ports = SerialPortDiscovery.ListPortInfos();
        if (ports.Count == 0)
        {
            Console.WriteLine("No COM ports found.");
            return;
        }

        Console.WriteLine("Detected COM ports:");
        PrintDetectedPorts(ports);
    }

    public static void Diagnose()
    {
        IReadOnlyList<SerialPortInfo> ports = SerialPortDiscovery.ListPortInfos();
        DjiPortResolution autoPortResolution = DjiPortResolver.Resolve("auto", ports);

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
            PrintDetectedPorts(ports);
        }

        Console.WriteLine();
        Console.WriteLine("Auto port selection (--port auto):");
        switch (autoPortResolution.Status)
        {
            case PortResolutionStatus.Resolved:
                Console.WriteLine($"- OK: {autoPortResolution.PortName}");
                break;
            case PortResolutionStatus.AmbiguousMatches:
                Console.WriteLine("- NOT RESOLVED: multiple DJI-like COM ports found.");
                PrintCandidates(autoPortResolution.Candidates);
                break;
            case PortResolutionStatus.NoDjiMatch:
                Console.WriteLine("- NOT RESOLVED: no DJI-like VCOM name match.");
                Console.WriteLine("  Use `--port COMx` manually or verify DJI USB/VCOM driver.");
                break;
            case PortResolutionStatus.NoPortsDetected:
                Console.WriteLine("- NOT RESOLVED: no COM ports detected.");
                break;
            default:
                Console.WriteLine("- NOT RESOLVED.");
                break;
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
        else if (autoPortResolution.Status == PortResolutionStatus.AmbiguousMatches)
        {
            Console.WriteLine("2. Use explicit port: `rcbridge run --port COMx --baud 115200 --config config.json`.");
            Console.WriteLine("3. Close apps that may keep COM busy (e.g., DJI Assistant 2).");
        }
        else
        {
            Console.WriteLine("2. Run `rcbridge capture --port auto --baud 115200 --out captures/session.bin --seconds 20`.");
            Console.WriteLine("3. Tune `config.json`, then run `rcbridge run --port auto --baud 115200 --config config.json`.");
        }
    }

    public async Task CaptureAsync(string port, int baud, string outputPath, int seconds, CancellationToken cancellationToken)
    {
        string? resolvedPortCandidate = ResolvePortOrReport(port, "capture");
        if (resolvedPortCandidate is null)
        {
            return;
        }

        string resolvedPort = resolvedPortCandidate;

        using CancellationTokenSource cts = SetupCtrlC(cancellationToken);
        ILogger<SerialFrameSource> sourceLogger = _loggerFactory.CreateLogger<SerialFrameSource>();

        try
        {
            using SerialFrameSource source = new(resolvedPort, baud, sourceLogger);
            await using BinaryCaptureWriter writer = new(outputPath);

            DateTimeOffset stopAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, seconds));
            int frameCount = 0;
            int totalBytes = 0;

            Console.WriteLine($"Capturing on {resolvedPort} @ {baud} for {seconds} second(s)... Press Ctrl+C to stop.");

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
            Console.Error.WriteLine($"Cannot open {resolvedPort}. It may be in use by another app (e.g., DJI Assistant 2).");
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Serial I/O error while capturing from {resolvedPort}: {ex.Message}");
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

        string? resolvedPortCandidate = ResolvePortOrReport(port, "run");
        if (resolvedPortCandidate is null)
        {
            return;
        }

        string resolvedPort = resolvedPortCandidate;

        using CancellationTokenSource cts = SetupCtrlC(cancellationToken);

        try
        {
            ConfigRoot config = ConfigLoader.LoadAndValidate(configPath);

            ILogger<SerialFrameSource> sourceLogger = _loggerFactory.CreateLogger<SerialFrameSource>();
            ILogger<DiagnosticDjiDecoder> decoderLogger = _loggerFactory.CreateLogger<DiagnosticDjiDecoder>();
            ILogger<XInputSink> sinkLogger = _loggerFactory.CreateLogger<XInputSink>();

            using SerialFrameSource source = new(resolvedPort, baud, sourceLogger);
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
            Console.Error.WriteLine($"Cannot open {resolvedPort}. It may be in use by another app or require reconnect.");
        }
        catch (ViGEmUnavailableException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Install ViGEmBus and rerun `rcbridge diagnose`.");
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Serial I/O error on {resolvedPort}: {ex.Message}");
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

    private static string? ResolvePortOrReport(string requestedPort, string commandName)
    {
        IReadOnlyList<SerialPortInfo> ports = SerialPortDiscovery.ListPortInfos();
        DjiPortResolution resolution = DjiPortResolver.Resolve(requestedPort, ports);

        switch (resolution.Status)
        {
            case PortResolutionStatus.Resolved:
                Console.WriteLine($"Auto-selected {resolution.PortName} for `{commandName}`.");
                return resolution.PortName;
            case PortResolutionStatus.ManualPortSelected:
                return resolution.PortName;
            case PortResolutionStatus.ManualPortNotFound:
                Console.Error.WriteLine($"Requested port '{requestedPort}' was not found.");
                PrintDetectedPorts(ports);
                return null;
            case PortResolutionStatus.NoPortsDetected:
                Console.Error.WriteLine("No COM ports detected. Install/check DJI USB/VCOM driver and reconnect RC-N1.");
                return null;
            case PortResolutionStatus.NoDjiMatch:
                Console.Error.WriteLine("`--port auto` could not find a DJI-like VCOM port.");
                PrintDetectedPorts(ports);
                Console.Error.WriteLine("Use `--port COMx` manually if you know the correct device.");
                return null;
            case PortResolutionStatus.AmbiguousMatches:
                Console.Error.WriteLine("`--port auto` found multiple DJI-like ports. Please select one explicitly with `--port COMx`.");
                PrintCandidates(resolution.Candidates);
                return null;
            default:
                return null;
        }
    }

    private static void PrintDetectedPorts(IReadOnlyList<SerialPortInfo> ports)
    {
        foreach (SerialPortInfo port in ports)
        {
            Console.WriteLine($"- {port.DisplayName}");
        }
    }

    private static void PrintCandidates(IReadOnlyList<DjiPortCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return;
        }

        Console.Error.WriteLine("Ranked candidates:");
        foreach (DjiPortCandidate candidate in candidates)
        {
            Console.Error.WriteLine($"- {candidate.Port.DisplayName} (score: {candidate.Score}, reason: {candidate.MatchReason})");
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
