using System.Text.Json.Serialization;

namespace FocusTracker.Core;

public class IpcRequest
{
    public string Command { get; set; } = string.Empty;

    public int? DurationMinutes { get; set; }

    public bool? ToggleValue { get; set; }

    public string? Username { get; set; }

    public string? TeamId { get; set; }
    public string? Password { get; set; }

}

public class IpcResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public ServiceStatus? Status { get; set; }
}

public class ServiceStatus
{
    public bool IsFocusActive { get; set; }

    public DateTime? FocusEndsAtUtc { get; set; }

    public DateTime? SnoozedUntilUtc { get; set; }

    public bool TrackingEnabled { get; set; }

    public bool IsLoggedIn { get; set; }

    public string? Username { get; set; }

    public string? TeamId { get; set; }
}
