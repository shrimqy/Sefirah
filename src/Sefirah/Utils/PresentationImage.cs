using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Sefirah.Utils;

public static class PresentationImage
{
    public const int MaxBytes = 64 * 1024;
    public const int MaxEdge = 384;
    public static string Id(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static byte[]? Decode(string? base64, string? expectedId)
    {
        if (base64 is null || base64.Length > 4 * ((MaxBytes + 2) / 3)) return null;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            return IsValid(bytes) && Id(bytes) == expectedId?.ToLowerInvariant() ? bytes : null;
        }
        catch (FormatException) { return null; }
    }
    public static bool IsValid(byte[] bytes)
    {
        if (bytes.Length is < 24 or > MaxBytes) return false;
        int width = 0, height = 0;
        if (bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
            height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        }
        else if (bytes[0] == 0xff && bytes[1] == 0xd8)
        {
            for (var offset = 2; offset + 8 < bytes.Length;)
            {
                if (bytes[offset++] != 0xff) return false;
                while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
                if (offset + 2 >= bytes.Length) return false;
                var marker = bytes[offset++];
                if (marker is 0xd9 or 0xda) break;
                if (marker is 0x01 or >= 0xd0 and <= 0xd7) continue;
                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
                if (length < 2 || offset + length > bytes.Length) return false;
                if (marker is 0xc0 or 0xc1 or 0xc2)
                {
                    if (length < 8) return false;
                    height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 3, 2));
                    width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 5, 2));
                    break;
                }
                offset += length;
            }
        }
        return width is > 0 and <= MaxEdge && height is > 0 and <= MaxEdge;
    }
}
