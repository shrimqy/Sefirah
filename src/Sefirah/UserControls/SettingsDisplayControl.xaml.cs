using Microsoft.UI.Xaml.Markup;

namespace Sefirah.UserControls;

[ContentProperty(Name = nameof(SettingsActionableElement))]
public sealed partial class SettingsDisplayControl : UserControl
{
    public FrameworkElement SettingsActionableElement { get; set; }

    public static readonly DependencyProperty AdditionalDescriptionContentProperty = DependencyProperty.Register(
        nameof(AdditionalDescriptionContent),
        typeof(FrameworkElement),
        typeof(SettingsDisplayControl),
        new PropertyMetadata(null)
    );

    public FrameworkElement AdditionalDescriptionContent
    {
        get => (FrameworkElement)GetValue(AdditionalDescriptionContentProperty);
        set => SetValue(AdditionalDescriptionContentProperty, value);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(SettingsDisplayControl),
        new PropertyMetadata(null)
    );

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(SettingsDisplayControl),
        new PropertyMetadata(null)
    );

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(FrameworkElement),
        typeof(SettingsDisplayControl),
        new PropertyMetadata(null)
    );

    public FrameworkElement Icon
    {
        get => (FrameworkElement)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public SettingsDisplayControl()
    {
        InitializeComponent();
        VisualStateManager.GoToState(this, "NormalState", false);
    }

    private void MainPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width == e.PreviousSize.Width || ActionableElement is null)
            return;

        var stateToGoName = (ActionableElement.ActualWidth > e.NewSize.Width / 3) ? "CompactState" : "NormalState";
        VisualStateManager.GoToState(this, stateToGoName, false);
    }
}
