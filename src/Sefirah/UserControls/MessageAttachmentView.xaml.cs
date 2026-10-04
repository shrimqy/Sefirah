using Sefirah.Data.Models.Messages;

namespace Sefirah.UserControls;

public sealed partial class MessageAttachmentView : UserControl
{
    public MessageAttachmentView()
    {
        InitializeComponent();
        Loaded += (_, _) => TryRequestGifPrefetch();
    }

    public static readonly DependencyProperty AttachmentProperty =
        DependencyProperty.Register(
            nameof(Attachment),
            typeof(MessageAttachment),
            typeof(MessageAttachmentView),
            new PropertyMetadata(null, OnAttachmentChanged));

    public MessageAttachment? Attachment
    {
        get => (MessageAttachment?)GetValue(AttachmentProperty);
        set => SetValue(AttachmentProperty, value);
    }

    public event RoutedEventHandler? Click;
    public event RoutedEventHandler? OpenWithClick;
    public event RoutedEventHandler? ShowInFolderClick;
    public event RoutedEventHandler? GifPrefetchRequested;

    private static void OnAttachmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MessageAttachmentView view)
            view.TryRequestGifPrefetch();
    }

    private void TryRequestGifPrefetch()
    {
        if (Attachment is { IsGif: true, HasFullFile: false, IsLoading: false })
            GifPrefetchRequested?.Invoke(this, new RoutedEventArgs());
    }

    private void OnClicked(object sender, RoutedEventArgs e) => Click?.Invoke(this, e);

    private void OnOpenWithClick(object sender, RoutedEventArgs e) => OpenWithClick?.Invoke(this, e);

    private void OnShowInFolderClick(object sender, RoutedEventArgs e) => ShowInFolderClick?.Invoke(this, e);
}
