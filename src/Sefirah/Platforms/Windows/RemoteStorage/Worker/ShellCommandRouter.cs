using System.Collections.Concurrent;
using System.Threading.Channels;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker;

/// <summary>Sends shell commands to the queue of the sync root that contains the item.</summary>
public sealed class ShellCommandRouter(ILogger logger)
{
    private readonly ConcurrentDictionary<string, ChannelWriter<ShellCommand>> _writers = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string rootDirectory, ChannelWriter<ShellCommand> writer) =>
        _writers[Path.TrimEndingDirectorySeparator(rootDirectory)] = writer;

    public void Unregister(string rootDirectory, ChannelWriter<ShellCommand> writer) =>
        _writers.TryRemove(new KeyValuePair<string, ChannelWriter<ShellCommand>>(Path.TrimEndingDirectorySeparator(rootDirectory), writer));

    public void TryWrite(ShellCommand command)
    {
        foreach (var (rootDirectory, writer) in _writers)
        {
            if (command.FullPath.Equals(rootDirectory, StringComparison.OrdinalIgnoreCase)
                || command.FullPath.StartsWith(rootDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                writer.TryWrite(command);
                return;
            }
        }
        logger.Warn($"No running sync root for {command.FullPath}");
    }
}
