using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace WindowResizeCapture.Studio;

// The studio set: a borderless window, exactly the size of the listing
// picture, painted to look like a desktop.
//
// This app lives in the tray and owns no window worth photographing. Copying
// the real screen instead would drag in the operator's own windows, the real
// clock and the operating system's language, and no two pictures would match.
// So the set is drawn: a brand wallpaper, a taskbar measured and sampled from
// the machine running the shoot, this app's own tray icon, and a clock in the
// language being photographed.
//
// Nothing here is a stored photograph of Windows. The taskbar's height, its
// colors and the icons on it are read from the live system at the moment a
// picture is taken, so no picture of somebody's desktop has to be kept in the
// repository or shipped inside the product.
internal sealed class StudioSetForm : Form
{
    // Where the app's own tray icon sits, right to left along the band, and
    // where the clock goes. Both are measured from the right edge, because
    // the clock's width changes with the language.
    private const int TrayIconRightInset = 310;
    private const int ClockRightMargin = 33;

    // The picture is taken on the day of the shoot, so a listing never shows
    // a stale date, at an hour that reads well in every culture.
    private const int ClockHour = 10;
    private const int ClockMinute = 8;

    private readonly StudioShell _shell;
    private readonly Bitmap? _wallpaper;
    private readonly Icon? _trayIcon;
    private readonly StudioCopy _copy;
    private readonly CultureInfo _language;

    internal StudioSetForm(StudioRequest request)
    {
        _language = request.Language;
        _copy = StudioCopy.Load(request.Language);
        _shell = StudioShell.Read();
        _wallpaper = LoadWallpaper();
        _trayIcon = LoadTrayIcon();

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(0, 0);
        Size = request.Size;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(16, 32, 58);

        // The set is scenery, never something the operator interacts with.
        Text = "Window Resize & Capture studio set";
    }

    // The point the tray menu should open from: just above this app's own icon
    // in the band, in screen coordinates.
    // Sit clear of the band rather than on its edge: a menu opened exactly at
    // the top of the taskbar had its last item cut off by it.
    internal Point TrayIconAnchor => PointToScreen(new Point(
        NotificationAreaRight - IconSize * 2 / 3 * 2 - IconSize / 2,
        ClientSize.Height - BandHeight - BandHeight / 4));

    // Where the icons end and the clock begins. The clock is the widest thing
    // in this corner and its width changes with the language, so everything
    // else is placed from here.
    private int NotificationAreaRight =>
        ClientSize.Width - ClockRightMargin - ClockWidth - IconSize / 2;

    // ── Painting ─────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var canvas = e.Graphics;
        canvas.SmoothingMode = SmoothingMode.AntiAlias;
        canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
        canvas.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // The clock is measured first, because everything else in that corner
        // is placed to the left of it and its width changes with the language.
        _clockWidth = MeasureClockWidth(canvas);

