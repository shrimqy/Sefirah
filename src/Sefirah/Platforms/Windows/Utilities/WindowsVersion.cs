using Windows.System.Profile;

namespace Sefirah.Platforms.Windows.Utilities;

/// <summary>
/// OS build/revision from <see cref="AnalyticsInfo.VersionInfo.DeviceFamilyVersion"/>.
/// <see cref="Major"/> is the Windows <b>build</b> (e.g. 26200), not .NET's version major (10).
/// </summary>
public readonly struct WindowsVersion(ushort major, ushort revision = 0) : IComparable<WindowsVersion>, IEquatable<WindowsVersion>
{
    public ushort Major { get; } = major;

    public ushort Revision { get; } = revision;

    public static WindowsVersion Current => TryParseCurrent(out var v) ? v : default;

    public static bool TryParseCurrent(out WindowsVersion version)
    {
        version = default;
        try
        {
            var s = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            if (string.IsNullOrEmpty(s) || !ulong.TryParse(s, out var ver))
            {
                return false;
            }

            version = new WindowsVersion(
                (ushort)((ver >> 16) & 0xFFFF),
                (ushort)(ver & 0xFFFF));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public int CompareTo(WindowsVersion other)
    {
        var c = Major.CompareTo(other.Major);
        return c != 0 ? c : Revision.CompareTo(other.Revision);
    }

    public bool Equals(WindowsVersion other) => Major == other.Major && Revision == other.Revision;

    public override bool Equals(object? obj) => obj is WindowsVersion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Revision);

    public static bool operator ==(WindowsVersion a, WindowsVersion b) => a.Equals(b);

    public static bool operator !=(WindowsVersion a, WindowsVersion b) => !a.Equals(b);

    public static bool operator <(WindowsVersion a, WindowsVersion b) => a.CompareTo(b) < 0;

    public static bool operator >(WindowsVersion a, WindowsVersion b) => a.CompareTo(b) > 0;

    public static bool operator <=(WindowsVersion a, WindowsVersion b) => a.CompareTo(b) <= 0;

    public static bool operator >=(WindowsVersion a, WindowsVersion b) => a.CompareTo(b) >= 0;
}
