using System.CommandLine;
using Spectre.Console;

namespace Max;

public class StatusCommand : Command
{
    private readonly Option<bool> _verboseOption;

    public StatusCommand(Option<bool> verboseOption)
        : base("status", "Get the current registration status and backup information")
    {
        _verboseOption = verboseOption;

        this.SetAction(async (parseResult) =>
        {
            var verbose = parseResult.GetValue(_verboseOption);
            return await ExecuteAsync(verbose);
        });
    }

    private async Task<int> ExecuteAsync(bool verbose)
    {
        try
        {
            if (verbose)
            {
                AnsiConsole.MarkupLine("[dim]Connecting to MaxBackup service...[/]");
            }

            var responses = await PipeClient.SendRequestAsync("Status");

            foreach (var response in responses)
            {
                switch (response.Status.ToLowerInvariant())
                {
                    case "success":
                        // Parse the message and display in a nice format
                        var lines = response.Message.Split('\n');
                        var table = new Table();
                        table.Border = TableBorder.Rounded;
                        table.BorderColor(Color.Green);
                        table.HideHeaders();
                        table.AddColumn("Property");
                        table.AddColumn("Value");

                        foreach (var line in lines)
                        {
                            var parts = line.Split(':', 2);
                            if (parts.Length == 2)
                            {
                                table.AddRow($"[bold]{parts[0].Trim()}[/]", parts[1].Trim());
                            }
                        }

                        AnsiConsole.Write(table);
                        break;

                    case "info":
                        // Not registered
                        AnsiConsole.MarkupLine($"[yellow]{response.Message}[/]");
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine("[dim]To register, run:[/] [blue]max register[/]");
                        break;

                    case "error":
                        AnsiConsole.MarkupLine($"[red]✗[/] {response.Message}");
                        return 1;

                    case "verbose":
                        if (verbose)
                        {
                            AnsiConsole.MarkupLine($"[dim]  {response.Message}[/]");
                        }
                        break;
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
}
