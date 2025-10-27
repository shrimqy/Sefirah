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

    public static async Task<StorageFile?> PickFileAsync(List<string>? fileTypes = null)
    {
        try
        {
            var picker = new FileOpenPicker();
            if (fileTypes is not null)
            {
                foreach (var type in fileTypes)
                {
                    picker.FileTypeFilter.Add(type);
                }
            } 
            else
            {
                picker.FileTypeFilter.Add("*");
            }

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
