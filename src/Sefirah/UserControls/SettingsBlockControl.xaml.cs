using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Markup;

namespace Sefirah.UserControls;

[ContentProperty(Name = nameof(SettingsActionableElement))]
public sealed partial class SettingsBlockControl : UserControl
{
    public FrameworkElement SettingsActionableElement { get; set; }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SettingsBlockControl), new PropertyMetadata(null));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingsBlockControl), new PropertyMetadata(null));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(FrameworkElement),
        typeof(SettingsBlockControl),
        new PropertyMetadata(null));

    public FrameworkElement Icon
    {
        get => (FrameworkElement)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty IsClickableProperty = DependencyProperty.Register(
        nameof(IsClickable), typeof(bool), typeof(SettingsBlockControl), new PropertyMetadata(false));

    public bool IsClickable
    {
        get => (bool)GetValue(IsClickableProperty);
        set => SetValue(IsClickableProperty, value);
    }

    public static readonly DependencyProperty ButtonCommandProperty = DependencyProperty.Register(
        nameof(ButtonCommand), typeof(ICommand), typeof(SettingsBlockControl), new PropertyMetadata(null));

    public ICommand ButtonCommand
    {
        get => (ICommand)GetValue(ButtonCommandProperty);
        set => SetValue(ButtonCommandProperty, value);
    }

    public static readonly DependencyProperty ExpandableContentProperty = DependencyProperty.Register(
        nameof(ExpandableContent), typeof(FrameworkElement), typeof(SettingsBlockControl), new PropertyMetadata(null));

    public FrameworkElement ExpandableContent
    {
        get => (FrameworkElement)GetValue(ExpandableContentProperty);
        set => SetValue(ExpandableContentProperty, value);
    }

    public static readonly DependencyProperty AdditionalDescriptionContentProperty = DependencyProperty.Register(
        nameof(AdditionalDescriptionContent), typeof(FrameworkElement), typeof(SettingsBlockControl), new PropertyMetadata(null));

    public FrameworkElement AdditionalDescriptionContent
    {
        get => (FrameworkElement)GetValue(AdditionalDescriptionContentProperty);
        set => SetValue(AdditionalDescriptionContentProperty, value);
    }

    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(SettingsBlockControl), new PropertyMetadata(false));

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public static new readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(SettingsBlockControl), new PropertyMetadata(new CornerRadius(6)));

    public new CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty ContentPaddingProperty = DependencyProperty.Register(
        nameof(ContentPadding),
        typeof(Thickness),
        typeof(SettingsBlockControl),
        new PropertyMetadata(new Thickness(16, 12, 16, 12)));

    public Thickness ContentPadding
    {
        get => (Thickness)GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public event EventHandler<bool> Click;

    public SettingsBlockControl()
    {
        InitializeComponent();
        Loaded += SettingsBlockControl_Loaded;
    }

    private void SettingsBlockControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (ActionableButton is not null)
        {
            AutomationProperties.SetName(ActionableButton, Title);
            ActionableButton.Click += ActionableButton_Click;
        }
    }

    private void ActionableButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsClickable)
        {
            Click?.Invoke(this, true);
        }
    }

    private void Expander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        Click?.Invoke(this, true);
    }

    private void Expander_Collapsed(Expander sender, ExpanderCollapsedEventArgs args)
    {
        Click?.Invoke(this, false);
    }
}
