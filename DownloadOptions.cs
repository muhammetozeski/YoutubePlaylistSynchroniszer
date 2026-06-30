namespace YoutubePlaylistSynchroniszer;

/// <summary>Whether a playlist downloads as audio-only ("music") or full video.</summary>
internal enum MediaKind { Music, Video }

/// <summary>Music quality tiers: best original, a detailed custom choice, or smallest-on-disk.</summary>
internal enum MusicQualityTier { Best, Custom, Worst }

/// <summary>
/// Per-playlist download choices and the yt-dlp argument logic derived from them. Kept with the options
/// (not scattered in the engine) so a format change is a one-place edit. Whenever a requested quality is
/// higher than the video actually offers, the selectors fall back to that video's best — for both music
/// and video.
/// </summary>
internal sealed class DownloadOptions
{
    public MediaKind Kind { get; set; } = MediaKind.Music;

    // ---- Music ----
    public MusicQualityTier MusicTier { get; set; } = MusicQualityTier.Best;
    /// <summary>Target audio format for the custom tier: m4a | mp3 | opus | flac | best.</summary>
    public string CustomAudioFormat { get; set; } = "m4a";
    /// <summary>Custom-tier bitrate in kbps; 0 keeps the best/original quality.</summary>
    public int CustomAudioBitrateKbps { get; set; }
    /// <summary>For the worst tier: re-encode to Codec2 with ffmpeg for maximum space saving.</summary>
    public bool ConvertWorstToCodec2 { get; set; }

    // ---- Video ----
    /// <summary>Cap video height (e.g. 1080); 0 means take the best available.</summary>
    public int VideoMaxHeight { get; set; } = 1080;

    // ---- Common ----
    public bool EmbedThumbnail { get; set; } = true;
    public bool EmbedMetadata { get; set; } = true;

    /// <summary>Builds the yt-dlp format + embedding arguments for these options.</summary>
    public IReadOnlyList<string> BuildFormatArguments()
    {
        var arguments = new List<string>();

        if (Kind == MediaKind.Video)
        {
            string heightFilter = VideoMaxHeight > 0 ? $"[height<={VideoMaxHeight}]" : "";
            // Prefer best <= cap; if no format satisfies the cap, fall back to the video's absolute best.
            string selector = VideoMaxHeight > 0
                ? $"bv*{heightFilter}+ba/b{heightFilter}/bv*+ba/b"
                : "bv*+ba/b";
            arguments.AddRange(["-f", selector, "--merge-output-format", "mp4"]);
        }
        else
        {
            arguments.AddRange(MusicTier switch
            {
                MusicQualityTier.Best => ["-f", "bestaudio/best", "-x", "--audio-format", "best", "--audio-quality", "0"],
                MusicQualityTier.Worst => ["-f", "worstaudio/worst", "-x", "--audio-format", "best", "--audio-quality", "0"],
                _ => BuildCustomAudioArguments(),
            });
        }

        if (EmbedThumbnail) arguments.AddRange(["--embed-thumbnail", "--convert-thumbnails", "jpg"]);
        if (EmbedMetadata) arguments.AddRange(["--embed-metadata", "--embed-chapters"]);
        return arguments;
    }

    string[] BuildCustomAudioArguments()
    {
        string format = string.IsNullOrWhiteSpace(CustomAudioFormat) ? "best" : CustomAudioFormat.ToLowerInvariant();
        // Prefer a same-container source when possible to avoid a needless re-encode.
        string source = format == "m4a" ? "bestaudio[ext=m4a]/bestaudio/best" : "bestaudio/best";
        string quality = CustomAudioBitrateKbps > 0 ? $"{CustomAudioBitrateKbps}K" : "0";
        return ["-f", source, "-x", "--audio-format", format, "--audio-quality", quality];
    }

    /// <summary>Short human description for the UI/logs.</summary>
    public string Describe() => Kind == MediaKind.Video
        ? $"{Strings.MediaKindVideo} ≤{(VideoMaxHeight > 0 ? VideoMaxHeight + "p" : "best")}"
        : MusicTier switch
        {
            MusicQualityTier.Best => $"{Strings.MediaKindMusic} · {Strings.QualityBest}",
            MusicQualityTier.Worst => $"{Strings.MediaKindMusic} · {Strings.QualityWorst}{(ConvertWorstToCodec2 ? " · Codec2" : "")}",
            _ => $"{Strings.MediaKindMusic} · {CustomAudioFormat}{(CustomAudioBitrateKbps > 0 ? " " + CustomAudioBitrateKbps + "k" : "")}",
        };
}
