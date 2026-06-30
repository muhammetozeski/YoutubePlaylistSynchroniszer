using System.Net;
using System.Net.Http;

namespace YoutubePlaylistSynchroniszer;

/// <summary>One shared <see cref="HttpClient"/> for all network calls (OAuth + YouTube API), with
/// connection pooling and automatic decompression. A single client avoids socket exhaustion.</summary>
internal static class HttpClientProvider
{
    public static readonly HttpClient Shared = Create();

    static HttpClient Create()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All,
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(100) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppConstants.AppFolderName}/{AppConstants.Version}");
        return http;
    }
}
