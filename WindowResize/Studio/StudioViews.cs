using System.Collections.Generic;
using System.Drawing;

namespace WindowResizeCapture.Studio;

// One pose the studio can hold, named after the thing a person sees rather
// than after the code that arranges it. The name reaches the file name and
// the frame in the store listing, so it is read by people.
internal sealed record StudioView(string Name, string Description);

// The shot list. This is the only copy: --list-views prints it, and the shoot
// script reads that output. A script that carried its own list would drift
// away from the app the first time a pose was added.
internal static class StudioViews
{
    // The size of every listing picture, in physical pixels. The screen must
    // be at least this large, because the picture is copied off it. On a
    // display running at 200% this fills half the width, which leaves the set
    // large enough in logical pixels to hold the settings window.
    internal static readonly Size DefaultSize = new(2560, 1440);

    // Time to wait after a resize and again after a pose is arranged. Menus
    // and shadows arrive asynchronously; half-drawn is worse than slow.
    internal const int DefaultSettleMs = 1500;

    internal static readonly IReadOnlyList<StudioView> All = new[]
    {
        new StudioView("tray-menu", "The tray menu open over the desktop"),
        new StudioView("settings-general", "Settings, General tab: preset sizes and launch at login"),
        new StudioView("settings-capture", "Settings, Capture tab: what to do after a resize"),
        new StudioView("settings-behavior", "Settings, Behavior tab: position and front-most options"),
    };

    // The languages the app speaks. Keep in step with Resources/Strings.*.resx;
    // a language listed here with no translation is photographed in English,
    // which is a picture nobody can tell is wrong.
    internal static readonly IReadOnlyList<string> Languages = new[]
    {
        "en", "ja", "de", "fr", "es", "pt", "it", "ru",
        "ko", "zh-Hans", "zh-Hant", "ar", "hi", "id", "th", "vi",
    };
}
