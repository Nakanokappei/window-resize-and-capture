using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;

namespace WindowResizeCapture.Studio;

// Lays out a line of marketing copy.
//
// Where the text may break is written into the copy itself, as two spaces in
// a row. Everything else is one unbreakable run. That puts the decision with
// whoever wrote the sentence, which is the only place it can honestly sit: a
// machine can find the spaces in English, but nothing here can tell where a
// Thai or Japanese phrase is willing to be cut.
//
// The marks are only offers. The layout takes them in order and breaks at the
// last one that still fits, so the same copy suits a wide picture and a narrow
// column without being rewritten.
//
// A break that is not used disappears. Two spaces collapse into one, and even
// that one is dropped where it meets CJK punctuation, which never wants a
// space beside it.
internal static class StudioText
{
    private static readonly Regex BreakOffer = new(@" +", RegexOptions.Compiled);

    // Where a language with no spaces is allowed to break: after a mark that
    // ends a clause. Inserting these is arithmetic, not judgement, so the copy
    // does not have to carry them and nobody has to remember to.
    // Chinese and Japanese marks, the Devanagari danda that ends a Hindi
    // sentence, and the Arabic comma, semicolon and question mark. Hindi and
    // Arabic already separate their words with spaces, so these only add a
    // break where a clause ends; they cost nothing and cover copy written as
    // one long sentence.
    //
    // Thai has neither word spaces nor an end-of-sentence mark. It breaks at
    // the spaces its own convention puts between phrases, which is the right
    // place, so copy in Thai has to be written with them. Finding word
    // boundaries there needs a dictionary, which is far more than this is
    // worth.
    private static readonly Regex AfterClause =
        new(@"(?<=[、。，．！？；：।،؛؟])(?![、。，．！？；：」』）】〕》〉”’])",
            RegexOptions.Compiled);

    // Punctuation that closes or opens a clause in Chinese, Japanese and
    // Korean. A space next to any of these reads as a gap in the sentence.
    private const string CjkPunctuation =
        "、。，．！？；：「」『』（）【】〔〕《》〈〉“”‘’・…ー";

    internal static IReadOnlyList<string> Wrap(
        Graphics canvas, string text, Font font, float width)
    {
        // A space is where the text may break. English already has them
        // between its words; Japanese and Chinese have none, so one is put
        // after every clause mark first. Both then wrap by the same rule.
        var runs = BreakOffer.Split(AfterClause.Replace(text ?? "", " "));
        var lines = new List<string>();
        string line = "";

        foreach (var raw in runs)
        {
            string run = raw.Trim();
            if (run.Length == 0)
                continue;

            if (line.Length == 0)
            {
                line = run;
                continue;
            }

            string joined = line + Glue(line, run) + run;
            if (Measure(canvas, joined, font) <= width)
            {
                line = joined;
                continue;
            }

            lines.Add(line);
            line = run;
        }

        if (line.Length > 0)
            lines.Add(line);

        return lines;
    }

    // What goes between two runs kept on the same line: nothing when either
    // side is CJK punctuation, a single space otherwise. The author wrote two;
    // two spaces mid-sentence would be a typographic mistake in any language.
    private static string Glue(string before, string after)
    {
        char left = before[^1];
        char right = after[0];

        bool touchesCjk =
            CjkPunctuation.IndexOf(left) >= 0 || CjkPunctuation.IndexOf(right) >= 0;

        return touchesCjk ? "" : " ";
    }

    // Measured against a generous but finite box. An earlier version passed
    // int.MaxValue as the layout width, which GDI+ turns into a rectangle it
    // cannot reason about: every string came back narrow enough to fit, so
    // nothing ever wrapped and the whole line ran off the picture.
    private static readonly SizeF Unbounded = new(1_000_000f, 1_000_000f);

    private static float Measure(Graphics canvas, string text, Font font) =>
        canvas.MeasureString(text, font, Unbounded, StringFormat.GenericTypographic).Width;

    // Draw the wrapped lines and report how tall they came out.
    //
    // Arabic reads from the right, so its lines are laid out from the right
    // edge of the column rather than the left. Without this the block would
    // start where a European language starts and end ragged on the side the
    // reader begins at.
    internal static float Draw(
        Graphics canvas, IReadOnlyList<string> lines, Font font, Brush ink,
        float left, float top, float width, bool rightToLeft)
    {
        using var format = new StringFormat(StringFormat.GenericTypographic);
        if (rightToLeft)
        {
            format.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            format.Alignment = StringAlignment.Far;
        }

        float step = font.GetHeight(canvas) * 1.15f;
        float y = top;

        foreach (var line in lines)
        {
            canvas.DrawString(line, font, ink,
                new RectangleF(left, y, width, step), format);
            y += step;
        }

        return y - top;
    }
}
