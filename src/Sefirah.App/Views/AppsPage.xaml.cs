using Microsoft.UI.Xaml.Controls;
using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.ViewModels;

namespace Sefirah.App.Views;

public sealed partial class AppsPage : Page
{
    public AppsViewModel ViewModel { get; }
    public AppsPage()
    {
        this.InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<AppsViewModel>();
    }

    private async void AppsGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ApplicationInfoEntity app)
        {
            await ViewModel.OpenApp(app.AppPackage);
        }
    }
    
    private void AppSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            string query = sender.Text.ToLower();
            
            if (string.IsNullOrWhiteSpace(query))
            {
                sender.ItemsSource = null;
            }
            else
            {
                // Filter apps based on the query
                var suggestions = ViewModel.Apps
                    .Where(app => app.AppName.ToLower().Contains(query))
                    .ToList();
                    
                sender.ItemsSource = suggestions;
            }
        }
    }

    private void AppSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is ApplicationInfoEntity selectedApp)
        {
            sender.Text = selectedApp.AppName;
        }
    }

    private async void AppSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is ApplicationInfoEntity selectedApp)
        {
            sender.Text = string.Empty;
            sender.ItemsSource = null;
            
            await ViewModel.OpenApp(selectedApp.AppPackage);
        }
    }
}
