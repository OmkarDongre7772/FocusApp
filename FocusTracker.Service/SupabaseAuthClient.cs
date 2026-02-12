using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FocusTracker.Service;

public class SupabaseAuthClient
{
    private readonly HttpClient _http;
    private readonly SupabaseOptions _options;

    public SupabaseAuthClient(HttpClient http, SupabaseOptions options)
    {
        _http = http;
        _options = options;
    }

    // ===============================
    // LOGIN
    // ===============================
    public async Task<AuthResult?> LoginAsync(string email, string password)
    {
        var payload = new
        {
            email,
            password
        };

        return await SendAuthRequest(
            $"{_options.Url}/auth/v1/token?grant_type=password",
            payload);
    }

    // ===============================
    // REGISTER
    // ===============================
    public async Task<AuthResult?> RegisterAsync(string email, string password)
    {
        var payload = new
        {
            email,
            password
        };

        return await SendAuthRequest(
            $"{_options.Url}/auth/v1/signup",
            payload);
    }

    // ===============================
    // REFRESH TOKEN
    // ===============================
    public async Task<AuthResult?> RefreshAsync(string refreshToken)
    {
        var payload = new
        {
            refresh_token = refreshToken
        };

        return await SendAuthRequest(
            $"{_options.Url}/auth/v1/token?grant_type=refresh_token",
            payload);
    }

    // ===============================
    // SHARED REQUEST HANDLER
    // ===============================
    private async Task<AuthResult?> SendAuthRequest(
        string url,
        object payload)
    {
        var json = JsonSerializer.Serialize(payload);

        Debug.WriteLine("Auth Request JSON => " + json);

        var request = new HttpRequestMessage(HttpMethod.Post, url);

        request.Headers.Add("apikey", _options.AnonPublicKey);
        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var response = await _http.SendAsync(request);

        Debug.WriteLine("Auth Response => " + response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Debug.WriteLine("Auth Error => " + error);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();

        var auth =
            JsonSerializer.Deserialize<SupabaseAuthResponse>(content);

        if (auth == null)
            return null;

        return new AuthResult
        {
            UserId = auth.user?.id ?? "",
            UserEmail = auth.user?.email ?? "",
            AccessToken = auth.access_token,
            RefreshToken = auth.refresh_token,
            ExpiresIn = auth.expires_in
        };
    }
}

public class AuthResult
{
    public string UserId { get; set; } = "";
    public string UserEmail { get; set; } = "";

    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public int ExpiresIn { get; set; }
}

public class SupabaseAuthResponse
{
    public string access_token { get; set; } = "";
    public string refresh_token { get; set; } = "";
    public int expires_in { get; set; }
    public SupabaseUser? user { get; set; }
}

public class SupabaseUser
{
    public string? id { get; set; }
    public string? email { get; set; }
}
