using FocusTracker.Core;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace FocusTracker.Service;

public class IpcServer
{
    private const string PipeName = "FocusTrackerPipe";

    private readonly FocusModeService _focusMode;
    private readonly NotificationPolicy _notificationPolicy;

    public IpcServer(
        FocusModeService focusMode,
        NotificationPolicy notificationPolicy)
    {
        _focusMode = focusMode;
        _notificationPolicy = notificationPolicy;
    }

    public Task StartAsync(CancellationToken token)
    {
        Console.WriteLine("IPC Server starting...");
        return Task.Run(() => ListenLoop(token), token);
    }



    private async Task ListenLoop(CancellationToken token)
    {
        Console.WriteLine("IPC ListenLoop running...");

        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;

            try
            {
                server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                System.Diagnostics.Debug.WriteLine("Waiting for IPC connection...");
                await server.WaitForConnectionAsync(token);
                System.Diagnostics.Debug.WriteLine("IPC client connected.");

                using var ms = new MemoryStream();
                var buffer = new byte[4096];
                int bytesRead;

                do
                {
                    bytesRead = await server.ReadAsync(buffer, 0, buffer.Length, token);
                    ms.Write(buffer, 0, bytesRead);
                }
                while (!server.IsMessageComplete);

                var requestJson = Encoding.UTF8.GetString(ms.ToArray());



                System.Diagnostics.Debug.WriteLine("Received JSON: " + requestJson);

                if (string.IsNullOrWhiteSpace(requestJson))
                {
                    System.Diagnostics.Debug.WriteLine("Empty request.");
                    continue;
                }

                System.Diagnostics.Debug.WriteLine("Deserializing...");
                var request = JsonSerializer.Deserialize<IpcRequest>(requestJson);

                System.Diagnostics.Debug.WriteLine("Handling request...");
                var response = HandleRequest(request);

                System.Diagnostics.Debug.WriteLine("Serializing response...");
                var responseJson = JsonSerializer.Serialize(response);

                System.Diagnostics.Debug.WriteLine("Sending response...");
                var responseWithNewLine = responseJson + "\n";
                var responseBytes = Encoding.UTF8.GetBytes(responseWithNewLine);
                await server.WriteAsync(responseBytes, 0, responseBytes.Length, token);
                await server.FlushAsync(token);


                System.Diagnostics.Debug.WriteLine("Response sent.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("IPC CRASH: " + ex);
            }
            finally
            {
                if (server != null && server.IsConnected)
                {
                    server.Disconnect();
                }

                server?.Dispose();
            }
        }

        System.Diagnostics.Debug.WriteLine("IPC loop exited safely.");
    }



    private IpcResponse HandleRequest(IpcRequest? request)
    {
        if (request == null)
            return new IpcResponse { Success = false, Message = "Invalid request" };

        switch (request.Command)
        {
            case "StartFocus":
                if (request.DurationMinutes == null)
                    return new IpcResponse { Success = false, Message = "Missing duration" };

                _focusMode.Start(TimeSpan.FromMinutes(request.DurationMinutes.Value));

                return new IpcResponse
                {
                    Success = true,
                    Message = "Focus started",
                    Status = GetStatus()
                };

            case "StopFocus":
                _focusMode.Stop();

                return new IpcResponse
                {
                    Success = true,
                    Message = "Focus stopped",
                    Status = GetStatus()
                };

            case "GetStatus":
                return new IpcResponse
                {
                    Success = true,
                    Status = GetStatus()
                };

            default:
                return new IpcResponse
                {
                    Success = false,
                    Message = "Unknown command"
                };
        }
    }

    private ServiceStatus GetStatus()
    {
        return new ServiceStatus
        {
            IsFocusActive = _focusMode.IsActive,
            FocusEndsAtUtc = _focusMode.EndsAt,
            SnoozedUntilUtc = _notificationPolicy.SnoozedUntil
        };
    }
}
