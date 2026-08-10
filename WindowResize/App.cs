using System;
using System.IO;
using System.Reflection;

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

    private static string VersionWithoutCommit()
    {
        string declared = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";

        int commit = declared.IndexOf('+');
        return commit < 0 ? declared : declared[..commit];
    }

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
