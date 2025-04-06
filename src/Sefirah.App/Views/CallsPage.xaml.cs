using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sefirah.App.ViewModels;

namespace Sefirah.App.Views;

public sealed partial class CallsPage : Page
{
    public CallsViewModel ViewModel { get; }

    public CallsPage()
    {
        this.InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<CallsViewModel>();
        this.DataContext = ViewModel;
    }

    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RegisterApp();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ConnectAsync();
    }
}
