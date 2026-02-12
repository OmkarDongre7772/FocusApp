using FocusTracker.Core;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace FocusTracker.Service;

public class IpcServer
{
    private const string PipeName = "FocusTrackerPipe";

    private readonly FocusModeService _focusMode;
    private readonly NotificationPolicy _notificationPolicy;
    private readonly SupabaseOptions _supabaseOptions;

    public IpcServer(
        FocusModeService focusMode,
        NotificationPolicy notificationPolicy,
        SupabaseOptions supabaseOptions)
    {
        _focusMode = focusMode;
        _notificationPolicy = notificationPolicy;
        _supabaseOptions = supabaseOptions;
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
                var response = await HandleRequest(request);

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



    private async Task<IpcResponse> HandleRequest(IpcRequest? request)
    {
        if (request == null)
            return new IpcResponse { Success = false, Message = "Invalid request" };

        var repo = new LocalUserRepository();

        switch (request.Command)
        {
            case "StartFocus":
                if (request.DurationMinutes == null)
                    return new IpcResponse { Success = false, Message = "Missing duration" };

                _focusMode.Start(TimeSpan.FromMinutes(request.DurationMinutes.Value));

                return Success("Focus started");

            case "StopFocus":
                _focusMode.Stop();
                return Success("Focus stopped");

            case "ToggleTracking":
                if (request.ToggleValue == null)
                    return Fail("Missing value");

                _focusMode.Stop(false);
                repo.SetTracking(request.ToggleValue.Value);

                return Success(request.ToggleValue.Value
                    ? "Tracking enabled"
                    : "Tracking disabled");

            case "Login":
                Debug.WriteLine("Server Request =>" + request);
                if (string.IsNullOrWhiteSpace(request.Username) ||
                    string.IsNullOrWhiteSpace(request.Password))
                    return Fail("Email and password required");

                var authClient = new SupabaseAuthClient(
                    new HttpClient(),
                    _supabaseOptions);

                var result = await authClient.LoginAsync(
                    request.Username.Trim(),
                    request.Password.Trim());
                Debug.WriteLine("Server Result =>" + result);
                if (result == null)
                    return Fail("Invalid credentials");

                repo.SaveAuth(
                    result.UserId,
                    result.UserEmail!,
                    request.TeamId,
                    result.AccessToken,
                    result.RefreshToken,
                    DateTime.UtcNow.AddSeconds(result.ExpiresIn));

                return Success("Login successful");

            case "Register":
                Debug.WriteLine("Server Request =>" + request);
                if (string.IsNullOrWhiteSpace(request.Username) ||
                    string.IsNullOrWhiteSpace(request.Password))
                    return Fail("Email and password required");

                var registerClient = new SupabaseAuthClient(
                    new HttpClient(),
                    _supabaseOptions);

                var registerResult = await registerClient.RegisterAsync(
                    request.Username.Trim(),
                    request.Password.Trim());
                Debug.WriteLine("Server Result =>" + registerResult);
                if (registerResult == null)
                    return Fail("Registration failed");

                // Auto-login immediately
                repo.SaveAuth(
                    registerResult.UserId,
                    registerResult.UserEmail,
                    request.TeamId,
                    registerResult.AccessToken,
                    registerResult.RefreshToken,
                    DateTime.UtcNow.AddSeconds(registerResult.ExpiresIn));

                return Success("Registration successful");



            case "Logout":
                _focusMode.Stop(false);
                repo.Logout();
                return Success("Logged out");

            case "GetStatus":
                return Success();

            default:
                return Fail("Unknown command");
        }

        IpcResponse Success(string? msg = null)
            => new IpcResponse
            {
                Success = true,
                Message = msg ?? "",
                Status = GetStatus()
            };

        IpcResponse Fail(string msg)
            => new IpcResponse
            {
                Success = false,
                Message = msg
            };
    }

    private ServiceStatus GetStatus()
    {
        var repo = new LocalUserRepository();
        var user = repo.Get();

        return new ServiceStatus
        {
            IsFocusActive = _focusMode.IsActive,
            FocusEndsAtUtc = _focusMode.EndsAt,
            SnoozedUntilUtc = _notificationPolicy.SnoozedUntil,
            TrackingEnabled = user.TrackingEnabled,
            IsLoggedIn = user.Username != null,
            Username = user.Username,
            TeamId = user.TeamId
        };
    }

}
