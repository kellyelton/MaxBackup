using System.Security.Principal;
using Microsoft.Win32;

namespace MaxBackup.ServiceApp;

public static class UserPathResolver
{
    public static string? ResolveUserProfilePath(string sid)
    {
        try
        {
            // Try registry lookup first
            var profileListKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";
            using var key = Registry.LocalMachine.OpenSubKey($"{profileListKey}\\{sid}");
            if (key != null)
            {
                var profilePath = key.GetValue("ProfileImagePath") as string;
                if (!string.IsNullOrEmpty(profilePath) && Directory.Exists(profilePath))
                {
                    return profilePath;
                }
            }

            // Fallback: try to get username and construct path
            var securityIdentifier = new SecurityIdentifier(sid);
            var account = securityIdentifier.Translate(typeof(NTAccount)) as NTAccount;
            if (account != null)
            {
                var username = account.Value.Split('\\').Last();
                var profilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "..", username);
                profilePath = Path.GetFullPath(profilePath);
                if (Directory.Exists(profilePath))
                {
                    return profilePath;
                }
            }
        }
        catch
        {
            // Ignore errors, return null
        }

        return null;
    }

    /// <summary>
    /// Expands user path tokens in a string. When used for JSON content, the paths
    /// must remain properly JSON-escaped (backslashes doubled).
    /// </summary>
    /// <param name="content">The content to expand (may be JSON or a plain path)</param>
    /// <param name="userProfilePath">The user's profile path (e.g., C:\Users\username)</param>
    /// <param name="isJsonContent">True if the content is raw JSON text that needs escaped backslashes</param>
    public static string ExpandUserPath(string content, string userProfilePath, bool isJsonContent = false)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        if (isJsonContent)
        {
            // For JSON content, backslashes are escaped as \\
            // So ~\\ in JSON means ~\ as a path
            // We need to replace with properly escaped path: C:\\Users\\username\\
            var escapedProfilePath = userProfilePath.Replace("\\", "\\\\");
            
            // Replace ~\\ (JSON-escaped ~\) with escaped profile path + \\
            content = content.Replace("~\\\\", escapedProfilePath + "\\\\", StringComparison.Ordinal);
            
            // Replace ~/ with escaped profile path + /
            content = content.Replace("~/", escapedProfilePath + "/", StringComparison.Ordinal);
            
            // Replace %USERPROFILE% with escaped profile path
            content = content.Replace("%USERPROFILE%", escapedProfilePath, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // For plain paths, do simple replacement
            if (content.StartsWith("~\\") || content.StartsWith("~/"))
            {
                content = userProfilePath + content.Substring(1);
            }
            else if (content == "~")
            {
                content = userProfilePath;
            }

            // Replace %USERPROFILE% with user profile path
            content = content.Replace("%USERPROFILE%", userProfilePath, StringComparison.OrdinalIgnoreCase);

            // Expand other environment variables (but they'll resolve to service account's environment)
            content = Environment.ExpandEnvironmentVariables(content);
        }

        return content;
    }

    public static void EnsureDirectoryExists(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
