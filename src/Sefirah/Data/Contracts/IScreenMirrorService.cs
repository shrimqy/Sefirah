namespace Sefirah.Data.Contracts;
public interface IScreenMirrorService
{
    Task<bool> StartScrcpy(string? customArgs = null, string? iconPath = null);
}
