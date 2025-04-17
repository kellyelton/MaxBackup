using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Text.Json;
using Spectre.Console;

namespace max
{
    public sealed class JobsCommand : Command
    {
        public JobsCommand(Option<string> config_path, Option<bool> verbose)
            : base("jobs", "Manage backup jobs")
        {
            AddCommand(new ListCommand(config_path, verbose));
            AddCommand(new CreateCommand(config_path, verbose));
            AddCommand(new ModifyCommand(config_path, verbose));
            AddCommand(new DeleteCommand(config_path, verbose));
        }

        private static string GetConfigPath(InvocationContext context, Option<string> config_path)
        {
            var config_file_path = context.BindingContext.ParseResult.GetValueForOption(config_path);
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

        public class ListCommand : Command, ICommandHandler
        {
            private readonly Option<string> _config_path;
            private readonly Option<bool> _verbose;
            public ListCommand(Option<string> config_path, Option<bool> verbose)
                : base("list", "List the backup jobs")
            {
                _config_path = config_path;
                _verbose = verbose;
                Handler = this;
            }
            public int Invoke(InvocationContext context)
            {
                var config_file_path = GetConfigPath(context, _config_path);
                var config = LoadConfig(config_file_path);
                if (config?.Backup?.Jobs == null || config.Backup.Jobs.Length == 0)
                {
                    context.Console.Error.WriteLine($"No jobs found in config file at {config_file_path}");
                    return 1;
                }
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(Color.Red)
                    .AddColumn(new TableColumn("Name").Centered())
                    .AddColumn(new TableColumn("Source").Centered())
                    .AddColumn(new TableColumn("Destination").Centered());
                foreach (var job in config.Backup.Jobs)
                    table.AddRow(job.Name, job.Source, job.Destination);
                AnsiConsole.Write(table);
                return 0;
            }
            public Task<int> InvokeAsync(InvocationContext context) => Task.FromResult(Invoke(context));
        }

        public class CreateCommand : Command, ICommandHandler
        {
            private readonly Option<string> _config_path;
            private readonly Option<bool> _verbose;
            private readonly Argument<string> _nameArg = new("name", "Job name");
            private readonly Argument<string> _sourceArg = new("source", "Source path");
            private readonly Argument<string> _destArg = new("destination", "Destination path");
            private readonly Option<string[]> _includeArg = new("--include", () => new string[] { "**" }, "Include patterns");
            private readonly Option<string[]> _excludeArg = new("--exclude", () => new string[0], "Exclude patterns");
            public CreateCommand(Option<string> config_path, Option<bool> verbose)
                : base("create", "Create a new backup job")
            {
                _config_path = config_path;
                _verbose = verbose;
                AddArgument(_nameArg);
                AddArgument(_sourceArg);
                AddArgument(_destArg);
                AddOption(_includeArg);
                AddOption(_excludeArg);
                Handler = this;
            }
            public int Invoke(InvocationContext context)
            {
                var name = context.ParseResult.GetValueForArgument(_nameArg);
                var source = context.ParseResult.GetValueForArgument(_sourceArg);
                var destination = context.ParseResult.GetValueForArgument(_destArg);
                var include = context.ParseResult.GetValueForOption(_includeArg) ?? Array.Empty<string>();
                var exclude = context.ParseResult.GetValueForOption(_excludeArg) ?? Array.Empty<string>();
                var config_file_path = GetConfigPath(context, _config_path);
                var config = LoadConfig(config_file_path) ?? new Config(new Backup(Array.Empty<Job>()));
                if (config.Backup.Jobs.Any(j => j.Name == name))
                {
                    context.Console.Error.WriteLine($"Job '{name}' already exists.");
                    return 1;
                }
                var jobs = config.Backup.Jobs.ToList();
                jobs.Add(new Job(name, source, destination, include, exclude));
                config = config with { Backup = config.Backup with { Jobs = jobs.ToArray() } };
                SaveConfig(config_file_path, config);
                context.Console.Out.WriteLine($"Job '{name}' created.");
                return 0;
            }
            public Task<int> InvokeAsync(InvocationContext context) => Task.FromResult(Invoke(context));
        }

        public class ModifyCommand : Command, ICommandHandler
        {
            private readonly Option<string> _config_path;
            private readonly Option<bool> _verbose;
            private readonly Argument<string> _nameArg = new("name", "Job name");
            private readonly Option<string?> _sourceOpt = new("--source", description: "New source path");
            private readonly Option<string?> _destOpt = new("--destination", description: "New destination path");
            private readonly Option<string[]> _includeOpt = new("--include", () => new string[0], "New include patterns");
            private readonly Option<string[]> _excludeOpt = new("--exclude", () => new string[0], "New exclude patterns");
            public ModifyCommand(Option<string> config_path, Option<bool> verbose)
                : base("modify", "Modify an existing backup job")
            {
                _config_path = config_path;
                _verbose = verbose;
                AddArgument(_nameArg);
                AddOption(_sourceOpt);
                AddOption(_destOpt);
                AddOption(_includeOpt);
                AddOption(_excludeOpt);
                Handler = this;
            }
            public int Invoke(InvocationContext context)
            {
                var name = context.ParseResult.GetValueForArgument(_nameArg);
                var source = context.ParseResult.GetValueForOption(_sourceOpt);
                var destination = context.ParseResult.GetValueForOption(_destOpt);
                var include = context.ParseResult.GetValueForOption(_includeOpt) ?? Array.Empty<string>();
                var exclude = context.ParseResult.GetValueForOption(_excludeOpt) ?? Array.Empty<string>();
                var config_file_path = GetConfigPath(context, _config_path);
                var config = LoadConfig(config_file_path);
                if (config?.Backup?.Jobs == null)
                {
                    context.Console.Error.WriteLine($"No jobs found in config file at {config_file_path}");
                    return 1;
                }
                var jobs = config.Backup.Jobs.ToList();
                var idx = jobs.FindIndex(j => j.Name == name);
                if (idx == -1)
                {
                    context.Console.Error.WriteLine($"Job '{name}' not found.");
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
                context.Console.Out.WriteLine($"Job '{name}' modified.");
                return 0;
            }
            public Task<int> InvokeAsync(InvocationContext context) => Task.FromResult(Invoke(context));
        }

        public class DeleteCommand : Command, ICommandHandler
        {
            private readonly Option<string> _config_path;
            private readonly Option<bool> _verbose;
            private readonly Argument<string> _nameArg = new("name", "Job name");
            public DeleteCommand(Option<string> config_path, Option<bool> verbose)
                : base("delete", "Delete a backup job")
            {
                _config_path = config_path;
                _verbose = verbose;
                AddArgument(_nameArg);
                Handler = this;
            }
            public int Invoke(InvocationContext context)
            {
                var name = context.ParseResult.GetValueForArgument(_nameArg);
                var config_file_path = GetConfigPath(context, _config_path);
                var config = LoadConfig(config_file_path);
                if (config?.Backup?.Jobs == null)
                {
                    context.Console.Error.WriteLine($"No jobs found in config file at {config_file_path}");
                    return 1;
                }
                var jobs = config.Backup.Jobs.ToList();
                var removed = jobs.RemoveAll(j => j.Name == name);
                if (removed == 0)
                {
                    context.Console.Error.WriteLine($"Job '{name}' not found.");
                    return 1;
                }
                config = config with { Backup = config.Backup with { Jobs = jobs.ToArray() } };
                SaveConfig(config_file_path, config);
                context.Console.Out.WriteLine($"Job '{name}' deleted.");
                return 0;
            }
            public Task<int> InvokeAsync(InvocationContext context) => Task.FromResult(Invoke(context));
        }
    }

    public record Config(Backup Backup);
    public record Backup(Job[] Jobs);
    public record Job(string Name, string Source, string Destination, string[] Include, string[] Exclude);
}
