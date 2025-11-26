using System.CommandLine;
using Spectre.Console;

namespace Max;

public class UnregisterCommand : Command
{
    private readonly Option<bool> _verboseOption;

    public UnregisterCommand(Option<bool> verboseOption)
        : base("unregister", "Unregister the current user from the MaxBackup service")
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
            AnsiConsole.MarkupLine("[dim]Connecting to MaxBackup service...[/]");

            var responses = await PipeClient.SendRequestAsync("Unregister");

            bool hasError = false;

            foreach (var response in responses)
            {
                switch (response.Status.ToLowerInvariant())
                {
                    case "success":
                        AnsiConsole.MarkupLine($"[green]✓[/] {response.Message}");
                        break;
                    case "error":
                        AnsiConsole.MarkupLine($"[red]✗[/] {response.Message}");
                        hasError = true;
                        break;
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
            }

            return hasError ? 1 : 0;
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
