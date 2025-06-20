using System.ComponentModel.DataAnnotations;

namespace Sefirah.Platforms.Windows.RemoteStorage.Configuration;
public record ProviderOptions
{
    [Required]
    public string ProviderId { get; set; } = string.Empty;
}
