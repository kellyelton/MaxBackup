using System.IO.Pipes;
using MaxBackup.Shared;

namespace Max.IntegrationTests;

/// <summary>
/// Integration tests that simulate the service-side behavior to test CLI interactions.
/// These tests use a mock pipe server to verify the full communication flow.
/// </summary>
public class MockServiceTests : IAsyncLifetime
{
    private const string TestPipeName = "MaxBackupMockTest";
    private NamedPipeServerStream? _serverPipe;
    private CancellationTokenSource _cts = null!;
    private Task? _serverTask;
    private int _testId;

    public Task InitializeAsync()
    {
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _testId = Environment.TickCount; // Unique ID for each test
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cts?.Cancel();
        try
        {
            if (_serverTask != null)
                await _serverTask;
        }
        catch { }
        _serverPipe?.Dispose();
        _cts?.Dispose();
    }

    private string GetUniquePipeName() => $"{TestPipeName}_{_testId}_{Guid.NewGuid():N}";

    /// <summary>
    /// Tests that a registration request receives multiple info messages followed by a final success.
    /// </summary>
    [Fact]
    public async Task MockService_RegisterRequest_SendsProgressThenFinal()
    {
        var pipeName = GetUniquePipeName();
        
        // Start mock server
        _serverPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        _serverTask = Task.Run(async () =>
        {
            await _serverPipe.WaitForConnectionAsync(_cts.Token);
            
            // Read request
            var request = await PipeProtocol.ReadMessageAsync<PipeRequest>(_serverPipe, 10, _cts.Token);
            Assert.NotNull(request);
            Assert.Equal("REGISTER", request.Action);

            // Simulate registration flow - send progress updates
            await PipeProtocol.WriteMessageAsync(_serverPipe, new PipeResponse
            {
                Status = "Info",
                Message = "Validating configuration...",
                IsFinal = false
            }, 10, _cts.Token);

            await Task.Delay(50); // Simulate work

            await PipeProtocol.WriteMessageAsync(_serverPipe, new PipeResponse
            {
                Status = "Info",
                Message = "Config path: C:\\Users\\Test\\config.json",
                IsFinal = false
            }, 10, _cts.Token);

            await Task.Delay(50); // Simulate work

            // Final response
            await PipeProtocol.WriteMessageAsync(_serverPipe, new PipeResponse
            {
                Status = "Success",
                Message = "User TestUser registered successfully. Backup worker started.",
                IsFinal = true
            }, 10, _cts.Token);
        });

        // Client connects and sends request
        using var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(_cts.Token);

        var request = new PipeRequest
        {
            Action = "REGISTER",
            Sid = "S-1-5-21-test",
            ConfigPath = @"C:\Users\Test\config.json"
        };
        await PipeProtocol.WriteMessageAsync(clientPipe, request, 10, _cts.Token);

        // Read responses
        var responses = new List<PipeResponse>();
        while (true)
        {
            var response = await PipeProtocol.ReadMessageAsync<PipeResponse>(clientPipe, 10, _cts.Token);
            Assert.NotNull(response);
            responses.Add(response);
            if (response.IsFinal) break;
        }

        // Verify
        Assert.Equal(3, responses.Count);
        Assert.All(responses.Take(2), r => Assert.False(r.IsFinal));
        Assert.True(responses[2].IsFinal);
        Assert.Equal("Success", responses[2].Status);
        Assert.Contains("registered successfully", responses[2].Message);
    }

    /// <summary>
    /// Tests that a status request returns immediate result.
    /// </summary>
    [Fact]
    public async Task MockService_StatusRequest_ReturnsSingleResponse()
    {
        var pipeName = GetUniquePipeName();
        
        _serverPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        _serverTask = Task.Run(async () =>
        {
            await _serverPipe.WaitForConnectionAsync(_cts.Token);
            
            var request = await PipeProtocol.ReadMessageAsync<PipeRequest>(_serverPipe, 10, _cts.Token);
            Assert.NotNull(request);
            Assert.Equal("STATUS", request.Action);

            // Single response for status
            await PipeProtocol.WriteMessageAsync(_serverPipe, new PipeResponse
            {
                Status = "Success",
                Message = "Registered: Yes\nConfig: C:\\config.json\nWorker: Running",
                IsFinal = true
            }, 10, _cts.Token);
        });

        using var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(_cts.Token);

        await PipeProtocol.WriteMessageAsync(clientPipe, new PipeRequest
        {
            Action = "STATUS",
            Sid = "S-1-5-21-test"
        }, 10, _cts.Token);

        var response = await PipeProtocol.ReadMessageAsync<PipeResponse>(clientPipe, 10, _cts.Token);

        Assert.NotNull(response);
        Assert.Equal("Success", response.Status);
        Assert.True(response.IsFinal);
        Assert.Contains("Registered: Yes", response.Message);
    }

