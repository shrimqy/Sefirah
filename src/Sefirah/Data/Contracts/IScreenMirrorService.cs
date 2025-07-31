using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;
public interface IScreenMirrorService
{
    Task<bool> StartScrcpy(PairedDevice device, string? customArgs = null, string? iconPath = null);
}
