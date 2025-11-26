using Microsoft.Extensions.Configuration.Json;
using System.Text.Json;

namespace MaxBackup.ServiceApp;

/// <summary>
/// Custom configuration source that expands user paths (~ and %USERPROFILE%) in the JSON config
/// </summary>
public class UserPathExpandingConfigurationSource : IConfigurationSource
{
    private readonly string _configPath;
    private readonly string _userProfilePath;

    public UserPathExpandingConfigurationSource(string configPath, string userProfilePath)
    {
        _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
        _userProfilePath = userProfilePath ?? throw new ArgumentNullException(nameof(userProfilePath));
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new UserPathExpandingConfigurationProvider(this, _configPath, _userProfilePath);
    }
}

public class UserPathExpandingConfigurationProvider : ConfigurationProvider
{
    private readonly string _configPath;
    private readonly string _userProfilePath;
    private FileSystemWatcher? _watcher;

    public UserPathExpandingConfigurationProvider(
        UserPathExpandingConfigurationSource source,
        string configPath,
        string userProfilePath)
    {
        _configPath = configPath;
        _userProfilePath = userProfilePath;
    }

    public override void Load()
    {
        if (!File.Exists(_configPath))
        {
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            
            // Expand paths in the JSON before parsing (keeping JSON escaping)
            json = UserPathResolver.ExpandUserPath(json, _userProfilePath, isJsonContent: true);

            // Parse JSON into configuration dictionary
            var jsonDoc = JsonDocument.Parse(json);
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            ParseJsonElement("", jsonDoc.RootElement, data);
            Data = data;

            // Set up file watcher for hot reload
            SetupFileWatcher();
        }
        catch (Exception ex)
        {
            // If config fails to load, set empty data
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            throw new InvalidOperationException($"Failed to load config from {_configPath}", ex);
        }
    }

    private void ParseJsonElement(string prefix, JsonElement element, Dictionary<string, string?> data)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}:{property.Name}";
                    ParseJsonElement(key, property.Value, data);
                }
                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    ParseJsonElement($"{prefix}:{index}", item, data);
                    index++;
                }
                break;

            case JsonValueKind.String:
                data[prefix] = element.GetString();
                break;

            case JsonValueKind.Number:
                data[prefix] = element.GetRawText();
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                data[prefix] = element.GetBoolean().ToString();
                break;

            case JsonValueKind.Null:
                data[prefix] = null;
                break;
        }
    }

    private void SetupFileWatcher()
    {
        var directory = Path.GetDirectoryName(_configPath);
        var fileName = Path.GetFileName(_configPath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            return;

        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };

        _watcher.Changed += (s, e) => ReloadConfig();
        _watcher.Created += (s, e) => ReloadConfig();
        _watcher.Renamed += (s, e) => ReloadConfig();
        
        _watcher.EnableRaisingEvents = true;
    }

    private void ReloadConfig()
    {
        // Debounce rapid file changes
        Thread.Sleep(100);

        try
        {
            Load();
            OnReload();
        }
        catch
        {
            // Ignore reload errors
        }
    }
}
