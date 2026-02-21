using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using RcBridge.Core.Abstractions;
using RcBridge.Core.Models;

namespace RcBridge.Output.XInput.XInput;

public sealed partial class XInputSink : IXInputSink
{
    private readonly ILogger<XInputSink> _logger;
    private ViGEmClient? _client;
    private IXbox360Controller? _controller;
    private bool _connected;

    public XInputSink(ILogger<XInputSink> logger)
    {
        _logger = logger;
    }

    public ValueTask ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_connected)
        {
            return ValueTask.CompletedTask;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new ViGEmUnavailableException("XInput output is supported only on Windows 10/11.");
        }

        try
        {
            _client = new ViGEmClient();
            _controller = _client.CreateXbox360Controller();
            _controller.AutoSubmitReport = false;
            _controller.Connect();
            _connected = true;
            LogMessages.ControllerConnected(_logger);
        }
        catch (VigemBusNotFoundException ex)
        {
            throw new ViGEmUnavailableException("ViGEmBus is not installed. Install ViGEmBus and retry.", ex);
        }
        catch (Exception ex)
        {
            throw new XInputOutputException("Failed to initialize virtual Xbox 360 controller.", ex);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask SendAsync(NormalizedControllerState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_connected || _controller is null)
        {
            throw new InvalidOperationException("XInput sink is not connected.");
        }

        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, XInputValueConverter.ToStick(state.LeftThumbX));
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, XInputValueConverter.ToStick(state.LeftThumbY));
        _controller.SetAxisValue(Xbox360Axis.RightThumbX, XInputValueConverter.ToStick(state.RightThumbX));
        _controller.SetAxisValue(Xbox360Axis.RightThumbY, XInputValueConverter.ToStick(state.RightThumbY));
        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, XInputValueConverter.ToTrigger(state.LeftTrigger));
        _controller.SetSliderValue(Xbox360Slider.RightTrigger, XInputValueConverter.ToTrigger(state.RightTrigger));

        _controller.SetButtonState(Xbox360Button.A, state.A);
        _controller.SetButtonState(Xbox360Button.B, state.B);
        _controller.SetButtonState(Xbox360Button.X, state.X);
        _controller.SetButtonState(Xbox360Button.Y, state.Y);
        _controller.SetButtonState(Xbox360Button.LeftShoulder, state.LeftShoulder);
        _controller.SetButtonState(Xbox360Button.RightShoulder, state.RightShoulder);
        _controller.SetButtonState(Xbox360Button.Back, state.Back);
        _controller.SetButtonState(Xbox360Button.Start, state.Start);

        _controller.SubmitReport();

        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_controller is not null)
        {
            try
            {
                _controller.Disconnect();
            }
            catch (Exception ex)
            {
                LogMessages.DisconnectIgnored(_logger, ex);
            }

            _controller = null;
        }

        if (_client is not null)
        {
            _client.Dispose();
            _client = null;
        }

        if (_connected)
        {
            LogMessages.ControllerReleased(_logger);
        }

        _connected = false;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private static partial class LogMessages
    {
        [LoggerMessage(EventId = 1201, Level = LogLevel.Information, Message = "Virtual Xbox 360 controller connected.")]
        public static partial void ControllerConnected(ILogger logger);

        [LoggerMessage(EventId = 1202, Level = LogLevel.Debug, Message = "Ignoring error during virtual controller disconnect.")]
        public static partial void DisconnectIgnored(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 1203, Level = LogLevel.Information, Message = "Virtual Xbox 360 controller released.")]
        public static partial void ControllerReleased(ILogger logger);
    }
}
