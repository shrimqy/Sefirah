using Microsoft.UI.Xaml.Media.Imaging;

namespace Sefirah.Data.Models.Messages;

public class Contact(string address, string? displayName = null, BitmapImage? avatar = null)
{
    public string Address { get; set; } = address;
    public string? DisplayName { get; set; } = displayName;
    public BitmapImage? Avatar { get; set; } = avatar;
}
