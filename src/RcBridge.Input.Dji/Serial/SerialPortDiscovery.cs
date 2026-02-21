using System.IO.Ports;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace RcBridge.Input.Dji.Serial;

public static class SerialPortDiscovery
{
    private static readonly Regex ComPortPattern = new(
        @"\((COM\d+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));

    public static IReadOnlyList<string> ListPorts()
    {
        return ListPortInfos().Select(static port => port.PortName).ToArray();
    }

    public static IReadOnlyList<SerialPortInfo> ListPortInfos()
    {
        string[] portNames = SerialPort.GetPortNames()
            .OrderBy(static port => port, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Dictionary<string, string> friendlyNames = new(StringComparer.OrdinalIgnoreCase);
        if (OperatingSystem.IsWindows())
        {
            friendlyNames = GetFriendlyNamesByPortOnWindows();
        }

        List<SerialPortInfo> ports = new(portNames.Length);
        foreach (string portName in portNames)
        {
            friendlyNames.TryGetValue(portName, out string? friendlyName);
            ports.Add(new SerialPortInfo(portName, friendlyName ?? string.Empty));
        }

        return ports;
    }

    [SupportedOSPlatform("windows")]
    private static Dictionary<string, string> GetFriendlyNamesByPortOnWindows()
    {
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            CollectFromSerialPortClass(map);
            CollectFromPnpEntity(map);
        }
        catch (ManagementException)
        {
            return map;
        }
        catch (COMException)
        {
            return map;
        }
        catch (InvalidOperationException)
        {
            return map;
        }
        catch (UnauthorizedAccessException)
        {
            return map;
        }

        return map;
    }

    [SupportedOSPlatform("windows")]
    private static void CollectFromSerialPortClass(Dictionary<string, string> map)
    {
        const string query = "SELECT DeviceID, Name, Description FROM Win32_SerialPort";

        using ManagementObjectSearcher searcher = new(query);
        using ManagementObjectCollection collection = searcher.Get();

        foreach (ManagementObject item in collection)
        {
            string? deviceId = item["DeviceID"] as string;
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                continue;
            }

            string? name = item["Name"] as string;
            string? description = item["Description"] as string;
            string friendlyName = !string.IsNullOrWhiteSpace(name) ? name : description ?? deviceId;

            AddFriendlyName(map, deviceId, friendlyName);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void CollectFromPnpEntity(Dictionary<string, string> map)
    {
        const string query = "SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'";

        using ManagementObjectSearcher searcher = new(query);
        using ManagementObjectCollection collection = searcher.Get();

        foreach (ManagementObject item in collection)
        {
            string? name = item["Name"] as string;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!TryExtractComPort(name, out string? portName))
            {
                continue;
            }

            AddFriendlyName(map, portName, name);
        }
    }

    private static bool TryExtractComPort(string text, out string portName)
    {
        Match match = ComPortPattern.Match(text);
        if (!match.Success)
        {
            portName = string.Empty;
            return false;
        }

        portName = match.Groups[1].Value.ToUpperInvariant();
        return true;
    }

    private static void AddFriendlyName(Dictionary<string, string> map, string portName, string friendlyName)
    {
        string normalizedPortName = portName.Trim().ToUpperInvariant();
        string cleanedFriendlyName = friendlyName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedPortName) || string.IsNullOrWhiteSpace(cleanedFriendlyName))
        {
            return;
        }

        if (map.TryGetValue(normalizedPortName, out string? current))
        {
            if (current.Length >= cleanedFriendlyName.Length)
            {
                return;
            }
        }

        map[normalizedPortName] = cleanedFriendlyName;
    }
}
