using Azure;

namespace MaxBackup.Shared;

/// <summary>
/// Shared utility for converting Azure SDK errors to user-friendly messages.
/// </summary>
public static class AzureErrorHelper
{
    /// <summary>
    /// Converts Azure SDK errors to user-friendly messages.
    /// </summary>
    public static string GetFriendlyErrorMessage(RequestFailedException ex)
    {
        return ex.ErrorCode switch
        {
            "AuthenticationFailed" => "Authentication failed. Please verify your account name and access key are correct.",
            "AccountIsDisabled" => "The storage account is disabled.",
            "ContainerNotFound" => "Container not found and could not be created. Check your permissions.",
            "AuthorizationFailure" => "Authorization failed. The access key may not have sufficient permissions.",
            "InvalidResourceName" => "Invalid container name. Container names must be 3-63 characters, lowercase letters, numbers, and hyphens only.",
            _ => $"Azure error: {ex.Message}"
        };
    }
}
