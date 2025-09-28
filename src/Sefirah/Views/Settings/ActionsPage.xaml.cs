using Sefirah.Data.Items;
using Sefirah.Extensions;

namespace Sefirah.Views.Settings;

public sealed partial class ActionsPage : Page
{
    public ActionsPage()
    {
        InitializeComponent();
        SetupBreadcrumb();
    }

    private void SetupBreadcrumb()
    {
        BreadcrumbBar.ItemsSource = new ObservableCollection<BreadcrumbBarItemModel>
        {
            new("General".GetLocalizedResource(), typeof(GeneralPage)),
            new("Actions".GetLocalizedResource(), typeof(ActionsPage))
        };
        BreadcrumbBar.ItemClicked += BreadcrumbBar_ItemClicked;
    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        var items = BreadcrumbBar.ItemsSource as ObservableCollection<BreadcrumbBarItemModel>;
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
