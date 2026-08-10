using System;
using System.Globalization;

namespace WindowResizeCapture;

// The app's command line.
//
// Only one thing is read from it: the language. Everything else a person can
// change lives in the settings window, where it can be seen and undone.
internal static class CommandLine
{
    // The language named with --language, or null when none was, in which case
    // the app follows the language Windows itself is set to.
    //
    // A culture tag is what a script writes and an English name is what a
    // person writes from memory, so both are accepted. A name that is neither
    // is treated as though nothing had been asked for, rather than guessed at:
    // a typo then leaves the app in the language Windows is set to, which is
    // the language its user reads.
    internal static CultureInfo? Language(string[] args)
    {
        int at = Array.IndexOf(args, "--language");
        if (at < 0 || at + 1 >= args.Length)
            return null;

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

        return null;
    }
}
