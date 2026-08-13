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

    // Fence a run of text off as left to right, so that a language which reads
    // the other way leaves the order of what is inside alone.
    //
    // A language that reads right to left lays the parts of a string out from
    // the right, which turned the dimensions round: 1280 x 720 was drawn as
    // "x 720 1280" in the Arabic menu. And a name that ends in a mark rather
    // than a letter - WSXGA+, HD+, WXGA+ - had the + carried to the front of
    // the line, because a mark on its own belongs to whichever direction
    // surrounds it: the Arabic settings list offered a size called +WSXGA and
    // the Arabic menu a size called +HD.
    //
    // Windows does this to any such string, and the way out is the one its own
    // dialogs use - fence the run off with characters that take no space and
    // draw nothing.
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

    private static string ReadLeftToRight(string text) => App.ReadsRightToLeft
        ? EmbedLeftToRight + text + PopEmbedding
        : text;

    // "1280 x 720", and in that order in every language. Dimensions rather than
    // a name, because that is what it is: the name of a size is its Label, and
    // the separator between the two numbers is Strings.SettingsDimensionSeparator.
    //
    // Computed for the menu, never stored. While this was called DisplayName the
    // settings file gained a key of that name beside every custom size, and in a
    // right-to-left language that copy carried the two invisible characters above
    // into the file.
    [JsonIgnore]
    public string DisplayDimensions => ReadLeftToRight($"{Width} x {Height}");

    // The label as it is shown, fenced off the same way and for the same reason.
    [JsonIgnore]
    public string? DisplayLabel => Label == null ? null : ReadLeftToRight(Label);

    public PresetSize() { }

    public PresetSize(int width, int height, string? label = null)
    {
        Width = width;
        Height = height;
        Label = label;
    }
}
