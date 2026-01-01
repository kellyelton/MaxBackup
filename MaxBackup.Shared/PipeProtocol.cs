using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace MaxBackup.Shared;

/// <summary>
/// Shared pipe communication protocol for MaxBackup service and CLI.
/// Uses length-prefixed JSON messages.
/// </summary>
public static class PipeProtocol
{
    public const string PipeName = "MaxBackupPipe";
    public const int MaxMessageSize = 4096 * 2; // 8KB
    public const int DefaultTimeoutSeconds = 30;

    /// <summary>
    /// Reads a length-prefixed JSON message from a pipe stream.
    /// </summary>
    public static async Task<T?> ReadMessageAsync<T>(
        Stream stream,
        int timeoutSeconds = DefaultTimeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // Read message length (4 bytes)
            var lengthBuffer = new byte[4];
            var bytesRead = await ReadExactlyAsync(stream, lengthBuffer, linkedCts.Token);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Connection closed while reading message length");
            }

            var length = BitConverter.ToInt32(lengthBuffer, 0);

            if (length <= 0 || length > MaxMessageSize)
            {
                throw new InvalidOperationException($"Invalid message length: {length}");
            }

            // Read message body
            var messageBuffer = new byte[length];
            bytesRead = await ReadExactlyAsync(stream, messageBuffer, linkedCts.Token);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Connection closed while reading message body");
            }

            var json = Encoding.UTF8.GetString(messageBuffer);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            throw new TimeoutException($"Read operation timed out after {timeoutSeconds} seconds");
        }
    }

    /// <summary>
    /// Writes a length-prefixed JSON message to a pipe stream.
    /// </summary>
    public static async Task WriteMessageAsync<T>(
        Stream stream,
        T message,
        int timeoutSeconds = DefaultTimeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var json = JsonSerializer.Serialize(message, JsonOptions);
            var messageBytes = Encoding.UTF8.GetBytes(json);

            if (messageBytes.Length > MaxMessageSize)
            {
                throw new InvalidOperationException($"Message too large: {messageBytes.Length} bytes (max: {MaxMessageSize})");
            }

            var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

            await stream.WriteAsync(lengthBytes, linkedCts.Token);
            await stream.WriteAsync(messageBytes, linkedCts.Token);
            await stream.FlushAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            throw new TimeoutException($"Write operation timed out after {timeoutSeconds} seconds");
        }
    }

    /// <summary>
    /// Reads exactly the specified number of bytes, handling partial reads.
    /// Returns 0 if the stream is closed before any bytes are read.
    /// </summary>
    private static async Task<int> ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (read == 0)
            {
                // Stream closed
                return totalRead == 0 ? 0 : throw new EndOfStreamException("Stream closed unexpectedly");
            }
            totalRead += read;
        }
        return totalRead;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>
/// Request sent from CLI client to service.
/// </summary>
public class PipeRequest
{
    public required string Action { get; set; } // Register, Unregister, Status
    public required string Sid { get; set; }
    public string? ConfigPath { get; set; }
}

/// <summary>
/// Response sent from service to CLI client.
/// </summary>
public class PipeResponse
{
    public required string Status { get; set; } // Info, Success, Error, Verbose, End
    public required string Message { get; set; }
    public List<ValidationError>? ValidationErrors { get; set; }

    /// <summary>
    /// Indicates this is the final response and the client should not expect more messages.
    /// </summary>
    public bool IsFinal { get; set; }
}

/// <summary>
/// Validation error details.
/// </summary>
public class ValidationError
{
    public string? Job { get; set; }
    public required string Field { get; set; }
    public required string Error { get; set; }
}
