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

    // Measured on a real taskbar: the clock's text stops 0.42 of the band's
    // height from the screen's edge, and stands 0.35 clear of the icons beside
    // it. Both were fixed pixel counts before, which held at one band height
    // and at no other.
    private int ClockRightMargin => Math.Max(BandHeight * 42 / 100, 8);
    private int ClockGap => Math.Max(BandHeight * 35 / 100, 6);

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
        // A file named for this picture stands in for the one this language
        // keeps in store-shots/copy, so wording can be tried without editing
        // the copy a shoot uses.
        _copy = string.IsNullOrEmpty(request.SourcePath)
            ? StudioCopy.Load(request.Language, request.View)
            : StudioCopy.Read(request.SourcePath, request.View);
        _shell = StudioShell.Read();
        _trayIcon = LoadTrayIcon();

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Size = request.Size;

        // Sit in the screen's corner where the real tray is: the bottom right,
        // or the bottom left in a language Windows mirrors its taskbar for.
        // Menus open away from the edge they are near, so from that corner the
        // tray menu unfolds up and inward, into the picture. Staged in the
        // opposite corner it unfolded outward and the camera, which only copies
        // the set's own rectangle, cut it off.
        var screen = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(
            Mirrored ? screen.Left : screen.Right - request.Size.Width,
            screen.Bottom - request.Size.Height);
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(16, 32, 58);

        // The set is scenery, never something the operator interacts with.
        Text = $"{App.Name} studio set";
    }

    // Windows mirrors its whole taskbar for a right-to-left language: Start and
    // the search box move to the right end, the notification area and the clock
    // to the left, and the tray menu therefore unfolds up and to the right. The
    // set follows, because a listing picture for Arabic showing a left-to-right
    // taskbar is a picture of a desktop that reader has never seen.
    internal bool Mirrored => _language.TextInfo.IsRightToLeft;

    // Which way the tray menu opens: inward from the edge the tray sits at, so
    // it unfolds into the picture rather than off it.
    //
    // These names are not turned round by a language that reads right to left.
    // They say where the menu goes on screen, so the mirrored set - whose tray
    // is at the left end of the band - is the one that asks for Right. Asking
    // for Left there put the menu's own left edge outside the picture and the
    // camera cut its first column off.
    internal ToolStripDropDownDirection MenuDirection => Mirrored
        ? ToolStripDropDownDirection.AboveRight
        : ToolStripDropDownDirection.AboveLeft;

    // Which way each level below the top one opens.
    //
    // Left to itself, a submenu opens to the right of the item it belongs to
    // and only turns back when the screen edge leaves it no room. The tray
    // stands close to that edge, so the second level still fitted, the third
    // did not, and it turned back onto the first - which left the picture with
    // no top-level menu in it at all. The reader could not see that any of
    // this starts from the icon in the notification area.
    //
    // Naming the direction instead makes every level turn the same way, so the
    // four panels stand side by side. Windows itself does this whenever a tray
    // menu opens with a wide submenu near the edge; what it does not do is pick
    // the turn one level too late.
    internal ToolStripDropDownDirection SubmenuDirection => Mirrored
        ? ToolStripDropDownDirection.Right
        : ToolStripDropDownDirection.Left;

    // The point the tray menu should open from: just above this app's own icon
    // in the band, in screen coordinates. Taken from the same slot the icon is
    // drawn in, so the menu cannot drift away from the icon it belongs to.
    // Sit clear of the band rather than on its edge: a menu opened exactly at
    // the top of the taskbar had its last item cut off by it.
    internal Point TrayIconAnchor
    {
        get
        {
            var slot = NotificationSlot(TrayIconPlace);
            return PointToScreen(new Point(
                slot.Left + slot.Width / 2,
                ClientSize.Height - BandHeight - BandHeight / 4));
        }
    }

    // The order the notification area is laid out in, counting away from the
    // clock. This app's own icon sits where a third-party one belongs.
    private const int VolumePlace = 0;
    private const int NetworkPlace = 1;
    private const int TrayIconPlace = 2;
    private const int CloudPlace = 3;
    private const int ChevronPlace = 4;

    // Measured on a real taskbar: a notification glyph is 0.33 of the band, so
    // it is smaller than a pinned icon.
    private int NotificationIconSize => Math.Max(BandHeight * 33 / 100, 12);

    // Where one place in the notification area is drawn.
    //
    // The spacing is not one number. Volume and network belong to the same group
    // and sit 0.17 of the band apart; every other neighbour stands 0.47 away.
    // Drawn all at 0.17, the row came out packed tighter than any real
    // notification area.
    private Rectangle NotificationSlot(int place)
    {
        int size = NotificationIconSize;
        int tight = Math.Max(BandHeight * 17 / 100, 4);
        int loose = Math.Max(BandHeight * 47 / 100, 6);

        int offset = 0;
        for (int step = 0; step < place; step++)
            offset += size + (step == VolumePlace ? tight : loose);

        int left = Mirrored
            ? NotificationAreaInnerEdge + offset
            : NotificationAreaInnerEdge - offset - size;

        return new Rectangle(
            left, ClientSize.Height - BandHeight + (BandHeight - size) / 2, size, size);
    }

    // Where the row of notification icons begins, beside the clock: its right
    // end on a left-to-right desktop, its left end on a mirrored one. The clock
    // is the widest thing in this corner and its width changes with the
    // language, so everything else is placed from here.
    private int NotificationAreaInnerEdge => Mirrored
        ? ClockRightMargin + ClockWidth + ClockGap
        : ClientSize.Width - ClockRightMargin - ClockWidth - ClockGap;

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
        PaintNotice(canvas);
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
        // Measured on a real taskbar: pinned icons repeat every 0.92 of the
        // band, which at 0.49 each leaves 0.43 between them. The search box is
        // closer to its neighbours than that - 0.28 on either side - so Start,
        // the box and the icons do not read as one evenly spaced row.
        int size = IconSize;
        int gap = BandHeight * 43 / 100;
        int searchGap = BandHeight * 28 / 100;
        int bandTop = ClientSize.Height - BandHeight;
        int y = bandTop + (BandHeight - size) / 2;

        // The placeholder text is smaller than it first appeared: an earlier
        // measurement of 0.42 had swept in the magnifier and the pill's own
        // outline along with the letters. Given in pixels, because a size in
        // points would then have to be converted twice.
        using var searchFont = new Font("Segoe UI", BandHeight * 0.27f, GraphicsUnit.Pixel);
        string label = StudioSearchLabel.For(_language);
        int searchWidth = SearchBoxWidth(canvas, label, searchFont);

        // The row's own width, with no gap hanging off its far end: counting one
        // left the whole cluster sitting half a gap to one side of centre.
        int icons = _shell.PinnedIcons.Length;
        int iconsWidth = icons * size + Math.Max(icons - 1, 0) * gap;
        int total = size + searchGap + searchWidth + searchGap + iconsWidth;
        int clusterLeft = (ClientSize.Width - total) / 2;

        // The row is walked in reading order - Start, the search box, then the
        // pinned icons - and each slot turned into a left edge afterwards. On a
        // mirrored desktop reading order runs the other way, which is the whole
        // of what makes Start sit at the right end.
        int walked = 0;
        Rectangle Slot(int slotWidth, int slotTop, int slotHeight)
        {
            int left = Mirrored
                ? clusterLeft + total - walked - slotWidth
                : clusterLeft + walked;
            walked += slotWidth;
            return new Rectangle(left, slotTop, slotWidth, slotHeight);
        }

        DrawStart(canvas, Slot(size, y, size));
        walked += searchGap;

        // Measured on a real taskbar: the pill stands 0.62 of the band tall,
        // centred in it. At the 0.46 it was drawn at before, it read as a thin
        // slot rather than the box a person types into.
        PaintSearchBox(canvas, label, searchFont,
            Slot(searchWidth, bandTop + BandHeight * 19 / 100, BandHeight * 62 / 100));
        walked += searchGap;

        foreach (var icon in _shell.PinnedIcons)
        {
            canvas.DrawIcon(icon, Slot(size, y, size));
            walked += gap;
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
    //
    // Measured off a real taskbar, where the logo is a square 0.479 of the band:
    // each pane is 0.478 of that square and the gutter between them 0.043. The
    // gutter had been drawn at 0.10, twice as wide as it is, which is what made
    // the real logo look chunkier than this one beside it.
    //
    // The blue is not flat either. One gradient crosses the whole logo, light at
    // the top left and deeper at the bottom right - the top right and bottom
    // left panes come out the same shade, which is how a single diagonal is
    // recognizable rather than four separately shaded squares.
    private static void DrawStart(Graphics canvas, Rectangle box)
    {
        float pane = box.Width * 0.478f;
        float gutter = box.Width * 0.043f;

        using var blue = new LinearGradientBrush(
            box, Color.FromArgb(74, 206, 253), Color.FromArgb(1, 121, 212), 45f);

        canvas.FillRectangle(blue, box.X, box.Y, pane, pane);
        canvas.FillRectangle(blue, box.X + pane + gutter, box.Y, pane, pane);
        canvas.FillRectangle(blue, box.X, box.Y + pane + gutter, pane, pane);
        canvas.FillRectangle(blue, box.X + pane + gutter, box.Y + pane + gutter, pane, pane);
    }

    private void PaintSearchBox(Graphics canvas, string label, Font font, Rectangle box)
    {
        int radius = box.Height / 2;
        using var shape = new GraphicsPath();
        shape.AddArc(box.X, box.Y, radius * 2, radius * 2, 90, 180);
        shape.AddArc(box.Right - radius * 2, box.Y, radius * 2, radius * 2, 270, 180);
        shape.CloseFigure();

        // The outline is 0.02 of the band thick. A one-pixel pen was a hairline
        // at the size the band is drawn, and the pill lost its edge entirely
        // where the wallpaper behind it was light.
        using var fill = new SolidBrush(Color.FromArgb(252, 252, 252));
        using var edge = new Pen(
            Color.FromArgb(196, 196, 196), Math.Max(BandHeight / 48f, 1f));
        canvas.FillPath(fill, shape);
        canvas.DrawPath(edge, shape);

        // A magnifier drawn from a circle and a handle, so no glyph font has
        // to be present on the machine taking the picture. It stands 0.42 of
        // the pill, which is 0.26 of the band, as the real one does.
        int glyph = box.Height * 42 / 100;
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

    // The notification area, counting outward from the clock the way Windows 11
    // does: volume and network, then third-party icons, then the chevron that
    // hides the rest. This app's own icon sits among the third-party ones. Which
    // way "outward" runs is NotificationSlot's business, so a mirrored taskbar
    // needs nothing here.
    //
    // Volume and network are drawn from shapes rather than copied off the real
    // taskbar. The shell owns those glyphs; nothing can extract them the way
    // an application icon can be extracted, and lifting the pixels would put a
    // picture of Windows back into this project.
    private void PaintNotificationArea(Graphics canvas)
    {
        using var ink = new Pen(
            Color.FromArgb(48, 48, 48), Math.Max(NotificationIconSize / 12f, 1.5f));
        using var solid = new SolidBrush(Color.FromArgb(48, 48, 48));

        var volume = NotificationSlot(VolumePlace);
        if (!DrawGlyph(canvas, GlyphVolume, solid, volume))
            DrawVolume(canvas, ink, solid, volume);

        var network = NotificationSlot(NetworkPlace);
        if (!DrawGlyph(canvas, GlyphNetwork, solid, network))
            DrawNetwork(canvas, ink, network);

        if (_trayIcon != null)
            canvas.DrawIcon(_trayIcon, NotificationSlot(TrayIconPlace));

        // A cloud, standing in for the sync client every Windows desktop
        // carries. A generic shape rather than a particular product's icon:
        // this app's own icon should not sit alone in a notification area no
        // real desktop ever has, but which cloud it is does not matter.
        var cloud = NotificationSlot(CloudPlace);
        if (!DrawGlyph(canvas, GlyphCloud, solid, cloud))
            DrawCloud(canvas, ink, cloud);

        // The chevron that opens the icons Windows keeps hidden.
        var chevron = NotificationSlot(ChevronPlace);
        if (!DrawGlyph(canvas, GlyphChevronUp, solid, chevron))
            DrawChevron(canvas, ink, chevron);
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

        // The glyph is asked for at the box's full height, not a fraction of it.
        // These fonts draw their icons across the whole em, so a font two thirds
        // of the box left ink two thirds of the size the taskbar was measured
        // at - and the gaps between icons looked wider than they are for the
        // same reason.
        using var font = new Font(family, box.Height, GraphicsUnit.Pixel);
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

    // Two lines, both aligned to the screen's outer edge - the right on a
    // left-to-right desktop, the left on a mirrored one, where the clock stands
    // in the opposite corner. Centering them does not look like Windows.
    private void PaintClock(Graphics canvas)
    {
        var (time, date) = ClockText();

        using var font = ClockFont();
        using var ink = new SolidBrush(Color.FromArgb(28, 28, 28));

        int middle = ClientSize.Height - BandHeight / 2;
        // The two lines sit 0.33 of the band apart, measured rather than taken
        // from the font, whose reported height carries leading the real clock
        // does not use.
        int lineHeight = (int)Math.Round(BandHeight * 0.33f);

        DrawClockLine(canvas, time, font, ink, middle - lineHeight);
        DrawClockLine(canvas, date, font, ink, middle);
    }

    // Align to the outer edge of a box rather than by subtracting a measured
    // width. MeasureString pads both ends of a string, and the padding differs
    // between "10:08" and "2026/08/10", which left the two lines a few pixels
    // out of line with each other.
    //
    // A mirrored desktop also gets the reading direction, so that a language
    // whose clock carries a word - Arabic's ص for the morning - puts it where
    // Windows puts it rather than where a left-to-right layout would.
    private void DrawClockLine(Graphics canvas, string text, Font font, Brush ink, int top)
    {
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Far,
        };

        if (Mirrored)
        {
            // With the reading direction reversed, "far" is the left edge.
            format.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            canvas.DrawString(text, font, ink, new RectangleF(
                ClockRightMargin, top, ClientSize.Width - ClockRightMargin, BandHeight), format);
            return;
        }

        canvas.DrawString(text, font, ink, new RectangleF(
            0, top, ClientSize.Width - ClockRightMargin, BandHeight), format);
    }

    // The marketing line is a translated string like any other. Burning it
    // into an image by hand would mean opening a graphics editor for every
    // new language.
    // Keep the marketing line clear of whatever the pose put on the set. A
    // menu four levels deep reaches a long way up and to the left, and text
    // running underneath it is worse than text that had to wrap early.
    //
    // One rectangle per window the pose opened, not the one box around them all.
    // An open menu is a staircase: the list of sizes stands tall and the three
    // levels beside it sit low, so the box around the four of them claims a
    // large empty area to the right of the sizes that a paragraph fits in. Five
    // pictures went out with their text crushed into the strip above that box
    // while that area sat empty.
    internal void Reserve(IReadOnlyList<Rectangle> screenAreas)
    {
        var areas = new Rectangle[screenAreas.Count];
        for (int index = 0; index < screenAreas.Count; index++)
            areas[index] = RectangleToClient(screenAreas[index]);

        _reserved = areas;
        Invalidate();
        Update();
    }

    private IReadOnlyList<Rectangle> _reserved = Array.Empty<Rectangle>();

    // The box one block of text gets: as wide as the set allows, less the
    // same margin on the right that it has on the left, and pulled in further
    // where the pose has put something beside it.
    //
    // Measured twice. How tall the text stands depends on how wide the box
    // is, and whether the obstacle is beside the text depends on how tall it
    // stands, so the first pass guesses with the full width and the second
    // settles it.
    private float DrawBlock(
        Graphics canvas, string text, Font font, Brush ink, int margin, int top,
        float lineHeight)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var box = BoxBeside(margin, top, top + (int)font.GetHeight(canvas));
        var lines = StudioText.Wrap(canvas, text, font, box.width);

        float height = lines.Count * font.GetHeight(canvas) * lineHeight;
        box = BoxBeside(margin, top, top + (int)Math.Ceiling(height));

        // Settle the size against the box the block ended up with, then lay it
        // out again at that size.
        using var fitted = StudioText.FitToBox(
            canvas, text, font, box.width, RoomBelow(margin, top, box), lineHeight);
        lines = StudioText.Wrap(canvas, text, fitted, box.width);

        return StudioText.Draw(canvas, lines, fitted, ink, box.left, top,
            box.width, Mirrored, lineHeight);
    }

    // The box a block of text gets, as a left edge and a width: the set less its
    // margins, pulled in to whichever side of the pose has more room for it.
    private (int left, int width) BoxBeside(int margin, int top, int bottom)
    {
        int left = margin;
        int right = ClientSize.Width - margin;

        // Only what the block would actually run into: the windows the pose
        // opened that stand at this height. A menu level lower down the frame
        // than the block leaves it the whole width.
        int inTheWay = int.MaxValue;
        int pastTheWay = int.MinValue;

        foreach (var area in _reserved)
        {
            if (area.IsEmpty || area.Bottom <= top || area.Top >= bottom)
                continue;

            inTheWay = Math.Min(inTheWay, area.Left);
            pastTheWay = Math.Max(pastTheWay, area.Right);
        }

        if (inTheWay != int.MaxValue)
        {
            // A pose that stands in the middle of the frame leaves a column on
            // each side of it. Both are measured and the wider one is kept.
            //
            // Only one was measured before - the one the reading starts at - and
            // that side is regularly the sliver while the other holds the whole
            // sentence. Searching from one end only cannot see the larger room
            // behind the obstacle it stopped at.
            int leftColumn = Math.Min(right, inTheWay - margin) - left;
            int rightLeft = Math.Max(left, pastTheWay + margin);
            int rightColumn = right - rightLeft;

            // A tie goes to the side the reading starts at, which is where a
            // block belongs whenever the pose leaves it a choice.
            bool takeRight = Mirrored
                ? rightColumn >= leftColumn
                : rightColumn > leftColumn;

            int keptLeft = takeRight ? rightLeft : left;
            int keptWidth = takeRight ? rightColumn : leftColumn;

            // Step aside only while a sentence still fits in what is left. The
            // tray menu unfolds across most of the set, and the strip beside it
            // is narrower than a single word of Arabic; the block is better off
            // keeping the full width and standing above the menu, which is where
            // it starts from anyway.
            //
            // Widening a collapsed box instead, which is what this did before,
            // kept the edge the obstacle had pushed the text to. The Arabic body
            // was pushed right and then made wide enough to run off the picture.
            if (keptWidth >= ClientSize.Width / 3)
                return (keptLeft, keptWidth);
        }

        return (left, right - left);
    }

    // How far down a block of text may run: to the top of the taskbar when the
    // block has the frame to itself, and only as far as the pose when the pose
    // is underneath it rather than beside it.
    //
    // A block that has already stepped aside keeps the full height, because
    // what it stepped aside from is no longer in its way.
    private float RoomBelow(int margin, int top, (int left, int width) box)
    {
        int floor = ClientSize.Height - BandHeight - margin;

        foreach (var area in _reserved)
        {
            bool beside = box.left + box.width <= area.Left || box.left >= area.Right;
            if (area.IsEmpty || beside)
                continue;

            floor = Math.Min(floor, area.Top - margin / 2);
        }

        // Never nothing. A pose that reaches almost to the top of the picture
        // would otherwise shrink the text away rather than crowd it.
        return Math.Max(floor - top, ClientSize.Height / 20f);
    }

    // The margin the whole picture keeps: the height of the taskbar band, so
    // one measurement governs the frame the same way it governs the desktop
    // inside it. A sixteenth of the width before, which started the text
    // further in than the band it sits above.
    internal int PictureMargin => BandHeight;

    private void PaintMarketingLine(Graphics canvas)
    {
        if (string.IsNullOrEmpty(_copy.Headline))
            return;

        int margin = PictureMargin;

        using var headlineFont = new Font("Segoe UI", ClientSize.Height / 26f, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", ClientSize.Height / 52f);
        using var headlineInk = new SolidBrush(Color.White);
        using var bodyInk = new SolidBrush(Color.FromArgb(214, 224, 238));

        // Each block runs as far right as it can. What stops it is whatever
        // the pose put on the set, and only where that thing actually sits
        // beside the block: a menu low in the frame leaves the headline the
        // full width and only narrows the paragraph beneath it.
        //
        // The headline's lines sit closer together than the paragraph's. At the
        // size a headline is drawn, the leading the typeface carries is already
        // more air than it needs.
        float used = DrawBlock(
            canvas, _copy.Headline, headlineFont, headlineInk, margin, margin, 0.85f);

        // The gap between the two blocks is its own measurement, not what is
        // left over from the headline's leading. Closing the headline's lines up
        // took that leftover away and left the paragraph sitting on top of the
        // headline; the space between them belongs here, where it can be seen.
        int bodyTop = (int)(margin + used + ClientSize.Height / 20f);
        DrawBlock(canvas, _copy.Body, bodyFont, bodyInk, margin, bodyTop, 0.95f);
    }

    // The note that says the desktop is drawn and not photographed: the
    // wallpaper, the taskbar, and the windows the menu lists. The app's own menu
    // and settings window in these pictures are the real ones, and the note is
    // what keeps the difference honest.
    //
    // It sits in the top corner the reading ends at - the right on a
    // left-to-right set, the left on a mirrored one - inside a strip as tall as
    // the taskbar, which is the margin the picture keeps all round. Small and
    // quiet: a listing has to say what it shows without competing with it.
    //
    // The bottom corner it had before is the corner the tray menu unfolds into.
    // Four levels deep, the menu reached across the note and cut it off in the
    // middle of a word. Up here nothing else is drawn in any language, because
    // the headline starts where this strip ends.
    private void PaintNotice(Graphics canvas)
    {
        string notice = StudioCopy.Notice(_language);
        if (notice.Length == 0)
            return;

        int margin = PictureMargin;

        using var font = new Font("Segoe UI", ClientSize.Height / 100f);
        using var ink = new SolidBrush(Color.FromArgb(150, 255, 255, 255));
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            // Far is the side the reading ends at, which the reversed direction
            // moves to the left on its own.
            Alignment = StringAlignment.Far,
        };

        if (Mirrored)
            format.FormatFlags |= StringFormatFlags.DirectionRightToLeft;

        // Centered in the top margin. That margin is the band's height, so the
        // note stands in a strip the same depth as the taskbar at the other end
        // of the picture.
        float height = font.GetHeight(canvas);
        canvas.DrawString(notice, font, ink, new RectangleF(
            margin,
            (margin - height) / 2f,
            ClientSize.Width - margin * 2,
            height + 1), format);
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

    // The real taskbar's height, as this machine draws it.
    //
    // HeightAt100Percent is that measurement in the units this window works in:
    // the app runs DPI unaware, so a coordinate here is a physical pixel divided
    // by the scaling of the display, which is exactly what dividing the scaling
    // out of the measured taskbar leaves.
    //
    // It used to be worked out from the width of the picture instead, against a
    // 1920-wide reference desktop, which made the band four thirds of the
    // taskbar this machine actually has. Nothing else in the picture is sized
    // that way: the menu and the settings window are laid out at the machine's
    // own DPI, and a band derived from the size of the file is a band that
    // matches nothing standing on it.
    private int BandHeight => Math.Max(_shell.Height, 16);

    private static Icon? LoadTrayIcon()
    {
        using var stream = App.OpenIcon();
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
