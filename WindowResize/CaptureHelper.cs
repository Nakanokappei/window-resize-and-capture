using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsResizeCapture;

// Captures the contents of a window after it has been resized, then saves
// it to a file and/or copies it to the clipboard depending on user settings.
// Uses the native PrintWindow API with Per-Monitor DPI awareness to produce
// correct captures even under DPI virtualisation (e.g. Parallels + Retina).
public static class CaptureHelper
{
    // ── Win32 API declarations ───────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    // Per-Monitor V2 awareness: GetWindowRect returns physical pixels
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

    // PW_RENDERFULLCONTENT: captures DWM-composed content including DirectX
    private const uint PW_RENDERFULLCONTENT = 2;

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

    // ── Public API ───────────────────────────────────────────────────────

    // Schedule a delayed capture of the given window. The delay allows the
    // window to finish repainting after the resize. Once captured, the
    // bitmap is scaled to the target preset size and dispatched to the
    // configured destinations (file and/or clipboard).
    public static void CaptureAfterResize(WindowInfo window, PresetSize targetSize, int delayMs = 500)
    {
        var store = SettingsStore.Shared;

        // Bail out early if capture is disabled
        if (!store.CaptureEnabled)
            return;

        // Remember the UI thread's synchronization context: the capture runs
        // on a thread-pool thread, but Clipboard.SetImage requires an STA
        // thread and must be marshalled back here.
        var uiContext = SynchronizationContext.Current;

        // Use a one-shot timer to let the window repaint before capturing
        var timer = new System.Windows.Forms.Timer { Interval = delayMs };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();

            // Run the capture off the UI thread. PrintWindow sends WM_PRINT
            // synchronously to the target window and offers no timeout, so a
            // busy or unresponsive target would otherwise stall our message
            // pump — the shape of hang that WER reports as HANG_QUIESCE.
            Task.Run(() => CaptureAndDispatch(window, targetSize, store, uiContext));
        };
        timer.Start();
    }

    // ── Private helpers ──────────────────────────────────────────────────

    // Capture the window, scale it to the target size and deliver it to the
    // configured destinations. Runs entirely on a thread-pool thread so that
    // a slow PrintWindow cannot block the UI; only the clipboard write is
    // posted back to the UI thread.
    private static void CaptureAndDispatch(
        WindowInfo window, PresetSize targetSize, SettingsStore store, SynchronizationContext? uiContext)
    {
        try
        {
            using var rawCapture = CaptureWindowBitmap(window.Handle, store.CaptureClientArea);
            if (rawCapture == null)
                return;

            // Scale from physical pixels down to the user-specified target size
            using var scaled = new Bitmap(targetSize.Width, targetSize.Height);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(rawCapture, 0, 0, targetSize.Width, targetSize.Height);
            }

            // Dispatch to configured destinations
            if (store.CaptureSaveToFile && !string.IsNullOrEmpty(store.CaptureSaveFolderPath))
                SaveCaptureToFile(scaled, window, store.CaptureSaveFolderPath);

            // Send (not Post) so the bitmap stays alive until the UI thread
            // has finished handing it to the clipboard.
            if (store.CaptureCopyToClipboard)
                uiContext?.Send(_ => Clipboard.SetImage(scaled), null);
        }
        catch { }
    }

    // Capture the window's visual content into a Bitmap using native GDI.
    // Temporarily switches the thread to Per-Monitor V2 DPI awareness so
    // that GetWindowRect returns physical-pixel dimensions, avoiding the
    // quarter-capture bug under DPI virtualisation.
    // In client-only mode the full window is captured (the reliable
    // PW_RENDERFULLCONTENT path) and then cropped to the client area, because
    // PW_CLIENTONLY is ignored when PW_RENDERFULLCONTENT is set.
    private static Bitmap? CaptureWindowBitmap(IntPtr hWnd, bool clientOnly)
    {
        IntPtr prevDpiContext = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        try
        {
            if (!GetWindowRect(hWnd, out RECT windowRect))
                return null;

            int width = windowRect.Right - windowRect.Left;
            int height = windowRect.Bottom - windowRect.Top;
            if (width <= 0 || height <= 0)
                return null;

            // Create a native GDI memory DC compatible with the window's DC,
            // then ask PrintWindow to render the whole window into it.
            IntPtr windowDC = GetDC(hWnd);
            IntPtr memoryDC = CreateCompatibleDC(windowDC);
            IntPtr hBitmap = CreateCompatibleBitmap(windowDC, width, height);
            IntPtr previousBitmap = SelectObject(memoryDC, hBitmap);

            bool success = PrintWindow(hWnd, memoryDC, PW_RENDERFULLCONTENT);
            Bitmap? fullCapture = success ? Image.FromHbitmap(hBitmap) : null;

            // Release all GDI resources in reverse order
            SelectObject(memoryDC, previousBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memoryDC);
            ReleaseDC(hWnd, windowDC);

            if (fullCapture == null || !clientOnly)
                return fullCapture;

            // Crop the full-window bitmap down to the client area.
            using (fullCapture)
                return CropToClientArea(hWnd, fullCapture, windowRect);
        }
        finally
        {
            // Always restore the previous DPI context
            SetThreadDpiAwarenessContext(prevDpiContext);
        }
    }

    // Return the client-area sub-rectangle of a full-window capture. The
    // client origin is located by mapping its (0,0) to screen coordinates and
    // subtracting the window's top-left; all values are physical pixels under
    // the Per-Monitor V2 context active during capture.
    private static Bitmap CropToClientArea(IntPtr hWnd, Bitmap fullCapture, RECT windowRect)
    {
        if (!GetClientRect(hWnd, out RECT clientRect))
            return (Bitmap)fullCapture.Clone();

        int clientWidth = clientRect.Right - clientRect.Left;
        int clientHeight = clientRect.Bottom - clientRect.Top;
        if (clientWidth <= 0 || clientHeight <= 0)
            return (Bitmap)fullCapture.Clone();

        // Locate the client area's top-left corner within the window bitmap
        var clientOrigin = new POINT { X = 0, Y = 0 };
        ClientToScreen(hWnd, ref clientOrigin);
        int offsetX = clientOrigin.X - windowRect.Left;
        int offsetY = clientOrigin.Y - windowRect.Top;

        // Clamp the crop rectangle to the captured bitmap's bounds
        var crop = new Rectangle(offsetX, offsetY, clientWidth, clientHeight);
        crop.Intersect(new Rectangle(0, 0, fullCapture.Width, fullCapture.Height));
        if (crop.Width <= 0 || crop.Height <= 0)
            return (Bitmap)fullCapture.Clone();

        return fullCapture.Clone(crop, fullCapture.PixelFormat);
    }

    // Persist the bitmap as a PNG file using the naming convention
    // MMddHHmmss_ProcessName_WindowTitle.png, truncating overly long
    // titles to avoid filesystem path-length issues.
    private static void SaveCaptureToFile(Bitmap bitmap, WindowInfo window, string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return;

        string timestamp = DateTime.Now.ToString("MMddHHmmss");
        string processName = SanitizeForFilename(window.ProcessName);
        string windowTitle = SanitizeForFilename(window.Title);

        // Cap title length to avoid exceeding MAX_PATH
        if (windowTitle.Length > 50)
            windowTitle = windowTitle[..50];

        string fileName = $"{timestamp}_{processName}_{windowTitle}.png";
        bitmap.Save(Path.Combine(folderPath, fileName), ImageFormat.Png);
    }

    // Strip characters that are illegal in filenames, collapse consecutive
    // underscores, and fall back to "Unknown" for empty results.
    private static string SanitizeForFilename(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Unknown";

        string invalidChars = new string(Path.GetInvalidFileNameChars());
        string pattern = $"[{Regex.Escape(invalidChars)}]";
        string sanitized = Regex.Replace(name, pattern, "_");
        sanitized = Regex.Replace(sanitized, "_+", "_").Trim('_');

        return string.IsNullOrEmpty(sanitized) ? "Unknown" : sanitized;
    }
}
