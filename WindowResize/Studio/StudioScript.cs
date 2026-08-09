using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace WindowResizeCapture.Studio;

// Reads the cast of window titles from store-shots/script/window-titles.txt.
//
// It lives with the other shoot material rather than in Strings.resx, for the
// same reason the marketing line does: a translator working on the app should
// never be handed text that only ever appears in a store picture.
internal static class StudioScript
{
    // Seven window titles followed by three application names.
    private const int LinesPerLanguage = 10;

    // Used when the file is missing, so a shoot still produces something
    // rather than throwing halfway through.
    private static readonly string[] Fallback =
    {
        "Getting started", "Release notes", "Documentation", "Downloads",
        "New tab", "Notes", "Untitled",
        "Browser", "Notepad", "Paint",
    };

    private static Dictionary<string, string[]>? _script;

    internal static string[] WindowTitles(CultureInfo language)
    {
        _script ??= Load();

        if (_script.TryGetValue(language.Name, out var exact))
            return exact;

        if (_script.TryGetValue(language.TwoLetterISOLanguageName, out var neutral))
            return neutral;

        return _script.TryGetValue("en", out var english) ? english : Fallback;
    }

    private static Dictionary<string, string[]> Load()
    {
        var script = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        string? path = Find();

        if (path == null)
            return script;

        string? tag = null;
        var titles = new List<string>();

        foreach (var raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                Commit(script, tag, titles);
                tag = line[1..^1];
                titles.Clear();
                continue;
            }

            titles.Add(line);
        }

        Commit(script, tag, titles);
        return script;
    }

    // A section with the wrong number of lines is dropped rather than used
    // short, which would leave a window with no name in a listing picture.
    private static void Commit(
        Dictionary<string, string[]> script, string? tag, List<string> titles)
    {
        if (tag != null && titles.Count == LinesPerLanguage)
            script[tag] = titles.ToArray();
    }

    private static string? Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            string candidate = Path.Combine(
                directory.FullName, "store-shots", "script", "window-titles.txt");

            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        return null;
    }
}
