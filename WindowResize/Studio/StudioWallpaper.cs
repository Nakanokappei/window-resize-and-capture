using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace WindowResizeCapture.Studio;

// Draws the desktop wallpaper by computing it rather than loading a picture.
//
// A backdrop that is calculated needs no image file: nothing binary sits in
// the repository, and the same backdrop comes out at any size, so a change of
// picture dimensions never means going back to an image editor.
//
// The subject is the Mandelbrot set, framed and colored to behave like
// wallpaper rather than like a poster of a fractal. Two things keep it in its
// place behind the marketing line:
//
//   * The view is offset so the set's body and its detailed edge fall to the
//     lower right. The upper left looks at empty space far outside the set,
//     where points escape in a step or two and the color stays near black.
//   * A gradient darkens the upper left further, so the text above it keeps
//     its contrast whatever the arithmetic does.
internal static class StudioWallpaper
{
    // Far enough to resolve the edge, cheap enough that sixty-four pictures do
    // not turn into a long wait. The higher this runs, the finer the filaments
    // reaching out of the set become, which is what makes the backdrop read as
    // delicate rather than as a blunt silhouette.
    private const int MaxIterations = 420;

    // Where each language's wallpaper looks, and how wide a window it takes.
    //
    // The whole set is a poor backdrop: its interior is a large flat black
    // shape, and the eye reads it as a hole rather than as texture. Every
    // viewpoint below sits on the boundary instead, where the filaments are,
    // and none of them contains much interior.
    //
    // They differ by language so that sixteen pictures of the same product do
    // not share one backdrop. The widths stay between about 0.02 and 0.4: far
    // enough in to show filigree, not so far that the frame becomes noise.
    private static readonly Dictionary<string, (double Real, double Imaginary, double Width)>
        Viewpoints = new()
    {
        ["en"] = (-0.7450, 0.1130, 0.055),        // seahorse valley
        ["ja"] = (-0.1002, 0.8383, 0.070),        // double scepter
        ["de"] = (0.2750, 0.0070, 0.090),         // elephant valley
        ["fr"] = (-0.0880, 0.6540, 0.060),        // triple spiral
        ["es"] = (-1.2507, 0.0201, 0.045),
        ["pt"] = (-0.7757, 0.1365, 0.030),        // Misiurewicz point
        ["it"] = (-0.1600, 1.0405, 0.050),
        ["ru"] = (-1.7492, 0.0000, 0.080),        // the western antenna
        ["ko"] = (-0.2351, 0.8272, 0.040),
        ["zh-Hans"] = (-0.9250, 0.2660, 0.120),
        ["zh-Hant"] = (-1.3602, 0.0000, 0.100),
        ["ar"] = (0.4325, 0.2261, 0.035),
        ["hi"] = (-0.5503, 0.6259, 0.065),
        ["id"] = (0.2930, 0.6118, 0.045),
        ["th"] = (-1.9990, 0.0000, 0.060),
        ["vi"] = (-0.7269, 0.1889, 0.075),
    };

    private static (double Real, double Imaginary, double Width) Viewpoint(
        System.Globalization.CultureInfo language)
    {
        if (Viewpoints.TryGetValue(language.Name, out var exact))
            return exact;
        if (Viewpoints.TryGetValue(language.TwoLetterISOLanguageName, out var neutral))
            return neutral;
        return Viewpoints["en"];
    }

    // The brightest the wallpaper may become, as relative luminance. White
    // text on a background at this level still clears the 4.5:1 that a store
    // listing has to be readable at.
    private const double LightnessCeiling = 0.34;

