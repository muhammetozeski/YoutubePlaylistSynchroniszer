namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// Caches the current OAuth access token and refreshes it (via the stored refresh token) shortly before
/// it expires. Serialized with a gate so concurrent API calls trigger only one refresh.
/// </summary>
internal static class AccessTokenProvider
{
    static readonly SemaphoreSlim Gate = new(1, 1);
    static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(1);

    static string? _accessToken;
    static DateTime _expiresAtUtc = DateTime.MinValue;

    public static async Task<string> GetAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && DateTime.UtcNow < _expiresAtUtc - ExpiryMargin)
                return _accessToken;

            var client = CredentialStore.ClientSecret ?? throw new InvalidOperationException("No client secret configured.");
            var refreshToken = CredentialStore.RefreshToken ?? throw new InvalidOperationException("No refresh token configured.");

            var response = await GoogleOAuthService.GetAccessTokenAsync(client, refreshToken, cancellationToken);
            _accessToken = response.AccessToken;
            _expiresAtUtc = DateTime.UtcNow.AddSeconds(response.ExpiresIn <= 0 ? 3600 : response.ExpiresIn);
            return _accessToken!;
        }
        finally { Gate.Release(); }
    }

    /// <summary>Drops the cached token (after sign-out or a credential change).</summary>
    public static void Reset()
    {
        _accessToken = null;
        _expiresAtUtc = DateTime.MinValue;
    }
}
