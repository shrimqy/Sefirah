using System.Diagnostics;

namespace Sefirah.Utils;

public static class UserInformation
{
    /// <summary>
    /// Gets information about the current user
    /// </summary>
    /// <returns>Tuple containing (name, avatar base64 string)</returns>
    public static async Task<(string name, string? avatar)> GetCurrentUserInfoAsync()
    {
#if WINDOWS
        try
        {
            string name = string.Empty;
            string? avatarBase64 = null;
            
            // Windows-specific code
            var users = await global::Windows.System.User.FindAllAsync();
            if (!users.Any())
            {
                return (GetFallbackUserName(), null);
            }
            var currentUser = users[0];
            if (currentUser == null)
            {
                return (GetFallbackUserName(), null);
            }

            // Try to get the avatar
            try
            {
                var picture = await currentUser.GetPictureAsync(global::Windows.System.UserPictureSize.Size1080x1080);
                if (picture != null)
                {
                    using var stream = await picture.OpenReadAsync();
                    using var reader = new global::Windows.Storage.Streams.DataReader(stream);

                    await reader.LoadAsync((uint)stream.Size);
                    byte[] buffer = new byte[stream.Size];
                    reader.ReadBytes(buffer);

                    avatarBase64 = Convert.ToBase64String(buffer);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting user avatar: {ex}");
            }

            // Try to get the name using properties
            var properties = await currentUser.GetPropertiesAsync(["FirstName", "DisplayName", "AccountName"]);

            if (properties.Any())
            {
                if (properties.TryGetValue("FirstName", out object? value) && 
                    value is string firstNameProperty &&
                    !string.IsNullOrEmpty(firstNameProperty))
                {
                    name = firstNameProperty;
                }
                else
                {
                    name = properties["DisplayName"] as string
                        ?? properties["AccountName"] as string
                        ?? Environment.UserName;
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                // Try to get name from WindowsIdentity
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var identityName = identity.Name;
                if (!string.IsNullOrEmpty(identityName))
                {
                    name = identityName.Split('\\').Last().Split(' ').First();
                }
            }
            
            // Last resort fallback
            if (string.IsNullOrEmpty(name))
            {
                name = GetFallbackUserName();
            }
            
            return (name, avatarBase64);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting user info: {ex}");
            return (GetFallbackUserName(), null);
        }
#else
        // For other platforms (Linux/Skia, etc.)
        string username = Environment.UserName;
        
        // On Linux, we can try to get a more friendly name from the USER or USERNAME env vars
        if (string.IsNullOrEmpty(username))
        {
            username = Environment.GetEnvironmentVariable("USER") ?? 
                      Environment.GetEnvironmentVariable("USERNAME") ??
                      "User";
        }
        
        // We don't have a way to get the avatar in other platforms
        return (username, null);
#endif
    }

    private static string GetFallbackUserName()
        => Environment.UserName.Split('\\').Last().Split(' ').First();
}
