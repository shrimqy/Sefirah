using System.Buffers.Binary;

namespace Sefirah.Utils;

/// <summary>
/// Pulls the small preview most cameras embed in a JPEG's EXIF block. Reading it costs a few
/// kilobytes off the head of the file, against megabytes for the real image, which is the
/// difference between a gallery that fills in at once and one that crawls over the network.
/// </summary>
public static class ExifThumbnail
{
    private const ushort JpegInterchangeFormat = 0x0201;
    private const ushort JpegInterchangeFormatLength = 0x0202;

    /// <summary>
    /// Returns the embedded preview found in <paramref name="head"/>, the first bytes of a JPEG,
    /// or null when the file carries none.
    /// </summary>
    public static byte[]? TryExtract(ReadOnlySpan<byte> head)
    {
        try
        {
            var app1 = FindExifApp1(head);
            if (app1 < 0) return null;

            // The TIFF header the EXIF offsets are relative to starts right after "Exif\0\0"
            var tiff = app1 + 6;
            if (tiff + 8 > head.Length) return null;

            var little = head[tiff] == 0x49 && head[tiff + 1] == 0x49;
            if (!little && !(head[tiff] == 0x4D && head[tiff + 1] == 0x4D)) return null;

            var ifd0 = tiff + (int)ReadUInt32(head, tiff + 4, little);
            var ifd1 = ReadNextIfdOffset(head, tiff, ifd0, little);
            if (ifd1 <= 0) return null;

            // IFD1 describes the thumbnail: where it starts and how long it is
            var offset = 0;
            var length = 0;
            foreach (var (tag, value) in ReadEntries(head, tiff, tiff + ifd1, little))
            {
                if (tag == JpegInterchangeFormat) offset = (int)value;
                else if (tag == JpegInterchangeFormatLength) length = (int)value;
            }

            if (offset <= 0 || length <= 0) return null;

            var start = tiff + offset;
            if (start < 0 || start + length > head.Length) return null;

            return head.Slice(start, length).ToArray();
        }
        catch
        {
            // A malformed header is not worth reporting, the caller just falls back to the real image
            return null;
        }
    }

    private static int FindExifApp1(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8) return -1;

        var position = 2;
        while (position + 4 <= data.Length)
        {
            if (data[position] != 0xFF) return -1;

            var marker = data[position + 1];
            if (marker is 0xD8 or 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                position += 2;
                continue;
            }
            if (marker == 0xDA) return -1; // start of scan, no metadata past here

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(data[(position + 2)..]);
            if (marker == 0xE1 && position + 10 <= data.Length &&
                data.Slice(position + 4, 4).SequenceEqual("Exif"u8))
            {
                return position + 4;
            }

            position += 2 + segmentLength;
        }
        return -1;
    }

    private static int ReadNextIfdOffset(ReadOnlySpan<byte> data, int tiff, int ifd, bool little)
    {
        if (ifd + 2 > data.Length) return 0;

        var count = ReadUInt16(data, ifd, little);
        var next = ifd + 2 + count * 12;
        return next + 4 > data.Length ? 0 : (int)ReadUInt32(data, next, little);
    }

    private static List<(ushort Tag, uint Value)> ReadEntries(ReadOnlySpan<byte> data, int tiff, int ifd, bool little)
    {
        List<(ushort, uint)> entries = [];
        if (ifd + 2 > data.Length) return entries;

        var count = ReadUInt16(data, ifd, little);
        for (var i = 0; i < count; i++)
        {
            var entry = ifd + 2 + i * 12;
            if (entry + 12 > data.Length) break;

            entries.Add((ReadUInt16(data, entry, little), ReadUInt32(data, entry + 8, little)));
        }
        return entries;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset, bool little)
        => little
            ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..])
            : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset, bool little)
        => little
            ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..])
            : BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
}
