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

using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.CommandLine.Invocation;
using System.CommandLine;
using Max;
using System.CommandLine.Help;
using System.CommandLine.IO;
using max;

// Max backup app for windows. This is the cli app that controls/configures the service.

var default_config_path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "maxbackupconfig.json");

var verboseOption = new Option<bool>("--verbose");
var configPathOption = new Option<string>(
    "--config-path",
    () => default_config_path,
    "The path to the config file. Defaults to %USERPROFILE%\\maxbackupconfig.json"
);

var jobs_command = new JobsCommand(configPathOption, verboseOption);

var rootCommand = new RootCommand("Max backup app for windows. This is the cli app that controls/configures the service.");
rootCommand.AddGlobalOption(verboseOption);
rootCommand.AddGlobalOption(configPathOption);
rootCommand.SetHandler(context => {
    var con = new HelpContext(context.HelpBuilder, rootCommand, context.Console.Out.CreateTextWriter());
    context.HelpBuilder.Write(con);
});

rootCommand.AddCommand(jobs_command);
rootCommand.AddCommand(new ServiceCommand());

var commandLineBuilder = new CommandLineBuilder(rootCommand);

//commandLineBuilder.AddMiddleware(async (context, next) => {
//    if (context.ParseResult.Directives.Contains("just-say-hi")) {
//        context.Console.WriteLine("Hi!");
//    } else {
//        await next(context);
//    }
//});

commandLineBuilder.UseDefaults();

var parser = commandLineBuilder.Build();

var result = await parser.InvokeAsync(args);

return result;
