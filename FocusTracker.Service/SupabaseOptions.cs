namespace FocusTracker.Service;

public class SupabaseOptions
{
    public string Url { get; set; } = "";
    public string AnonPublicKey { get; set; } = "";
    public string ServiceRoleKey { get; set; } = "";
    public int SyncIntervalMinutes { get; set; } = 1;
}
