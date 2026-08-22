namespace Sefirah.Data.Contracts;

public interface IAction
{
    string DefaultIcon { get; }

    bool IsValid { get; }

    Task ExecuteAsync();
}
