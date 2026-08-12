using System;
using System.Text.Json.Serialization;

namespace WindowResizeCapture;

// A window size preset with width, height, and an optional human-readable label
// (e.g. "Full HD"). Each instance carries a stable GUID for identity in the
// custom-sizes list.
public class PresetSize
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    // "1280 x 720", and in that order in every language.
    //
    // A language that reads right to left lays the parts of this out from the
    // right, which turned the dimensions round: 1280 x 720 was drawn as
    // "x 720 1280" in the Arabic menu. Windows does the same to any such string,
    // and the way out is the one its own dialogs use - fence the run off as
    // left-to-right with characters that take no space and draw nothing.
    //
    // The embedding pair, not the newer isolate pair. Segoe UI has no glyph for
    // the isolates, so GDI+ drew them as two boxes lettered LRI and PDI, one on
    // each side of every size in the menu.
    //
    // Written as escapes rather than as the characters themselves, which would
    // sit in this line invisibly. Only where it is needed, so that every other
    // language keeps a string with nothing hidden in it.
    private const string EmbedLeftToRight = "\u202A";
    private const string PopEmbedding = "\u202C";

    // Computed for the menu, never stored. Without this the settings file gained
    // a DisplayName beside every custom size, and in a right-to-left language
    // that copy carried the two invisible characters below into the file.
    [JsonIgnore]
    public string DisplayName => App.ReadsRightToLeft
        ? EmbedLeftToRight + $"{Width} x {Height}" + PopEmbedding
        : $"{Width} x {Height}";

    public PresetSize() { }

    public PresetSize(int width, int height, string? label = null)
    {
        Width = width;
        Height = height;
        Label = label;
    }
}
