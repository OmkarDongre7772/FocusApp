namespace FocusTracker.Core
{
    public class LocalUser
    {
        public string? UserId { get; set; }          // Supabase UUID
        public string? Username { get; set; }        // Email
        public string? TeamId { get; set; }

        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? TokenExpiryUtc { get; set; }

        public bool TrackingEnabled { get; set; }
        public DateTime? LastLogin { get; set; }
    }
}
