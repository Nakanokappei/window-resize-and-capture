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
// wallpaper rather than like a poster of a fractal. What keeps it in its place
// behind the marketing line is the ceiling on how light it may become: at that
// level the paragraph's own gray clears 4.5:1 against the lightest pixel the
// backdrop can produce, anywhere in the frame.
//
// It has to hold anywhere, because the marketing line goes wherever the pose
// leaves room for it. An earlier version darkened the upper left by nearly three
// quarters to protect a line that always started there - which is what made the
// body of the set read as a black hole in the corner of the picture.
internal static class StudioWallpaper
{
    // Far enough to resolve the edge, cheap enough that sixty-four pictures do
    // not turn into a long wait. The higher this runs, the finer the filaments
    // reaching out of the set become, which is what makes the backdrop read as
    // delicate rather than as a blunt silhouette.
    private const int MaxIterations = 420;

    // Where each language's wallpaper looks, and how wide a window it takes.
    //
    // Every one of them is mostly outside the set. The interior is one value -
    // points that never escape - so it can only ever be one flat color, and a
    // frame that is mostly interior has nothing for the flag's colors to divide
    // between them. Painting it near black is what the picture used to do, and
    // that is the hole the eye was drawn to.
    //
    // The share of each frame that never escapes was measured, one viewpoint at a
    // time. Seven of the sixteen were more than a third interior - Spanish was
    // nine tenths - and those seven were moved to the nearest frame that is under
    // a tenth. Raising the iteration limit does not help: at ten times the limit
    // the shares move by less than two points, because those points really are
    // inside the set rather than merely slow to leave.
    //
    // They differ by language so that sixteen pictures of the same product do
    // not share one backdrop. The widths stay between about 0.02 and 0.4: far
    // enough in to show filigree, not so far that the frame becomes noise.
    private static readonly Dictionary<string, (double Real, double Imaginary, double Width)>
        Viewpoints = new()
    {
        ["en"] = (-0.7780, 0.1790, 0.088),        // above seahorse valley
        ["ja"] = (-0.0162, 0.9223, 0.070),        // beside the double scepter
        ["de"] = (0.3830, 0.0070, 0.090),         // outside elephant valley
        ["fr"] = (-0.0160, 0.6900, 0.060),        // above the triple spiral
        ["es"] = (-1.3047, 0.0741, 0.045),
        ["pt"] = (-0.7757, 0.1365, 0.030),        // Misiurewicz point
        ["it"] = (-0.1600, 1.0405, 0.050),
        ["ru"] = (-1.7492, 0.0000, 0.080),        // the western antenna
        ["ko"] = (-0.2351, 0.8272, 0.040),
        ["zh-Hans"] = (-0.9250, 0.2660, 0.120),
        ["zh-Hant"] = (-1.4802, 0.1200, 0.100),
        ["ar"] = (0.4325, 0.2261, 0.035),
        ["hi"] = (-0.5503, 0.6259, 0.065),
        ["id"] = (0.2930, 0.6118, 0.045),
        ["th"] = (-1.9990, 0.0000, 0.060),
        ["vi"] = (-0.8169, 0.2789, 0.075),
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

    // The brightest the wallpaper may become, as relative luminance.
    //
    // Set by the paragraph rather than by the headline: the paragraph is drawn in
    // a light grey rather than in white, so it is the one that runs out of
    // contrast first. At this level it clears 4.5 to 1 against the lightest pixel
    // the backdrop can produce, anywhere in the frame, which is what a listing has
    // to be readable at.
    //
    // It has to hold everywhere, because the marketing line goes wherever the pose
    // leaves room for it. There is no corner that can be kept dark for it.
    private const double LightnessCeiling = 0.12;

    internal static Bitmap Render(int width, int height, System.Globalization.CultureInfo language)
    {
        var flag = StudioFlagColors.For(language);
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

            // How fast every point runs away, measured before anything is
            // colored, because the coloring depends on the whole frame.
            var escapes = new double[width * height];

            // Rows are independent, so the work spreads across the machine.
            //
            // Sampled at the middle of each pixel rather than at its corner. On the
            // real axis the orbit stays real, and its escape lands a shade away
            // from the escapes of the rows either side of it: two of the sixteen
            // viewpoints sit on that axis, and in both the row that fell exactly on
            // it came out as a dark line drawn across the whole picture. Half a
            // pixel down, no row lands on it.
            Parallel.For(0, height, y =>
            {
                double imaginary = imaginaryMin + (y + 0.5) * scale;
                int row = y * width;

                for (int x = 0; x < width; x++)
                    escapes[row + x] = Escape(realMin + (x + 0.5) * scale, imaginary);
            });

            var spread = new Distribution(escapes);

            Parallel.For(0, height, y =>
            {
                IntPtr row = data.Scan0 + y * data.Stride;

                for (int x = 0; x < width; x++)
                {
                    double escape = escapes[y * width + x];
                    var color = Shade(
                        escape, spread.ShareBefore(escape),
                        x / (double)width, y / (double)height, flag);

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

    // How much of the frame escapes no later than a given point does, which is
    // what decides which flag color that point takes.
    //
    // A fixed curve cannot decide it fairly. Sixteen viewpoints have sixteen
    // distributions of escape time, and the curve this had - the escape value
    // raised to 0.30 - left most of every frame at one end of the ramp with only
    // the set's edge reaching the other. Splitting on the share instead puts half
    // the picture in each color whatever the viewpoint does.
    //
    // Bucketed over the range the frame actually uses, not over nought to one.
    // Far outside the set every point escapes in a step or two, so those times sit
    // in a hair-thin slice of that interval: bucketed against the whole of it they
    // all fell in one bucket, took one share, and the picture came out a single
    // flat field with no fractal visible in it at all.
    private sealed class Distribution
    {
        private const int Buckets = 4096;

        private readonly double[] _share = new double[Buckets];
        private readonly double _from;
        private readonly double _span;

        internal Distribution(double[] escapes)
        {
            double from = double.MaxValue;
            double upto = double.MinValue;

            foreach (double escape in escapes)
            {
                from = Math.Min(from, escape);
                upto = Math.Max(upto, escape);
            }

            _from = from;
            _span = Math.Max(upto - from, 1e-12);

            var counts = new int[Buckets];
            foreach (double escape in escapes)
                counts[Bucket(escape)]++;

            // What escaped strictly earlier, not counting this bucket itself. The
            // body of the set escapes never, so it is the first bucket and has to
            // come out at nought: counted inclusively its own share was the answer,
            // and where the body filled six tenths of the frame that put the body
            // and everything else in the same band - one flat color, no fractal.
            long seen = 0;
            for (int bucket = 0; bucket < Buckets; bucket++)
            {
                _share[bucket] = seen / (double)escapes.Length;
                seen += counts[bucket];
            }
        }

        internal double ShareBefore(double escape) => _share[Bucket(escape)];

        private int Bucket(double escape) =>
            Math.Clamp((int)((escape - _from) / _span * Buckets), 0, Buckets - 1);
    }

    // One field per flag color, each holding the same share of the frame, meeting
    // along the fractal's own edges.
    //
    // Bands rather than a gradient. The share arriving here is spread evenly, so a
    // blend from the first color to the last would spend most of the picture in the
    // mixtures between them: Germany's red and gold met as a flat brown across the
    // whole desktop. Each color keeps its own band and only the last quarter of
    // each band turns into the next, which is what stops a boundary from looking
    // cut with scissors.
    //
    // The last quarter, in shares of the frame rather than in escape time, so the
    // seams are the same width in all sixteen pictures.
    private const double Seam = 0.25;

    private static Color Shade(
        double escape, double share, double across, double down, Color[] flag)
    {
        double place = Math.Clamp(share, 0, 0.999999) * flag.Length;
        int band = Math.Min((int)place, flag.Length - 1);
        var next = flag[Math.Min(band + 1, flag.Length - 1)];

        double through = place - band;
        double mix = through <= 1 - Seam ? 0 : (through - (1 - Seam)) / Seam;

        // Smoothstep, so no band ends in a line drawn with a ruler.
        mix = mix * mix * (3 - 2 * mix);

        var (r, g, b) = Blend(
            ((double)flag[band].R, flag[band].G, flag[band].B),
            ((double)next.R, next.G, next.B),
            mix);

        // The color still moves inside its own field, with how long the point takes
        // to escape. The filigree is the whole reason the backdrop is a fractal,
        // and two flat fields would show none of it.
        //
        // Far outside the set the escape time is nearly even, so that part gets a
        // slight fall toward the lower right as well - the way light falls across a
        // wall, and enough to keep a large calm area from reading as a printed
        // block of ink.
        double texture = 0.86 + 0.24 * Math.Pow(escape, 0.30);
        double fall = 1 - 0.10 * (across + down) / 2;

        r *= texture * fall;
        g *= texture * fall;
        b *= texture * fall;

        // No part of the wallpaper may get light enough to swallow the text over
        // it. A flag's yellow, or its white, would leave the paragraph unreadable
        // wherever it fell.
        //
        // Relative luminance as the contrast standard defines it, which means the
        // channels have to be linearised first. The weighted average of the
        // gamma-encoded values was standing in for it, and the two disagree by
        // enough to matter: the French red passed that test and still left the
        // paragraph over it at 3.7 to 1.
        double luminance = Luminance(r, g, b);
        if (luminance > LightnessCeiling)
        {
            // Pulled down where luminance is linear and then written back, so the
            // result measures at the ceiling rather than near it. Scaling the
            // gamma-encoded values instead left the lightest pixel a third over
            // it - that curve is not a plain power - and the paragraph over that
            // pixel at 3.7 to 1.
            double pull = LightnessCeiling / luminance;
            r = Encode(Linear(r) * pull);
            g = Encode(Linear(g) * pull);
            b = Encode(Linear(b) * pull);
        }

        return Color.FromArgb(
            (int)Math.Clamp(r, 0, 255),
            (int)Math.Clamp(g, 0, 255),
            (int)Math.Clamp(b, 0, 255));
    }

    // Relative luminance, the way the contrast standard measures it.
    private static double Luminance(double r, double g, double b) =>
        0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);

    private static double Linear(double channel)
    {
        double value = Math.Clamp(channel, 0, 255) / 255;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static double Encode(double linear)
    {
        double value = Math.Clamp(linear, 0, 1);
        return 255 * (value <= 0.0031308
            ? value * 12.92
            : 1.055 * Math.Pow(value, 1 / 2.4) - 0.055);
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
