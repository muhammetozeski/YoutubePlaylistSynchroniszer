namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// File-extension knowledge for scanning a target folder. The existing-file scan EXCLUDES only image
/// extensions (an EXCLUDE filter, never an include filter), so a thumbnail that landed ahead of its video
/// — e.g. <c>Title (id).jpg</c> — can never be mistaken for the video itself and suppress its download.
/// </summary>
internal static class MediaScan
{
    public static readonly string[] ImageExtensions =
        [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".ico", ".tif", ".tiff", ".heic", ".heif"];

    public static bool IsImageExtension(string extension) =>
        ImageExtensions.Contains(extension.ToLowerInvariant());
}
