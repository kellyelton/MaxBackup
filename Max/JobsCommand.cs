using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Text.Json;
using Spectre.Console;

namespace max
{
    public sealed class JobsCommand : Command, ICommandHandler
    {
        private readonly Option<string> _config_path;

        public JobsCommand(Option<string> config_path)
            : base("jobs", "List the backup jobs")
        {
            _config_path = config_path ?? throw new ArgumentNullException(nameof(config_path));
        }

        public int Invoke(InvocationContext context) {
            throw new NotImplementedException();
        }

        public Task<int> InvokeAsync(InvocationContext context) {
            var config_file_path = context.BindingContext.ParseResult.GetValueForOption(_config_path);

            if (string.IsNullOrWhiteSpace(config_file_path)) {
                // TODO: Shouldn't duplicate this line
                config_file_path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "maxbackupconfig.json");
            }

            if (!File.Exists(config_file_path)) {
                context.Console.Error.WriteLine($"Config file not found at {config_file_path}");

                return Task.FromResult(1);
            }

            // jobs is in the $.Backup.Jobs array

            var json_options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = false
            };

            var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(config_file_path), json_options);

            if (config?.Backup?.Jobs == null) {
                context.Console.Error.WriteLine($"No jobs found in config file at {config_file_path}");

                return Task.FromResult(1);
            }

            if (config.Backup.Jobs.Length == 0) {
                context.Console.Error.WriteLine($"No jobs found in config file at {config_file_path}");

                return Task.FromResult(1);
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Red)
                .AddColumn(new TableColumn("Name").Centered())
                .AddColumn(new TableColumn("Source").Centered())
                .AddColumn(new TableColumn("Destination").Centered())
            ;

            foreach (var job in config.Backup.Jobs) {
                  table.AddRow(job.Name, job.Source, job.Destination);
            }

            AnsiConsole.Write(table);

            return Task.FromResult(0);

        }
    }

    public record Config(Backup Backup);
    public record Backup(Job[] Jobs);
    public record Job(string Name, string Source, string Destination, string[] Include, string[] Exclude);
}
