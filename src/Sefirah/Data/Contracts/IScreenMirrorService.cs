using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IScreenMirrorService
{
    Task<bool> StartScrcpy(PairedDevice device, ApplicationItem? app = null);

    void LaunchAppByPackage(string package);
}
