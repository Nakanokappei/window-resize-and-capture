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
    private readonly Icon? _trayIcon;
    private readonly StudioCopy _copy;
    private readonly CultureInfo _language;

    internal StudioSetForm(StudioRequest request)
    {
        _language = request.Language;
        _copy = StudioCopy.Load(request.Language);
        _shell = StudioShell.Read();
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
    internal Point TrayIconAnchor => PointToScreen(new Point(
        ClientSize.Width - TrayIconRightInset,
        ClientSize.Height - _shell.Height));

    // ── Painting ─────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var canvas = e.Graphics;
        canvas.SmoothingMode = SmoothingMode.AntiAlias;
        canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
        canvas.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        PaintWallpaper(canvas);
        PaintTaskbar(canvas);
        PaintPinnedIcons(canvas);
        PaintOwnTrayIcon(canvas);
        PaintClock(canvas);
        PaintMarketingLine(canvas);
    }

    // The wallpaper is the brand's, not the operator's.
    private void PaintWallpaper(Graphics canvas)
    {
        var area = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height - _shell.Height);
        using var wash = new LinearGradientBrush(
            area, Color.FromArgb(23, 48, 87), Color.FromArgb(12, 24, 45), 60f);
        canvas.FillRectangle(wash, area);
    }

    // The band is drawn from three colors sampled off the real taskbar: the
    // light line along its top and the two ends of its vertical gradient. A
    // flat fill would not match, because the real band is a gradient.
    private void PaintTaskbar(Graphics canvas)
    {
        int top = ClientSize.Height - _shell.Height;
        var band = new Rectangle(0, top, ClientSize.Width, _shell.Height);

        using (var fill = new LinearGradientBrush(
            band, _shell.FillTop, _shell.FillBottom, LinearGradientMode.Vertical))
        {
            canvas.FillRectangle(fill, band);
        }

        using var edge = new Pen(_shell.TopLine);
        canvas.DrawLine(edge, 0, top, ClientSize.Width, top);
    }

    // The icons a desktop normally shows, centered the way Windows 11 centers
    // them. They come from the installed programs, so a machine without one
    // simply shows fewer.
    private void PaintPinnedIcons(Graphics canvas)
    {
        if (_shell.PinnedIcons.Length == 0)
            return;

        int size = IconSize;
        int gap = size / 2;
        int total = _shell.PinnedIcons.Length * size + (_shell.PinnedIcons.Length - 1) * gap;
        int x = (ClientSize.Width - total) / 2;
        int y = ClientSize.Height - _shell.Height + (_shell.Height - size) / 2;

        foreach (var icon in _shell.PinnedIcons)
        {
            canvas.DrawIcon(icon, new Rectangle(x, y, size, size));
            x += size + gap;
        }
    }

    // This app's own icon goes where a third-party tray icon sits, left of
    // where the system icons would be.
    private void PaintOwnTrayIcon(Graphics canvas)
    {
        if (_trayIcon == null)
            return;

        int size = IconSize * 2 / 3;
        var spot = new Rectangle(
            ClientSize.Width - TrayIconRightInset,
            ClientSize.Height - _shell.Height + (_shell.Height - size) / 2,
            size, size);

        canvas.DrawIcon(_trayIcon, spot);
    }

    // Two lines, both right-aligned to the wider one. Centering them does not
    // look like Windows. The width changes with the language, so each line is
    // placed from its measured width rather than a fixed left edge.
    private void PaintClock(Graphics canvas)
    {
        var today = DateTime.Now;
        var instant = new DateTime(
            today.Year, today.Month, today.Day, ClockHour, ClockMinute, 0);

        string time = instant.ToString(_language.DateTimeFormat.ShortTimePattern, _language);
        string date = instant.ToString(_language.DateTimeFormat.ShortDatePattern, _language);

        using var font = new Font("Segoe UI", _shell.Height / 6f);
        using var ink = new SolidBrush(Color.FromArgb(28, 28, 28));

        int right = ClientSize.Width - ClockRightMargin;
        int middle = ClientSize.Height - _shell.Height / 2;
        int lineHeight = (int)Math.Ceiling(font.GetHeight(canvas));

        DrawRightAligned(canvas, time, font, ink, right, middle - lineHeight);
        DrawRightAligned(canvas, date, font, ink, right, middle);
    }

    private static void DrawRightAligned(
        Graphics canvas, string text, Font font, Brush ink, int right, int top)
    {
        var size = canvas.MeasureString(text, font);
        canvas.DrawString(text, font, ink, right - size.Width, top);
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

    // Taskbar icons are about half the band's height on Windows 11.
    private int IconSize => Math.Max(_shell.Height / 2, 16);

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
            _trayIcon?.Dispose();
            foreach (var icon in _shell.PinnedIcons)
                icon.Dispose();
        }
        base.Dispose(disposing);
    }
}
