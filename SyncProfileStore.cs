using System.Text.Json;

namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// Persists the per-playlist <see cref="SyncProfile"/> list as JSON under UserData. Keyed by playlist id;
/// upserting the same id replaces it. A parse failure preserves the old file as <c>.corrupt-…</c> rather
/// than silently discarding the user's saved setups.
/// </summary>
internal static class SyncProfileStore
{
    static readonly object Gate = new();
    static List<SyncProfile> _profiles = [];
    static bool _loaded;

    public static IReadOnlyList<SyncProfile> All
    {
        get { EnsureLoaded(); lock (Gate) return _profiles.ToList(); }
    }

    public static SyncProfile? Get(string playlistId)
    {
        EnsureLoaded();
        lock (Gate) return _profiles.FirstOrDefault(p => p.PlaylistId == playlistId);
    }

    /// <summary>Adds or replaces the profile for its playlist id, then persists.</summary>
    public static void Upsert(SyncProfile profile)
    {
        EnsureLoaded();
        lock (Gate)
        {
            _profiles.RemoveAll(p => p.PlaylistId == profile.PlaylistId);
            _profiles.Add(profile);
        }
        Save();
    }

    /// <summary>Adds/replaces several profiles and persists once (for bulk select/clear).</summary>
    public static void UpsertMany(IReadOnlyCollection<SyncProfile> profiles)
    {
        if (profiles.Count == 0) return;
        EnsureLoaded();
        lock (Gate)
        {
            foreach (var profile in profiles)
            {
                _profiles.RemoveAll(p => p.PlaylistId == profile.PlaylistId);
                _profiles.Add(profile);
            }
        }
        Save();
    }

    public static void Remove(string playlistId)
    {
        EnsureLoaded();
        lock (Gate) _profiles.RemoveAll(p => p.PlaylistId == playlistId);
        Save();
    }

    static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_loaded) return;
            _loaded = true;
            string path = ConfigPathResolver.ProfilesPath;
            if (!File.Exists(path)) return;
            try { _profiles = JsonSerializer.Deserialize<List<SyncProfile>>(File.ReadAllText(path), AppJson.Options) ?? []; }
            catch (Exception ex)
            {
                Log("Sync profiles unreadable, preserving and starting empty: " + ex.Message, LogLevel.Error);
                AtomicFile.BackupCorrupt(path);
                _profiles = [];
            }
        }
    }

    static void Save()
    {
        try
        {
            string json;
            lock (Gate) json = JsonSerializer.Serialize(_profiles, AppJson.Options);
            AtomicFile.WriteAllText(ConfigPathResolver.ProfilesPath, json);
        }
        catch (Exception ex) { Log("Saving sync profiles failed: " + ex, LogLevel.Error); }
    }
}
