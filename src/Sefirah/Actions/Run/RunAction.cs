using Sefirah.Utils;

namespace Sefirah.Actions.Run;

public sealed class RunSettings
{
    public string Path { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string StartInDirectory { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = [];
    public bool UseShellExecute { get; set; } = true;
    public bool CreateNoWindow { get; set; } = true;
}

public sealed class RunAction(ActionItem item) : IAction, IActionSettings
{
    public static ActionMetadata Metadata { get; } = new("Run", "Apps");

    public string DefaultIcon => Metadata.DefaultIcon;

    public bool IsValid => !string.IsNullOrWhiteSpace(item.Get<RunSettings>().Path);

    public UIElement CreateSettingPanel() => new RunActionSettings(item);

    public Task ExecuteAsync()
    {
        return Task.Run(() =>
        {
            var settings = item.Get<RunSettings>();
            var psi = new ProcessStartInfo(settings.Path)
            {
                Arguments = settings.Arguments ?? string.Empty,
                UseShellExecute = settings.UseShellExecute,
                CreateNoWindow = settings.CreateNoWindow,
                WorkingDirectory = settings.StartInDirectory,
            };

            foreach (var (key, value) in settings.EnvironmentVariables)
            {
                psi.EnvironmentVariables[key] = value;
            }

            Process.Start(psi);
        });
    }
}
