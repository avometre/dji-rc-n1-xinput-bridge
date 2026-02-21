using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using RcBridge.Core.Abstractions;
using RcBridge.Core.Models;

namespace RcBridge.Output.Linux.UInput;

public sealed partial class LinuxUInputSink : IXInputSink
{
    private const int UinputMaxNameSize = 80;
    private const int AbsCnt = 64;

    private const ushort BusUsb = 0x03;

    private const ushort EvSyn = 0x00;
    private const ushort EvKey = 0x01;
    private const ushort EvAbs = 0x03;

    private const ushort SynReport = 0;

    private const ushort AbsX = 0x00;
    private const ushort AbsY = 0x01;
    private const ushort AbsZ = 0x02;
    private const ushort AbsRx = 0x03;
    private const ushort AbsRy = 0x04;
    private const ushort AbsRz = 0x05;

    private const ushort BtnSouth = 0x130;
    private const ushort BtnEast = 0x131;
    private const ushort BtnNorth = 0x133;
    private const ushort BtnWest = 0x134;
    private const ushort BtnTl = 0x136;
    private const ushort BtnTr = 0x137;
    private const ushort BtnSelect = 0x13a;
    private const ushort BtnStart = 0x13b;

    private const uint UiSetEvBit = 1074025828;
    private const uint UiSetKeyBit = 1074025829;
    private const uint UiSetAbsBit = 1074025831;
    private const uint UiDevCreate = 21761;
    private const uint UiDevDestroy = 21762;

    private readonly ILogger<LinuxUInputSink> _logger;
    private FileStream? _stream;
    private int _fd;
    private bool _connected;

    public LinuxUInputSink(ILogger<LinuxUInputSink> logger)
    {
        _logger = logger;
        _fd = -1;
    }

