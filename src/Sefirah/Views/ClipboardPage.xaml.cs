using Sefirah.Data.Models;
using Sefirah.ViewModels;

namespace Sefirah.Views;

public sealed partial class ClipboardPage : Page
{
    public ClipboardViewModel ViewModel { get; }

    public ClipboardPage()
    {
        InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<ClipboardViewModel>();

        Loaded += ClipboardPage_Loaded;
        Unloaded += ClipboardPage_Unloaded;
    }

    private async void ClipboardPage_Loaded(object sender, RoutedEventArgs e)
        => await ViewModel.InitializeAsync();

    private void ClipboardPage_Unloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ClipboardPage_Loaded;
        Unloaded -= ClipboardPage_Unloaded;
        ViewModel.Suspend();
    }

    private async void EntriesListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ClipboardEntry entry)
        {
            await ViewModel.CopyCommand.ExecuteAsync(entry);
        }
    }

    private async void CopyEntryClick(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is ClipboardEntry entry)
        {
            await ViewModel.CopyCommand.ExecuteAsync(entry);
        }
    }

    private async void DeleteEntryClick(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is ClipboardEntry entry)
        {
            await ViewModel.DeleteCommand.ExecuteAsync(entry);
        }
    }

    private static ClipboardEntry? GetEntry(object sender)
        => (sender as FrameworkElement)?.DataContext as ClipboardEntry;
}
