using System.IO.Ports;

namespace RcBridge.Input.Dji.Serial;

public static class SerialPortDiscovery
{
    public static IReadOnlyList<string> ListPorts()
    {
        return SerialPort.GetPortNames()
            .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
