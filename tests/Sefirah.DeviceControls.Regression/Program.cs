using System.Text.Json;
using Sefirah.Data.Models;

var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var count = 0;
void Check(bool value, string scenario) { if (!value) throw new Exception(scenario); count++; }
var defaults = new DeviceControls();
Check(!defaults.RingerControl && defaults.BluetoothEnabled is null && defaults.BluetoothControl == "unsupported", "safe defaults");
foreach (var mode in new[] { "direct", "local", "denied", "unsupported" })
{
    var original = new DeviceControls { BluetoothControl = mode, BluetoothEnabled = false };
    var read = JsonSerializer.Deserialize<DeviceControls>(JsonSerializer.Serialize(original, options), options)!;
    Check(read.Version == 1 && read.BluetoothControl == mode && read.BluetoothEnabled == false, "round trip capability");
}
var request = new DeviceControlRequest { RequestId = Guid.NewGuid().ToString(), Control = "bluetooth", Value = 0 };
var json = JsonSerializer.Serialize(request, options);
Check(json.Contains("\"value\":0") && json.Contains("\"requestId\""), "explicit target state uses shared camelCase");
var future = JsonSerializer.Deserialize<DeviceControls>("""{"version":1,"futureField":"ignored"}""", options)!;
Check(future.BluetoothEnabled is null, "unknown property does not invent off state");
Console.WriteLine($"{count} device control contract checks passed.");
namespace Sefirah.Data.Models { public abstract class SocketMessage; }
