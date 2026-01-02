using System.CommandLine;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace Max;

public partial class WatchCommand : Command
{
    private static readonly string ServiceLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "MaxBackup", "logs");
    private readonly Option<bool> _verboseOption;
    private readonly Option<int> _tailLinesOption;
    private readonly Option<bool> _userLogOption;

    public WatchCommand(Option<bool> verboseOption)
        : base("watch", "Watch live service logs")
    {
        _verboseOption = verboseOption;
        _tailLinesOption = new Option<int>("--tail")
        {
            Description = "Number of lines to show from end of log before watching",
            DefaultValueFactory = _ => 20
        };
        _userLogOption = new Option<bool>("--user")
        {
            Description = "Watch user backup log instead of service log"
        };

        Options.Add(_tailLinesOption);
        Options.Add(_userLogOption);

        this.SetAction(async (parseResult) =>
        {
            var verbose = parseResult.GetValue(_verboseOption);
            var tailLines = parseResult.GetValue(_tailLinesOption);
            var watchUserLog = parseResult.GetValue(_userLogOption);

            // Use Console.CancelKeyPress to handle Ctrl+C
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            return await ExecuteAsync(verbose, tailLines, watchUserLog, cts.Token);
        });
    }

    private async Task<int> ExecuteAsync(bool verbose, int tailLines, bool watchUserLog, CancellationToken cancellationToken)
    {
        try
        {
            string logPath;
            string logType;

            if (watchUserLog)
            {
                // User log path: ~\.max\logs\backup*.log (mandatory rolling log files)
                var userLogDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".max", "logs");
                logPath = GetLatestLogFile(userLogDir, "backup");
                logType = "user backup";
            }
            else
            {
                // Find the most recent service log file
                logPath = GetLatestLogFile(ServiceLogPath, "service");
                logType = "service";
            }

            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
            {
                AnsiConsole.MarkupLine($"[red]✗[/] No {logType} log file found.");
                if (!watchUserLog)
                {
                    AnsiConsole.MarkupLine($"[dim]Expected location: {ServiceLogPath}\\service*.log[/]");
                    AnsiConsole.MarkupLine("[dim]Is the MaxBackup service installed and has it been run?[/]");
                }
                else
                {
                    var expectedDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".max", "logs");
                    AnsiConsole.MarkupLine($"[dim]Expected location: {expectedDir}\\backup*.log[/]");
                    AnsiConsole.MarkupLine("[dim]Have you registered with 'max register' and is the service running?[/]");
                }
                return 1;
            }

            AnsiConsole.MarkupLine($"[dim]Watching {logType} log:[/] [blue]{logPath}[/]");
            AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop watching[/]");
            AnsiConsole.WriteLine();

            await TailLogFileAsync(logPath, tailLines, cancellationToken);

            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Stopped watching logs.[/]");
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

    private static string GetLatestLogFile(string logDirectory, string prefix)
    {
        if (!Directory.Exists(logDirectory))
            return string.Empty;

        // Find the most recent log file matching pattern service*.log
        var logFiles = Directory.GetFiles(logDirectory, $"{prefix}*.log")
            .OrderByDescending(f => new FileInfo(f).LastWriteTime)
            .ToArray();

        return logFiles.Length > 0 ? logFiles[0] : string.Empty;
    }

    private static async Task TailLogFileAsync(string logPath, int tailLines, CancellationToken cancellationToken)
    {
        using var fileStream = new FileStream(
            logPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fileStream);

        // Read initial lines (tail)
        var initialLines = await ReadLastLinesAsync(fileStream, reader, tailLines);
        foreach (var line in initialLines)
        {
            WriteFormattedLogLine(line);
        }

        // Now watch for new content
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line != null)
            {
                WriteFormattedLogLine(line);
            }
            else
            {
                // No new content, wait a bit
                await Task.Delay(100, cancellationToken);

                // Check if file was rotated (size decreased)
                var currentLength = fileStream.Length;
                if (fileStream.Position > currentLength)
                {
                    // File was truncated/rotated, seek to beginning
                    fileStream.Seek(0, SeekOrigin.Begin);
                    reader.DiscardBufferedData();
                    AnsiConsole.MarkupLine("[dim]--- Log file rotated ---[/]");
                }
            }
        }
    }

    private static async Task<List<string>> ReadLastLinesAsync(FileStream fileStream, StreamReader reader, int lineCount)
    {
        var lines = new List<string>();

        // Simple approach: read all and take last N
        // For large files, we could use a more efficient reverse read
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lines.Add(line);
        }

        // Return only the last N lines
        if (lines.Count <= lineCount)
            return lines;

        return lines.Skip(lines.Count - lineCount).ToList();
    }

    private static void WriteFormattedLogLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        // Serilog default format: [Timestamp Level] Message
        // Example: [2024-01-15 10:30:45 INF] Starting worker
        var match = LogLineRegex().Match(line);

        if (match.Success)
        {
            var timestamp = match.Groups["timestamp"].Value;
            var level = match.Groups["level"].Value.ToUpperInvariant();
            var message = match.Groups["message"].Value;

            var (levelColor, levelText) = level switch
            {
                "VRB" or "VERBOSE" => ("dim", "VRB"),
                "DBG" or "DEBUG" => ("grey", "DBG"),
                "INF" or "INFORMATION" => ("blue", "INF"),
                "WRN" or "WARNING" => ("yellow", "WRN"),
                "ERR" or "ERROR" => ("red", "ERR"),
                "FTL" or "FATAL" => ("red bold", "FTL"),
                _ => ("white", level)
            };

            // Escape any Spectre markup characters in the message
            message = Markup.Escape(message);
            timestamp = Markup.Escape(timestamp);

            AnsiConsole.MarkupLine($"[dim]{timestamp}[/] [{levelColor}]{levelText}[/] {message}");
        }
        else
        {
            // Fallback: just print the line, escaping any markup
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(line)}[/]");
        }
    }

    // Regex to parse Serilog log lines
    // Matches patterns like: [2024-01-15 10:30:45.123 INF] Message
    // Or: 2024-01-15 10:30:45.123 [INF] Message
    [GeneratedRegex(@"^\[?(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}(?:\.\d+)?)\s+(?<level>\w{3})\]?\s*(?<message>.*)$")]
    private static partial Regex LogLineRegex();
}
