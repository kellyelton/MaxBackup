using System.IO.Pipes;
using MaxBackup.Shared;

namespace Max.IntegrationTests;

/// <summary>
/// Tests for the pipe communication protocol between CLI and service.
/// These tests verify that the protocol works correctly without needing the full service.
/// </summary>
public class PipeCommunicationTests : IAsyncLifetime
{
    private CancellationTokenSource _cts = null!;

    public Task InitializeAsync()
    {
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        return Task.CompletedTask;
    }

    private string GetUniquePipeName() => $"MaxBackupTest_{Guid.NewGuid():N}";

    [Fact]
    public async Task PipeProtocol_WriteAndReadMessage_RoundTrips()
    {
        // Arrange
        var pipeName = GetUniquePipeName();
        var originalMessage = new PipeRequest
        {
            Action = "TEST",
            Sid = "S-1-5-21-1234567890",
            ConfigPath = @"C:\Test\config.json"
        };

        PipeRequest? receivedMessage = null;

        using var serverPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        // Start server task first
        var serverTask = Task.Run(async () =>
        {
            await serverPipe.WaitForConnectionAsync(_cts.Token);
            receivedMessage = await PipeProtocol.ReadMessageAsync<PipeRequest>(serverPipe, 10, _cts.Token);
        });

        // Client connects and writes
        using var clientPipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await clientPipe.ConnectAsync(_cts.Token);
        await PipeProtocol.WriteMessageAsync(clientPipe, originalMessage, 10, _cts.Token);

        await serverTask;

        // Assert
        Assert.NotNull(receivedMessage);
        Assert.Equal(originalMessage.Action, receivedMessage.Action);
        Assert.Equal(originalMessage.Sid, receivedMessage.Sid);
        Assert.Equal(originalMessage.ConfigPath, receivedMessage.ConfigPath);
    }

    [Fact]
    public async Task PipeProtocol_MultipleResponses_AllReceived()
    {
        // Arrange
        var pipeName = GetUniquePipeName();
        
        using var serverPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        var responses = new[]
        {
            new PipeResponse { Status = "Info", Message = "Starting...", IsFinal = false },
            new PipeResponse { Status = "Info", Message = "Processing...", IsFinal = false },
            new PipeResponse { Status = "Success", Message = "Done!", IsFinal = true }
        };

        // Server sends multiple messages after connection
        var serverTask = Task.Run(async () =>
        {
            await serverPipe.WaitForConnectionAsync(_cts.Token);
            foreach (var response in responses)
            {
                await PipeProtocol.WriteMessageAsync(serverPipe, response, 10, _cts.Token);
            }
        });

        // Client connects and reads all messages
        using var clientPipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await clientPipe.ConnectAsync(_cts.Token);

        var receivedResponses = new List<PipeResponse>();
        while (true)
        {
            var response = await PipeProtocol.ReadMessageAsync<PipeResponse>(clientPipe, 10, _cts.Token);
            Assert.NotNull(response);
            receivedResponses.Add(response);
            if (response.IsFinal)
                break;
        }

        await serverTask;

        // Assert
        Assert.Equal(3, receivedResponses.Count);
        Assert.Equal("Info", receivedResponses[0].Status);
        Assert.Equal("Starting...", receivedResponses[0].Message);
        Assert.False(receivedResponses[0].IsFinal);
        Assert.Equal("Success", receivedResponses[2].Status);
        Assert.True(receivedResponses[2].IsFinal);
    }

    [Fact]
    public async Task PipeProtocol_WithValidationErrors_SerializesCorrectly()
    {
        // Arrange
        var pipeName = GetUniquePipeName();
        
        using var serverPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        var response = new PipeResponse
        {
            Status = "Error",
            Message = "Validation failed",
            IsFinal = true,
            ValidationErrors = new List<ValidationError>
            {
                new() { Job = "BackupJob1", Field = "Source", Error = "Path not found" },
                new() { Job = "BackupJob1", Field = "Destination", Error = "Access denied" }
            }
        };

        // Server sends response after connection
        var serverTask = Task.Run(async () =>
        {
            await serverPipe.WaitForConnectionAsync(_cts.Token);
            await PipeProtocol.WriteMessageAsync(serverPipe, response, 10, _cts.Token);
        });

        // Client reads
        using var clientPipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await clientPipe.ConnectAsync(_cts.Token);
        var received = await PipeProtocol.ReadMessageAsync<PipeResponse>(clientPipe, 10, _cts.Token);

        await serverTask;

        // Assert
        Assert.NotNull(received);
        Assert.NotNull(received.ValidationErrors);
        Assert.Equal(2, received.ValidationErrors.Count);
        Assert.Equal("BackupJob1", received.ValidationErrors[0].Job);
        Assert.Equal("Source", received.ValidationErrors[0].Field);
        Assert.Equal("Path not found", received.ValidationErrors[0].Error);
    }

