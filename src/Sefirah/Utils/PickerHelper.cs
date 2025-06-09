using Windows.Storage.Pickers;

namespace Sefirah.Utils;
public static class PickerHelper
{
    public static async Task<StorageFolder?> PickFolderAsync(string startLocation = "DocumentsLibrary")
    {
        try
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = Enum.Parse<PickerLocationId>(startLocation)
            };
            picker.FileTypeFilter.Add("*");

            InitializePicker(picker);
            return await picker.PickSingleFolderAsync();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<StorageFile?> PickFileAsync(string fileType = ".exe")
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(fileType);

            InitializePicker(picker);
            return await picker.PickSingleFileAsync();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void InitializePicker(object picker)
    {
        var window = App.MainWindow;
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(window));
    }
}
