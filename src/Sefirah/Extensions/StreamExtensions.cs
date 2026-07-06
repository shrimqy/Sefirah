using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Sefirah.Extensions;

public static partial class StreamExtensions
{
    public static IRandomAccessStreamWithContentType AsRandomAccessStreamWithContentType(this Stream stream, string contentType)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new RandomAccessStreamWithContentType(stream.AsRandomAccessStream(), contentType);
    }

    private sealed partial class RandomAccessStreamWithContentType(IRandomAccessStream stream, string contentType) : IRandomAccessStreamWithContentType, IRandomAccessStream, IDisposable, IInputStream, IOutputStream, IContentTypeProvider
    {
        public string ContentType => contentType;

        public bool CanRead => stream.CanRead;

        public bool CanWrite => stream.CanWrite;

        public ulong Position => stream.Position;

        public ulong Size
        {
            get => stream.Size;
            set => stream.Size = value;
        }

        public Stream AsStream() => stream.AsStream();

        public IRandomAccessStream CloneStream() => stream.CloneStream();

        public void Dispose() => stream.Dispose();

        public IAsyncOperation<bool> FlushAsync() => stream.FlushAsync();

        public IInputStream GetInputStreamAt(ulong position) => stream.GetInputStreamAt(position);

        public IOutputStream GetOutputStreamAt(ulong position) => stream.GetOutputStreamAt(position);

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options) =>
            stream.ReadAsync(buffer, count, options);

        public void Seek(ulong position) => stream.Seek(position);

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer) => stream.WriteAsync(buffer);
    }
}
