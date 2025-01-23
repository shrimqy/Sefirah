using System.ComponentModel.DataAnnotations;

namespace Sefirah.App.RemoteStorage.Configuration;
public record ProviderOptions
{
    [Required]
    public string ProviderId { get; set; } = string.Empty;
}
