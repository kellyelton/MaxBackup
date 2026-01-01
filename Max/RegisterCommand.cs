using System.CommandLine;
using Spectre.Console;
using MaxBackup.Shared;

namespace Max;

public class RegisterCommand : Command
{
    private readonly Option<string?> _configPathOption;
    private readonly Option<bool> _verboseOption;

    public RegisterCommand(Option<bool> verboseOption)
        : base("register", "Register the current user with the MaxBackup service")
    {
        _verboseOption = verboseOption;
        _configPathOption = new Option<string?>("--config");
        _configPathOption.Description = "Path to config file (default: ~\\maxbackupconfig.json)";

        Add(_configPathOption);

        this.SetAction(async (parseResult) =>
        {
            var configPath = parseResult.GetValue(_configPathOption);
            var verbose = parseResult.GetValue(_verboseOption);

            return await ExecuteAsync(configPath, verbose);
        });
    }

    private async Task<int> ExecuteAsync(string? configPath, bool verbose)
    {
        try
        {
            // Determine config path
            if (string.IsNullOrWhiteSpace(configPath))
            {
                configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "maxbackupconfig.json");
            }
            else
            {
                // Expand ~ if present
                if (configPath.StartsWith("~"))
                {
                    var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    // Remove the tilde
                    var remainder = configPath.Substring(1);
                    // Trim any leading path separators
                    remainder = remainder.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    configPath = string.IsNullOrEmpty(remainder) ? homeDir : Path.Combine(homeDir, remainder);
                }
                configPath = Path.GetFullPath(configPath);
            }

            AnsiConsole.MarkupLine($"[dim]Config path:[/] {configPath}");

            // Check if config exists, if not copy default
            if (!File.Exists(configPath))
            {
                AnsiConsole.MarkupLine("[yellow]Config file not found, creating default config...[/]");

                // Find Default_maxbackupconfig.json (should be next to the exe)
                var exeDir = AppContext.BaseDirectory;
                var defaultConfigPath = Path.Combine(exeDir, "Default_maxbackupconfig.json");

                if (File.Exists(defaultConfigPath))
                {
                    var configDir = Path.GetDirectoryName(configPath);
                    if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }

                    File.Copy(defaultConfigPath, configPath);
                    AnsiConsole.MarkupLine($"[green]✓[/] Created default config at {configPath}");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Could not find Default_maxbackupconfig.json at {defaultConfigPath}");
                    AnsiConsole.MarkupLine("[yellow]Please create a config file manually or reinstall MaxBackup.[/]");
                    return 1;
                }
            }

            // Validate config
            AnsiConsole.MarkupLine("[dim]Validating configuration...[/]");
            var validationErrors = ValidateConfig(configPath);

            if (validationErrors.Count > 0)
            {
                AnsiConsole.MarkupLine("[red]✗ Configuration validation failed:[/]");
                AnsiConsole.WriteLine();

                var table = new Table();
                table.Border = TableBorder.Rounded;
                table.BorderColor(Color.Red);
                table.AddColumn("Job");
                table.AddColumn("Field");
                table.AddColumn("Error");

                foreach (var error in validationErrors)
                {
                    table.AddRow(
                        error.Job ?? "-",
                        error.Field,
                        error.Error);
                }

                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Fix the errors above and try again.[/]");
                return 1;
            }

            if (verbose)
            {
                AnsiConsole.MarkupLine("[green]✓[/] [dim]Configuration is valid[/]");
            }

            // Send registration request
            AnsiConsole.MarkupLine("[dim]Connecting to MaxBackup service...[/]");

            var responses = await PipeClient.SendRequestAsync("Register", configPath);

