using System;
using System.Text.Json.Serialization;

namespace WindowsResizeCapture;

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

    public string DisplayName => $"{Width} x {Height}";

    public PresetSize() { }

    public PresetSize(int width, int height, string? label = null)
    {
        Width = width;
        Height = height;
        Label = label;
    }
}
