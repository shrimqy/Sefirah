using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Sefirah.Data.Models;
using Sefirah.ViewModels;
using Windows.System;
using Windows.UI.Core;

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
        Loaded += (_, _) =>
        {
            UpdatePaneLayout();
            HookWindowKeys(true);
        };
        Unloaded += (_, _) => HookWindowKeys(false);
    }

    private const double TwoPaneMinWidth = 900;
    private bool _showDialer;

    private void HookWindowKeys(bool enable)
    {
        if (App.MainWindow.Content is not UIElement root)
            return;

        if (enable)
            root.KeyDown += Window_KeyDown;
        else
            root.KeyDown -= Window_KeyDown;
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdatePaneLayout();

    private void UpdatePaneLayout()
    {
        var narrow = ActualWidth > 0 && ActualWidth < TwoPaneMinWidth;
        OpenDialerButton.Visibility = narrow && !_showDialer ? Visibility.Visible : Visibility.Collapsed;
        DialerBackButton.Visibility = narrow && _showDialer ? Visibility.Visible : Visibility.Collapsed;

        if (!narrow)
        {
            HistoryColumn.Width = new GridLength(2, GridUnitType.Star);
            DialerColumn.Width = new GridLength(1, GridUnitType.Star);
            HistoryPane.Visibility = Visibility.Visible;
            DialerPane.Visibility = Visibility.Visible;
            return;
        }

        HistoryPane.Visibility = _showDialer ? Visibility.Collapsed : Visibility.Visible;
        DialerPane.Visibility = _showDialer ? Visibility.Visible : Visibility.Collapsed;
        HistoryColumn.Width = _showDialer ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        DialerColumn.Width = _showDialer ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    private void OpenDialerButton_Click(object sender, RoutedEventArgs e)
    {
        _showDialer = true;
        UpdatePaneLayout();
    }

    private void DialerBackButton_Click(object sender, RoutedEventArgs e)
    {
        _showDialer = false;
        UpdatePaneLayout();
    }

    private void Window_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Back)
        {
            ViewModel.RemoveLastDialDigitCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key is VirtualKey.Enter)
        {
            _ = ViewModel.DialAsync();
            e.Handled = true;
            return;
        }

        if (TryGetDialKey(e.Key, out string? dialKey))
        {
            ViewModel.AppendDialKeyCommand.Execute(dialKey);
            e.Handled = true;
        }
    }

    private static bool TryGetDialKey(VirtualKey key, out string? dialKey)
    {
        var shift = InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);

        dialKey = key switch
        {
            VirtualKey.Number0 or VirtualKey.NumberPad0 => "0",
            VirtualKey.Number1 or VirtualKey.NumberPad1 => "1",
            VirtualKey.Number2 or VirtualKey.NumberPad2 => "2",
            VirtualKey.Number3 or VirtualKey.NumberPad3 => shift ? "#" : "3",
            VirtualKey.Number4 or VirtualKey.NumberPad4 => "4",
            VirtualKey.Number5 or VirtualKey.NumberPad5 => "5",
            VirtualKey.Number6 or VirtualKey.NumberPad6 => "6",
            VirtualKey.Number7 or VirtualKey.NumberPad7 => "7",
            VirtualKey.Number8 or VirtualKey.NumberPad8 => shift ? "*" : "8",
            VirtualKey.Number9 or VirtualKey.NumberPad9 => "9",
            VirtualKey.Multiply => "*",
            VirtualKey.Add => "+",
            // 187 = OEM Equals key (= / + )
            (VirtualKey)187 when shift => "+",
            _ => null
        };

        return dialKey is not null;
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

    private void ContactSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is Contact contact)
        {
            ViewModel.ApplyContactToDialer(contact);
        }
        else
        {
            ViewModel.ApplySearchQueryAsNumber(args.QueryText);
        }

        sender.Text = string.Empty;
        sender.ItemsSource = null;
        ShowDialer();
    }

    private void ShowDialer()
    {
        if (ActualWidth < TwoPaneMinWidth)
        {
            _showDialer = true;
            UpdatePaneLayout();
        }
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
        if (e.HoldingState is HoldingState.Completed)
        {
            suppressZeroClick = true;
            ViewModel.PhoneNumber += "+";
            e.Handled = true;
        }
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
