
namespace Sefirah.App.Data.Contracts;
public interface IScreenMirrorService
{
    Task<bool> StartScrcpy(string? deviceId = null, bool wireless = false, string? customArgs = null);
}
