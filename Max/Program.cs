// max status
// shows the status of the service, the config file path, how many files were recently backed up, how many errors there have been recently, etc.
// max start
// starts the service
// max stop
// stops the service
// max restart
// restarts the service
// max config
// opens the config file in the default editor
// max config --editor code-insiders // opens the config file in the specified editor
// max config --editor code-insiders --wait // opens the config file in the specified editor and waits for the editor to close before continuing
// max tail // tails the log file
// max jobs // lists the jobs
// max job create "My Job" "C:\Users\Me\Documents" "D:\Backups\Documents"
// max job disable "My Job"
// max job enable "My Job"
// max job remove "My Job"
// max job run "My Job"
// max job run "My Job" --wait
// max job run "My Job" --wait --verbose
// max job run "My Job" --wait --verbose --dry-run

using System.CommandLine;
using Max;
using max;

// Max backup app for windows. This is the cli app that controls/configures the service.

var default_config_path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "maxbackupconfig.json");

var verboseOption = new Option<bool>("--verbose") { Recursive = true };
var configPathOption = new Option<string>("--config-path")
{
    Description = "The path to the config file. Defaults to %USERPROFILE%\\maxbackupconfig.json",
    DefaultValueFactory = _ => default_config_path,
    Recursive = true
};

var jobs_command = new JobsCommand(configPathOption, verboseOption);

var rootCommand = new RootCommand("Max backup app for windows. This is the cli app that controls/configures the service.");
rootCommand.Options.Add(verboseOption);
rootCommand.Options.Add(configPathOption);

// When root command is invoked without subcommand, the default help will be shown
// (this is automatic in 2.0.0)

rootCommand.Subcommands.Add(jobs_command);
rootCommand.Subcommands.Add(new ServiceCommand());

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
