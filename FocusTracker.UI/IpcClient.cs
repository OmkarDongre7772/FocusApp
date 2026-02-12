using FocusTracker.Core;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace FocusTracker.UI;

public class IpcClient
{
    private const string PipeName = "FocusTrackerPipe";

    public async Task<IpcResponse?> SendAsync(IpcRequest request)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await client.ConnectAsync(2000);
            client.ReadMode = PipeTransmissionMode.Message;

            var json = JsonSerializer.Serialize(request);

            Debug.WriteLine("Json =>" + json);

            var requestBytes = Encoding.UTF8.GetBytes(json);

            await client.WriteAsync(requestBytes, 0, requestBytes.Length);
            await client.FlushAsync();

            // ---- READ RESPONSE ----
            using var ms = new MemoryStream();
            var buffer = new byte[4096];
            int bytesRead;

            do
            {
                bytesRead = await client.ReadAsync(buffer, 0, buffer.Length);
                ms.Write(buffer, 0, bytesRead);
            }
            while (!client.IsMessageComplete);

            var responseJson = Encoding.UTF8.GetString(ms.ToArray());

            Debug.WriteLine("Json response =>" + responseJson);

            if (string.IsNullOrWhiteSpace(responseJson))
                return null;

            return JsonSerializer.Deserialize<IpcResponse>(responseJson);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("IPC ERROR: " + ex);
            return new IpcResponse
            {
                Success = false,
                Message = "Service unavailable"
            };
        }
    }

}
