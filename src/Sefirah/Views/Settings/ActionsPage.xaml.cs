using Sefirah.Extensions;

namespace Sefirah.Views.Settings;

public class NavigationItem
{
    public string Name { get; set; } = string.Empty;
    public Type? PageType { get; set; }
}

public sealed partial class ActionsPage : Page
{
    public ActionsPage()
    {
        this.InitializeComponent();
        SetupBreadcrumb();
    }

    private void SetupBreadcrumb()
    {
        BreadcrumbBar.ItemsSource = new ObservableCollection<NavigationItem>
        {
            new() { Name = "General".GetLocalizedResource(), PageType = typeof(GeneralPage) },
            new() { Name = "Actions".GetLocalizedResource(), PageType = typeof(ActionsPage) }
        };
        BreadcrumbBar.ItemClicked += BreadcrumbBar_ItemClicked;
    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        var items = BreadcrumbBar.ItemsSource as ObservableCollection<NavigationItem>;
        var clickedItem = items?[args.Index];
        
        if (clickedItem?.PageType != null && clickedItem.PageType != typeof(ActionsPage))
        {
            // Navigate back to general page
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
} 
