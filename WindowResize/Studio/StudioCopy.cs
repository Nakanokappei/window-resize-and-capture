using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WindowResizeCapture.Studio;

// The marketing line painted on the set: a headline and one supporting
// sentence, for one picture in one language.
//
// It lives under store-shots/copy/ rather than in Strings.resx because it is
// not part of the product. A user never sees it, and shipping sixteen
// marketing lines inside the executable would put listing copy in front of
// every translator working on the app itself.
//
// One file per language, and inside it one [section] per picture, named after
// the pose exactly as --list-views prints it. Four pictures say four different
// things about the app, and a listing that repeated one line four times would
// waste three of them. A file written without sections is read whole and used
// on every picture, which is what a language whose copy has not been split yet
// still gets.
internal sealed record StudioCopy(string Headline, string Body)
{
    private static readonly StudioCopy Empty = new("", "");

    // A file named for this one picture, read by the same rules as the files
    // in store-shots/copy so that wording tried this way can be moved into
    // them unchanged. The session has already refused a path with no file at
    // it, which is where that failure can be reported.
    internal static StudioCopy Read(string path, string view) =>
        Parse(File.ReadAllLines(path), view);

    internal static StudioCopy Load(CultureInfo language, string view)
    {
        string? folder = FindCopyFolder();
        if (folder == null)
            return Empty;

        // Try the exact tag, then the parent (pt-BR -> pt), then English. A
        // file that exists but says nothing about this picture is passed over
        // rather than accepted, so a half-translated language still gets a line
        // from English on the pictures it has not reached.
        foreach (var candidate in Candidates(language))
        {
            string path = Path.Combine(folder, candidate + ".txt");
            if (!File.Exists(path))
                continue;

            var copy = Parse(File.ReadAllLines(path), view);
            if (copy != Empty)
                return copy;
        }

        return Empty;
    }

    // The line that says the desktop around the app is drawn rather than
    // photographed. One file with a section per language, because there is one
    // of these per language rather than one per picture, and because the phrase
    // that carries it is a different word in each - it cannot be one sentence
    // translated sixteen times.
    internal static string Notice(CultureInfo language)
    {
        string? folder = FindCopyFolder();
        if (folder == null)
            return "";

        string path = Path.Combine(folder, "notice.txt");
        if (!File.Exists(path))
            return "";

        var lines = File.ReadAllLines(path);
        foreach (var candidate in Candidates(language))
        {
            var found = Parse(lines, candidate);
            if (found != Empty)
                return found.Headline;
        }

        return "";
    }

    private static string[] Candidates(CultureInfo language)
    {
        var names = new[] { language.Name, language.TwoLetterISOLanguageName, "en" };
        return names.Where(name => !string.IsNullOrEmpty(name)).Distinct().ToArray();
    }

    // The first line of the picture's section is the headline; the rest of that
    // section, joined, is the body. Lines before any section belong to no
    // picture in particular and are used when the one asked for is not there.
    private static StudioCopy Parse(string[] lines, string view)
    {
        var wanted = new List<string>();
        var everyPicture = new List<string>();
        string? section = null;

        foreach (var raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                continue;
            }

            if (section == null)
                everyPicture.Add(line);
            else if (string.Equals(section, view, StringComparison.OrdinalIgnoreCase))
                wanted.Add(line);
        }

        var chosen = wanted.Count > 0 ? wanted : everyPicture;
        if (chosen.Count == 0)
            return Empty;

        return new StudioCopy(chosen[0], string.Join(" ", chosen.Skip(1)));
    }

    private static string? FindCopyFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "store-shots", "copy");
            if (Directory.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        return null;
    }
}
