using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// Google OAuth 2.0 for installed apps using the loopback-redirect flow (with PKCE): a throwaway local
/// TCP listener captures the browser redirect, so it needs neither admin rights nor a urlacl reservation.
/// Produces a long-lived refresh token (access_type=offline + prompt=consent) and exchanges it for short
/// access tokens on demand.
/// </summary>
internal static class GoogleOAuthService
{
    /// <summary>Opens the browser, captures the redirect and exchanges the code. The returned response
    /// carries a refresh token to persist.</summary>
    public static Task<OAuthTokenResponse> AuthorizeAsync(GoogleClientSecret client, CancellationToken cancellationToken = default) =>
        Resilience.RunAsync("OAuth authorize (loopback flow)", async token =>
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            string redirectUri = $"http://127.0.0.1:{port}/";

            string codeVerifier = RandomUrlToken(64);
            string codeChallenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
            string state = RandomUrlToken(32);

            string authUrl = client.AuthUri +
                "?client_id=" + Uri.EscapeDataString(client.ClientId) +
                "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                "&response_type=code" +
                "&scope=" + Uri.EscapeDataString(AppConstants.YouTubeReadOnlyScope) +
                "&access_type=offline&prompt=consent&include_granted_scopes=true" +
                "&state=" + state +
                "&code_challenge=" + codeChallenge + "&code_challenge_method=S256";

            OpenUrlInBrowser(authUrl);

            string code = await CaptureRedirectCodeAsync(listener, state, token);
            var tokens = await ExchangeAsync(client, new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = client.ClientId,
                ["client_secret"] = client.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = codeVerifier,
            }, token);

            if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
                throw new InvalidOperationException("Authorization succeeded but no refresh token was returned.");
            return tokens;
        }, input: "loopback OAuth", cancellationToken: cancellationToken);

    /// <summary>Trades a stored refresh token for a fresh access token (response carries its lifetime).</summary>
    public static Task<OAuthTokenResponse> GetAccessTokenAsync(GoogleClientSecret client, string refreshToken, CancellationToken cancellationToken = default) =>
        Resilience.RunAsync("OAuth refresh access token", async token =>
        {
            var tokens = await ExchangeAsync(client, new Dictionary<string, string>
            {
                ["client_id"] = client.ClientId,
                ["client_secret"] = client.ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
            }, token);

            if (string.IsNullOrWhiteSpace(tokens.AccessToken))
                throw new InvalidOperationException("Refresh succeeded but no access token was returned.");
            return tokens;
        }, input: "grant_type=refresh_token", perAttemptTimeout: TimeSpan.FromSeconds(30),
           pipeline: Resilience.NetworkPipeline, cancellationToken: cancellationToken);

    static async Task<OAuthTokenResponse> ExchangeAsync(GoogleClientSecret client, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await HttpClientProvider.Shared.PostAsync(client.TokenUri, content, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        var tokens = JsonSerializer.Deserialize<OAuthTokenResponse>(json)
            ?? throw new InvalidOperationException("Empty token endpoint response.");
        if (!response.IsSuccessStatusCode || tokens.Error is not null)
            throw new InvalidOperationException($"Token endpoint error ({(int)response.StatusCode}): {tokens.Error} {tokens.ErrorDescription}");
        return tokens;
    }

    /// <summary>Accepts the single browser redirect, validates state, returns the authorization code and
    /// writes a friendly "you can close this tab" page back to the browser.</summary>
    static async Task<string> CaptureRedirectCodeAsync(TcpListener listener, string expectedState, CancellationToken cancellationToken)
    {
        using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
        using var stream = connection.GetStream();

        var buffer = new byte[8192];
        int read = await stream.ReadAsync(buffer, cancellationToken);
        string requestLine = Encoding.ASCII.GetString(buffer, 0, read).Split('\n', 2)[0];
        string target = requestLine.Split(' ') is { Length: >= 2 } parts ? parts[1] : "/";

        var query = ParseQuery(new Uri("http://127.0.0.1" + target).Query);

        string responseBody = "<html><head><meta charset=\"utf-8\"></head><body style=\"font-family:Segoe UI;padding:2rem\">" +
            "<h3>" + AppConstants.AppTitle + "</h3><p>Yetkilendirme tamamlandı. Bu sekmeyi kapatabilirsiniz.</p></body></html>";
        byte[] responseBytes = Encoding.UTF8.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {Encoding.UTF8.GetByteCount(responseBody)}\r\nConnection: close\r\n\r\n{responseBody}");
        await stream.WriteAsync(responseBytes, cancellationToken);

        if (query.TryGetValue("error", out var error))
            throw new InvalidOperationException("Authorization denied: " + error);
        if (!query.TryGetValue("state", out var state) || state != expectedState)
            throw new InvalidOperationException("OAuth state mismatch (possible interception).");
        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("No authorization code in the redirect.");
        return code;
    }

    static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=');
            if (equals < 0) result[Uri.UnescapeDataString(pair)] = "";
            else result[Uri.UnescapeDataString(pair[..equals])] = Uri.UnescapeDataString(pair[(equals + 1)..]);
        }
        return result;
    }

    static string RandomUrlToken(int byteCount) => Base64Url(RandomNumberGenerator.GetBytes(byteCount));

    static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
