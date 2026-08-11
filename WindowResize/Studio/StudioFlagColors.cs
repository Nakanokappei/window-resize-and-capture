using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace WindowResizeCapture.Studio;

// The colors of the flag of the country a language belongs to, so a listing
// picture carries a quiet hint of who it is for.
//
// Ordered from the darkest to the lightest, because the wallpaper hands the
// first one to the calmest and largest part of the frame and works up from
// there.
//
// White is in. A flag that is mostly white does not look like itself without it,
// and the wallpaper holds every color down to a ceiling on lightness, so white
// arrives as a pale grey rather than as a glare and costs the text over it
// nothing.
//
// Black is the one color left out. The backdrop used to have a near-black stop
// of its own, and that is exactly what made the body of the fractal read as a
// hole in the picture rather than as part of it.
internal static class StudioFlagColors
{
    private static readonly Color White = Color.FromArgb(255, 255, 255);

    private static readonly Dictionary<string, Color[]> ByLanguage = new()
    {
        // United States: old glory blue, old glory red, white.
        ["en"] = new[] { Color.FromArgb(60, 59, 110), Color.FromArgb(178, 34, 52), White },

        // Japan: one red disc on white.
        ["ja"] = new[] { Color.FromArgb(188, 0, 45), White },

        // Germany: red and gold. The third band is black, which is left out.
        ["de"] = new[] { Color.FromArgb(221, 0, 0), Color.FromArgb(255, 206, 0) },

        ["fr"] = new[] { Color.FromArgb(0, 85, 164), Color.FromArgb(239, 65, 53), White },
        ["es"] = new[] { Color.FromArgb(170, 21, 27), Color.FromArgb(241, 191, 0) },
        ["pt"] = new[] { Color.FromArgb(214, 28, 34), Color.FromArgb(0, 102, 51) },
        ["it"] = new[] { Color.FromArgb(205, 33, 42), Color.FromArgb(0, 140, 69), White },
        ["ru"] = new[] { Color.FromArgb(0, 57, 166), Color.FromArgb(213, 43, 30), White },
        ["ko"] = new[] { Color.FromArgb(0, 71, 160), Color.FromArgb(205, 46, 58), White },
        ["zh-Hans"] = new[] { Color.FromArgb(222, 41, 16), Color.FromArgb(255, 222, 0) },
        ["zh-Hant"] = new[] { Color.FromArgb(0, 0, 149), Color.FromArgb(254, 0, 0), White },

        // Arabic is spoken across many countries, so the pan-Arab colors stand in
        // for any one flag. Their black is left out with every other black.
        ["ar"] = new[] { Color.FromArgb(206, 17, 38), Color.FromArgb(0, 122, 61), White },

        ["hi"] = new[] { Color.FromArgb(19, 136, 8), Color.FromArgb(255, 153, 51), White },
        ["id"] = new[] { Color.FromArgb(255, 0, 0), White },
        ["th"] = new[] { Color.FromArgb(45, 42, 74), Color.FromArgb(165, 25, 49), White },
        ["vi"] = new[] { Color.FromArgb(218, 37, 29), Color.FromArgb(255, 255, 0) },
    };

    internal static Color[] For(CultureInfo language)
    {
        if (ByLanguage.TryGetValue(language.Name, out var exact))
            return exact;

        if (ByLanguage.TryGetValue(language.TwoLetterISOLanguageName, out var neutral))
            return neutral;

        return ByLanguage["en"];
    }
}
