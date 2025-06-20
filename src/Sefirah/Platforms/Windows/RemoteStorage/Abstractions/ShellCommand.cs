namespace Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
public record ShellCommand
{
    public required ShellCommandKind Kind { get; init; }
    public required string FullPath { get; init; }
}

public enum ShellCommandKind
{
    Do
}
