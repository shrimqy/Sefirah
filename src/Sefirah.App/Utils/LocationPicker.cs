using Windows.Storage;
using Windows.Storage.Pickers;

namespace Sefirah.App.Utils;
public static class LocationPicker
{
    public static async Task<string> FileLocationPicker()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
        };

        picker.FileTypeFilter.Add(".exe");

        var window = MainWindow.Instance;
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(window));

        if (await picker.PickSingleFileAsync() is StorageFile file)
        {
            return file.Path;
        }

        return string.Empty;
    }
}
