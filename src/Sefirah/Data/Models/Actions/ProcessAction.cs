using Sefirah.Data.Contracts;
using Sefirah.Dialogs;

namespace Sefirah.Data.Models.Actions;

public partial class ProcessAction : BaseAction, IActionDialog
{
    public string Path { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string StartInDirectory { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = [];
    public bool UseShellExecute { get; set; } = false;
    public bool CreateNoWindow { get; set; } = true;

    public Task ExecuteAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo(Path)
                {
                    Arguments = Arguments ?? string.Empty,
                    UseShellExecute = UseShellExecute,
                    CreateNoWindow = CreateNoWindow,
                    WorkingDirectory = StartInDirectory,
                };

                foreach (var (key, value) in EnvironmentVariables)
                {
                    psi.EnvironmentVariables[key] = value;
                }

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error executing process: {ex.Message}");
            }
        });
    }

    public async Task<BaseAction?> ShowDialogAsync(XamlRoot xamlRoot)
    {
        var dialog = new ProcessActionDialog(this)
        {
            XamlRoot = xamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            return dialog.Result;
        }

        return null;
    }
}
