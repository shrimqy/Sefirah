namespace Sefirah.Data.Models.Messages;

public partial class AddressEntry : ObservableObject
{
    public string Address { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    private bool isConnected;
    [JsonIgnore]
    public bool IsConnected
    {
        get => isConnected;
        set => SetProperty(ref isConnected, value);
    }
}
