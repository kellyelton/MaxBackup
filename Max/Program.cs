using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.CommandLine;
using Max;
using System.CommandLine.Help;
using System.CommandLine.IO;

var delayOption = new Option<int>("--delay");
var messageOption = new Option<string>("--message");

var rootCommand = new RootCommand("Middleware example");
rootCommand.Add(delayOption);
rootCommand.Add(messageOption);
rootCommand.SetHandler(context => {
    var con = new HelpContext(context.HelpBuilder, rootCommand, context.Console.Out.CreateTextWriter());
    context.HelpBuilder.Write(con);
});

rootCommand.Add(new ServiceCommand());

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
