namespace Sefirah.Data.Models;

/// <summary>Optional DeviceInfo extension. Never send new control messages to a legacy peer.</summary>
public sealed class DeviceControls
{
    public int Version { get; set; } = 1;
    public bool RingerControl { get; set; }
    // direct, local (requires interaction on the target), denied, unsupported
    public string BluetoothControl { get; set; } = "unsupported";
    public bool? BluetoothEnabled { get; set; }
}

public sealed class DeviceControlRequest : SocketMessage
{
    public required string RequestId { get; set; }
    public required string Control { get; set; }
    // ringer: 0=silent, 1=vibrate, 2=normal; bluetooth: 0=off, 1=on
    public int Value { get; set; }
}

public sealed class DeviceControlResult : SocketMessage
{
    public required string RequestId { get; set; }
    // applied, denied, unsupported, invalid, local, unconfirmed
    public required string Status { get; set; }
}
