using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// Read-only YouTube Data API v3 access used to enumerate the signed-in user's playlists and their video
/// ids. Each public call is logged + retried via <see cref="Resilience"/>; pages are followed until the
/// API stops returning a continuation token.
/// </summary>
internal static class YouTubeApiClient
{
    const int PageSize = 50; // API maximum per page

    /// <summary>All playlists owned by the signed-in account.</summary>
    public static Task<List<YouTubePlaylist>> ListMyPlaylistsAsync(CancellationToken cancellationToken = default) =>
        Resilience.RunAsync("YouTube list my playlists", async token =>
        {
            var playlists = new List<YouTubePlaylist>();
            string? pageToken = null;
            do
            {
                string url = $"/playlists?part=snippet,contentDetails&mine=true&maxResults={PageSize}" + PageParam(pageToken);
                var page = await GetAsync<PlaylistListResponse>(url, token);
                foreach (var item in page.Items)
                    playlists.Add(new YouTubePlaylist(item.Id, item.Snippet?.Title ?? item.Id, item.ContentDetails?.ItemCount ?? 0, item.Snippet?.PublishedAt));
                pageToken = page.NextPageToken;
            }
            while (!string.IsNullOrEmpty(pageToken));
            return playlists;
        }, input: "mine=true", perAttemptTimeout: TimeSpan.FromSeconds(60),
           pipeline: Resilience.NetworkPipeline, cancellationToken: cancellationToken);

    /// <summary>All video entries (id + title) of a playlist, in playlist order.</summary>
    public static Task<List<PlaylistVideo>> ListPlaylistVideosAsync(string playlistId, CancellationToken cancellationToken = default) =>
        Resilience.RunAsync("YouTube list playlist videos", async token =>
        {
            var videos = new List<PlaylistVideo>();
            string? pageToken = null;
            do
            {
                string url = $"/playlistItems?part=snippet,contentDetails&playlistId={Uri.EscapeDataString(playlistId)}&maxResults={PageSize}" + PageParam(pageToken);
                var page = await GetAsync<PlaylistItemsResponse>(url, token);
                foreach (var item in page.Items)
                {
                    string? videoId = item.ContentDetails?.VideoId;
                    if (!string.IsNullOrWhiteSpace(videoId))
                        videos.Add(new PlaylistVideo(videoId, item.Snippet?.Title ?? videoId, item.Snippet?.PublishedAt));
                }
                pageToken = page.NextPageToken;
            }
            while (!string.IsNullOrEmpty(pageToken));
            return videos;
        }, input: playlistId, perAttemptTimeout: TimeSpan.FromSeconds(120),
           pipeline: Resilience.NetworkPipeline, cancellationToken: cancellationToken);

    /// <summary>The signed-in account's channel title (for the "connected as …" label). Null on failure.</summary>
    public static async Task<string?> TryGetAccountLabelAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await GetAsync<ChannelListResponse>("/channels?part=snippet&mine=true&maxResults=1", cancellationToken);
            return response.Items.FirstOrDefault()?.Snippet?.Title;
        }
        catch (Exception ex) { Log("Account label fetch failed: " + ex.Message, LogLevel.Warning); return null; }
    }

    static string PageParam(string? pageToken) =>
        string.IsNullOrEmpty(pageToken) ? "" : "&pageToken=" + Uri.EscapeDataString(pageToken);

    static async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        string accessToken = await AccessTokenProvider.GetAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, AppConstants.YouTubeApiBase + relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await HttpClientProvider.Shared.SendAsync(request, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"YouTube API {(int)response.StatusCode}: {json}");

        return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("Empty YouTube API response.");
    }
}
