using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace WindowResizeCapture;

// What the app calls itself, and the pictures it carries.
//
// The name and the version are read from the assembly rather than written
// here, because the project file already declares both and a second copy is
// one a rename forgets. That has happened: two strings kept the app's previous
// name in all sixteen languages for three releases.
internal static class App
{
    // The name shown wherever the app introduces itself: the tray tooltip, the
    // splash and the one message box that has to speak before any window
    // exists. Sentences that contain the name are translated strings in
    // Strings.resx; this is the bare name, which is a brand and is not.
    //
    // The fallback is only for an assembly built without <Product>, which the
    // project file does declare - a blank name would be worse than a stale one.
    internal static string Name { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? "Window Resize & Capture";

    // The version exactly as the project file writes it, so 1.9 stays "1.9"
    // rather than becoming "1.9.0". The informational version carries the
    // commit hash after a plus sign when the build has source-link data, and
    // that half is not for a person to read.
    internal static string Version { get; } = VersionWithoutCommit();

    // The copyright line, as the project file declares it. Windows already shows
    // this string in the file properties, so the splash reads the same one
    // rather than keeping a second copy: the copy it did keep said "Window
    // Resize", the app's name before 1.8.2, three releases after the rename.
    //
    // The same words as the LICENSE file, in the same order, down to writing the
    // sign as (c). The name reads Nakano Kappei here and Kappei Nakano wherever
    // Windows or the Store asks who published the app - a different question,
    // and registered with Microsoft under that spelling.
    internal static string Copyright { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright
        ?? "Copyright (c) 2026 Nakano Kappei";

    private static string VersionWithoutCommit()
    {
        string declared = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";

        int commit = declared.IndexOf('+');
        return commit < 0 ? declared : declared[..commit];
    }

    // ── How the app reads ────────────────────────────────────────────────

    // Whether the language being shown reads right to left, which for this app
    // means Arabic. Windows mirrors everything for such a language - the
    // taskbar, a window's controls, the direction a menu unfolds - and an app
    // that does not follow looks like it was translated and never laid out.
    //
    // Read each time rather than stored: the language is settled at startup,
    // and by whatever the command line asked for.
    internal static bool ReadsRightToLeft =>
        CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

    // A message box does not follow the app's own layout the way a window
    // does; it has to be told, at every call.
    internal static MessageBoxOptions MessageReading => ReadsRightToLeft
        ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
        : 0;

    // ── Embedded pictures ────────────────────────────────────────────────

    // The tray icon, and the larger picture the splash draws. Both are
    // embedded in the executable, and the names are spelled once so that
    // moving a file breaks the build rather than a picture.
    internal static Stream? OpenIcon() => Open("app.ico");

    internal static Stream? OpenSplashPicture() => Open("splash.png");

    private static Stream? Open(string fileName) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(
            $"WindowResizeCapture.Resources.{fileName}");
}
