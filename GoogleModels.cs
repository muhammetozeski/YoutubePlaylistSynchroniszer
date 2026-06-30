using System.Text.Json;
using System.Text.Json.Serialization;

namespace YoutubePlaylistSynchroniszer;

/// <summary>OAuth client credentials parsed from a Google "client secret" JSON. The file wraps the real
/// fields under an <c>installed</c> or <c>web</c> object; both shapes (and a bare object) are accepted.</summary>
internal sealed class GoogleClientSecret
{
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public string AuthUri { get; init; } = AppConstants.GoogleAuthEndpoint;
    public string TokenUri { get; init; } = AppConstants.GoogleTokenEndpoint;

    public static GoogleClientSecret Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        JsonElement section =
            root.TryGetProperty("installed", out var installed) ? installed :
            root.TryGetProperty("web", out var web) ? web :
            root;

        string? clientId = ReadString(section, "client_id");
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidDataException("client_id not found in the credential JSON.");

        return new GoogleClientSecret
        {
            ClientId = clientId,
            ClientSecret = ReadString(section, "client_secret") ?? "",
            AuthUri = ReadString(section, "auth_uri") ?? AppConstants.GoogleAuthEndpoint,
            TokenUri = ReadString(section, "token_uri") ?? AppConstants.GoogleTokenEndpoint,
        };
    }

    static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>The token endpoint's JSON response (authorization-code or refresh-token grant).</summary>
internal sealed class OAuthTokenResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("token_type")] public string? TokenType { get; set; }
    [JsonPropertyName("scope")] public string? Scope { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("error_description")] public string? ErrorDescription { get; set; }
}
