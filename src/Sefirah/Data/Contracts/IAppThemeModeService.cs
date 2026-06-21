namespace Sefirah.Data.Contracts;

public interface IAppThemeModeService
{
    event EventHandler? AppThemeModeChanged;

    event EventHandler? BackdropChanged;

    Theme Theme { get; set; }

    BackdropMaterialType BackdropMaterial { get; set; }

    void SetAppThemeMode(Window window);

    void ApplyBackdrop(Window window);

    /// <summary>Subscribes to theme/backdrop changes for <paramref name="window"/> and unsubscribes when it closes.</summary>
    void ManageAppearance(Window window);
}
