using Microsoft.Extensions.Logging;
using RcBridge.Core.Abstractions;
using RcBridge.Core.Config;
using RcBridge.Core.Mapping;
using RcBridge.Core.Models;
using RcBridge.Input.Dji.Capture;
using RcBridge.Input.Dji.Decoder;
using RcBridge.Input.Dji.Serial;
using RcBridge.Output.Linux.UInput;
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

        UInputProbeResult? uinputProbe = null;
        if (OperatingSystem.IsLinux())
        {
            uinputProbe = UInputAvailabilityProbe.Probe();
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

        if (uinputProbe is not null)
        {
            Console.WriteLine();
            Console.WriteLine("Linux uinput:");
            Console.WriteLine($"- {(uinputProbe.IsAvailable ? "OK" : "NOT AVAILABLE")}: {uinputProbe.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Recommended next steps:");
        int step = 1;

        if (OperatingSystem.IsWindows())
        {
            if (!probe.IsAvailable)
            {
                Console.WriteLine($"{step}. Install ViGEmBus, then reboot if requested.");
                step++;
            }
        }
        else
        {
            if (uinputProbe?.IsAvailable == true)
            {
                Console.WriteLine($"{step}. Non-Windows detected: use `run --mode linux-uinput` for virtual gamepad output.");
                Console.WriteLine("   Use `--mode dry-run` if you only want decode/mapping diagnostics.");
            }
            else
            {
                Console.WriteLine($"{step}. Non-Windows detected: use `run --mode dry-run` or `replay --mode dry-run`.");
                Console.WriteLine("   To enable Linux virtual gamepad output: `sudo modprobe uinput` then grant `/dev/uinput` write access.");
            }

            Console.WriteLine("   XInput output (`--mode xinput`) requires Windows 10/11 + ViGEmBus.");
            step++;
        }

        if (ports.Count == 0)
        {
            Console.WriteLine($"{step}. Install/check DJI USB/VCOM driver, then reconnect RC-N1.");
        }
        else if (autoPortResolution.Status == PortResolutionStatus.AmbiguousMatches)
        {
            string manualMode = OperatingSystem.IsWindows()
                ? "xinput"
                : (uinputProbe?.IsAvailable == true ? "linux-uinput" : "dry-run");

            Console.WriteLine($"{step}. Use explicit port: `rcbridge run --port COMx --baud 115200 --config config.json --mode {manualMode}`.");
            step++;
            Console.WriteLine($"{step}. Close apps that may keep COM busy (e.g., DJI Assistant 2).");
        }
        else
        {
            Console.WriteLine($"{step}. Run `rcbridge capture --port auto --baud 115200 --out captures/session.bin --seconds 20`.");
            step++;

            string recommendedMode = OperatingSystem.IsWindows()
                ? "xinput"
                : (uinputProbe?.IsAvailable == true ? "linux-uinput" : "dry-run");
            Console.WriteLine($"{step}. Tune `config.json`, then run `rcbridge run --port auto --baud 115200 --config config.json --mode {recommendedMode}`.");
        }
    }

    public async Task CaptureAsync(
        string port,
        int baud,
        string outputPath,
        int seconds,
        string note,
        CancellationToken cancellationToken)
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
            await using BinaryCaptureWriter writer = new(
                outputPath,
                new CaptureMetadata
                {
                    CreatedUtc = DateTimeOffset.UtcNow,
                    Port = resolvedPort,
                    BaudRate = baud,
                    Note = note.Trim(),
                    Tool = "rcbridge",
                },
                CaptureFileFormat.MetadataV2);

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
            Console.WriteLine("Capture format: v2 (metadata header + frame records).");
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

    public async Task RunAsync(string port, int baud, string configPath, string mode, CancellationToken cancellationToken)
    {
        if (!TryResolveOutputMode(mode, "run", out OutputMode outputMode))
        {
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
            ILogger<XInputSink> xInputSinkLogger = _loggerFactory.CreateLogger<XInputSink>();
            ILogger<LinuxUInputSink> linuxUInputSinkLogger = _loggerFactory.CreateLogger<LinuxUInputSink>();

            using SerialFrameSource source = new(resolvedPort, baud, sourceLogger);
            DiagnosticDjiDecoder decoder = new(BuildDecoderOptions(config, includeHexDump: true), decoderLogger);

            AxisMapper mapper = new(config);
            await using IXInputSink sink = CreateOutputSink(outputMode, xInputSinkLogger, linuxUInputSinkLogger, "run");
            await sink.ConnectAsync(cts.Token).ConfigureAwait(false);

            TimeSpan minInterval = TimeSpan.FromSeconds(1.0d / config.UpdateRateHz);
            DateTimeOffset nextWriteAt = DateTimeOffset.MinValue;

            Console.WriteLine($"Bridge running in mode '{GetOutputModeLabel(outputMode)}'. Press Ctrl+C to stop.");

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
            Console.Error.WriteLine("Use `--mode dry-run` or install ViGEmBus and rerun `rcbridge diagnose`.");
        }
        catch (LinuxUInputUnavailableException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Use `--mode dry-run` or run `rcbridge diagnose` to verify `/dev/uinput` readiness.");
        }
        catch (LinuxOutputException ex)
        {
            Console.Error.WriteLine($"Linux uinput output failed: {ex.Message}");
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

    public async Task InspectAsync(string capturePath, string configPath, bool decodePreview, CancellationToken cancellationToken)
    {
        using CancellationTokenSource cts = SetupCtrlC(cancellationToken);

        try
        {
            CaptureInspectionReport report = await CaptureInspector.InspectAsync(capturePath, cts.Token).ConfigureAwait(false);
            PrintInspectionReport(capturePath, report);

            if (!decodePreview)
            {
                return;
            }

            ConfigRoot config = ConfigLoader.LoadAndValidate(configPath);
            ILogger<DiagnosticDjiDecoder> decoderLogger = _loggerFactory.CreateLogger<DiagnosticDjiDecoder>();
            DiagnosticDjiDecoder decoder = new(BuildDecoderOptions(config, includeHexDump: false), decoderLogger);

            DecodedCaptureInspectionReport decodedReport =
                await DecodedCaptureInspector.InspectAsync(capturePath, decoder, cts.Token).ConfigureAwait(false);
            PrintDecodedInspectionReport(decodedReport);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
        }
        catch (ConfigValidationException ex)
        {
            Console.Error.WriteLine(ex.Message);
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Capture file is invalid: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Inspect canceled.");
        }
        catch (Exception ex)
        {
            LogMessages.InspectUnhandledError(_logger, ex);
            Console.Error.WriteLine($"Inspect failed: {ex.Message}");
        }
    }

    public async Task ReplayAsync(string capturePath, string configPath, string mode, CancellationToken cancellationToken)
    {
        using CancellationTokenSource cts = SetupCtrlC(cancellationToken);

        try
        {
            if (!TryResolveOutputMode(mode, "replay", out OutputMode outputMode))
            {
                return;
            }

            ConfigRoot config = ConfigLoader.LoadAndValidate(configPath);

            ILogger<DiagnosticDjiDecoder> decoderLogger = _loggerFactory.CreateLogger<DiagnosticDjiDecoder>();
            ILogger<XInputSink> xInputSinkLogger = _loggerFactory.CreateLogger<XInputSink>();
            ILogger<LinuxUInputSink> linuxUInputSinkLogger = _loggerFactory.CreateLogger<LinuxUInputSink>();

            DiagnosticDjiDecoder decoder = new(BuildDecoderOptions(config, includeHexDump: true), decoderLogger);

            AxisMapper mapper = new(config);

            await using IXInputSink sink = CreateOutputSink(outputMode, xInputSinkLogger, linuxUInputSinkLogger, "replay");
            await sink.ConnectAsync(cts.Token).ConfigureAwait(false);

            await using BinaryCaptureReader reader = new(capturePath);

            int frameCount = 0;
            int decodedCount = 0;
            int sentCount = 0;

            TimeSpan minInterval = TimeSpan.FromSeconds(1.0d / config.UpdateRateHz);
            DateTimeOffset nextWriteAt = DateTimeOffset.MinValue;

            Console.WriteLine($"Replay started from {capturePath} in mode '{GetOutputModeLabel(outputMode)}'. Press Ctrl+C to stop.");

            await foreach (var frame in reader.ReadFramesAsync(cts.Token))
            {
                frameCount++;

                if (!decoder.TryDecode(frame, out DecodedFrame? decoded))
                {
                    continue;
                }

                decodedCount++;

                DateTimeOffset now = DateTimeOffset.UtcNow;
                if (now < nextWriteAt)
                {
                    continue;
                }

                var state = mapper.Map(decoded);
                await sink.SendAsync(state, cts.Token).ConfigureAwait(false);
                sentCount++;
                nextWriteAt = now.Add(minInterval);
            }

            Console.WriteLine($"Replay complete: {frameCount} frame(s), {decodedCount} decoded, {sentCount} sent.");
        }
        catch (ConfigValidationException ex)
        {
            Console.Error.WriteLine(ex.Message);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Capture file is invalid: {ex.Message}");
        }
        catch (ViGEmUnavailableException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Use `--mode dry-run` or install ViGEmBus.");
        }
        catch (LinuxUInputUnavailableException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Use `--mode dry-run` or run `rcbridge diagnose` to verify `/dev/uinput` readiness.");
        }
        catch (LinuxOutputException ex)
        {
            Console.Error.WriteLine($"Linux uinput output failed: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Replay canceled.");
        }
        catch (Exception ex)
        {
            LogMessages.ReplayUnhandledError(_logger, ex);
            Console.Error.WriteLine($"Replay failed: {ex.Message}");
        }
    }

    private static IXInputSink CreateOutputSink(
        OutputMode outputMode,
        ILogger<XInputSink> xInputSinkLogger,
        ILogger<LinuxUInputSink> linuxUInputSinkLogger,
        string commandName)
    {
        switch (outputMode)
        {
            case OutputMode.DryRun:
                return new ReplayDryRunSink();

            case OutputMode.XInput:
                if (!OperatingSystem.IsWindows())
                {
                    throw new ViGEmUnavailableException($"{commandName} mode 'xinput' is supported only on Windows 10/11.");
                }

                return new XInputSink(xInputSinkLogger);

            case OutputMode.LinuxUInput:
                if (!OperatingSystem.IsLinux())
                {
                    throw new LinuxUInputUnavailableException($"{commandName} mode 'linux-uinput' is supported only on Linux.");
                }

                return new LinuxUInputSink(linuxUInputSinkLogger);

            default:
                throw new InvalidOperationException($"Unsupported output mode: {outputMode}.");
        }
    }

    private static void PrintInspectionReport(string capturePath, CaptureInspectionReport report)
    {
        Console.WriteLine($"== capture inspect: {capturePath} ==");

        if (report.Metadata is not null)
        {
            Console.WriteLine();
            Console.WriteLine("Metadata:");
            Console.WriteLine($"- formatVersion: {report.Metadata.FormatVersion}");
            Console.WriteLine($"- createdUtc: {report.Metadata.CreatedUtc:O}");
            Console.WriteLine($"- port: {report.Metadata.Port}");
            Console.WriteLine($"- baudRate: {report.Metadata.BaudRate}");
            if (!string.IsNullOrWhiteSpace(report.Metadata.Note))
            {
                Console.WriteLine($"- note: {report.Metadata.Note}");
            }
            Console.WriteLine($"- tool: {report.Metadata.Tool}");
        }

        if (report.FrameCount == 0)
        {
            Console.WriteLine("No frames found in capture.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Frames: {report.FrameCount}");
        Console.WriteLine($"Payload bytes: {report.TotalPayloadBytes}");
        Console.WriteLine(
            $"Frame length: min={report.MinFrameLength}, max={report.MaxFrameLength}, avg={report.AverageFrameLength:F2}");

        Console.WriteLine();
        Console.WriteLine("Frame length histogram (top):");
        foreach (FrameLengthBucket bucket in report.FrameLengthHistogram.Take(10))
        {
            Console.WriteLine($"- len={bucket.Length}, count={bucket.Count}");
        }

        Console.WriteLine();
        Console.WriteLine("Top byte frequencies:");
        foreach (ByteFrequencyEntry entry in report.TopByteFrequencies)
        {
            Console.WriteLine($"- 0x{entry.Value:X2}: count={entry.Count}, share={(entry.Percentage * 100.0):F2}%");
        }

        Console.WriteLine();
        Console.WriteLine("Sync-byte candidates (first-byte frequency):");
        foreach (SyncByteCandidate candidate in report.SyncByteCandidates)
        {
            Console.WriteLine($"- 0x{candidate.Value:X2}: count={candidate.Count}, share={(candidate.Percentage * 100.0):F2}%");
        }

        Console.WriteLine();
        Console.WriteLine("Correlation hints (byte-position pairs):");
        if (report.CorrelationHints.Count == 0)
        {
            Console.WriteLine("- none above threshold");
            return;
        }

        foreach (CorrelationHint hint in report.CorrelationHints.Take(10))
        {
            Console.WriteLine(
                $"- pos[{hint.PositionA}] <-> pos[{hint.PositionB}] : r={hint.Correlation:F3}, n={hint.SampleCount}");
        }
    }

    private static void PrintDecodedInspectionReport(DecodedCaptureInspectionReport report)
    {
        Console.WriteLine();
        Console.WriteLine("Decode preview:");
        Console.WriteLine($"- source frames: {report.FrameCount}");
        Console.WriteLine($"- decoded frames: {report.DecodedFrameCount}");

        if (report.DecodedFrameCount == 0)
        {
            Console.WriteLine("- no decodable frames with current decoder config.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Decoder hints:");
        foreach (DecoderHintStat hint in report.DecoderHints)
        {
            Console.WriteLine($"- {hint.Hint}: count={hint.Count}, share={(hint.PercentageOfDecodedFrames * 100.0d):F2}%");
        }

        Console.WriteLine();
        Console.WriteLine("Channel activity (top 12 by samples):");
        foreach (ChannelActivityStat stat in report.ChannelStats
                     .OrderByDescending(static channel => channel.Samples)
                     .ThenBy(static channel => channel.Channel)
                     .Take(12))
        {
            Console.WriteLine(
                $"- ch{stat.Channel}: n={stat.Samples}, min={stat.Min:F3}, max={stat.Max:F3}, mean={stat.Mean:F3}, sd={stat.StdDev:F3}, buckets={stat.DistinctBucketCount}");
        }

        Console.WriteLine();
        Console.WriteLine("Button/switch candidates:");
        if (report.ButtonCandidates.Count == 0)
        {
            Console.WriteLine("- none");
            return;
        }

        foreach (ButtonCandidateHint candidate in report.ButtonCandidates)
        {
            Console.WriteLine(
                $"- ch{candidate.Channel}: {candidate.Kind} ({candidate.Reason}, min={candidate.Min:F3}, max={candidate.Max:F3})");
        }
    }

    private static DjiDecoderOptions BuildDecoderOptions(ConfigRoot config, bool includeHexDump)
    {
        return new DjiDecoderOptions
        {
            DiagnosticMode = config.Decoder.DiagnosticMode,
            HexDumpFrames = includeHexDump && config.Decoder.HexDumpFrames,
            MaxChannels = config.Decoder.MaxChannels,
            EnableProtocolDecodeAttempt = config.Decoder.EnableProtocolDecodeAttempt,
            FrameSyncByte = (byte)config.Decoder.FrameSyncByte,
            MinFramePayloadLength = config.Decoder.MinFramePayloadLength,
            MaxFramePayloadLength = config.Decoder.MaxFramePayloadLength,
            PackedChannelMinRaw = config.Decoder.PackedChannelMinRaw,
            PackedChannelMaxRaw = config.Decoder.PackedChannelMaxRaw,
            ChecksumMode = ParseChecksumMode(config.Decoder.ChecksumMode),
            ChecksumIncludesHeader = config.Decoder.ChecksumIncludesHeader,
        };
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

    private static string GetOutputModeLabel(OutputMode outputMode)
    {
        return outputMode switch
        {
            OutputMode.DryRun => "dry-run",
            OutputMode.XInput => "xinput",
            OutputMode.LinuxUInput => "linux-uinput",
            _ => "unknown",
        };
    }

    private static bool TryResolveOutputMode(string? modeValue, string commandName, out OutputMode outputMode)
    {
        string requestedMode = string.IsNullOrWhiteSpace(modeValue) ? "auto" : modeValue.Trim().ToLowerInvariant();

        switch (requestedMode)
        {
            case "dry-run":
                outputMode = OutputMode.DryRun;
                return true;

            case "xinput":
                if (!OperatingSystem.IsWindows())
                {
                    Console.Error.WriteLine($"`{commandName} --mode xinput` is supported only on Windows 10/11.");
                    Console.Error.WriteLine("Use `--mode linux-uinput` (Linux) or `--mode dry-run`.");
                    outputMode = default;
                    return false;
                }

                outputMode = OutputMode.XInput;
                return true;

            case "linux-uinput":
                if (!OperatingSystem.IsLinux())
                {
                    Console.Error.WriteLine($"`{commandName} --mode linux-uinput` is supported only on Linux.");
                    Console.Error.WriteLine("Use `--mode xinput` on Windows or `--mode dry-run`.");
                    outputMode = default;
                    return false;
                }

                UInputProbeResult probe = UInputAvailabilityProbe.Probe();
                if (!probe.IsAvailable)
                {
                    Console.Error.WriteLine($"linux-uinput is not ready: {probe.Message}");
                    Console.Error.WriteLine("Try `sudo modprobe uinput` and ensure current user can write `/dev/uinput`.");
                    outputMode = default;
                    return false;
                }

                outputMode = OutputMode.LinuxUInput;
                return true;

            case "auto":
                if (OperatingSystem.IsWindows())
                {
                    outputMode = OutputMode.XInput;
                    return true;
                }

                if (OperatingSystem.IsLinux())
                {
                    UInputProbeResult autoProbe = UInputAvailabilityProbe.Probe();
                    if (autoProbe.IsAvailable)
                    {
                        outputMode = OutputMode.LinuxUInput;
                        return true;
                    }

                    Console.WriteLine($"Auto mode fallback: {autoProbe.Message}");
                    Console.WriteLine("Falling back to dry-run mode.");
                    outputMode = OutputMode.DryRun;
                    return true;
                }

                outputMode = OutputMode.DryRun;
                return true;

            default:
                Console.Error.WriteLine($"Unsupported output mode: '{modeValue}'.");
                outputMode = default;
                return false;
        }
    }

    private static ProtocolChecksumMode ParseChecksumMode(string? value)
    {
        if (value is not null && value.Equals("xor8-tail", StringComparison.OrdinalIgnoreCase))
        {
            return ProtocolChecksumMode.Xor8Tail;
        }

        return ProtocolChecksumMode.None;
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

        [LoggerMessage(EventId = 1302, Level = LogLevel.Error, Message = "Unhandled error in replay command.")]
        public static partial void ReplayUnhandledError(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 1303, Level = LogLevel.Error, Message = "Unhandled error in inspect command.")]
        public static partial void InspectUnhandledError(ILogger logger, Exception exception);
    }

    private enum OutputMode
    {
        DryRun = 0,
        XInput = 1,
        LinuxUInput = 2,
    }
}
