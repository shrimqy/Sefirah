using Sefirah.Data.Models;
using Sefirah.ViewModels;

namespace Sefirah.Views;

public sealed partial class AppsPage : Page
{
    public AppsViewModel ViewModel { get; }
    public AppsPage()
    {
        InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<AppsViewModel>();
    }

    private async void AppsGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ApplicationInfo app)
        {
            await ViewModel.OpenApp(app.PackageName, app.AppName);
        }
    }

    private void AppSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason is AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var suggestions = ViewModel.Apps
                .Where(app => app.AppName.Contains(sender.Text, StringComparison.OrdinalIgnoreCase))
                .ToList();

            sender.ItemsSource = suggestions;
            
        }
    }

    private void AppSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is ApplicationInfo selectedApp)
        {
            sender.Text = selectedApp.AppName;
        }
    }

    private async void AppSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is ApplicationInfo selectedApp)
        {
            sender.Text = string.Empty;
            sender.ItemsSource = null;

            await ViewModel.OpenApp(selectedApp.PackageName, selectedApp.AppName);
        }
    }

    private void PinAppClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ApplicationInfo app)
        {
            ViewModel.PinApp(app);
        }
    }

    private async void UninstallAppClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ApplicationInfo app)
        {
            ViewModel.UninstallApp(app);
        }
    }
}
