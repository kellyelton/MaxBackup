using System;
using System.CommandLine;
using System.Text.Json;
using Spectre.Console;

namespace max
{
    public sealed class JobsCommand : Command
    {
        public JobsCommand(Option<string> config_path, Option<bool> verbose)
            : base("jobs", "Manage backup jobs")
        {
            Subcommands.Add(new ListCommand(config_path, verbose));
            Subcommands.Add(new CreateCommand(config_path, verbose));
            Subcommands.Add(new ModifyCommand(config_path, verbose));
            Subcommands.Add(new DeleteCommand(config_path, verbose));
        }

        private static string GetConfigPath(ParseResult parseResult, Option<string> config_path)
        {
            var config_file_path = parseResult.GetValue(config_path);
            if (string.IsNullOrWhiteSpace(config_file_path))
            {
                config_file_path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "maxbackupconfig.json");
            }
            return config_file_path;
        }

        private static Config? LoadConfig(string config_file_path)
        {
            if (!File.Exists(config_file_path)) return null;
            var json_options = new JsonSerializerOptions { PropertyNameCaseInsensitive = false };
            return JsonSerializer.Deserialize<Config>(File.ReadAllText(config_file_path), json_options);
        }

        private static void SaveConfig(string config_file_path, Config config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(config_file_path, json);
        }

        public class ListCommand : Command
        {
            public ListCommand(Option<string> config_path, Option<bool> verbose)
                : base("list", "List the backup jobs")
            {
                this.SetAction(parseResult =>
                {
                    var config_file_path = GetConfigPath(parseResult, config_path);
                    var config = LoadConfig(config_file_path);
                    if (config?.Backup?.Jobs == null || config.Backup.Jobs.Length == 0)
                    {
                        Console.Error.WriteLine($"No jobs found in config file at {config_file_path}");
                        return 1;
                    }
                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .BorderColor(Color.Blue)
                        .AddColumn(new TableColumn("Name").Centered())
                        .AddColumn(new TableColumn("Source").Centered())
                        .AddColumn(new TableColumn("Destination").Centered())
                        .AddColumn(new TableColumn("Provider").Centered());
                    foreach (var job in config.Backup.Jobs)
                        table.AddRow(job.Name, job.Source, job.Destination, job.Provider ?? "[dim]local[/]");
                    AnsiConsole.Write(table);
                    return 0;
                });
            }
        }

        public class CreateCommand : Command
        {
            private readonly Argument<string> _nameArg = new("name") { Description = "Job name" };
            private readonly Argument<string> _sourceArg = new("source") { Description = "Source path" };
            private readonly Argument<string> _destArg = new("destination") { Description = "Destination path (local path or relative path if --provider is set)" };
            private readonly Option<string[]> _includeOpt;
            private readonly Option<string[]> _excludeOpt;
            private readonly Option<string?> _providerOpt;

            public CreateCommand(Option<string> config_path, Option<bool> verbose)
                : base("create", "Create a new backup job")
            {
                _includeOpt = new Option<string[]>("--include") 
                { 
                    Description = "Include patterns",
                    DefaultValueFactory = _ => new string[] { "**" }
                };
                _excludeOpt = new Option<string[]>("--exclude")
                {
                    Description = "Exclude patterns",
                    DefaultValueFactory = _ => Array.Empty<string>()
                };
                _providerOpt = new Option<string?>("--provider")
                {
                    Description = "Storage provider name for cloud backups (use 'max provider list' to see available)"
                };

                Arguments.Add(_nameArg);
                Arguments.Add(_sourceArg);
                Arguments.Add(_destArg);
                Options.Add(_includeOpt);
                Options.Add(_excludeOpt);
                Options.Add(_providerOpt);

                this.SetAction(parseResult =>
                {
                    var name = parseResult.GetValue(_nameArg)!;
                    var source = parseResult.GetValue(_sourceArg)!;
                    var destination = parseResult.GetValue(_destArg)!;
                    var include = parseResult.GetValue(_includeOpt) ?? Array.Empty<string>();
                    var exclude = parseResult.GetValue(_excludeOpt) ?? Array.Empty<string>();
                    var provider = parseResult.GetValue(_providerOpt);
                    var config_file_path = GetConfigPath(parseResult, config_path);
                    var config = LoadConfig(config_file_path) ?? new Config(new Backup(Array.Empty<Job>()), null);
                    
                    if (config.Backup.Jobs.Any(j => j.Name == name))
                    {
                        Console.Error.WriteLine($"Job '{name}' already exists.");
                        return 1;
                    }
                    
                    // Validate provider if specified
                    if (!string.IsNullOrEmpty(provider))
                    {
                        if (config.Providers == null || !config.Providers.Any(p => p.Name == provider))
                        {
                            Console.Error.WriteLine($"Provider '{provider}' not found. Use 'max provider list' to see available providers.");
                            return 1;
                        }
                    }
                    
                    var jobs = config.Backup.Jobs.ToList();
                    jobs.Add(new Job(name, source, destination, include, exclude, provider));
                    config = config with { Backup = config.Backup with { Jobs = jobs.ToArray() } };
                    SaveConfig(config_file_path, config);
                    
                    if (!string.IsNullOrEmpty(provider))
                    {
                        Console.WriteLine($"Job '{name}' created with provider '{provider}'.");
                    }
                    else
                    {
                        Console.WriteLine($"Job '{name}' created.");
                    }
                    return 0;
                });
            }
        }

