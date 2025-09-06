namespace Sefirah.Data.Items;

public class ScrcpyPreferenceItem(int id, string command, string display)
{
    public int Id { get; set; } = id;
    public string Command { get; set; } = command;
    public string Display { get; set; } = display;
}
