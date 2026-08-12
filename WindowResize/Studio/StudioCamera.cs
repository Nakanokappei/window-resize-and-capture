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

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
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

    // Where SetWindowPos puts a window in the z-order, and the three things it
    // is told not to change while doing it.
    private static readonly IntPtr TopOfTopmost = new(-1);
    private const uint NoSize = 0x0001;
    private const uint NoMove = 0x0002;
    private const uint NoActivate = 0x0010;

    // ── Public API ───────────────────────────────────────────────────────

    // Lift a window to the top of the topmost band, asking for nothing else.
    //
    // Show and Activate are not enough. A process nobody clicked to start
    // cannot take the foreground, so the set is left under whatever was
    // already at the top of that band - usually the taskbar, which the check
    // before the shutter then reports as explorer. Moving the window in the
    // z-order needs no such permission.
    //
    // Called for the set before the pose is arranged, and for the pose's own
    // windows once it is. Both are topmost, so which of them a person sees is
    // decided by the order they were last raised in, and the order they were
    // shown in does not settle that on its own.
    internal static void Raise(IntPtr handle) =>
        SetWindowPos(handle, TopOfTopmost, 0, 0, 0, 0, NoMove | NoSize | NoActivate);

    // Ask a window and everything in it to stop drawing the keyboard focus
    // rectangle.
    //
    // Windows draws that dotted box once it has seen a key rather than a mouse,
    // and the studio drives these windows entirely from code. Whether it landed
    // on the General tab or on a check box changed from one shot to the next, so
    // two pictures of the same window in one listing differed by a detail nobody
    // meant to photograph - and it was the last thing keeping a settings picture
    // from being the same file every time it is taken.
    //
    // The message travels up to the top-level window and back down to every
    // child, which is why one call covers the tabs and what is on them.
    internal static void HideFocusCues(IntPtr handle) =>
        SendMessage(handle, WM_CHANGEUISTATE,
            new IntPtr(UIS_SET | (UISF_HIDEFOCUS << 16)), IntPtr.Zero);

    private const int WM_CHANGEUISTATE = 0x0127;
    private const int UIS_SET = 1;
    private const int UISF_HIDEFOCUS = 0x1;

    // The first of these windows that the given one is covering, or null when
    // every one of them is where a person would see it.
    //
    // Each is sampled at its own middle. Logical pixels, because the caller is
    // the message loop this app runs on, where WinForms reports its windows in
    // the same units; entering the aware context here would sample a different
    // point on a scaled display.
    internal static Control? FirstCoveredBy(
        IntPtr coverer, IReadOnlyList<Control> windows)
    {
        foreach (var window in windows)
        {
            var bounds = window.Bounds;
            var middle = new POINT
            {
                X = bounds.Left + bounds.Width / 2,
                Y = bounds.Top + bounds.Height / 2,
            };

            if (WindowFromPoint(middle) == coverer)
                return window;
        }

        return null;
    }

    // Resize the form until the rectangle a person sees measures exactly
    // width x height physical pixels, then copy that rectangle off the screen
    // and save it. Returns the size actually written, which the caller checks.
    internal static async Task<Size> Photograph(
        Form form, int width, int height, string outputPath, int settleMs,
        IReadOnlyList<Control> pose, Action restage)
    {
        SquareTheCorners(form.Handle);
        await MatchVisibleSize(form, width, height);

        // Let anything asynchronous finish arriving before the shutter opens.
        await Settle(settleMs);

        // Then put what the pose opened in front of the set one last time, and
        // look at the screen rather than trust the call.
        //
        // The pose already did this when it was arranged, and it was not enough:
        // everything above pumps messages, and the set's own activation - asked
        // for at startup and refused, because a process nobody clicked cannot
        // take the foreground - arrives during that pumping often enough to
        // matter. It lifts the set inside the topmost band and the menu opened
        // before it drops behind. Four pictures in one pass of sixteen came out
        // that way, all of them missing the top level of the menu.
        Control? covered = null;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            // Looked at before anything is lifted, because lifting is not free:
            // it takes the selection off whatever menu item is carrying the
            // highlight. In the ordinary case nothing is covered and nothing is
            // touched.
            covered = FirstCoveredBy(form.Handle, pose);
            if (covered == null)
                break;

            foreach (var window in pose)
                Raise(window.Handle);

            // And put back what the lift disturbed.
            restage();
            await Settle(120);
        }

        if (covered != null)
        {
            throw new InvalidOperationException(
                $"the set is in front of the {covered.GetType().Name} " +
                $"the pose opened at {covered.Bounds}");
        }

        // Measure, check and copy without leaving the aware context, so the
        // rectangle found clear is exactly the one photographed.
        IntPtr previous = SetThreadDpiAwarenessContext(PerMonitorAwareV2);
        try
        {
            var frame = VisibleFrame(form.Handle);

            // A window that has slipped in front of the set is copied along
            // with it, and the file comes out exactly the right size, so
            // nothing else catches it. This has happened: a browser left open
            // over the set put its own page and the real taskbar into a
            // finished picture.
            string? intruder = WhatIsCovering(frame);
            if (intruder != null)
                throw new InvalidOperationException($"{intruder} is in front of the set");

            return CopyScreenRegion(frame, outputPath);
        }
        finally
        {
            if (previous != IntPtr.Zero)
                SetThreadDpiAwarenessContext(previous);
        }
    }

    // The name of the program covering the set, or null when only this app's
    // own windows are in front of it - the menu and the settings window in a
    // pose belong there and are the point of the picture.
    //
    // The set is sampled at its middle and just inside its corners rather than
    // asked which window is foreground: a window covering a corner is enough
    // to spoil a picture without ever being activated.
    //
    // Physical pixels, so this runs inside the aware context its caller sets.
    private static string? WhatIsCovering(Rectangle frame)
    {
        if (frame.IsEmpty)
            return null;

        int inset = Math.Min(frame.Width, frame.Height) / 20;
        var samples = new[]
        {
            new POINT { X = frame.Left + frame.Width / 2, Y = frame.Top + frame.Height / 2 },
            new POINT { X = frame.Left + inset, Y = frame.Top + inset },
            new POINT { X = frame.Right - inset, Y = frame.Top + inset },
            new POINT { X = frame.Left + inset, Y = frame.Bottom - inset },
            new POINT { X = frame.Right - inset, Y = frame.Bottom - inset },
        };

        uint own = (uint)Environment.ProcessId;
        foreach (var sample in samples)
        {
            GetWindowThreadProcessId(WindowFromPoint(sample), out uint owner);
            if (owner == 0 || owner == own)
                continue;

            return Name(owner);
        }

        return null;
    }

    // Named in the log so the operator knows what to close, not just that
    // something was there.
    private static string Name(uint processId)
    {
        try
        {
            return System.Diagnostics.Process.GetProcessById((int)processId).ProcessName;
        }
        catch (ArgumentException)
        {
            return $"process {processId}";
        }
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

            // A window grows from its top left, which walks the corner the set
            // is staged in away from the screen's own corner. The set is there
            // so that its menus open the way they open from a real tray, so it
            // goes back there after every change of size.
            (form as StudioSetForm)?.PinToTheTrayCorner();

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

    // Copy the given rectangle off the screen at full physical resolution.
    // Physical pixels again, so this too runs inside the caller's context.
    private static Size CopyScreenRegion(Rectangle visible, string outputPath)
    {
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
