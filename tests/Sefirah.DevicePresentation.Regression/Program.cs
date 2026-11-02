global using System.Text.Json;
global using System.Text.Json.Serialization;
using Sefirah.Data.Models;
using Sefirah.Utils;
using System.Buffers.Binary;

var count = 0;
void Check(bool value, string scenario) { if (!value) throw new Exception(scenario); count++; }
foreach (var kind in DevicePresentation.Kinds)
foreach (var ratio in DevicePresentation.Ratios)
foreach (var orientation in new[] { "portrait", "landscape" })
{
    var value = new DevicePresentation { Kind = kind, AspectRatio = ratio, Orientation = orientation };
    Check(value.IsValid, "allowed preset");
    Check(orientation == "portrait" ? value.DisplayRatio <= 1 : value.DisplayRatio >= 1, "orientation normalizes ratio once");
}
Check(!new DevicePresentation { AspectRatio = "0:0" }.IsValid, "invalid ratio rejected");
Check(!new DevicePresentation { Version = 2 }.IsValid, "unknown version rejected");
Check(!new DevicePresentation { Kind = "Meta-OMEN" }.IsValid, "name is not form factor");
Check(!new DevicePresentation { WallpaperSource = "wallpaper" }.IsValid, "image source requires content id");
Check(DevicePresentation.Parse("garbage") is null, "corrupt persisted metadata");
var image = new byte[32];
new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(image, 0);
BinaryPrimitives.WriteInt32BigEndian(image.AsSpan(16), 384);
BinaryPrimitives.WriteInt32BigEndian(image.AsSpan(20), 200);
Check(PresentationImage.IsValid(image), "bounded PNG header");
Check(PresentationImage.Decode(Convert.ToBase64String(image), PresentationImage.Id(image)) is not null, "content id verified");
Check(PresentationImage.Decode(Convert.ToBase64String(image), new string('0', 64)) is null, "wrong content id");
BinaryPrimitives.WriteInt32BigEndian(image.AsSpan(16), 10000);
Check(!PresentationImage.IsValid(image), "oversized decoded image rejected before UI decoder");
Check(PresentationImage.Decode(new string('A', 90000), null) is null, "oversized Base64 rejected");
Check(PresentationImage.Decode("!!", null) is null, "invalid Base64 rejected");
var metadata = new DevicePresentation { WallpaperSource = "wallpaper", WallpaperId = new string('a', 64), Wallpaper = "private" }.Metadata();
Check(metadata.Wallpaper is null && metadata.WallpaperId is not null, "metadata does not duplicate image");
Check(new DevicePresentation { WallpaperSource = "none" }.IsValid, "explicit revocation has no image");
Console.WriteLine($"{count} device presentation checks passed.");