        public class ModifyCommand : Command
        {
            private readonly Argument<string> _nameArg = new("name") { Description = "Job name" };
            private readonly Option<string?> _sourceOpt;
            private readonly Option<string?> _destOpt;
            private readonly Option<string[]> _includeOpt;
            private readonly Option<string[]> _excludeOpt;

            public ModifyCommand(Option<string> config_path, Option<bool> verbose)
                : base("modify", "Modify an existing backup job")
            {
                _sourceOpt = new Option<string?>("--source") { Description = "New source path" };
                _destOpt = new Option<string?>("--destination") { Description = "New destination path" };
                _includeOpt = new Option<string[]>("--include")
                {
                    Description = "New include patterns",
                    DefaultValueFactory = _ => Array.Empty<string>()
                };
                _excludeOpt = new Option<string[]>("--exclude")
                {
                    Description = "New exclude patterns",
                    DefaultValueFactory = _ => Array.Empty<string>()
                };

                Arguments.Add(_nameArg);
                Options.Add(_sourceOpt);
                Options.Add(_destOpt);
                Options.Add(_includeOpt);
                Options.Add(_excludeOpt);

                this.SetAction(parseResult =>
                {
                    var name = parseResult.GetValue(_nameArg)!;
                    var source = parseResult.GetValue(_sourceOpt);
                    var destination = parseResult.GetValue(_destOpt);
                    var include = parseResult.GetValue(_includeOpt) ?? Array.Empty<string>();
                    var exclude = parseResult.GetValue(_excludeOpt) ?? Array.Empty<string>();
                    var config_file_path = GetConfigPath(parseResult, config_path);
                    var config = LoadConfig(config_file_path);
                    if (config?.Backup?.Jobs == null)
                    {
                        Console.Error.WriteLine($"No jobs found in config file at {config_file_path}");
                        return 1;
                    }
                    var jobs = config.Backup.Jobs.ToList();
                    var idx = jobs.FindIndex(j => j.Name == name);
                    if (idx == -1)
                    {
                        Console.Error.WriteLine($"Job '{name}' not found.");
                        return 1;
                    }
                    var job = jobs[idx];
                    jobs[idx] = new Job(
                        name,
                        source ?? job.Source,
                        destination ?? job.Destination,
                        (include.Length > 0 ? include : job.Include ?? Array.Empty<string>()),
                        (exclude.Length > 0 ? exclude : job.Exclude ?? Array.Empty<string>())
                    );
                    config = config with { Backup = config.Backup with { Jobs = jobs.ToArray() } };
                    SaveConfig(config_file_path, config);
                    Console.WriteLine($"Job '{name}' modified.");
                    return 0;
                });
            }
        }

        public class DeleteCommand : Command
        {
            private readonly Argument<string> _nameArg = new("name") { Description = "Job name" };

            public DeleteCommand(Option<string> config_path, Option<bool> verbose)
                : base("delete", "Delete a backup job")
            {
                Arguments.Add(_nameArg);

                this.SetAction(parseResult =>
                {
                    var name = parseResult.GetValue(_nameArg)!;
                    var config_file_path = GetConfigPath(parseResult, config_path);
                    var config = LoadConfig(config_file_path);
                    if (config?.Backup?.Jobs == null)
                    {
                        Console.Error.WriteLine($"No jobs found in config file at {config_file_path}");
                        return 1;
                    }
                    var jobs = config.Backup.Jobs.ToList();
                    var removed = jobs.RemoveAll(j => j.Name == name);
                    if (removed == 0)
                    {
                        Console.Error.WriteLine($"Job '{name}' not found.");
                        return 1;
                    }
                    config = config with { Backup = config.Backup with { Jobs = jobs.ToArray() } };
                    SaveConfig(config_file_path, config);
                    Console.WriteLine($"Job '{name}' deleted.");
                    return 0;
                });
            }
        }
    }

    /// <summary>Root configuration containing backup settings and optional cloud providers.</summary>
    /// <param name="Backup">Backup job definitions.</param>
    /// <param name="Providers">Cloud storage provider configurations, or null if only local backups are used.</param>
    public record Config(Backup Backup, ProviderConfig[]? Providers = null);

    /// <summary>Container for backup job definitions.</summary>
    /// <param name="Jobs">Array of backup jobs to execute.</param>
    public record Backup(Job[] Jobs);

    /// <summary>Backup job configuration defining source, destination, and file filters.</summary>
    /// <param name="Name">Display name for the job.</param>
    /// <param name="Source">Source directory path to back up.</param>
    /// <param name="Destination">Destination path for local backup, or blob prefix for cloud backup.</param>
    /// <param name="Include">Glob patterns for files to include.</param>
    /// <param name="Exclude">Glob patterns for files to exclude.</param>
    /// <param name="Provider">Name of a configured cloud provider for cloud backup, or null for local backup.</param>
    public record Job(
        string Name, 
        string Source, 
        string Destination, 
        string[] Include, 
        string[] Exclude,
        string? Provider = null);
    
    // Provider configuration types for JSON serialization
    [System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    [System.Text.Json.Serialization.JsonDerivedType(typeof(AzureBlobProvider), "azure-blob")]
    public abstract record ProviderConfig(string Name);
    
    public record AzureBlobProvider(
        string Name,
        string AccountName,
        string AccountKey,
        string ContainerName,
        string? BlobPrefix = null) : ProviderConfig(Name);
}