    /// <summary>
    /// Tests validation error handling in registration.
    /// </summary>
    [Fact]
    public async Task MockService_RegisterWithValidationErrors_ReturnsErrors()
    {
        var pipeName = GetUniquePipeName();
        
        _serverPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        _serverTask = Task.Run(async () =>
        {
            await _serverPipe.WaitForConnectionAsync(_cts.Token);
            
            var request = await PipeProtocol.ReadMessageAsync<PipeRequest>(_serverPipe, 10, _cts.Token);
            Assert.NotNull(request);

            await PipeProtocol.WriteMessageAsync(_serverPipe, new PipeResponse
            {
                Status = "Info",
                Message = "Validating configuration...",
                IsFinal = false
            }, 10, _cts.Token);

            // Return validation errors
            await PipeProtocol.WriteMessageAsync(_serverPipe, new PipeResponse
            {
                Status = "Error",
                Message = "Configuration validation failed",
                IsFinal = true,
                ValidationErrors = new List<ValidationError>
                {
                    new() { Job = "BackupJob", Field = "Source", Error = "Directory does not exist" },
                    new() { Job = "BackupJob", Field = "Destination", Error = "Invalid path format" }
                }
            }, 10, _cts.Token);
        });

        using var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(_cts.Token);

        await PipeProtocol.WriteMessageAsync(clientPipe, new PipeRequest
        {
            Action = "REGISTER",
            Sid = "S-1-5-21-test",
            ConfigPath = @"C:\invalid\config.json"
        }, 10, _cts.Token);

        var responses = new List<PipeResponse>();
        while (true)
        {
            var response = await PipeProtocol.ReadMessageAsync<PipeResponse>(clientPipe, 10, _cts.Token);
            Assert.NotNull(response);
            responses.Add(response);
            if (response.IsFinal) break;
        }

        Assert.Equal(2, responses.Count);
        var errorResponse = responses[1];
        Assert.Equal("Error", errorResponse.Status);
        Assert.NotNull(errorResponse.ValidationErrors);
        Assert.Equal(2, errorResponse.ValidationErrors.Count);
    }

    /// <summary>
    /// Tests that unknown actions return an error.
    /// </summary>
    [Fact]
    public async Task MockService_UnknownAction_ReturnsError()
    {
        var pipeName = GetUniquePipeName();
        
        _serverPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        _serverTask = Task.Run(async () =>
        {
            await _serverPipe.WaitForConnectionAsync(_cts.Token);
            
            var request = await PipeProtocol.ReadMessageAsync<PipeRequest>(_serverPipe, 10, _cts.Token);
            Assert.NotNull(request);

            // Unknown action error
            await PipeProtocol.WriteMessageAsync(_serverPipe, new PipeResponse
            {
                Status = "Error",
                Message = $"Unknown action: {request.Action}",
                IsFinal = true
            }, 10, _cts.Token);
        });

        using var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(_cts.Token);

        await PipeProtocol.WriteMessageAsync(clientPipe, new PipeRequest
        {
            Action = "INVALID_ACTION",
            Sid = "S-1-5-21-test"
        }, 10, _cts.Token);

        var response = await PipeProtocol.ReadMessageAsync<PipeResponse>(clientPipe, 10, _cts.Token);

        Assert.NotNull(response);
        Assert.Equal("Error", response.Status);
        Assert.Contains("Unknown action", response.Message);
        Assert.True(response.IsFinal);
    }

    /// <summary>
    /// Tests concurrent connections (simulates multiple CLI instances).
    /// </summary>
    [Fact]
    public async Task MockService_ConcurrentClients_HandledCorrectly()
    {
        var pipeName = GetUniquePipeName();
        var clientCount = 3;
        var serverHandledCount = 0;
        
        // Server handles multiple connections sequentially
        var serverTask = Task.Run(async () =>
        {
            for (int i = 0; i < clientCount; i++)
            {
                using var serverPipe = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await serverPipe.WaitForConnectionAsync(_cts.Token);
                
                var request = await PipeProtocol.ReadMessageAsync<PipeRequest>(serverPipe, 10, _cts.Token);
                if (request != null)
                {
                    await PipeProtocol.WriteMessageAsync(serverPipe, new PipeResponse
                    {
                        Status = "Success",
                        Message = $"Handled request {request.Action}",
                        IsFinal = true
                    }, 10, _cts.Token);
                    Interlocked.Increment(ref serverHandledCount);
                }
            }
        });

        // Create multiple clients
        var clientTasks = Enumerable.Range(0, clientCount).Select(async i =>
        {
            await Task.Delay(i * 100); // Stagger connections
            
            using var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await clientPipe.ConnectAsync(_cts.Token);

            await PipeProtocol.WriteMessageAsync(clientPipe, new PipeRequest
            {
                Action = $"TEST_{i}",
                Sid = $"S-1-5-21-test-{i}"
            }, 10, _cts.Token);

            var response = await PipeProtocol.ReadMessageAsync<PipeResponse>(clientPipe, 10, _cts.Token);
            return response;
        }).ToList();

        var responses = await Task.WhenAll(clientTasks);
        await serverTask;

        Assert.Equal(clientCount, serverHandledCount);
        Assert.All(responses, r =>
        {
            Assert.NotNull(r);
            Assert.Equal("Success", r.Status);
            Assert.True(r.IsFinal);
        });
    }
}
