using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
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

    // A file of marketing copy to use instead of the one store-shots/copy
    // holds for this language: first line the headline, the rest the body.
    // Wording is tried and thrown away many times before it is settled, and a
    // file being named rather than the text itself keeps each attempt out of
    // the shell's own quoting and word splitting.
    internal string SourcePath { get; init; } = "";
}

// Reads the studio's command line.
//
//   WindowResizeCapture.exe --list-views
//   WindowResizeCapture.exe --language ja --screenshot view=choose-a-size out=shot.png
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

        // Everything after --screenshot is key=value for this one picture, in
        // any order. The value keeps its spaces, because a path may contain
        // them.
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
            // A shoot names its language for every picture. When it does not,
            // English is photographed rather than whatever the machine taking
            // the picture happens to be set to.
            Language = CommandLine.Language(args) ?? CultureInfo.GetCultureInfo("en"),
            Size = size,
            SettleMs = settle,
            OutputPath = settings.GetValueOrDefault("out", "studio-shot.png"),
            SourcePath = settings.GetValueOrDefault("source", ""),
        };
        return true;
    }
}