        PaintWallpaper(canvas);
        PaintTaskbar(canvas);
        PaintPinnedIcons(canvas);
        PaintNotificationArea(canvas);
        PaintClock(canvas);
        PaintMarketingLine(canvas);
    }

    private int _clockWidth;

    private int ClockWidth => _clockWidth;

    private Font ClockFont() => new("Segoe UI", BandHeight / 9f);

    private (string time, string date) ClockText()
    {
        var today = DateTime.Now;
        var instant = new DateTime(
            today.Year, today.Month, today.Day, ClockHour, ClockMinute, 0);

        return (instant.ToString(_language.DateTimeFormat.ShortTimePattern, _language),
                instant.ToString(_language.DateTimeFormat.ShortDatePattern, _language));
    }

    // The date is normally the wider of the two lines, but not in every
    // language, so both are measured.
    private int MeasureClockWidth(Graphics canvas)
    {
        var (time, date) = ClockText();
        using var font = ClockFont();
        return (int)Math.Ceiling(Math.Max(
            canvas.MeasureString(time, font).Width,
            canvas.MeasureString(date, font).Width));
    }

    // The wallpaper is ours, not the operator's, so every picture in the
    // listing shares one backdrop. When an image has been placed in
    // store-shots/wallpaper it is used; otherwise a plain gradient stands in,
    // which is enough to see the layout while working.
    private void PaintWallpaper(Graphics canvas)
    {
        var area = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height - BandHeight);

        if (_wallpaper == null)
        {
            using var wash = new LinearGradientBrush(
                area, Color.FromArgb(23, 48, 87), Color.FromArgb(12, 24, 45), 60f);
            canvas.FillRectangle(wash, area);
            return;
        }

        // Cover the area without distorting the picture: scale by whichever
        // edge needs the most, then center what spills over.
        double scale = Math.Max(
            area.Width / (double)_wallpaper.Width,
            area.Height / (double)_wallpaper.Height);

        int width = (int)Math.Ceiling(_wallpaper.Width * scale);
        int height = (int)Math.Ceiling(_wallpaper.Height * scale);

        canvas.DrawImage(_wallpaper, new Rectangle(
            area.X - (width - area.Width) / 2,
            area.Y - (height - area.Height) / 2,
            width, height));
    }

    // The band is drawn from three colors sampled off the real taskbar: the
    // light line along its top and the two ends of its vertical gradient. A
    // flat fill would not match, because the real band is a gradient.
    private void PaintTaskbar(Graphics canvas)
    {
        int top = ClientSize.Height - BandHeight;
        var band = new Rectangle(0, top, ClientSize.Width, BandHeight);

        using (var fill = new LinearGradientBrush(
            band, _shell.FillTop, _shell.FillBottom, LinearGradientMode.Vertical))
        {
            canvas.FillRectangle(fill, band);
        }

        using var edge = new Pen(_shell.TopLine);
        canvas.DrawLine(edge, 0, top, ClientSize.Width, top);
    }

    // The search box and the pinned icons, centered together the way Windows
    // 11 centers them. The icons come from the installed programs, so a
    // machine missing one simply shows fewer.
    //
    // The search box carries the word "Search" in the language being
    // photographed. It is the one piece of the desktop itself that a reader
    // can see is translated, which is what makes a picture read as a
    // localized app rather than an English one with translated menus.
    private void PaintPinnedIcons(Graphics canvas)
    {
        int size = IconSize;
        int gap = BandHeight * 43 / 100;
        int bandTop = ClientSize.Height - BandHeight;
        int y = bandTop + (BandHeight - size) / 2;

        // Measured off a real taskbar: the ink of the placeholder text stands
        // 40 pixels tall in a band of 96, so the em is a little over four
        // tenths of the band. Given in pixels, because a size in points would
        // then have to be converted twice.
        using var searchFont = new Font("Segoe UI", BandHeight * 0.42f, GraphicsUnit.Pixel);
        string label = StudioSearchLabel.For(_language);
        int searchWidth = SearchBoxWidth(canvas, label, searchFont);

        int iconsWidth = _shell.PinnedIcons.Length * (size + gap);
        int total = size + gap + searchWidth + gap + iconsWidth;
        int x = (ClientSize.Width - total) / 2;

        DrawStart(canvas, new Rectangle(x, y, size, size));
        x += size + gap;

        PaintSearchBox(canvas, label, searchFont, new Rectangle(
            x, bandTop + BandHeight * 27 / 100, searchWidth, BandHeight * 46 / 100));
        x += searchWidth + gap;

        foreach (var icon in _shell.PinnedIcons)
        {
            canvas.DrawIcon(icon, new Rectangle(x, y, size, size));
            x += size + gap;
        }
    }

    // Wide enough for the word, and never narrower than the box Windows draws
    // even when the word is as short as the Japanese one.
    private int SearchBoxWidth(Graphics canvas, string label, Font font)
        => Math.Max(
            (int)canvas.MeasureString(label, font).Width + BandHeight * 3 / 2,
            BandHeight * 9 / 2);

    // Four squares. Drawn rather than lifted from the running shell, like the
    // volume and network glyphs beside the clock.
    private static void DrawStart(Graphics canvas, Rectangle box)
    {
        float pane = box.Width * 0.45f;
        float split = box.Width - pane * 2;

        using var blue = new SolidBrush(Color.FromArgb(0, 120, 212));
        canvas.FillRectangle(blue, box.X, box.Y, pane, pane);
        canvas.FillRectangle(blue, box.X + pane + split, box.Y, pane, pane);
        canvas.FillRectangle(blue, box.X, box.Y + pane + split, pane, pane);
        canvas.FillRectangle(blue, box.X + pane + split, box.Y + pane + split, pane, pane);
    }

    private void PaintSearchBox(Graphics canvas, string label, Font font, Rectangle box)
    {
        int radius = box.Height / 2;
        using var shape = new GraphicsPath();
        shape.AddArc(box.X, box.Y, radius * 2, radius * 2, 90, 180);
        shape.AddArc(box.Right - radius * 2, box.Y, radius * 2, radius * 2, 270, 180);
        shape.CloseFigure();

        using var fill = new SolidBrush(Color.FromArgb(252, 252, 252));
        using var edge = new Pen(Color.FromArgb(196, 196, 196));
        canvas.FillPath(fill, shape);
        canvas.DrawPath(edge, shape);

        // A magnifier drawn from a circle and a handle, so no glyph font has
        // to be present on the machine taking the picture.
        int glyph = box.Height / 3;
        int glyphLeft = box.X + box.Height / 3;
        int glyphTop = box.Y + (box.Height - glyph) / 2;
        using var ink = new Pen(Color.FromArgb(96, 96, 96), Math.Max(glyph / 8f, 1.5f));
        canvas.DrawEllipse(ink, glyphLeft, glyphTop, glyph * 3 / 4, glyph * 3 / 4);
        canvas.DrawLine(ink,
            glyphLeft + glyph * 5 / 8, glyphTop + glyph * 5 / 8,
            glyphLeft + glyph, glyphTop + glyph);

        // The real placeholder text measures about rgb(48,61,64) at its core.
        using var text = new SolidBrush(Color.FromArgb(48, 61, 64));
        var textArea = new RectangleF(
            glyphLeft + glyph * 2, box.Y, box.Right - glyphLeft - glyph * 2, box.Height);
        using var format = new StringFormat
        {
            LineAlignment = StringAlignment.Center,
            Alignment = StringAlignment.Near,
        };
        canvas.DrawString(label, font, text, textArea, format);
    }

    // The notification area, laid out right to left the way Windows 11 does:
    // the clock, then volume and network, then third-party icons, then the
    // chevron that hides the rest. This app's own icon sits among the
    // third-party ones.
    //
    // Volume and network are drawn from shapes rather than copied off the real
    // taskbar. The shell owns those glyphs; nothing can extract them the way
    // an application icon can be extracted, and lifting the pixels would put a
    // picture of Windows back into this project.
    private void PaintNotificationArea(Graphics canvas)
    {
        // Measured on a real taskbar: a notification glyph is 32 pixels in a
        // band of 96 and repeats every 48, so it is both smaller and more
        // tightly packed than a pinned icon.
        int size = Math.Max(BandHeight * 33 / 100, 12);
        int gap = Math.Max(BandHeight * 17 / 100, 4);
        int top = ClientSize.Height - BandHeight + (BandHeight - size) / 2;
        int right = NotificationAreaRight;

        using var ink = new Pen(Color.FromArgb(48, 48, 48), Math.Max(size / 12f, 1.5f));
        using var solid = new SolidBrush(Color.FromArgb(48, 48, 48));

        // Volume, closest to the clock.
        right -= size;
        if (!DrawGlyph(canvas, GlyphVolume, solid, new Rectangle(right, top, size, size)))
            DrawVolume(canvas, ink, solid, new Rectangle(right, top, size, size));

        // Network.
        right -= size + gap;
        if (!DrawGlyph(canvas, GlyphNetwork, solid, new Rectangle(right, top, size, size)))
            DrawNetwork(canvas, ink, new Rectangle(right, top, size, size));

        // This app, where a third-party icon belongs.
        right -= size + gap;
        if (_trayIcon != null)
            canvas.DrawIcon(_trayIcon, new Rectangle(right, top, size, size));

        // A cloud, standing in for the sync client every Windows desktop
        // carries. A generic shape rather than a particular product's icon:
        // this app's own icon should not sit alone in a notification area no
        // real desktop ever has, but which cloud it is does not matter.
        right -= size + gap;
        if (!DrawGlyph(canvas, GlyphCloud, solid, new Rectangle(right, top, size, size)))
            DrawCloud(canvas, ink, new Rectangle(right, top, size, size));

        // The chevron that opens the icons Windows keeps hidden.
        right -= size + gap;
        if (!DrawGlyph(canvas, GlyphChevronUp, solid, new Rectangle(right, top, size, size)))
            DrawChevron(canvas, ink, new Rectangle(right, top, size, size));
    }

    // ── The system icon font ─────────────────────────────────────────────

    // Windows 11 draws its own shell glyphs from this font, and Windows 10
    // from the older one. Using it means the picture shows the same shapes a
    // person sees, without copying any pixels off the running shell: the app
    // asks for a character, exactly as any Windows program does.
    private const string FluentIcons = "Segoe Fluent Icons";
    private const string LegacyIcons = "Segoe MDL2 Assets";

    private const string GlyphSearch = "";
    private const string GlyphChevronUp = "";
    private const string GlyphCloud = "";
    private const string GlyphNetwork = "";
    private const string GlyphVolume = "";

    private static string? _iconFamily;

    // The installed icon font, or null on a machine that has neither. Looked
    // up once: enumerating families is slow enough to matter when it happens
    // per glyph, per picture, across a shoot of sixty-four.
    private static string? IconFamily()
    {
        if (_iconFamily != null)
            return _iconFamily.Length == 0 ? null : _iconFamily;

        _iconFamily = "";
        foreach (var candidate in new[] { FluentIcons, LegacyIcons })
        {
            try
            {
                using var family = new FontFamily(candidate);
                _iconFamily = candidate;
                break;
            }
            catch (ArgumentException)
            {
                // Not installed; try the next one.
            }
        }

        return _iconFamily.Length == 0 ? null : _iconFamily;
    }

    // Draw one shell glyph centered in the box. Returns false when no icon
    // font is present, so the caller can fall back to drawing the shape.
    private static bool DrawGlyph(Graphics canvas, string glyph, Brush ink, Rectangle box)
    {
        string? family = IconFamily();
        if (family == null)
            return false;

        using var font = new Font(family, box.Height * 0.62f, GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        canvas.DrawString(glyph, font, ink, box, format);
        return true;
    }

    // A speaker with two waves.
    private static void DrawVolume(Graphics canvas, Pen ink, Brush solid, Rectangle box)
    {
        float unit = box.Width / 8f;
        var cone = new[]
        {
            new PointF(box.X + unit, box.Y + unit * 3),
            new PointF(box.X + unit * 2.4f, box.Y + unit * 3),
            new PointF(box.X + unit * 4, box.Y + unit * 1.4f),
            new PointF(box.X + unit * 4, box.Y + unit * 6.6f),
            new PointF(box.X + unit * 2.4f, box.Y + unit * 5),
            new PointF(box.X + unit, box.Y + unit * 5),
        };
        canvas.FillPolygon(solid, cone);

        for (int wave = 1; wave <= 2; wave++)
        {
            float span = unit * wave * 1.6f;
            canvas.DrawArc(ink,
                box.X + unit * 4 - span / 2, box.Y + unit * 4 - span,
                span * 1.4f, span * 2, -60, 120);
        }
    }

    // Three arcs over a dot: the shape everyone reads as a network.
    private static void DrawNetwork(Graphics canvas, Pen ink, Rectangle box)
    {
        float unit = box.Width / 8f;
        float baseY = box.Y + unit * 6;

        for (int arc = 1; arc <= 3; arc++)
        {
            float span = unit * arc * 2f;
            canvas.DrawArc(ink,
                box.X + box.Width / 2f - span, baseY - span,
                span * 2, span * 2, 210, 120);
        }

        using var dot = new SolidBrush(ink.Color);
        canvas.FillEllipse(dot, box.X + box.Width / 2f - unit / 2, baseY - unit / 2, unit, unit);
    }

    // Three overlapping circles on a flat base: the outline everyone reads as
    // a cloud, at any size.
    private static void DrawCloud(Graphics canvas, Pen ink, Rectangle box)
    {
        float unit = box.Width / 8f;
        using var shape = new GraphicsPath();
        shape.AddArc(box.X + unit, box.Y + unit * 3.4f, unit * 2.4f, unit * 2.4f, 90, 180);
        shape.AddArc(box.X + unit * 2, box.Y + unit * 2.2f, unit * 2.6f, unit * 2.6f, 180, 180);
        shape.AddArc(box.X + unit * 3.8f, box.Y + unit * 3f, unit * 3f, unit * 3f, 240, 190);
        shape.CloseFigure();

        canvas.DrawPath(ink, shape);
    }

    private static void DrawChevron(Graphics canvas, Pen ink, Rectangle box)
    {
        float unit = box.Width / 8f;
        canvas.DrawLines(ink, new[]
        {
            new PointF(box.X + unit * 2, box.Y + unit * 5),
            new PointF(box.X + unit * 4, box.Y + unit * 3),
            new PointF(box.X + unit * 6, box.Y + unit * 5),
        });
    }

    // Two lines, both right-aligned to the wider one. Centering them does not
    // look like Windows. The width changes with the language, so each line is
    // placed from its measured width rather than a fixed left edge.
    private void PaintClock(Graphics canvas)
    {
        var (time, date) = ClockText();

        using var font = ClockFont();
        using var ink = new SolidBrush(Color.FromArgb(28, 28, 28));

        int right = ClientSize.Width - ClockRightMargin;
        int middle = ClientSize.Height - BandHeight / 2;
        int lineHeight = (int)Math.Ceiling(font.GetHeight(canvas));

        DrawRightAligned(canvas, time, font, ink, right, middle - lineHeight);
        DrawRightAligned(canvas, date, font, ink, right, middle);
    }

    // Align to the right edge of a box rather than by subtracting a measured
    // width. MeasureString pads both ends of a string, and the padding differs
    // between "10:08" and "2026/08/10", which left the two lines a few pixels
    // out of line with each other.
    private void DrawRightAligned(
        Graphics canvas, string text, Font font, Brush ink, int right, int top)
    {
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Far,
        };

        canvas.DrawString(text, font, ink,
            new RectangleF(0, top, right, BandHeight), format);
    }

    // The marketing line is a translated string like any other. Burning it
    // into an image by hand would mean opening a graphics editor for every
    // new language.
    private void PaintMarketingLine(Graphics canvas)
    {
        if (string.IsNullOrEmpty(_copy.Headline))
            return;

        int margin = ClientSize.Width / 16;
        int wrapWidth = ClientSize.Width / 2;

        using var headlineFont = new Font("Segoe UI", ClientSize.Height / 26f, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", ClientSize.Height / 52f);
        using var headlineInk = new SolidBrush(Color.White);
        using var bodyInk = new SolidBrush(Color.FromArgb(214, 224, 238));

        canvas.DrawString(_copy.Headline, headlineFont, headlineInk,
            new RectangleF(margin, margin, wrapWidth, ClientSize.Height / 3f));

        float used = canvas.MeasureString(_copy.Headline, headlineFont, wrapWidth).Height;
        canvas.DrawString(_copy.Body, bodyFont, bodyInk,
            new RectangleF(margin, margin + used + 12, wrapWidth, ClientSize.Height / 3f));
    }

    // Measured on a real taskbar: a pinned icon is 47 pixels in a band of 96.
    private int IconSize => Math.Max(BandHeight * 49 / 100, 16);

    // The real taskbar's height, scaled to this picture. The screen is wider
    // than the picture, so a band drawn at its measured pixel height would
    // look far too thick: the picture has to shrink it by the same ratio it
    // shrinks the desktop.
    private int BandHeight => Math.Max(
        _shell.Height * ClientSize.Width / Math.Max(_shell.ScreenWidth, 1), 16);

    // The backdrop, found by walking up from the running binary to the
    // repository. It is a development input like the shot list, not something
    // the product ships, so it is not embedded in the executable.
    private static Bitmap? LoadWallpaper()
    {
        var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            string folder = System.IO.Path.Combine(
                directory.FullName, "store-shots", "wallpaper");

            if (System.IO.Directory.Exists(folder))
            {
                foreach (var file in System.IO.Directory.GetFiles(folder, "*.png"))
                {
                    try
                    {
                        using var stream = System.IO.File.OpenRead(file);
                        using var loaded = Image.FromStream(stream);
                        return new Bitmap(loaded);
                    }
                    catch (Exception)
                    {
                        // Try the next file rather than lose the shoot.
                    }
                }
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static Icon? LoadTrayIcon()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(
            "WindowResizeCapture.Resources.app.ico");
        return stream == null ? null : new Icon(stream, new Size(64, 64));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _wallpaper?.Dispose();
            _trayIcon?.Dispose();
            foreach (var icon in _shell.PinnedIcons)
                icon.Dispose();
        }
        base.Dispose(disposing);
    }
}
