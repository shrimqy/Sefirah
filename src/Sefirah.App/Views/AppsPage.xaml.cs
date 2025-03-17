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
}