            foreach (var response in responses)
            {
                switch (response.Status.ToLowerInvariant())
                {
                    case "success":
                        AnsiConsole.MarkupLine($"[green]✓[/] {response.Message}");
                        break;
                    case "error":
                        AnsiConsole.MarkupLine($"[red]✗[/] {response.Message}");
                        return 1;
                    case "info":
                        AnsiConsole.MarkupLine($"[blue]ℹ[/] {response.Message}");
                        break;
                    case "verbose":
                        if (verbose)
                        {
                            AnsiConsole.MarkupLine($"[dim]  {response.Message}[/]");
                        }
                        break;
                }

                // Display validation errors if present
                if (response.ValidationErrors != null && response.ValidationErrors.Count > 0)
                {
                    var table = new Table();
                    table.Border = TableBorder.Rounded;
                    table.BorderColor(Color.Red);
                    table.AddColumn("Job");
                    table.AddColumn("Field");
                    table.AddColumn("Error");

                    foreach (var error in response.ValidationErrors)
                    {
                        table.AddRow(
                            error.Job ?? "-",
                            error.Field,
                            error.Error);
                    }

                    AnsiConsole.Write(table);
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message}");
            if (verbose)
            {
                AnsiConsole.WriteException(ex);
            }
            return 1;
        }
    }

    private List<ValidationError> ValidateConfig(string configPath)
    {
        // Note: This is duplicated from the service ConfigValidator, but we want CLI
        // to be self-contained and not reference the service assembly
        var errors = new List<ValidationError>();

        if (!File.Exists(configPath))
        {
            errors.Add(new ValidationError
            {
                Field = "ConfigFile",
                Error = $"Config file not found at {configPath}"
            });
            return errors;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var doc = System.Text.Json.JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("Backup", out var backupSection))
            {
                errors.Add(new ValidationError { Field = "Backup", Error = "Config must have a 'Backup' section" });
                return errors;
            }

            if (!backupSection.TryGetProperty("Jobs", out var jobsElement))
            {
                errors.Add(new ValidationError { Field = "Backup.Jobs", Error = "'Backup' section must have a 'Jobs' array" });
                return errors;
            }

            if (jobsElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                errors.Add(new ValidationError { Field = "Backup.Jobs", Error = "'Jobs' must be an array" });
                return errors;
            }

            var jobs = jobsElement.EnumerateArray().ToList();
            if (jobs.Count == 0)
            {
                errors.Add(new ValidationError { Field = "Backup.Jobs", Error = "'Jobs' array cannot be empty" });
                return errors;
            }

            int jobIndex = 0;
            foreach (var job in jobs)
            {
                string? jobName = null;

                if (!job.TryGetProperty("Name", out var nameElement) || string.IsNullOrWhiteSpace(nameElement.GetString()))
                {
                    errors.Add(new ValidationError { Job = $"Job[{jobIndex}]", Field = "Name", Error = "Required" });
                }
                else
                {
                    jobName = nameElement.GetString();
                }

                var prefix = jobName ?? $"Job[{jobIndex}]";

                if (!job.TryGetProperty("Source", out var sourceElement) || string.IsNullOrWhiteSpace(sourceElement.GetString()))
                    errors.Add(new ValidationError { Job = prefix, Field = "Source", Error = "Required" });

                if (!job.TryGetProperty("Destination", out var destElement) || string.IsNullOrWhiteSpace(destElement.GetString()))
                    errors.Add(new ValidationError { Job = prefix, Field = "Destination", Error = "Required" });

                if (!job.TryGetProperty("Include", out var includeElement) || includeElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                    errors.Add(new ValidationError { Job = prefix, Field = "Include", Error = "Required (array)" });

                if (!job.TryGetProperty("Exclude", out var excludeElement) || excludeElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                    errors.Add(new ValidationError { Job = prefix, Field = "Exclude", Error = "Required (array)" });

                jobIndex++;
            }
        }
        catch (System.Text.Json.JsonException ex)
        {
            errors.Add(new ValidationError { Field = "JSON", Error = $"Invalid JSON: {ex.Message}" });
        }
        catch (Exception ex)
        {
            errors.Add(new ValidationError { Field = "ConfigFile", Error = $"Error: {ex.Message}" });
        }

        return errors;
    }
}
