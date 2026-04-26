using Microsoft.UI.Xaml.Media.Imaging;

namespace Sefirah.Data.Models;

public class Contact(string id, string address, string? displayName = null, BitmapImage? avatar = null)
{
    public string Id { get; set; } = id;

    public string Address { get; set; } = address;

    public string DisplayName { get; set; } = !string.IsNullOrEmpty(displayName) ? displayName : address;

    public BitmapImage? Avatar { get; set; } = avatar;
}
