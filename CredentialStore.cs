namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// Holds the Google client secret and OAuth refresh token, persisted DPAPI-encrypted under UserData.
/// The client secret can be imported from a plain <c>.json</c> or an already-DPAPI-encrypted <c>.enc</c>
/// (so the user's existing files import directly); the refresh token can be pasted, imported from an
/// <c>.enc</c>, or produced by the browser sign-in flow.
/// </summary>
internal static class CredentialStore
{
    public static GoogleClientSecret? ClientSecret { get; private set; }
    public static string? RefreshToken { get; private set; }

    /// <summary>True once both a client secret and a refresh token are present (enough to call the API).</summary>
    public static bool IsAuthorized => ClientSecret is not null && !string.IsNullOrWhiteSpace(RefreshToken);
    public static bool HasClientSecret => ClientSecret is not null;

    /// <summary>Loads any previously stored credential material into memory.</summary>
    public static void Load()
    {
        string? credentialJson = CryptoService.UnprotectFromFile(ConfigPathResolver.CredentialsPath);
        if (credentialJson is not null)
        {
            try { ClientSecret = GoogleClientSecret.Parse(credentialJson); }
            catch (Exception ex) { Log("Stored credential JSON is unreadable: " + ex.Message, LogLevel.Warning); }
        }
        RefreshToken = CryptoService.UnprotectFromFile(ConfigPathResolver.RefreshTokenPath)?.Trim();
    }

    /// <summary>Imports a Google client secret from a <c>.json</c> or DPAPI <c>.enc</c> file, validating and
    /// re-encrypting it under UserData.</summary>
    public static void ImportClientSecret(string path)
    {
        string json = path.EndsWith(".enc", StringComparison.OrdinalIgnoreCase)
            ? CryptoService.UnprotectFromFile(path) ?? throw new InvalidDataException("Could not decrypt the .enc credential file.")
            : File.ReadAllText(path);

        ClientSecret = GoogleClientSecret.Parse(json); // throws if malformed — never store junk
        CryptoService.ProtectToFile(ConfigPathResolver.CredentialsPath, json);
        Log("Client secret imported and stored.", LogLevel.Info);
    }

    /// <summary>Stores a refresh token (pasted text, or the path to a <c>.enc</c>/text file).</summary>
    public static void SetRefreshToken(string tokenOrFilePath)
    {
        string token = tokenOrFilePath.Trim();
        if (File.Exists(token))
            token = (token.EndsWith(".enc", StringComparison.OrdinalIgnoreCase)
                        ? CryptoService.UnprotectFromFile(token)
                        : File.ReadAllText(token))?.Trim()
                    ?? throw new InvalidDataException("Could not read the refresh token file.");

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidDataException("Refresh token is empty.");

        RefreshToken = token;
        CryptoService.ProtectToFile(ConfigPathResolver.RefreshTokenPath, token);
        Log("Refresh token stored.", LogLevel.Info);
    }

    /// <summary>Forgets and deletes all stored credential material.</summary>
    public static void Clear()
    {
        ClientSecret = null;
        RefreshToken = null;
        foreach (var path in new[] { ConfigPathResolver.CredentialsPath, ConfigPathResolver.RefreshTokenPath })
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Log("Could not delete " + Path.GetFileName(path) + ": " + ex.Message, LogLevel.Warning); }
        Log("Credentials cleared.", LogLevel.Info);
    }
}
