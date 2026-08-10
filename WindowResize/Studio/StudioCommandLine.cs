using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowResizeCapture.Studio;

// What one run of the app was asked to photograph.
internal sealed class StudioRequest
{
    internal string View { get; init; } = "";
    internal CultureInfo Language { get; init; } = CultureInfo.InvariantCulture;
    internal Size Size { get; init; } = StudioViews.DefaultSize;
    internal int SettleMs { get; init; } = StudioViews.DefaultSettleMs;
    internal string OutputPath { get; init; } = "";

    // Marketing copy given on the command line, which wins over the files in
    // store-shots/copy. Wording is tried and thrown away many times before it
    // is settled; having to edit a file for each attempt makes that slow.
    // Two spaces in a row mark a place the text may break.
    internal string Headline { get; init; } = "";
    internal string Body { get; init; } = "";
}

// Reads the studio's command line.
//
//   WindowResizeCapture.exe --list-views
//   WindowResizeCapture.exe --language ja --screenshot view=tray-menu out=shot.png
//
// A run without --screenshot or --list-views is an ordinary launch and must
// behave exactly as it always has.
internal static class StudioCommandLine
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    private const int ParentProcess = -1;

    // True when the app was asked to print its shot list. The list goes to the
    // console of whoever started the app: this is a WinExe, so it has none of
    // its own and anything written without borrowing one is lost.
    internal static bool IsListRequest(string[] args) =>
        Array.Exists(args, argument => argument == "--list-views");

    internal static void PrintViews()
    {
        if (!AttachConsole(ParentProcess))
            return;

        try
        {
            var text = new StringBuilder();
            text.AppendLine("views:");
            foreach (var view in StudioViews.All)
                text.AppendLine($"  {view.Name}\t{view.Description}");

            text.AppendLine("languages:");
            text.AppendLine("  " + string.Join(" ", StudioViews.Languages));

            text.AppendLine("size:");
            text.AppendLine($"  {StudioViews.DefaultSize.Width}x{StudioViews.DefaultSize.Height}");

            using var output = Console.OpenStandardOutput();
            var bytes = new UTF8Encoding(false).GetBytes(text.ToString());
            output.Write(bytes, 0, bytes.Length);
            output.Flush();
        }
        finally
        {
            FreeConsole();
        }
    }

    // Parse a screenshot run. Returns false for an ordinary launch.
    internal static bool TryParse(string[] args, out StudioRequest request)
    {
        request = new StudioRequest();

        int shootAt = Array.IndexOf(args, "--screenshot");
        if (shootAt < 0)
            return false;

        // Everything after --screenshot is key=value for this one picture.
        // The value keeps its spaces: two in a row are how the copy marks a
        // place it may break, and trimming them would erase the instruction.
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = shootAt + 1; index < args.Length; index++)
        {
            int equals = args[index].IndexOf('=');
            if (equals > 0)
                settings[args[index][..equals]] = args[index][(equals + 1)..];
        }

        var size = StudioViews.DefaultSize;
        if (settings.TryGetValue("width", out var width) && int.TryParse(width, out int parsedWidth))
            size.Width = parsedWidth;
        if (settings.TryGetValue("height", out var height) && int.TryParse(height, out int parsedHeight))
            size.Height = parsedHeight;

        int settle = StudioViews.DefaultSettleMs;
        if (settings.TryGetValue("settle", out var settleText))
            int.TryParse(settleText, out settle);

        request = new StudioRequest
        {
            View = settings.GetValueOrDefault("view", StudioViews.All[0].Name),
            Language = ResolveLanguage(args),
            Size = size,
            SettleMs = settle,
            OutputPath = settings.GetValueOrDefault("out", "studio-shot.png"),
            Headline = settings.GetValueOrDefault("headline", ""),
            Body = settings.GetValueOrDefault("body", ""),
        };
        return true;
    }

    // --language accepts a culture tag (ja, zh-Hant) or an English name
    // (japanese), because both turn up in scripts written by hand.
    private static CultureInfo ResolveLanguage(string[] args)
    {
        int at = Array.IndexOf(args, "--language");
        if (at < 0 || at + 1 >= args.Length)
            return CultureInfo.GetCultureInfo("en");

        string wanted = args[at + 1];

        try
        {
            return CultureInfo.GetCultureInfo(wanted);
        }
        catch (CultureNotFoundException)
        {
        }

        // Fall back to matching an English name such as "japanese".
        foreach (var candidate in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            if (string.Equals(candidate.EnglishName, wanted, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return CultureInfo.GetCultureInfo("en");
    }

}
