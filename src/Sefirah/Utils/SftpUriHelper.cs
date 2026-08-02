using System.Net;
using System.Net.Sockets;

namespace Sefirah.Utils;

public static class SftpUriHelper
{
    /// <summary>
    /// Builds an sftp:// URI suitable for launching the system SFTP handler (e.g. WinSCP on Windows).
    /// </summary>
    public static Uri CreateBrowseUri(string host, int port, string username, string password, string? path = null)
    {
        path = NormalizePath(path);
        var hostPart = FormatHost(host);
        return new Uri(
            $"sftp://{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(password)}@{hostPart}:{port}{path}");
    }

    /// <summary>
    /// Builds an sftp:// URI for GVfs/gio (no password — GNOME ignores embedded passwords).
    /// </summary>
    public static string CreateGvfsUri(string host, int port, string username, string? path = null)
    {
        path = NormalizePath(path);
        return $"sftp://{Uri.EscapeDataString(username)}@{FormatHost(host)}:{port}{path}";
    }

    private static string NormalizePath(string? path)
    {
        path ??= "/";
        if (!path.StartsWith('/'))
            path = "/" + path;
        if (!path.EndsWith('/'))
            path += "/";
        return path;
    }

    private static string FormatHost(string host)
        => IsIPv6(host) ? $"[{host}]" : host;

    private static bool IsIPv6(string host)
        => IPAddress.TryParse(host, out var address) && address.AddressFamily is AddressFamily.InterNetworkV6;
}
