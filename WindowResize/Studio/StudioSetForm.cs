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
    private Bitmap? _wallpaper;
    private readonly Icon? _trayIcon;
    private readonly StudioCopy _copy;
    private readonly CultureInfo _language;

    internal StudioSetForm(StudioRequest request)
    {
        _language = request.Language;
        // Copy given on the command line wins, so a line can be tried without
        // editing a file first.
        var written = StudioCopy.Load(request.Language);
        _copy = new StudioCopy(
            string.IsNullOrEmpty(request.Headline) ? written.Headline : request.Headline,
            string.IsNullOrEmpty(request.Body) ? written.Body : request.Body);
        _shell = StudioShell.Read();
        _trayIcon = LoadTrayIcon();

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Size = request.Size;

        // Sit in the screen's bottom right corner, where the real tray is.
        // Menus open away from the edge they are near, so from here the tray
        // menu unfolds up and to the left, into the picture. Staged in the top
        // left corner instead, it unfolded outward and the camera, which only
        // copies the set's own rectangle, cut it off.
        var screen = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(
            screen.Right - request.Size.Width,
            screen.Bottom - request.Size.Height);
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

    // Measured on a real taskbar: the digits stand 17 pixels tall in a band of
    // 96, which for this face means an em of about a quarter of the band. The
    // earlier ninth left the clock less than half the size it should be, and a
    // notification glyph towered over it.
    private Font ClockFont() =>
        new("Segoe UI", BandHeight * 0.245f, GraphicsUnit.Pixel);

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

        // Computed at exactly the size it is drawn at, so nothing is scaled
        // and no image file has to travel with the project.
        _wallpaper ??= StudioWallpaper.Render(area.Width, area.Height, _language);
        canvas.DrawImageUnscaled(_wallpaper, area.Location);
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

        // The placeholder text is smaller than it first appeared: an earlier
        // measurement of 0.42 had swept in the magnifier and the pill's own
        // outline along with the letters. Given in pixels, because a size in
        // points would then have to be converted twice.
        using var searchFont = new Font("Segoe UI", BandHeight * 0.27f, GraphicsUnit.Pixel);
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

        // The commonest ink color inside the real search box is rgb(93,94,95).
        // An earlier reading of rgb(48,61,64) came from taking the darkest
        // pixel, which belongs to the magnifier rather than to the letters.
        using var text = new SolidBrush(Color.FromArgb(93, 94, 95));
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
        var cloud = new Rectangle(right, top, size, size);
        if (_shell.SyncIcon != null)
            canvas.DrawIcon(_shell.SyncIcon, cloud);
        else if (!DrawGlyph(canvas, GlyphCloud, solid, cloud))
            DrawCloud(canvas, ink, cloud);

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
        // The two lines sit 0.27 of the band apart, measured rather than taken
        // from the font, whose reported height carries leading the real clock
        // does not use.
        int lineHeight = (int)Math.Round(BandHeight * 0.27f);

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
    // Keep the marketing line clear of whatever the pose put on the set. A
    // menu four levels deep reaches a long way up and to the left, and text
    // running underneath it is worse than text that had to wrap early.
    internal void Reserve(Rectangle screenArea)
    {
        _reserved = RectangleToClient(screenArea);
        Invalidate();
        Update();
    }

    private Rectangle _reserved = Rectangle.Empty;

    // The box one block of text gets: as wide as the set allows, less the
    // same margin on the right that it has on the left, and pulled in further
    // where the pose has put something beside it.
    //
    // Measured twice. How tall the text stands depends on how wide the box
    // is, and whether the obstacle is beside the text depends on how tall it
    // stands, so the first pass guesses with the full width and the second
    // settles it.
    private float DrawBlock(
        Graphics canvas, string text, Font font, Brush ink, int margin, int top)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int width = WidthBeside(margin, top, top + (int)font.GetHeight(canvas));
        var lines = StudioText.Wrap(canvas, text, font, width);

        float height = lines.Count * font.GetHeight(canvas) * 1.15f;
        width = WidthBeside(margin, top, top + (int)Math.Ceiling(height));
        lines = StudioText.Wrap(canvas, text, font, width);

        return StudioText.Draw(canvas, lines, font, ink, margin, top,
            width, _language.TextInfo.IsRightToLeft);
    }

    private int WidthBeside(int margin, int top, int bottom)
    {
        int right = ClientSize.Width - margin;

        if (!_reserved.IsEmpty && _reserved.Bottom > top && _reserved.Top < bottom)
            right = Math.Min(right, _reserved.Left - margin);

        // Never collapse to nothing: a sliver of text is worse than text that
        // runs a little close to the window beside it.
        return Math.Max(right - margin, ClientSize.Width / 5);
    }

    private void PaintMarketingLine(Graphics canvas)
    {
        if (string.IsNullOrEmpty(_copy.Headline))
            return;

        int margin = ClientSize.Width / 16;

        using var headlineFont = new Font("Segoe UI", ClientSize.Height / 26f, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", ClientSize.Height / 52f);
        using var headlineInk = new SolidBrush(Color.White);
        using var bodyInk = new SolidBrush(Color.FromArgb(214, 224, 238));

        // Each block runs as far right as it can. What stops it is whatever
        // the pose put on the set, and only where that thing actually sits
        // beside the block: a menu low in the frame leaves the headline the
        // full width and only narrows the paragraph beneath it.
        float used = DrawBlock(canvas, _copy.Headline, headlineFont, headlineInk, margin, margin);

        int bodyTop = (int)(margin + used + ClientSize.Height / 90f);
        DrawBlock(canvas, _copy.Body, bodyFont, bodyInk, margin, bodyTop);
    }

    // Measured on a real taskbar: a pinned icon is 47 pixels in a band of 96.
    private int IconSize => Math.Max(BandHeight * 49 / 100, 16);

    // A listing picture is never shown at its own size: the store scales it
    // down to fit a card. Drawn true to life, the taskbar and the menu come
    // out too small to read there, so the whole desktop is staged at twice
    // scale, as though the screen were half as wide as it is.
    internal const int Magnification = 2;

    // The menu grows less than the desktop around it. At the full doubling it
    // filled the frame and left no room for the marketing line, and a menu
    // four levels deep already stands tall on its own.
    // Less again than the set. At one and a half the menu's own icons were
    // clipped by the rows they sit in, because a row grows with its font while
    // the image beside it does not.
    internal const float MenuMagnification = 1.25f;

    // The real taskbar's height, scaled to this picture. The screen is wider
    // than the picture, so a band drawn at its measured pixel height would
    // look far too thick: the picture shrinks it by the same ratio it shrinks
    // the desktop, then the magnification above brings it back up.
    private int BandHeight => Math.Max(
        _shell.Height * ClientSize.Width * Magnification / Math.Max(_shell.ScreenWidth, 1), 16);

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
            _shell.SyncIcon?.Dispose();
            foreach (var icon in _shell.PinnedIcons)
                icon.Dispose();
        }
        base.Dispose(disposing);
    }
}
