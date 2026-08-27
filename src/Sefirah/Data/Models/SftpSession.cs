namespace Sefirah.Data.Models;

/// <summary>
/// The SFTP endpoint a connected device is currently serving, as announced in <see cref="SftpServerInfo"/>.
/// </summary>
public sealed record SftpSession(
    string Host,
    int Port,
    string Username,
    string Password,
    IReadOnlyList<string> Paths,
    IReadOnlyList<string> PathNames);
