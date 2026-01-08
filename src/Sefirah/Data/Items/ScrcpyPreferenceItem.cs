namespace Sefirah.Data.Items;

public class ScrcpyPreferenceItem(string command, string display)
{
    public string Command { get; set; } = command;
    public string Display { get; set; } = display;
}
