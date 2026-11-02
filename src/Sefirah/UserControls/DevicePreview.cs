using Sefirah.Data.Models;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Sefirah.UserControls;

/// <summary>The single device-frame renderer used by the header, list and settings.</summary>
public sealed class DevicePreview : UserControl
{
    public static readonly DependencyProperty DeviceProperty = DependencyProperty.Register(
        nameof(Device), typeof(PairedDevice), typeof(DevicePreview), new PropertyMetadata(null, Changed));
    public PairedDevice? Device { get => (PairedDevice?)GetValue(DeviceProperty); set => SetValue(DeviceProperty, value); }
    public static readonly DependencyProperty PresentationProperty = DependencyProperty.Register(
        nameof(Presentation), typeof(DevicePresentation), typeof(DevicePreview), new PropertyMetadata(null, (sender, _) => ((DevicePreview)sender).Refresh()));
    public DevicePresentation? Presentation
    {
        get => (DevicePresentation?)GetValue(PresentationProperty);
        set => SetValue(PresentationProperty, value);
    }
    public static readonly DependencyProperty PreviewImageProperty = DependencyProperty.Register(
        nameof(PreviewImage), typeof(ImageSource), typeof(DevicePreview), new PropertyMetadata(null, (sender, _) => ((DevicePreview)sender).Refresh()));
    public ImageSource? PreviewImage
    {
        get => (ImageSource?)GetValue(PreviewImageProperty);
        set => SetValue(PreviewImageProperty, value);
    }
    private readonly Grid frame = new();
    private readonly Border screen = new() { BorderThickness = new Thickness(3), BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray) };
    private readonly Image image = new() { Stretch = Stretch.UniformToFill };
    private readonly FontIcon placeholder = new() { FontSize = 24 };
    private readonly Border stand = new() { Width = 28, Height = 5, Background = new SolidColorBrush(Microsoft.UI.Colors.Gray), VerticalAlignment = VerticalAlignment.Bottom };

    public DevicePreview()
    {
        var contents = new Grid();
        contents.Children.Add(image);
        contents.Children.Add(placeholder);
        screen.Child = contents;
        frame.Children.Add(screen);
        frame.Children.Add(stand);
        Content = new Viewbox { Child = frame, Stretch = Stretch.Uniform };
        Loaded += (_, _) => { Subscribe(Device); Refresh(); };
        Unloaded += (_, _) => Unsubscribe(Device);
        Refresh();
    }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var preview = (DevicePreview)sender;
        preview.Unsubscribe(e.OldValue as PairedDevice);
        preview.Subscribe(e.NewValue as PairedDevice);
        preview.Refresh();
    }
    private void Subscribe(PairedDevice? peer)
    {
        if (peer is null) return;
        peer.PropertyChanged -= DeviceChanged;
        peer.PropertyChanged += DeviceChanged;
    }
    private void Unsubscribe(PairedDevice? peer) { if (peer is not null) peer.PropertyChanged -= DeviceChanged; }
    private void DeviceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PairedDevice.Presentation) or nameof(PairedDevice.Wallpaper)) Refresh();
    }
    private void Refresh()
    {
        var presentation = Device?.Presentation ?? Presentation;
        var kind = presentation?.Kind ?? "unknown";
        var ratio = presentation?.DisplayRatio ?? 1;
        frame.Width = 100 * ratio;
        frame.Height = 100;
        screen.CornerRadius = new CornerRadius(kind == "phone" ? 9 : kind == "tablet" ? 6 : 2);
        screen.Margin = kind == "computer" ? new Thickness(0, 0, 0, 9) : new Thickness(0);
        stand.Visibility = kind == "computer" ? Visibility.Visible : Visibility.Collapsed;
        image.Source = presentation?.WallpaperSource is "wallpaper" or "userSelected" ? Device?.Wallpaper ?? PreviewImage : null;
        placeholder.Visibility = image.Source is null ? Visibility.Visible : Visibility.Collapsed;
        placeholder.Glyph = kind switch { "phone" => "\uE8EA", "tablet" => "\uE70A", "computer" => "\uE7F4", _ => "\uE9CE" };
    }
}
