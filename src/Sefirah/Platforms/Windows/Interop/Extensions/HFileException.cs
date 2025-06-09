namespace Sefirah.Platforms.Windows.Interop.Extensions;
public class HFileException : Exception
{
    public int ErrorCode { get; }
    public string Path { get; }

    public HFileException(string message, int errorCode, string path) 
        : base($"{message}. Error: 0x{errorCode:X8} ({errorCode})")
    {
        ErrorCode = errorCode;
        Path = path;
        Data[nameof(Path)] = path;
        Data[nameof(ErrorCode)] = errorCode;
    }
}
