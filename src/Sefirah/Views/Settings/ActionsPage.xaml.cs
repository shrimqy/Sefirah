using Sefirah.Actions;
using Sefirah.Data.Items;
using Sefirah.UserControls;

namespace Sefirah.Views.Settings;

public sealed partial class ActionsPage : Page
{
    public ActionsPage()
    {
        InitializeComponent();
        SetupBreadcrumb();
    }

    private void AvailableAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ActionMetadata metadata })
        {
            return;
        }

        AddActionFlyout.Hide();
        ViewModel.AddActionCommand.Execute(metadata.ActionId);
    }

    private void ActionIconFlyout_Opening(object sender, object e)
    {
        if (sender is not Flyout { Content: ActionIconPicker picker, Target: Button { Tag: ActionItem action } } flyout)
        {
            return;
        }

        picker.Tag = flyout;
        picker.SelectedIcon = action.Icon;
        picker.PrepareForOpen();
    }

    private void ActionIconPicker_IconPicked(object sender, string icon)
    {
        if (sender is not ActionIconPicker { Tag: Flyout { Target: Button { Tag: ActionItem action } } flyout })
        {
            return;
        }

        action.Icon = icon;
        ViewModel.UpdateAction(action);
        flyout.Hide();
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

        if (clickedItem?.PageType is not null && clickedItem.PageType != typeof(ActionsPage))
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}
