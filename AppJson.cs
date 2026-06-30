using System.Text.Json;
using System.Text.Json.Serialization;

namespace YoutubePlaylistSynchroniszer;

/// <summary>Shared JSON options for the app's own stores: indented, enums as readable strings.</summary>
internal static class AppJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
