using Microsoft.UI.Xaml.Media.Imaging;

namespace Sefirah.Data.Models;

public class CallerContact(string address, string? displayName = null, BitmapImage? avatar = null)
{
    public string Address { get; set; } = address;

    public string DisplayName { get; set; } = !string.IsNullOrEmpty(displayName) ? displayName : address;

    public BitmapImage? Avatar { get; set; } = avatar;
}
