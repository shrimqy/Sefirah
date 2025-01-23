using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Sefirah.App.Views;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();

        // Window customization
        Window window = MainWindow.Instance;
        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(AppTitleBar);
    }
}
