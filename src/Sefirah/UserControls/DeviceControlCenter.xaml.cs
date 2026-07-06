using System.Collections.Specialized;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using Sefirah.Utils;
using Sefirah.ViewModels;

namespace Sefirah.UserControls;

public sealed partial class DeviceControlCenter : UserControl
{
    public MainPageViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<MainPageViewModel>();

    private Storyboard? currentOverlayAnimation;

    private static bool IsPhoneFrameScrollTeachingTipShown
    {
        get => ApplicationData.Current.LocalSettings.Values[Constants.LocalSettings.PhoneFrameScrollTeachingTipShown] is true;
        set => ApplicationData.Current.LocalSettings.Values[Constants.LocalSettings.PhoneFrameScrollTeachingTipShown] = value;
    }

    public DeviceControlCenter()
    {
        InitializeComponent();
        FadeOutStoryboard.Completed += OnFadeOutStoryboardCompleted;
        ViewModel.PairedDevices.CollectionChanged += OnPairedDevicesCollectionChanged;
        Unloaded += DeviceControlCenter_Unloaded;
    }

    private void DeviceControlCenter_Unloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= DeviceControlCenter_Unloaded;
        FadeOutStoryboard.Completed -= OnFadeOutStoryboardCompleted;
        ViewModel.PairedDevices.CollectionChanged -= OnPairedDevicesCollectionChanged;
    }

    private void OnPairedDevicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => TryShowPhoneFrameScrollTeachingTip();

    private void TryShowPhoneFrameScrollTeachingTip()
    {
        if (IsPhoneFrameScrollTeachingTipShown) return;
        if (ViewModel.PairedDevices.Count <= 1) return;
        if (PhoneFrameScrollTeachingTip.IsOpen) return;

        PhoneFrameScrollTeachingTip.IsOpen = true;
    }

    private void PhoneFrameScrollTeachingTip_Closed(object? _, TeachingTipClosedEventArgs args)
        => IsPhoneFrameScrollTeachingTipShown = true;

    private async void SendFilesButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Device is null)
            return;

        var files = await PickerHelper.PickMultipleFilesAsync();
        if (files.Count > 0)
            ViewModel.SendFiles(files);
    }

    private void PhoneFrame_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var pointerPoint = e.GetCurrentPoint(PhoneFrameGrid);
        ViewModel.SwitchToNextDevice(pointerPoint.Properties.MouseWheelDelta);
        e.Handled = true;
    }

    private void PhoneFrame_PointerEntered(object sender, PointerRoutedEventArgs e)
        => AnimateOverlay(true);

    private void PhoneFrame_PointerExited(object sender, PointerRoutedEventArgs e)
        => AnimateOverlay(false);

    private void RingerModeSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is Segmented segmented)
            ViewModel.SetRingerMode(segmented.SelectedIndex);
    }

    private void AudioSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Slider slider && slider.Tag is AudioStream stream)
            ViewModel.SetAudioLevel(stream.StreamType, (int)slider.Value);
    }

    private void AddressListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AddressEntry entry)
        {
            ViewModel.ConnectToAddress(entry);
            ConnectionFlyout.Hide();
        }
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DisconnectConnection();
        ConnectionFlyout.Hide();
    }

    private async void DisconnectAdbDeviceButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AdbDevice device })
            await ViewModel.DisconnectAdbDevice(device);
    }

    private void AnimateOverlay(bool show)
    {
        currentOverlayAnimation?.Stop();
        currentOverlayAnimation = null;

        if (show)
        {
            PhoneFrameOverlay.Visibility = Visibility.Visible;
            currentOverlayAnimation = FadeInStoryboard;
            FadeInStoryboard.Begin();
        }
        else
        {
            currentOverlayAnimation = FadeOutStoryboard;
            FadeOutStoryboard.Begin();
        }
    }

    private void OnFadeOutStoryboardCompleted(object? sender, object e)
    {
        PhoneFrameOverlay.Visibility = Visibility.Collapsed;
        currentOverlayAnimation = null;
    }
}
