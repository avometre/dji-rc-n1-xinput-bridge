namespace RcBridge.Input.Dji.Serial;

public sealed record SerialPortInfo(string PortName, string FriendlyName)
{
    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FriendlyName))
            {
                return PortName;
            }

            return $"{PortName} - {FriendlyName}";
        }
    }
}
