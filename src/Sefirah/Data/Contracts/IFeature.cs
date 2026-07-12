namespace Sefirah.Data.Contracts;

/// <summary>
/// Phone-facing feature that participates in app startup initialization.
/// </summary>
public interface IFeature
{
    Task InitializeAsync();
}
