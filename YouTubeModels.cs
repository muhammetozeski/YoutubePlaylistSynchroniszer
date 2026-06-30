using System.Text.Json.Serialization;

namespace YoutubePlaylistSynchroniszer;

/// <summary>A playlist owned by the signed-in user, as shown in the UI.</summary>
internal sealed record YouTubePlaylist(string Id, string Title, int ItemCount, DateTime? CreatedAt = null);

/// <summary>One video entry in a playlist. <see cref="AddedAt"/> is when it was added to the playlist.</summary>
internal sealed record PlaylistVideo(string Id, string Title, DateTime? AddedAt = null);

// ---- Raw YouTube Data API v3 DTOs (deserialization only) ----

internal sealed class PlaylistListResponse
{
    [JsonPropertyName("items")] public List<PlaylistResource> Items { get; set; } = [];
    [JsonPropertyName("nextPageToken")] public string? NextPageToken { get; set; }
}

internal sealed class PlaylistResource
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("snippet")] public TitleSnippet? Snippet { get; set; }
    [JsonPropertyName("contentDetails")] public PlaylistContentDetails? ContentDetails { get; set; }
}

internal sealed class PlaylistContentDetails
{
    [JsonPropertyName("itemCount")] public int ItemCount { get; set; }
}

internal sealed class PlaylistItemsResponse
{
    [JsonPropertyName("items")] public List<PlaylistItemResource> Items { get; set; } = [];
    [JsonPropertyName("nextPageToken")] public string? NextPageToken { get; set; }
}

internal sealed class PlaylistItemResource
{
    [JsonPropertyName("snippet")] public TitleSnippet? Snippet { get; set; }
    [JsonPropertyName("contentDetails")] public ItemContentDetails? ContentDetails { get; set; }
}

internal sealed class ItemContentDetails
{
    [JsonPropertyName("videoId")] public string VideoId { get; set; } = "";
}

/// <summary>Shared snippet shape — title plus the publish/added timestamp.</summary>
internal sealed class TitleSnippet
{
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("publishedAt")] public DateTime? PublishedAt { get; set; }
}

internal sealed class ChannelListResponse
{
    [JsonPropertyName("items")] public List<PlaylistResource> Items { get; set; } = [];
}
