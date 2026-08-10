using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WindowResizeCapture.Studio;

// The marketing line painted on the set: a headline and one supporting
// sentence, per language.
//
// It lives under store-shots/copy/ rather than in Strings.resx because it is
// not part of the product. A user never sees it, and shipping sixteen
// marketing lines inside the executable would put listing copy in front of
// every translator working on the app itself.
internal sealed record StudioCopy(string Headline, string Body)
{
    private static readonly StudioCopy Empty = new("", "");

    // A file named for this one picture, read by the same rules as the files
    // in store-shots/copy so that wording tried this way can be moved into
    // them unchanged. The session has already refused a path with no file at
    // it, which is where that failure can be reported.
    internal static StudioCopy Read(string path) => Parse(File.ReadAllLines(path));

    internal static StudioCopy Load(CultureInfo language)
    {
        string? folder = FindCopyFolder();
        if (folder == null)
            return Empty;

        // Try the exact tag, then the parent (pt-BR -> pt), then English.
        foreach (var candidate in Candidates(language))
        {
            string path = Path.Combine(folder, candidate + ".txt");
            if (File.Exists(path))
                return Parse(File.ReadAllLines(path));
        }

        return Empty;
    }

    private static string[] Candidates(CultureInfo language)
    {
        var names = new[] { language.Name, language.TwoLetterISOLanguageName, "en" };
        return names.Where(name => !string.IsNullOrEmpty(name)).Distinct().ToArray();
    }

    // First non-empty line is the headline; the rest, joined, is the body.
    private static StudioCopy Parse(string[] lines)
    {
        var meaningful = lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();

        if (meaningful.Length == 0)
            return Empty;

        return new StudioCopy(
            meaningful[0],
            string.Join(" ", meaningful.Skip(1)));
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
