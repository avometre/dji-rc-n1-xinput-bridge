using System.CommandLine;

namespace RcBridge.App.Cli;

public static class CliRootCommand
{
    public static RootCommand Create(CommandHandlers handlers)
    {
        RootCommand root = new("DJI RC-N1 to virtual Xbox 360 (XInput) bridge.");

        root.Add(CreateListPortsCommand());
        root.Add(CreateCaptureCommand(handlers));
        root.Add(CreateRunCommand(handlers));
        root.Add(CreateInspectCommand(handlers));
        root.Add(CreateReplayCommand(handlers));
        root.Add(CreateDiagnoseCommand());

        return root;
    }

    private static Command CreateListPortsCommand()
    {
        Command command = new("list-ports", "List available serial COM ports.");
        command.SetAction(_ =>
        {
            CommandHandlers.ListPorts();
            return 0;
        });
        return command;
    }

    private static Command CreateCaptureCommand(CommandHandlers handlers)
    {
        Command command = new("capture", "Capture raw serial frames into a binary file.");

        Option<string> portOption = new("--port")
        {
            Description = "Serial port name (COMx) or 'auto' to detect DJI VCOM.",
            DefaultValueFactory = static _ => "auto",
        };

        Option<int> baudOption = new("--baud")
        {
            Description = "Serial baud rate.",
            DefaultValueFactory = static _ => 115200,
        };

        Option<string> outOption = new("--out")
        {
            Description = "Output capture file path.",
            Required = true,
        };

        Option<int> secondsOption = new("--seconds")
        {
            Description = "Capture duration in seconds.",
            DefaultValueFactory = static _ => 20,
        };
        Option<string> noteOption = new("--note")
        {
            Description = "Optional note written into capture metadata (v2 format).",
            DefaultValueFactory = static _ => string.Empty,
        };

        command.Add(portOption);
        command.Add(baudOption);
        command.Add(outOption);
        command.Add(secondsOption);
        command.Add(noteOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string port = parseResult.GetValue(portOption) ?? "auto";
            int baud = parseResult.GetValue(baudOption);
            string outputPath = parseResult.GetRequiredValue(outOption);
            int seconds = parseResult.GetValue(secondsOption);
            string note = parseResult.GetValue(noteOption) ?? string.Empty;

            await handlers.CaptureAsync(port, baud, outputPath, seconds, note, cancellationToken).ConfigureAwait(false);
            return 0;
        });

        return command;
    }

    private static Command CreateRunCommand(CommandHandlers handlers)
    {
        Command command = new("run", "Run bridge from serial input to XInput output.");

        Option<string> portOption = new("--port")
        {
            Description = "Serial port name (COMx) or 'auto' to detect DJI VCOM.",
            DefaultValueFactory = static _ => "auto",
        };

        Option<int> baudOption = new("--baud")
        {
            Description = "Serial baud rate.",
            DefaultValueFactory = static _ => 115200,
        };

        Option<string> configOption = new("--config")
        {
            Description = "Configuration JSON file.",
            DefaultValueFactory = static _ => "config.json",
        };

        command.Add(portOption);
        command.Add(baudOption);
        command.Add(configOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string port = parseResult.GetValue(portOption) ?? "auto";
            int baud = parseResult.GetValue(baudOption);
            string configPath = parseResult.GetValue(configOption) ?? "config.json";

            await handlers.RunAsync(port, baud, configPath, cancellationToken).ConfigureAwait(false);
            return 0;
        });

        return command;
    }

    private static Command CreateDiagnoseCommand()
    {
        Command command = new("diagnose", "Print environment diagnostics and next steps.");
        command.SetAction(_ =>
        {
            CommandHandlers.Diagnose();
            return 0;
        });
        return command;
    }

    private static Command CreateInspectCommand(CommandHandlers handlers)
    {
        Command command = new("inspect", "Inspect capture file statistics for protocol analysis.");

        Option<string> captureOption = new("--capture")
        {
            Description = "Capture file path created by `capture` command.",
            Required = true,
        };

        command.Add(captureOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string capturePath = parseResult.GetRequiredValue(captureOption);
            await handlers.InspectAsync(capturePath, cancellationToken).ConfigureAwait(false);
            return 0;
        });

        return command;
    }

    private static Command CreateReplayCommand(CommandHandlers handlers)
    {
        Command command = new("replay", "Replay a capture file through decoder/mapping/output pipeline.");

        Option<string> captureOption = new("--capture")
        {
            Description = "Capture file path created by `capture` command.",
            Required = true,
        };

        Option<string> configOption = new("--config")
        {
            Description = "Configuration JSON file.",
            DefaultValueFactory = static _ => "config.json",
        };

        Option<string> modeOption = new("--mode")
        {
            Description = "Replay mode: dry-run (no virtual controller) or xinput (ViGEm output).",
            DefaultValueFactory = static _ => "dry-run",
        };
        modeOption.AcceptOnlyFromAmong("dry-run", "xinput");

        command.Add(captureOption);
        command.Add(configOption);
        command.Add(modeOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string capturePath = parseResult.GetRequiredValue(captureOption);
            string configPath = parseResult.GetValue(configOption) ?? "config.json";
            string mode = parseResult.GetValue(modeOption) ?? "dry-run";

            await handlers.ReplayAsync(capturePath, configPath, mode, cancellationToken).ConfigureAwait(false);
            return 0;
        });

        return command;
    }
}
