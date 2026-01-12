using System.CommandLine;
using System.Text.Json;
using Azure.Storage.Blobs;
using MaxBackup.Shared;
using Spectre.Console;

namespace max;

public sealed class ProviderCommand : Command
{
    public ProviderCommand(Option<string> configPath, Option<bool> verbose)
        : base("provider", "Manage storage providers for cloud backups")
    {
        Subcommands.Add(new AddCommand(configPath, verbose));
        Subcommands.Add(new ListCommand(configPath, verbose));
        Subcommands.Add(new RemoveCommand(configPath, verbose));
        Subcommands.Add(new TestCommand(configPath, verbose));
    }

    private static string GetConfigPath(ParseResult parseResult, Option<string> configPath)
    {
        var path = parseResult.GetValue(configPath);
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "maxbackupconfig.json");
        }
        return path;
    }

    private static Config? LoadConfig(string configFilePath)
    {
        if (!File.Exists(configFilePath)) return null;
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<Config>(File.ReadAllText(configFilePath), jsonOptions);
    }

    private static void SaveConfig(string configFilePath, Config config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configFilePath, json);
    }

    public class AddCommand : Command
    {
        private readonly Option<string?> _typeOpt;
        private readonly Option<string?> _nameOpt;
        private readonly Option<string?> _accountNameOpt;
        private readonly Option<string?> _accountKeyOpt;
        private readonly Option<string?> _containerOpt;
        private readonly Option<bool> _skipTestOpt;
        private readonly Option<string?> _blobPrefixOpt;

        public AddCommand(Option<string> configPath, Option<bool> verbose)
            : base("add", "Add a new storage provider")
        {
            _typeOpt = new Option<string?>("--type") { Description = "Provider type (e.g., azure-blob)" };
            _nameOpt = new Option<string?>("--name") { Description = "Provider name (lowercase, alphanumeric with dashes/underscores)" };
            _accountNameOpt = new Option<string?>("--account-name") { Description = "Azure Storage Account name" };
            _accountKeyOpt = new Option<string?>("--account-key") { Description = "Azure Storage Account access key" };
            _containerOpt = new Option<string?>("--container") { Description = "Azure Blob container name" };
            _blobPrefixOpt = new Option<string?>("--blob-prefix") { Description = "Optional blob prefix" };
            _skipTestOpt = new Option<bool>("--skip-test") { Description = "Skip connection verification" };

            Options.Add(_typeOpt);
            Options.Add(_nameOpt);
            Options.Add(_accountNameOpt);
            Options.Add(_accountKeyOpt);
            Options.Add(_containerOpt);
            Options.Add(_blobPrefixOpt);
            Options.Add(_skipTestOpt);

            this.SetAction(async parseResult =>
            {
                var configFilePath = GetConfigPath(parseResult, configPath);
                var isVerbose = parseResult.GetValue(verbose);

                // Get values from command line (may be null for interactive mode)
                var type = parseResult.GetValue(_typeOpt);
                var name = parseResult.GetValue(_nameOpt);
                var accountName = parseResult.GetValue(_accountNameOpt);
                var accountKey = parseResult.GetValue(_accountKeyOpt);
                var container = parseResult.GetValue(_containerOpt);
                var blobPrefix = parseResult.GetValue(_blobPrefixOpt);

                // Load existing config
                var config = LoadConfig(configFilePath) ?? new Config(new Backup(Array.Empty<Job>()), null);

                // Determine if interactive mode is needed
                var isInteractive = string.IsNullOrEmpty(type) || string.IsNullOrEmpty(name);

                if (isInteractive)
                {
                    return await RunInteractiveAsync(config, configFilePath, name, accountName, accountKey, container, blobPrefix);
                }
                else
                {
                    var skipTest = parseResult.GetValue(_skipTestOpt);
                    return await RunNonInteractiveAsync(config, configFilePath, type!, name!, accountName, accountKey, container, blobPrefix, skipTest);
                }
            });
        }

        private async Task<int> RunInteractiveAsync(
            Config config,
            string configFilePath,
            string? name,
            string? accountName,
            string? accountKey,
            string? container,
            string? blobPrefix)
        {
            AnsiConsole.Write(new Rule("[bold blue]MaxBackup - Add Storage Provider[/]").RuleStyle("dim"));
            AnsiConsole.WriteLine();

            // Select provider type
            var providerType = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select provider type:")
                    .AddChoices("Azure Blob Storage", "AWS S3 (coming soon)")
                    .HighlightStyle(new Style(Color.Blue)));

            if (providerType.Contains("coming soon"))
            {
                AnsiConsole.MarkupLine("[yellow]This provider is not yet available.[/]");
                return 1;
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Panel(
                "[dim]To get your Azure credentials:[/]\n" +
                "1. Go to [link=https://portal.azure.com]https://portal.azure.com[/]\n" +
                "2. Navigate to: [bold]Storage accounts[/] > [bold][Your Account][/]\n" +
                "3. Click: [bold]Security + networking[/] > [bold]Access keys[/]\n" +
                "4. Copy the [bold]Storage account name[/] and one of the [bold]Keys[/]")
                .Header("[bold]Azure Setup Instructions[/]")
                .BorderColor(Color.Blue));
            AnsiConsole.WriteLine();

            // Get provider name
            name ??= AnsiConsole.Prompt(
                new TextPrompt<string>("Provider name:")
                    .PromptStyle(new Style(Color.Green))
                    .Validate(n =>
                    {
                        var error = MaxBackup.Shared.ProviderConfig.ValidateNameGetError(n);
                        if (error != null) return ValidationResult.Error(error);
                        if (config.Providers?.Any(p => p.Name == n) == true)
                            return ValidationResult.Error($"Provider '{n}' already exists");
                        return ValidationResult.Success();
                    }));

            // Get Azure credentials
            accountName ??= AnsiConsole.Prompt(
                new TextPrompt<string>("Storage account name:")
                    .PromptStyle(new Style(Color.Green))
                    .Validate(s => string.IsNullOrWhiteSpace(s)
                        ? ValidationResult.Error("Account name is required")
                        : ValidationResult.Success()));

            accountKey ??= AnsiConsole.Prompt(
                new TextPrompt<string>("Access key:")
                    .PromptStyle(new Style(Color.Green))
                    .Secret()
                    .Validate(s => string.IsNullOrWhiteSpace(s)
                        ? ValidationResult.Error("Access key is required")
                        : ValidationResult.Success()));

            container ??= AnsiConsole.Prompt(
                new TextPrompt<string>("Container name:")
                    .PromptStyle(new Style(Color.Green))
                    .DefaultValue("backups")
                    .Validate(s => string.IsNullOrWhiteSpace(s)
                        ? ValidationResult.Error("Container name is required")
                        : ValidationResult.Success()));

            blobPrefix ??= AnsiConsole.Prompt(
                new TextPrompt<string>("Blob prefix (optional):")
                    .PromptStyle(new Style(Color.Green))
                    .AllowEmpty()
                    .DefaultValue($"maxbackup/{Environment.MachineName.ToLowerInvariant()}"));

            AnsiConsole.WriteLine();

            // Verify connection
            var testResult = await VerifyConnectionAsync(accountName, accountKey, container);
            if (!testResult.Success)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Connection failed: {testResult.ErrorMessage}");
                return 1;
            }

            // Save provider with encrypted credentials
            var encryptedKey = CredentialProtection.Encrypt(accountKey);
            var providers = (config.Providers ?? Array.Empty<ProviderConfig>()).ToList();
            providers.Add(new AzureBlobProvider(
                name,
                accountName,
                encryptedKey,
                container,
                string.IsNullOrWhiteSpace(blobPrefix) ? null : blobPrefix));

            config = config with { Providers = providers.ToArray() };
            SaveConfig(configFilePath, config);

            AnsiConsole.MarkupLine($"[green]✓[/] Provider '[bold]{name}[/]' added successfully!");
            AnsiConsole.MarkupLine($"[dim]Config saved to: {configFilePath}[/]");

            return 0;
        }

        private async Task<int> RunNonInteractiveAsync(
            Config config,
            string configFilePath,
            string type,
            string name,
            string? accountName,
            string? accountKey,
            string? container,
            string? blobPrefix,
            bool skipTest)
        {
            // Validate provider name
            var nameError = MaxBackup.Shared.ProviderConfig.ValidateNameGetError(name);
            if (nameError != null)
            {
                Console.Error.WriteLine($"Error: {nameError}");
                return 1;
            }

            // Check for duplicate
            if (config.Providers?.Any(p => p.Name == name) == true)
            {
                Console.Error.WriteLine($"Error: Provider '{name}' already exists");
                return 1;
            }

            if (type != "azure-blob")
            {
                Console.Error.WriteLine($"Error: Unknown provider type '{type}'. Supported: azure-blob");
                return 1;
            }

            // Validate required Azure fields
            if (string.IsNullOrWhiteSpace(accountName))
            {
                Console.Error.WriteLine("Error: --account-name is required for azure-blob provider");
                return 1;
            }
            if (string.IsNullOrWhiteSpace(accountKey))
            {
                Console.Error.WriteLine("Error: --account-key is required for azure-blob provider");
                return 1;
            }
            if (string.IsNullOrWhiteSpace(container))
            {
                Console.Error.WriteLine("Error: --container is required for azure-blob provider");
                return 1;
            }

            // Verify connection
            if (!skipTest)
            {
                var testResult = await VerifyConnectionAsync(accountName, accountKey, container);
                if (!testResult.Success)
                {
                    Console.Error.WriteLine($"Error: Connection failed - {testResult.ErrorMessage}");
                    return 1;
                }
            }

            // Save provider with encrypted credentials
            var encryptedKey = CredentialProtection.Encrypt(accountKey);
            var providers = (config.Providers ?? Array.Empty<ProviderConfig>()).ToList();
            providers.Add(new AzureBlobProvider(
                name,
                accountName,
                encryptedKey,
                container,
                string.IsNullOrWhiteSpace(blobPrefix) ? null : blobPrefix));

            config = config with { Providers = providers.ToArray() };
            SaveConfig(configFilePath, config);

            Console.WriteLine($"Provider '{name}' added successfully.");
            return 0;
        }

        private static async Task<(bool Success, string? ErrorMessage)> VerifyConnectionAsync(
            string accountName,
            string accountKey,
            string containerName)
        {
            try
            {
                await AnsiConsole.Status()
                    .StartAsync("Verifying connection...", async ctx =>
                    {
                        var connectionString = $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={accountKey};EndpointSuffix=core.windows.net";
                        var containerClient = new BlobContainerClient(connectionString, containerName);

                        ctx.Status("Creating container if needed...");
                        var response = await containerClient.CreateIfNotExistsAsync();

                        if (response?.Value != null)
                        {
                            AnsiConsole.MarkupLine($"[green]✓[/] Container '[bold]{containerName}[/]' created");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[green]✓[/] Container '[bold]{containerName}[/]' exists");
                        }
                    });

                AnsiConsole.MarkupLine("[green]✓[/] Connection verified successfully");
                return (true, null);
            }
            catch (Azure.RequestFailedException ex)
            {
                return (false, GetFriendlyErrorMessage(ex));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static string GetFriendlyErrorMessage(Azure.RequestFailedException ex)
        {
            return ex.ErrorCode switch
            {
                "AuthenticationFailed" => "Authentication failed. Please verify your account name and access key.",
                "AccountIsDisabled" => "The storage account is disabled.",
                "AuthorizationFailure" => "Authorization failed. The access key may not have sufficient permissions.",
                "InvalidResourceName" => "Invalid container name. Container names must be 3-63 characters, lowercase letters, numbers, and hyphens only.",
                _ => $"Azure error: {ex.Message}"
            };
        }
    }

    public class ListCommand : Command
    {
        public ListCommand(Option<string> configPath, Option<bool> verbose)
            : base("list", "List configured storage providers")
        {
            this.SetAction(parseResult =>
            {
                var configFilePath = GetConfigPath(parseResult, configPath);
                var config = LoadConfig(configFilePath);

                if (config?.Providers == null || config.Providers.Length == 0)
                {
                    AnsiConsole.MarkupLine("[dim]No providers configured.[/]");
                    AnsiConsole.MarkupLine("[dim]Use 'max provider add' to add a provider.[/]");
                    return 0;
                }

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(Color.Blue)
                    .AddColumn(new TableColumn("Name").Centered())
                    .AddColumn(new TableColumn("Type").Centered())
                    .AddColumn(new TableColumn("Details").Centered());

                foreach (var provider in config.Providers)
                {
                    var details = provider switch
                    {
                        AzureBlobProvider azure => $"{azure.AccountName}/{azure.ContainerName}",
                        _ => "Unknown"
                    };

                    var type = provider switch
                    {
                        AzureBlobProvider => "azure-blob",
                        _ => "unknown"
                    };

                    table.AddRow(provider.Name, type, details);
                }

                AnsiConsole.Write(table);
                return 0;
            });
        }
    }

    public class RemoveCommand : Command
    {
        private readonly Argument<string> _nameArg = new("name") { Description = "Provider name to remove" };

        public RemoveCommand(Option<string> configPath, Option<bool> verbose)
            : base("remove", "Remove a storage provider")
        {
            Arguments.Add(_nameArg);

            this.SetAction(parseResult =>
            {
                var name = parseResult.GetValue(_nameArg)!;
                var configFilePath = GetConfigPath(parseResult, configPath);
                var config = LoadConfig(configFilePath);

                if (config?.Providers == null || config.Providers.Length == 0)
                {
                    Console.Error.WriteLine("No providers configured.");
                    return 1;
                }

                var providers = config.Providers.ToList();
                var removed = providers.RemoveAll(p => p.Name == name);

                if (removed == 0)
                {
                    Console.Error.WriteLine($"Provider '{name}' not found.");
                    return 1;
                }

                // Check if any jobs reference this provider
                var jobsUsingProvider = config.Backup.Jobs.Where(j => j.Provider == name).ToList();
                if (jobsUsingProvider.Count > 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning:[/] The following jobs use this provider:");
                    foreach (var job in jobsUsingProvider)
                    {
                        AnsiConsole.MarkupLine($"  • {job.Name}");
                    }

                    if (!AnsiConsole.Confirm("Remove provider anyway?", false))
                    {
                        AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
                        return 1;
                    }
                }

                config = config with { Providers = providers.ToArray() };
                SaveConfig(configFilePath, config);

                AnsiConsole.MarkupLine($"[green]✓[/] Provider '[bold]{name}[/]' removed.");
                return 0;
            });
        }
    }

    public class TestCommand : Command
    {
        private readonly Argument<string> _nameArg = new("name") { Description = "Provider name to test" };

        public TestCommand(Option<string> configPath, Option<bool> verbose)
            : base("test", "Test connectivity to a storage provider")
        {
            Arguments.Add(_nameArg);

            this.SetAction(async parseResult =>
            {
                var name = parseResult.GetValue(_nameArg)!;
                var configFilePath = GetConfigPath(parseResult, configPath);
                var config = LoadConfig(configFilePath);

                if (config?.Providers == null || config.Providers.Length == 0)
                {
                    Console.Error.WriteLine("No providers configured.");
                    return 1;
                }

                var provider = config.Providers.FirstOrDefault(p => p.Name == name);
                if (provider == null)
                {
                    Console.Error.WriteLine($"Provider '{name}' not found.");
                    return 1;
                }

                if (provider is not AzureBlobProvider azure)
                {
                    Console.Error.WriteLine($"Unknown provider type for '{name}'.");
                    return 1;
                }

                AnsiConsole.MarkupLine($"Testing provider '[bold]{name}[/]'...");

                try
                {
                    // Decrypt account key if encrypted
                    var decryptedKey = CredentialProtection.TryDecrypt(azure.AccountKey, out var decryptError);
                    if (decryptedKey == null)
                    {
                        AnsiConsole.MarkupLine($"[red]✗[/] {decryptError}");
                        return 1;
                    }

                    var connectionString = $"DefaultEndpointsProtocol=https;AccountName={azure.AccountName};AccountKey={decryptedKey};EndpointSuffix=core.windows.net";
                    var containerClient = new BlobContainerClient(connectionString, azure.ContainerName);

                    await AnsiConsole.Status()
                        .StartAsync("Connecting...", async ctx =>
                        {
                            // Test listing blobs
                            ctx.Status("Listing container contents...");
                            var blobCount = 0;
                            await foreach (var blob in containerClient.GetBlobsAsync().Take(10))
                            {
                                blobCount++;
                            }

                            AnsiConsole.MarkupLine($"[green]✓[/] Connection successful");
                            AnsiConsole.MarkupLine($"[dim]  Account: {azure.AccountName}[/]");
                            AnsiConsole.MarkupLine($"[dim]  Container: {azure.ContainerName}[/]");
                            if (!string.IsNullOrEmpty(azure.BlobPrefix))
                            {
                                AnsiConsole.MarkupLine($"[dim]  Prefix: {azure.BlobPrefix}[/]");
                            }
                        });

                    return 0;
                }
                catch (Azure.RequestFailedException ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Connection failed: {ex.Message}");
                    return 1;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Error: {ex.Message}");
                    return 1;
                }
            });
        }
    }
}
