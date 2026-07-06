using Microsoft.UI.Xaml.Input;
using Sefirah.Data.Models;
using Sefirah.ViewModels;

namespace Sefirah.UserControls;

public sealed partial class MediaSessionsControl : UserControl
{
    public MainPageViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<MainPageViewModel>();

    public MediaSessionsControl()
    {
        InitializeComponent();
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is MediaSession session)
            ViewModel.HandlePlaybackAction(session, MediaActionType.Previous);
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is MediaSession session)
        {
            var actionType = session.IsPlaying ? MediaActionType.Pause : MediaActionType.Play;
            ViewModel.HandlePlaybackAction(session, actionType);
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is MediaSession session)
            ViewModel.HandlePlaybackAction(session, MediaActionType.Next);
    }

    private void MediaPositionSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Slider slider && slider.Tag is MediaSession session && session.CanSeek == true)
            ViewModel.HandlePlaybackAction(session, MediaActionType.Seek, slider.Value);
    }

    private void VolumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is MediaSession session)
            session.IsVolumeSliderVisible = !session.IsVolumeSliderVisible;
    }

    private void MediaVolumeSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Slider slider && slider.Tag is MediaSession session)
            ViewModel.HandlePlaybackAction(session, MediaActionType.VolumeUpdate, slider.Value);
    }
}