    public ValueTask ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_connected)
        {
            return ValueTask.CompletedTask;
        }

        if (!OperatingSystem.IsLinux())
        {
            throw new LinuxUInputUnavailableException("linux-uinput output is supported only on Linux.");
        }

        string path = UInputAvailabilityProbe.ResolveDevicePathOrThrow();

        try
        {
            _stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            _fd = (int)_stream.SafeFileHandle.DangerousGetHandle();
            if (_fd < 0)
            {
                throw new LinuxOutputException($"Invalid file descriptor for {path}.");
            }

            ConfigureDevice(_stream, _fd);

            _connected = true;
            LogMessages.ControllerConnected(_logger, path);
        }
        catch
        {
            Dispose();
            throw;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask SendAsync(NormalizedControllerState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_connected || _stream is null)
        {
            throw new InvalidOperationException("Linux uinput sink is not connected.");
        }

        EmitEvent(_stream, EvAbs, AbsX, LinuxInputValueConverter.ToStick(state.LeftThumbX));
        EmitEvent(_stream, EvAbs, AbsY, LinuxInputValueConverter.ToStick(state.LeftThumbY));
        EmitEvent(_stream, EvAbs, AbsRx, LinuxInputValueConverter.ToStick(state.RightThumbX));
        EmitEvent(_stream, EvAbs, AbsRy, LinuxInputValueConverter.ToStick(state.RightThumbY));
        EmitEvent(_stream, EvAbs, AbsZ, LinuxInputValueConverter.ToTrigger(state.LeftTrigger));
        EmitEvent(_stream, EvAbs, AbsRz, LinuxInputValueConverter.ToTrigger(state.RightTrigger));

        EmitEvent(_stream, EvKey, BtnSouth, state.A ? 1 : 0);
        EmitEvent(_stream, EvKey, BtnEast, state.B ? 1 : 0);
        EmitEvent(_stream, EvKey, BtnWest, state.X ? 1 : 0);
        EmitEvent(_stream, EvKey, BtnNorth, state.Y ? 1 : 0);
        EmitEvent(_stream, EvKey, BtnTl, state.LeftShoulder ? 1 : 0);
        EmitEvent(_stream, EvKey, BtnTr, state.RightShoulder ? 1 : 0);
        EmitEvent(_stream, EvKey, BtnSelect, state.Back ? 1 : 0);
        EmitEvent(_stream, EvKey, BtnStart, state.Start ? 1 : 0);

        EmitEvent(_stream, EvSyn, SynReport, 0);

        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_fd >= 0 && _connected)
        {
            if (IoctlPtr(_fd, UiDevDestroy, IntPtr.Zero) < 0)
            {
                LogMessages.DestroyIgnored(_logger, GetErrnoMessage(Marshal.GetLastWin32Error()));
            }
        }

        _stream?.Dispose();
        _stream = null;

        if (_connected)
        {
            LogMessages.ControllerReleased(_logger);
        }

        _fd = -1;
        _connected = false;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private static void ConfigureDevice(FileStream stream, int fd)
    {
        IoctlOrThrow(fd, UiSetEvBit, EvKey, "UI_SET_EVBIT(EV_KEY)");
        IoctlOrThrow(fd, UiSetEvBit, EvAbs, "UI_SET_EVBIT(EV_ABS)");

        IoctlOrThrow(fd, UiSetKeyBit, BtnSouth, "UI_SET_KEYBIT(BTN_SOUTH)");
        IoctlOrThrow(fd, UiSetKeyBit, BtnEast, "UI_SET_KEYBIT(BTN_EAST)");
        IoctlOrThrow(fd, UiSetKeyBit, BtnWest, "UI_SET_KEYBIT(BTN_WEST)");
        IoctlOrThrow(fd, UiSetKeyBit, BtnNorth, "UI_SET_KEYBIT(BTN_NORTH)");
        IoctlOrThrow(fd, UiSetKeyBit, BtnTl, "UI_SET_KEYBIT(BTN_TL)");
        IoctlOrThrow(fd, UiSetKeyBit, BtnTr, "UI_SET_KEYBIT(BTN_TR)");
        IoctlOrThrow(fd, UiSetKeyBit, BtnSelect, "UI_SET_KEYBIT(BTN_SELECT)");
        IoctlOrThrow(fd, UiSetKeyBit, BtnStart, "UI_SET_KEYBIT(BTN_START)");

        IoctlOrThrow(fd, UiSetAbsBit, AbsX, "UI_SET_ABSBIT(ABS_X)");
        IoctlOrThrow(fd, UiSetAbsBit, AbsY, "UI_SET_ABSBIT(ABS_Y)");
        IoctlOrThrow(fd, UiSetAbsBit, AbsRx, "UI_SET_ABSBIT(ABS_RX)");
        IoctlOrThrow(fd, UiSetAbsBit, AbsRy, "UI_SET_ABSBIT(ABS_RY)");
        IoctlOrThrow(fd, UiSetAbsBit, AbsZ, "UI_SET_ABSBIT(ABS_Z)");
        IoctlOrThrow(fd, UiSetAbsBit, AbsRz, "UI_SET_ABSBIT(ABS_RZ)");

        UInputUserDev definition = CreateDeviceDefinition();
        WriteStruct(stream, definition);

        if (IoctlPtr(fd, UiDevCreate, IntPtr.Zero) < 0)
        {
            throw new LinuxUInputUnavailableException(CreateIoError("UI_DEV_CREATE"));
        }
    }

    private static UInputUserDev CreateDeviceDefinition()
    {
        int[] absMin = new int[AbsCnt];
        int[] absMax = new int[AbsCnt];
        int[] absFuzz = new int[AbsCnt];
        int[] absFlat = new int[AbsCnt];

        absMin[AbsX] = -32768;
        absMin[AbsY] = -32768;
        absMin[AbsRx] = -32768;
        absMin[AbsRy] = -32768;
        absMin[AbsZ] = 0;
        absMin[AbsRz] = 0;

        absMax[AbsX] = 32767;
        absMax[AbsY] = 32767;
        absMax[AbsRx] = 32767;
        absMax[AbsRy] = 32767;
        absMax[AbsZ] = 255;
        absMax[AbsRz] = 255;

        return new UInputUserDev
        {
            Name = "RcBridge Virtual Gamepad",
            Id = new InputId
            {
                BusType = BusUsb,
                Vendor = 0x1209,
                Product = 0x2310,
                Version = 0x0001,
            },
            FfEffectsMax = 0,
            AbsMax = absMax,
            AbsMin = absMin,
            AbsFuzz = absFuzz,
            AbsFlat = absFlat,
        };
    }

    private static void EmitEvent(FileStream stream, ushort type, ushort code, int value)
    {
        InputEvent inputEvent = new()
        {
            TvSec = 0,
            TvUsec = 0,
            Type = type,
            Code = code,
            Value = value,
        };

        WriteStruct(stream, inputEvent);
    }

    private static void WriteStruct<T>(FileStream stream, T value)
        where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] bytes = new byte[size];

        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(value, ptr, false);
            Marshal.Copy(ptr, bytes, 0, size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        stream.Write(bytes, 0, bytes.Length);
    }

    private static void IoctlOrThrow(int fd, uint request, int value, string operation)
    {
        if (IoctlInt(fd, request, value) < 0)
        {
            throw new LinuxUInputUnavailableException(CreateIoError(operation));
        }
    }

    private static string CreateIoError(string operation)
    {
        int errno = Marshal.GetLastWin32Error();
        return $"{operation} failed (errno {errno}): {GetErrnoMessage(errno)}";
    }

    private static string GetErrnoMessage(int errno)
    {
        try
        {
            return new Win32Exception(errno).Message;
        }
        catch
        {
            return "unknown error";
        }
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "ioctl")]
    private static extern int IoctlInt(int fd, uint request, int value);

    [DllImport("libc", SetLastError = true, EntryPoint = "ioctl")]
    private static extern int IoctlPtr(int fd, uint request, IntPtr arg);

    [StructLayout(LayoutKind.Sequential)]
    private struct InputId
    {
        public ushort BusType;
        public ushort Vendor;
        public ushort Product;
        public ushort Version;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InputEvent
    {
        public long TvSec;
        public long TvUsec;
        public ushort Type;
        public ushort Code;
        public int Value;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct UInputUserDev
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UinputMaxNameSize)]
        public string Name;

        public InputId Id;
        public uint FfEffectsMax;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = AbsCnt)]
        public int[] AbsMax;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = AbsCnt)]
        public int[] AbsMin;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = AbsCnt)]
        public int[] AbsFuzz;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = AbsCnt)]
        public int[] AbsFlat;
    }

    private static partial class LogMessages
    {
        [LoggerMessage(EventId = 2201, Level = LogLevel.Information, Message = "Linux uinput virtual gamepad connected on {DevicePath}.")]
        public static partial void ControllerConnected(ILogger logger, string devicePath);

        [LoggerMessage(EventId = 2202, Level = LogLevel.Debug, Message = "Ignoring error during linux-uinput destroy: {Message}")]
        public static partial void DestroyIgnored(ILogger logger, string message);

        [LoggerMessage(EventId = 2203, Level = LogLevel.Information, Message = "Linux uinput virtual gamepad released.")]
        public static partial void ControllerReleased(ILogger logger);
    }
}
