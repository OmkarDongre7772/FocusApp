using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FocusTracker.Core;
using static FocusTracker.Service.LocalAggregateRepository;

namespace FocusTracker.Service;

public class SupabaseClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _anonKey;

    public SupabaseClient(HttpClient http, SupabaseOptions options)
    {
        _http = http;
        _baseUrl = options.Url.TrimEnd('/');
        _anonKey = options.AnonPublicKey;
    }

    public async Task<bool> UploadAggregate(
        string accessToken,
        string teamId,
        string userId,
        LocalAggregateRow row)
    {
        var request = new HttpRequestMessage(
    HttpMethod.Post,
    $"{_baseUrl}/rest/v1/daily_team_aggregates?on_conflict=team_id,user_id,date");


        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        request.Headers.Add("apikey", _anonKey);
        request.Headers.Add("Prefer", "resolution=merge-duplicates");


        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                team_id = teamId,
                user_id = userId,
                date = row.Date.ToString("yyyy-MM-dd"),
                focus_percentage = row.FocusPercentage,
                fragmentation_score = row.FragmentationScore
            }),
            Encoding.UTF8,
            "application/json");

        var response = await _http.SendAsync(request);
        return response.IsSuccessStatusCode;
    }
}
