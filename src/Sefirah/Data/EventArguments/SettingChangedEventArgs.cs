namespace Sefirah.Data.EventArguments;
public sealed class SettingChangedEventArgs(string settingName, object? newValue) : EventArgs
{
    public string SettingName { get; } = settingName;

    public object? NewValue { get; } = newValue;
}
