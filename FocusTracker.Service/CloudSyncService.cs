using FocusTracker.Core;
using Microsoft.Extensions.Logging;

namespace FocusTracker.Service;

public class CloudSyncService
{
    private readonly LocalUserRepository _userRepo;
    private readonly LocalAggregateRepository _aggregateRepo;
    private readonly SupabaseClient _client;
    private readonly SupabaseAuthClient _authClient;
    private readonly ILogger _logger;

    public CloudSyncService(
        LocalUserRepository userRepo,
        LocalAggregateRepository aggregateRepo,
        SupabaseClient client,
        SupabaseAuthClient authClient,
        ILogger logger)
    {
        _userRepo = userRepo;
        _aggregateRepo = aggregateRepo;
        _client = client;
        _authClient = authClient;
        _logger = logger;
    }

    public async Task RunOnceAsync()
    {
        var user = _userRepo.Get();

        if (string.IsNullOrWhiteSpace(user.UserId) ||
            string.IsNullOrWhiteSpace(user.TeamId) ||
            string.IsNullOrWhiteSpace(user.AccessToken))
            return;

        // ===== Token Expiry Check =====
        if (user.TokenExpiryUtc != null &&
            user.TokenExpiryUtc <= DateTime.UtcNow.AddMinutes(1))
        {
            _logger.LogInformation("Refreshing expired access token...");

            if (string.IsNullOrWhiteSpace(user.RefreshToken))
                return;

            var refreshResult =
                await _authClient.RefreshAsync(user.RefreshToken);

            if (refreshResult == null)
            {
                _logger.LogWarning("Token refresh failed.");
                return;
            }

            _userRepo.SaveAuth(
                refreshResult.UserId,
                refreshResult.UserEmail,
                user.TeamId,
                refreshResult.AccessToken,
                refreshResult.RefreshToken,
                DateTime.UtcNow.AddSeconds(refreshResult.ExpiresIn));

            user = _userRepo.Get();
        }

        // ===== Sync Aggregates =====
        var pending = _aggregateRepo.GetPending();

        foreach (var row in pending)
        {
            try
            {
                var success = await _client.UploadAggregate(
                    user.AccessToken!,
                    user.TeamId!,
                    user.UserId!,
                    row);

                if (success)
                {
                    _aggregateRepo.MarkSynced(row.Date);
                    _logger.LogInformation(
                        $"Synced {row.Date:yyyy-MM-dd}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    $"Sync failed for {row.Date:yyyy-MM-dd}: {ex.Message}");
            }
        }
    }
}
