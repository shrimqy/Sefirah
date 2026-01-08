using Sefirah.Helpers;
using Windows.System;

namespace Sefirah.Utils;

public static class UserInformation
{
    /// <summary>
    /// Gets the current user's name
    /// </summary>
    /// <returns>The user's name</returns>
    public static async Task<string> GetCurrentUserNameAsync()
    {
#if WINDOWS
        try
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser is null)
            {
                return GetFallbackUserName();
            }

            string name = string.Empty;

            // Try to get the name using properties
            try
            {
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
            }
            catch (Exception)
            {
            }

            if (string.IsNullOrEmpty(name))
            {
                // Try to get name from WindowsIdentity
                try
                {
                    using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                    var identityName = identity.Name;
                    if (!string.IsNullOrEmpty(identityName))
                    {
                        name = identityName.Split('\\').Last().Split(' ').First();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting WindowsIdentity: {ex}");
                }
            }
            
            // Last resort fallback
            if (string.IsNullOrEmpty(name))
            {
                name = GetFallbackUserName();
            }
            
            return name;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Unauthorized access when getting user name: {ex}");
            return GetFallbackUserName();
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Debug.WriteLine($"COM exception accessing user name (may be related to Windows credential vault): {ex.Message}");
            return GetFallbackUserName();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting user name: {ex}");
            return GetFallbackUserName();
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
        
        return username;
#endif
    }

    /// <summary>
    /// Gets the current user's avatar as a base64 string
    /// </summary>
    /// <returns>The user's avatar as a base64 string, or null if unavailable</returns>
    public static async Task<string?> GetCurrentUserAvatarAsync()
    {
#if WINDOWS
        try
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser is null)
            {
                return null;
            }

            // Try to get the avatar
            try
            {
                var picture = await currentUser.GetPictureAsync(UserPictureSize.Size1080x1080);
                if (picture is not null)
                {
                    return await picture.ToBase64Async();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting user avatar: {ex}");
            }

            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Unauthorized access when getting user avatar: {ex}");
            return null;
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Debug.WriteLine($"COM exception accessing user avatar (may be related to Windows credential vault): {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting user avatar: {ex}");
            return null;
        }
#else
        // We don't have a way to get the avatar in other platforms
        return null;
#endif
    }

#if WINDOWS
    private static async Task<User?> GetCurrentUserAsync()
    {
        try
        {
            var users = await User.FindAllAsync();
            if (users.Any())
            {
                return users[0];
            }
        }
        catch (Exception)
        {
            Debug.WriteLine("error accessing user information, using fallback");
        }
        
        return null;
    }
#endif

    private static string GetFallbackUserName()
        => Environment.UserName.Split('\\').Last().Split(' ').First();
}
