using CommunityToolkit.WinUI.Helpers;
using Microsoft.UI.Xaml.Input;
using Sefirah.Data.Models;
using Sefirah.ViewModels;

namespace Sefirah.Views;

public sealed partial class CallsPage : Page
{
    public CallsPageViewModel ViewModel { get; }
    private readonly Random random = new();
    private readonly Dictionary<string, Windows.UI.Color> contactColors = [];
    private bool suppressZeroClick;

    private static readonly string[] PredefinedColors =
    [
        "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7",
        "#DDA0DD", "#98D8C8", "#F7DC6F", "#BB8FCE", "#85C1E9",
        "#F8C471", "#82E0AA", "#F1948A", "#85C1E9", "#D7BDE2",
        "#A9DFBF", "#F9E79F", "#AED6F1", "#FADBD8", "#D5DBDB"
    ];

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
            ViewModel.SearchContacts(sender.Text);
    }

    private void ContactSearch_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Contact contact)
        {
            ViewModel.ApplyContactToDialer(contact);
            sender.Text = string.Empty;
        }
    }

    private void ContactSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is Contact contact)
        {
            ViewModel.ApplyContactToDialer(contact);
            sender.Text = string.Empty;
            return;
        }

        ViewModel.ApplySearchQueryAsNumber(args.QueryText);
        sender.Text = string.Empty;
    }

    private void CallAvatarBorder_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (sender is not Border border || border.DataContext is not CallLog callLog)
        {
            return;
        }

        var key = callLog.PhoneNumber;
        var color = GetOrCreateContactColor(key);
        border.Background = new SolidColorBrush(color);
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

    private Windows.UI.Color GetOrCreateContactColor(string key)
    {
        if (!contactColors.TryGetValue(key, out var color))
        {
            color = GenerateRandomColor();
            contactColors[key] = color;
        }

        return color;
    }

    private Windows.UI.Color GenerateRandomColor()
    {
        var randomColorHex = PredefinedColors[random.Next(PredefinedColors.Length)];
        return randomColorHex.ToColor();
    }
}
