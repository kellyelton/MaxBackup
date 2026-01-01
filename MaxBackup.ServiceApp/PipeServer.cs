using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using MaxBackup.Shared;

namespace MaxBackup.ServiceApp;

public class PipeServer : BackgroundService
{
    private readonly ILogger<PipeServer> _logger;
    private readonly UserWorkerManager _workerManager;
    private readonly ServiceConfigManager _configManager;
    private int _pipeTimeoutSeconds = 30;

    public PipeServer(
        ILogger<PipeServer> logger,
        UserWorkerManager workerManager,
        ServiceConfigManager configManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _workerManager = workerManager ?? throw new ArgumentNullException(nameof(workerManager));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PipeServer starting on {pipe}", PipeProtocol.PipeName);

        // Load timeout configuration
        var config = await _configManager.LoadConfigAsync();
        _pipeTimeoutSeconds = config.PipeTimeoutSeconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create pipe security that allows authenticated users to connect
                var pipeSecurity = CreatePipeSecurity();
                
                await using var pipeServer = NamedPipeServerStreamAcl.Create(
                    PipeProtocol.PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: 0,
                    outBufferSize: 0,
                    pipeSecurity);

                _logger.LogDebug("Waiting for pipe connection...");
                await pipeServer.WaitForConnectionAsync(stoppingToken);
                _logger.LogDebug("Client connected");

                // Handle client request synchronously to avoid pipe lifetime issues
                try
                {
                    await HandleClientAsync(pipeServer, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in HandleClientAsync");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in pipe server loop");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("PipeServer stopped");
    }

    /// <summary>
    /// Creates pipe security that allows any authenticated user to connect.
    /// This is necessary because the service runs as SYSTEM but users need to connect.
    /// </summary>
    private static PipeSecurity CreatePipeSecurity()
    {
        var pipeSecurity = new PipeSecurity();

        // Allow authenticated users to read/write (connect and communicate)
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        // Allow SYSTEM full control (for the service itself)
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // Allow Administrators full control
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return pipeSecurity;
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipeServer, CancellationToken stoppingToken)
    {
        try
        {
            // Read request
            var request = await PipeProtocol.ReadMessageAsync<PipeRequest>(pipeServer, _pipeTimeoutSeconds, stoppingToken);
            if (request == null)
            {
                await SendFinalResponseAsync(pipeServer, "Error", "Invalid request format", stoppingToken);
                return;
            }

            _logger.LogInformation("Received {action} request from SID {sid}", request.Action, request.Sid);

            // Get username for logging
            var username = "Unknown";
            try
            {
                var sid = new SecurityIdentifier(request.Sid);
                var account = sid.Translate(typeof(NTAccount)) as NTAccount;
                username = account?.Value ?? "Unknown";
            }
            catch { }

            // Process request
            PipeResponse response;
            switch (request.Action.ToUpperInvariant())
            {
                case "REGISTER":
                    await SendInfoResponseAsync(pipeServer, "Validating configuration...", stoppingToken);
                    
                    var configPath = request.ConfigPath ?? Path.Combine(
                        UserPathResolver.ResolveUserProfilePath(request.Sid) ?? "",
                        "maxbackupconfig.json");

                    await SendInfoResponseAsync(pipeServer, $"Config path: {configPath}", stoppingToken);
                    
                    response = await _workerManager.RegisterUserAsync(request.Sid, username, configPath, stoppingToken);
                    await SendFinalResponseAsync(pipeServer, response.Status, response.Message, stoppingToken, response.ValidationErrors);
                    break;

                case "UNREGISTER":
                    await SendInfoResponseAsync(pipeServer, "Stopping worker...", stoppingToken);
                    response = await _workerManager.UnregisterUserAsync(request.Sid, username, stoppingToken);
                    await SendFinalResponseAsync(pipeServer, response.Status, response.Message, stoppingToken);
                    break;

                case "STATUS":
                    response = await _workerManager.GetUserStatusAsync(request.Sid, username);
                    await SendFinalResponseAsync(pipeServer, response.Status, response.Message, stoppingToken);
                    break;

                default:
                    await SendFinalResponseAsync(pipeServer, "Error", $"Unknown action: {request.Action}", stoppingToken);
                    break;
            }
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Client timeout: {message}", ex.Message);
            try
            {
                await SendFinalResponseAsync(pipeServer, "Error", ex.Message, stoppingToken);
            }
            catch { }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client request");
            try
            {
                await SendFinalResponseAsync(pipeServer, "Error", $"Internal error: {ex.Message}", stoppingToken);
            }
            catch { }
        }
    }

    private async Task SendInfoResponseAsync(
        NamedPipeServerStream pipeServer,
        string message,
        CancellationToken stoppingToken)
    {
        var response = new PipeResponse
        {
            Status = "Info",
            Message = message,
            IsFinal = false
        };

        await PipeProtocol.WriteMessageAsync(pipeServer, response, _pipeTimeoutSeconds, stoppingToken);
    }

    private async Task SendFinalResponseAsync(
        NamedPipeServerStream pipeServer,
        string status,
        string message,
        CancellationToken stoppingToken,
        List<ValidationError>? validationErrors = null)
    {
        var response = new PipeResponse
        {
            Status = status,
            Message = message,
            ValidationErrors = validationErrors,
            IsFinal = true
        };

        await PipeProtocol.WriteMessageAsync(pipeServer, response, _pipeTimeoutSeconds, stoppingToken);
    }
}
