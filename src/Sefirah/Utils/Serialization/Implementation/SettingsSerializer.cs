namespace Sefirah.Utils.Serialization.Implementation;
internal sealed class SettingsSerializer : ISettingsSerializer
{
    private string? _filePath;

    public bool CreateFile(string path)
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            
            _filePath = path;
            if (File.Exists(path))
            {
                return true;
            }

            using FileStream fs = File.Create(path);
            // Create empty file
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public string ReadFromFile()
    {
        ArgumentNullException.ThrowIfNull(_filePath);

        try
        {
            return !File.Exists(_filePath) 
                ? string.Empty 
                : File.ReadAllText(_filePath);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public bool WriteToFile(string? text)
    {
        ArgumentNullException.ThrowIfNull(_filePath);

        try
        {
            File.WriteAllText(_filePath, text ?? string.Empty);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