    [Fact]
    public async Task PipeProtocol_Timeout_ThrowsTimeoutException()
    {
        // Arrange
        var pipeName = GetUniquePipeName();
        
        using var serverPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        // Server waits for connection but client doesn't write anything
        var serverTask = Task.Run(async () =>
        {
            await serverPipe.WaitForConnectionAsync(_cts.Token);
            // Try to read with a very short timeout, nothing to read
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await PipeProtocol.ReadMessageAsync<PipeRequest>(serverPipe, 1, _cts.Token);
            });
        });

        using var clientPipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await clientPipe.ConnectAsync(_cts.Token);
        // Don't write anything - let server timeout
        
        await serverTask;
    }

    [Fact]
    public async Task PipeProtocol_LargeMessage_HandledCorrectly()
    {
        // Arrange
        var pipeName = GetUniquePipeName();
        
        using var serverPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        // Create a message with a large payload (but under max size)
        var largeMessage = new string('X', 4000); // 4KB of X's
        var response = new PipeResponse
        {
            Status = "Info",
            Message = largeMessage,
            IsFinal = true
        };

        var serverTask = Task.Run(async () =>
        {
            await serverPipe.WaitForConnectionAsync(_cts.Token);
            await PipeProtocol.WriteMessageAsync(serverPipe, response, 10, _cts.Token);
        });

        using var clientPipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await clientPipe.ConnectAsync(_cts.Token);
        var received = await PipeProtocol.ReadMessageAsync<PipeResponse>(clientPipe, 10, _cts.Token);

        await serverTask;

        // Assert
        Assert.NotNull(received);
        Assert.Equal(largeMessage, received.Message);
    }

    [Fact]
    public async Task PipeProtocol_RequestResponse_FullCycle()
    {
        // Arrange - Simulate a full request-response cycle
        var pipeName = GetUniquePipeName();
        
        using var serverPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        var request = new PipeRequest
        {
            Action = "REGISTER",
            Sid = "S-1-5-21-test-sid",
            ConfigPath = @"C:\Users\Test\config.json"
        };

        PipeRequest? receivedRequest = null;

        // Server receives and responds
        var serverTask = Task.Run(async () =>
        {
            await serverPipe.WaitForConnectionAsync(_cts.Token);
            
            receivedRequest = await PipeProtocol.ReadMessageAsync<PipeRequest>(serverPipe, 10, _cts.Token);

            // Send info responses
            await PipeProtocol.WriteMessageAsync(serverPipe, new PipeResponse
            {
                Status = "Info",
                Message = "Validating...",
                IsFinal = false
            }, 10, _cts.Token);

            await PipeProtocol.WriteMessageAsync(serverPipe, new PipeResponse
            {
                Status = "Info",
                Message = "Processing...",
                IsFinal = false
            }, 10, _cts.Token);

            // Send final response
            await PipeProtocol.WriteMessageAsync(serverPipe, new PipeResponse
            {
                Status = "Success",
                Message = "Registered successfully",
                IsFinal = true
            }, 10, _cts.Token);
        });

        // Client sends request and reads responses
        using var clientPipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await clientPipe.ConnectAsync(_cts.Token);
        await PipeProtocol.WriteMessageAsync(clientPipe, request, 10, _cts.Token);

        // Read all responses until final
        var responses = new List<PipeResponse>();
        while (true)
        {
            var response = await PipeProtocol.ReadMessageAsync<PipeResponse>(clientPipe, 10, _cts.Token);
            if (response == null) break;
            responses.Add(response);
            if (response.IsFinal) break;
        }

        await serverTask;

        // Assert
        Assert.NotNull(receivedRequest);
        Assert.Equal("REGISTER", receivedRequest.Action);
        Assert.Equal(3, responses.Count);
        Assert.Equal("Info", responses[0].Status);
        Assert.Equal("Validating...", responses[0].Message);
        Assert.False(responses[0].IsFinal);
        Assert.Equal("Success", responses[2].Status);
        Assert.Equal("Registered successfully", responses[2].Message);
        Assert.True(responses[2].IsFinal);
    }

    [Fact]
    public async Task PipeProtocol_ConnectionClosed_ThrowsEndOfStream()
    {
        // Arrange
        var pipeName = GetUniquePipeName();
        
        using var serverPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        Exception? serverException = null;

        // Start server waiting for connection
        var serverWaitTask = serverPipe.WaitForConnectionAsync(_cts.Token);

        // Client connects
        var clientPipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await clientPipe.ConnectAsync(_cts.Token);
        await serverWaitTask; // Wait for server to recognize connection

        // Now close client
        clientPipe.Close();

        // Small delay to ensure close propagates
        await Task.Delay(50);

        // Server tries to read from closed connection
        try
        {
            await PipeProtocol.ReadMessageAsync<PipeRequest>(serverPipe, 5, _cts.Token);
        }
        catch (Exception ex)
        {
            serverException = ex;
        }

        // Assert - Should throw EndOfStreamException
        Assert.NotNull(serverException);
        Assert.IsType<EndOfStreamException>(serverException);
    }
}
