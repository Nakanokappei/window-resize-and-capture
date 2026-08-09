using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace WindowResizeCapture.Studio;

// Two colors per language, taken from the flag of the country the language
// belongs to, so a listing picture carries a quiet hint of who it is for.
//
// White is left out: the wallpaper runs from near black to a muted highlight,
// and white gives the ramp nowhere to go. Where a flag has only one color
// besides white, the pair is that color and a deep shade of it.
//
// Nothing here is drawn at full strength. The colors are pushed down into a
// dark ramp by StudioWallpaper; used raw they would fight the marketing line
// and stop looking like wallpaper.
internal static class StudioFlagColors
{
    private static readonly Dictionary<string, (Color Deep, Color Light)> ByLanguage = new()
    {
        // United States: old glory red and blue.
        ["en"] = (Color.FromArgb(178, 34, 52), Color.FromArgb(60, 59, 110)),

        // Japan: the flag carries one color, so it is paired with its shade.
        ["ja"] = (Color.FromArgb(70, 12, 26), Color.FromArgb(188, 0, 45)),

        ["de"] = (Color.FromArgb(221, 0, 0), Color.FromArgb(255, 206, 0)),
        ["fr"] = (Color.FromArgb(0, 85, 164), Color.FromArgb(239, 65, 53)),
        ["es"] = (Color.FromArgb(170, 21, 27), Color.FromArgb(241, 191, 0)),
        ["pt"] = (Color.FromArgb(0, 102, 51), Color.FromArgb(214, 28, 34)),
        ["it"] = (Color.FromArgb(0, 140, 69), Color.FromArgb(205, 33, 42)),
        ["ru"] = (Color.FromArgb(0, 57, 166), Color.FromArgb(213, 43, 30)),
        ["ko"] = (Color.FromArgb(0, 71, 160), Color.FromArgb(205, 46, 58)),
        ["zh-Hans"] = (Color.FromArgb(222, 41, 16), Color.FromArgb(255, 222, 0)),
        ["zh-Hant"] = (Color.FromArgb(0, 0, 149), Color.FromArgb(254, 0, 0)),

        // Arabic is spoken across many countries, so the pan-Arab pair stands
        // in for any one flag.
        ["ar"] = (Color.FromArgb(0, 122, 61), Color.FromArgb(206, 17, 38)),

        ["hi"] = (Color.FromArgb(19, 136, 8), Color.FromArgb(255, 153, 51)),

        // Indonesia, like Japan, is one color and white.
        ["id"] = (Color.FromArgb(90, 0, 0), Color.FromArgb(255, 0, 0)),

        ["th"] = (Color.FromArgb(45, 42, 74), Color.FromArgb(165, 25, 49)),
        ["vi"] = (Color.FromArgb(218, 37, 29), Color.FromArgb(255, 255, 0)),
    };

    internal static (Color Deep, Color Light) For(CultureInfo language)
    {
        if (ByLanguage.TryGetValue(language.Name, out var exact))
            return exact;

        if (ByLanguage.TryGetValue(language.TwoLetterISOLanguageName, out var neutral))
            return neutral;

        return ByLanguage["en"];
    }
}
