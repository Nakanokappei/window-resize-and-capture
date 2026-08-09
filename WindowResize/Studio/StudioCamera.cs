using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowResizeCapture.Studio;

// Photographs a window by copying the screen behind it.
//
// The product's own CaptureHelper cannot be reused here. It asks the window to
// draw itself, and a window never draws the menu or the dialog that sits on
// top of it, which is exactly what a listing picture has to show. So the
// studio copies the screen instead, and everything in front of the set comes
// with it.
//
// Two coordinate systems meet in this file. The app runs DPI unaware, so
// WinForms sizes windows in logical pixels, while DWM and a DPI-aware screen
// copy both speak physical pixels. A requested picture size is always
// physical, because that is what lands in the file. Mixing the two is what
// made an early version ask for 1920x1080 and write 3836x2156.
internal static class StudioCamera
{
    // ── Win32 ────────────────────────────────────────────────────────────

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hwnd, int attribute, out RECT value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    // The rectangle a person sees. Form.Bounds is larger: an invisible resize
    // border sits outside the visible frame, and copying that region pulls in
    // whatever is behind the window.
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // Windows 11 rounds corners by drawing outside the window, so a copy of
    // the screen shows the wallpaper in all four corners. Ask for square ones.
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_DONOTROUND = 1;

    // Only this thread becomes DPI aware, and only while it measures and
    // copies. The process stays unaware so that the menu and the settings
    // window keep the fixed pixel layout a user actually sees; making the
    // whole process aware was tried before and broke that layout.
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    // ── Public API ───────────────────────────────────────────────────────

    // Resize the form until the rectangle a person sees measures exactly
    // width x height physical pixels, then copy that rectangle off the screen
    // and save it. Returns the size actually written, which the caller checks.
    internal static async Task<Size> Photograph(
        Form form, int width, int height, string outputPath, int settleMs)
    {
        SquareTheCorners(form.Handle);
        await MatchVisibleSize(form, width, height);

        // Let anything asynchronous finish arriving before the shutter opens.
        await Settle(settleMs);

        return CopyScreenRegion(form.Handle, outputPath);
    }

    // Pump the message loop for the given time. Task.Delay alone would leave
    // the set unpainted, because the form draws on this same thread.
    internal static async Task Settle(int milliseconds)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            await Task.Delay(15);
        }
    }

    // ── Fitting the window to the picture ────────────────────────────────

    // Grow the outer window until the visible frame matches the request.
    //
    // Two corrections at once: the request is in physical pixels but the form
    // is sized in logical ones, so each shortfall is divided by the scale the
    // window is currently drawn at; and the compositor answers a frame late,
    // so a single pass would measure the previous window. An earlier
    // single-pass version left the bottom 25 rows showing what was behind.
    private static async Task MatchVisibleSize(Form form, int width, int height)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var visible = VisibleFrame(form.Handle);
            int widthShort = width - visible.Width;
            int heightShort = height - visible.Height;

            if (widthShort == 0 && heightShort == 0)
                return;

            double scale = EstimateScale(form, visible);
            form.Size = new Size(
                form.Width + (int)Math.Round(widthShort / scale),
                form.Height + (int)Math.Round(heightShort / scale));

            await Settle(80);
        }
    }

    // How many physical pixels the window currently spends per logical one.
    // Measured rather than asked for: a DPI-unaware process is told its
    // display runs at 96 DPI, so every DPI API here would answer 1.
    private static double EstimateScale(Form form, Rectangle visible)
    {
        if (form.Width <= 0 || visible.Width <= 0)
            return 1.0;

        double scale = visible.Width / (double)form.Width;

        // Guard against a nonsense reading rather than resizing the window to
        // something that cannot be undone.
        return scale is > 0.2 and < 8.0 ? scale : 1.0;
    }

    // ── The shutter ──────────────────────────────────────────────────────

    // Copy the visible frame off the screen at full physical resolution.
    private static Size CopyScreenRegion(IntPtr handle, string outputPath)
    {
        IntPtr previous = SetThreadDpiAwarenessContext(PerMonitorAwareV2);
        try
        {
            var visible = VisibleFrame(handle);

            using var picture = new Bitmap(
                visible.Width, visible.Height, PixelFormat.Format32bppArgb);
            using (var canvas = Graphics.FromImage(picture))
            {
                canvas.CopyFromScreen(
                    visible.Left, visible.Top, 0, 0, visible.Size,
                    CopyPixelOperation.SourceCopy);
            }

            picture.Save(outputPath, ImageFormat.Png);
            return picture.Size;
        }
        finally
        {
            if (previous != IntPtr.Zero)
                SetThreadDpiAwarenessContext(previous);
        }
    }

    // Ask the compositor for the rectangle a person actually sees, in
    // physical pixels.
    private static Rectangle VisibleFrame(IntPtr handle)
    {
        int result = DwmGetWindowAttribute(
            handle, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT bounds, Marshal.SizeOf<RECT>());

        // On a system where DWM cannot answer, the outer bounds are the best
        // available guess; the corners will show, and the check on the written
        // size is what tells the operator.
        if (result != 0)
            return Control.FromHandle(handle) is Form form ? form.Bounds : Rectangle.Empty;

        return Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    }

    private static void SquareTheCorners(IntPtr handle)
    {
        int preference = DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(
            handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }
}