    internal static Bitmap Render(int width, int height, System.Globalization.CultureInfo language)
    {
        var (deep, light) = StudioFlagColors.For(language);
        var picture = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = picture.LockBits(
            new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            var view = Viewpoint(language);
            double scale = view.Width / width;
            double realMin = view.Real - view.Width / 2;
            double imaginaryMin = view.Imaginary - scale * height / 2;

            // Rows are independent, so the work spreads across the machine.
            Parallel.For(0, height, y =>
            {
                double imaginary = imaginaryMin + y * scale;
                IntPtr row = data.Scan0 + y * data.Stride;

                for (int x = 0; x < width; x++)
                {
                    double real = realMin + x * scale;
                    double smooth = Escape(real, imaginary);

                    var color = Shade(smooth, x / (double)width, y / (double)height, deep, light);
                    System.Runtime.InteropServices.Marshal.WriteInt32(
                        row, x * 4, color.ToArgb());
                }
            });
        }
        finally
        {
            picture.UnlockBits(data);
        }

        return picture;
    }

    // How quickly the point runs away, as a fraction from 0 to 1. Points that
    // never escape return 0, which paints them as the darkest color, so the
    // body of the set reads as deep shadow rather than as a hole.
    private static double Escape(double real, double imaginary)
    {
        // The cardioid and the main bulb hold every point they contain. Both
        // are cheap to test and together they cover most of the black area,
        // which is where the iteration would otherwise run to its limit.
        double q = (real - 0.25) * (real - 0.25) + imaginary * imaginary;
        if (q * (q + (real - 0.25)) <= 0.25 * imaginary * imaginary)
            return 0;
        if ((real + 1) * (real + 1) + imaginary * imaginary <= 0.0625)
            return 0;

        double zr = 0, zi = 0;
        int step = 0;

        while (step < MaxIterations && zr * zr + zi * zi <= 1 << 16)
        {
            double next = zr * zr - zi * zi + real;
            zi = 2 * zr * zi + imaginary;
            zr = next;
            step++;
        }

        if (step >= MaxIterations)
            return 0;

        // Continuous escape time: without it the colors step in visible rings,
        // which no wallpaper should have.
        double magnitude = Math.Sqrt(zr * zr + zi * zi);
        double smooth = step + 1 - Math.Log(Math.Log(magnitude)) / Math.Log(2);

        return Math.Clamp(smooth / MaxIterations, 0, 1);
    }

    // A restrained ramp: three blues from near black to a soft steel, and
    // nothing saturated. The escape value is compressed so that most of the
    // frame sits in the darkest part of the ramp and only the set's edge
    // reaches the lighter end.
    private static Color Shade(
        double escape, double across, double down, Color deep, Color light)
    {
        double tone = Math.Pow(escape, 0.30);

        // Three stops: near black, then the two flag colors at their own
        // strength. Nothing is desaturated here; what keeps the backdrop
        // behind the text is the ceiling applied below, not a washed-out
        // palette.
        var shadow = (deep.R * 0.12, deep.G * 0.12, deep.B * 0.12 + 8);
        var middle = ((double)deep.R, (double)deep.G, (double)deep.B);
        var highlight = ((double)light.R, (double)light.G, (double)light.B);

        var (r, g, b) = tone < 0.62
            ? Blend(shadow, middle, tone / 0.62)
            : Blend(middle, highlight, (tone - 0.62) / 0.38);

        // No part of the wallpaper is allowed to get bright enough to swallow
        // white text. A flag's yellow reaches a luminance of about 0.93 on its
        // own, which would leave the headline unreadable wherever it fell.
        double luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
        if (luminance > LightnessCeiling)
        {
            double pull = LightnessCeiling / luminance;
            r *= pull;
            g *= pull;
            b *= pull;
        }

        // Hold the upper left down so the marketing line always has something
        // dark to sit on, whatever the fractal happens to do there.
        double quiet = Math.Clamp(1 - (across * 0.75 + down * 0.55), 0, 1);
        double keep = 1 - 0.72 * quiet * quiet;

        return Color.FromArgb(
            (int)Math.Clamp(r * keep, 0, 255),
            (int)Math.Clamp(g * keep, 0, 255),
            (int)Math.Clamp(b * keep, 0, 255));
    }

    private static (double r, double g, double b) Blend(
        (double r, double g, double b) from, (double r, double g, double b) to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return (from.r + (to.r - from.r) * amount,
                from.g + (to.g - from.g) * amount,
                from.b + (to.b - from.b) * amount);
    }
}
