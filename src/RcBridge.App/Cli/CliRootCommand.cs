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
            Description = "Serial port name (e.g., COM5)",
            Required = true,
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

        command.Add(portOption);
        command.Add(baudOption);
        command.Add(outOption);
        command.Add(secondsOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string port = parseResult.GetRequiredValue(portOption);
            int baud = parseResult.GetValue(baudOption);
            string outputPath = parseResult.GetRequiredValue(outOption);
            int seconds = parseResult.GetValue(secondsOption);

            await handlers.CaptureAsync(port, baud, outputPath, seconds, cancellationToken).ConfigureAwait(false);
            return 0;
        });

        return command;
    }

    private static Command CreateRunCommand(CommandHandlers handlers)
    {
        Command command = new("run", "Run bridge from serial input to XInput output.");

        Option<string> portOption = new("--port")
        {
            Description = "Serial port name (e.g., COM5)",
            Required = true,
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
            string port = parseResult.GetRequiredValue(portOption);
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
}
