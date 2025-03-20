
namespace Sefirah.App.Data.Contracts;
public interface IScreenMirrorService
{
    Task<bool> StartScrcpy(string? customArgs = null);
}
