using Microsoft.UI.Xaml.Input;
using Sefirah.Data.Models;
using Sefirah.ViewModels;

namespace Sefirah.Views;

public sealed partial class CallsPage : Page
{
    public CallsPageViewModel ViewModel { get; }
    private bool suppressZeroClick;

    public CallsPage()
    {
        InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<CallsPageViewModel>();
        DataContext = ViewModel;
    }

    private async void DialButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.DialAsync();
    }

    private async void RetryCallingSetupButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RetryCallingSetupAsync();
    }

    private async void EnableBluetoothButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.EnableBluetoothAsync();
    }

    private void ContactSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason is AutoSuggestionBoxTextChangeReason.UserInput)
        {
            sender.ItemsSource = ViewModel.SearchContacts(sender.Text);
        }
    }

    private void ContactSearch_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Contact contact)
        {
            ViewModel.ApplyContactToDialer(contact);
            sender.Text = string.Empty;
            sender.ItemsSource = null;
        }
    }

    private void ContactSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is Contact contact)
        {
            ViewModel.ApplyContactToDialer(contact);
            sender.Text = string.Empty;
            sender.ItemsSource = null;
            return;
        }

        ViewModel.ApplySearchQueryAsNumber(args.QueryText);
        sender.Text = string.Empty;
        sender.ItemsSource = null;
    }

    private void CallLogs_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CallLog callLog)
        {
            ViewModel.ToggleSelectingCallLog(callLog);
        }
    }

    private async void CallLogItemCallButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not CallLog callLog)
        {
            return;
        }
        await ViewModel.DialSelectedCallLogAsync(callLog);
    }

    private void DialZeroButton_Click(object sender, RoutedEventArgs e)
    {
        if (suppressZeroClick)
        {
            suppressZeroClick = false;
            return;
        }

        ViewModel.PhoneNumber += "0";
    }

    private void DialZeroButton_Holding(object sender, HoldingRoutedEventArgs e)
    {
        if (!string.Equals(e.HoldingState.ToString(), "Completed", StringComparison.Ordinal))
        {
            return;
        }

        suppressZeroClick = true;
        ViewModel.PhoneNumber += "+";
    }

    private void DialZeroButton_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        suppressZeroClick = true;
        ViewModel.PhoneNumber += "+";
        e.Handled = true;
    }

    private void RepeatBackspaceButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PhoneNumber.Length == 0)
        {
            return;
        }

        ViewModel.PhoneNumber = ViewModel.PhoneNumber[..^1];
    }
}
