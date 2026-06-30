using System.Security.Cryptography;
using System.Text;

namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// DPAPI wrapper for the credential/refresh-token files. The app's own files are bound to the current
/// Windows user (<see cref="DataProtectionScope.CurrentUser"/>) plus an app-level entropy value.
/// It also reads files produced WITHOUT entropy, because the user's pre-existing
/// <c>youtube_credentials.enc</c> / <c>youtube_refresh.enc</c> were encrypted that way and the app
/// can import them directly.
/// </summary>
internal static class CryptoService
{
    // App-level secondary entropy, combined with the per-user DPAPI key for files this app writes.
    static readonly byte[] Entropy = Encoding.UTF8.GetBytes("YoutubePlaylistSynchroniszer.v1.entropy.a91c33e7");

    /// <summary>Encrypts <paramref name="plainText"/> to raw DPAPI bytes and writes them to <paramref name="path"/>.</summary>
    public static void ProtectToFile(string path, string plainText)
    {
        byte[] plain = Encoding.UTF8.GetBytes(plainText);
        try
        {
            byte[] cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllBytes(path, cipher);
        }
        finally { Array.Clear(plain); }
    }

    /// <summary>
    /// Reads and decrypts a DPAPI file. Tries this app's entropy first, then no entropy (so the user's
    /// pre-existing .enc files import cleanly), and finally a Base64-wrapped variant.
    /// Returns null when none of the variants decrypt (e.g. encrypted by another user/machine).
    /// </summary>
    public static string? UnprotectFromFile(string path)
    {
        if (!File.Exists(path)) return null;
        byte[] raw;
        try { raw = File.ReadAllBytes(path); }
        catch (Exception ex) { Log("Read of encrypted file failed: " + ex.Message, LogLevel.Warning); return null; }

        // Some external tools store Base64 text rather than raw bytes; try to unwrap that too.
        byte[]? base64Variant = null;
        try { base64Variant = Convert.FromBase64String(Encoding.ASCII.GetString(raw).Trim()); } catch { }

        foreach (byte[] cipher in base64Variant is null ? new[] { raw } : [raw, base64Variant])
            foreach (byte[]? entropy in new byte[]?[] { Entropy, null })
            {
                try
                {
                    byte[] plain = ProtectedData.Unprotect(cipher, entropy, DataProtectionScope.CurrentUser);
                    try { return Encoding.UTF8.GetString(plain); }
                    finally { Array.Clear(plain); }
                }
                catch (CryptographicException) { /* try the next variant */ }
            }

        Log("Could not decrypt " + Path.GetFileName(path) + " with any known scheme.", LogLevel.Warning);
        return null;
    }
}
