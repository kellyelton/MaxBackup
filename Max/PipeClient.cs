using System.IO.Pipes;
using System.Security.Principal;
using MaxBackup.Shared;

namespace Max;

public static class PipeClient
{
    private const int TimeoutSeconds = 30;

    public static async Task<List<PipeResponse>> SendRequestAsync(string action, string? configPath = null)
    {
        var responses = new List<PipeResponse>();

        try
        {
            // Get current user info
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User?.Value;
            if (sid == null)
            {
                responses.Add(new PipeResponse
                {
                    Status = "Error",
                    Message = "Could not determine current user SID"
                });
                return responses;
            }

            using var pipeClient = new NamedPipeClientStream(".", PipeProtocol.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            // Try to connect
            try
            {
                await pipeClient.ConnectAsync(TimeoutSeconds * 1000);
            }
            catch (TimeoutException)
            {
                responses.Add(new PipeResponse
                {
                    Status = "Error",
                    Message = $"Could not connect to MaxBackup service. Is the service running? (Timeout after {TimeoutSeconds} seconds)"
                });
                return responses;
            }
            catch (IOException)
            {
                responses.Add(new PipeResponse
                {
                    Status = "Error",
                    Message = "Could not connect to MaxBackup service. The service may not be installed or not running."
                });
                return responses;
            }

            // Send request
            var request = new PipeRequest
            {
                Action = action,
                Sid = sid,
                ConfigPath = configPath
            };

            await PipeProtocol.WriteMessageAsync(pipeClient, request, TimeoutSeconds);

            // Read responses until we get a final response
            while (pipeClient.IsConnected)
            {
                try
                {
                    var response = await PipeProtocol.ReadMessageAsync<PipeResponse>(pipeClient, TimeoutSeconds);
                    if (response != null)
                    {
                        responses.Add(response);

                        // Stop reading if this is the final message
                        if (response.IsFinal)
                        {
                            break;
                        }
                    }
                }
                catch (EndOfStreamException)
                {
                    // Server closed the connection
                    break;
                }
                catch (IOException)
                {
                    // Connection issue
                    break;
                }
                catch (TimeoutException ex)
                {
                    responses.Add(new PipeResponse
                    {
                        Status = "Error",
                        Message = $"Communication timeout: {ex.Message}"
                    });
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            responses.Add(new PipeResponse
            {
                Status = "Error",
                Message = $"Communication error: {ex.Message}"
            });
        }

        return responses;
    }
}
