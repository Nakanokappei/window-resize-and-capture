using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WindowsResizeCapture;

// Captures a screenshot of a window after it has been resized, then saves
// it to a file and/or copies it to the clipboard depending on user settings.
// Uses the native PrintWindow API with Per-Monitor DPI awareness to produce
// correct captures even under DPI virtualisation (e.g. Parallels + Retina).
public static class ScreenshotHelper
{
    // ── Win32 API declarations ───────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

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

    // ── Public API ───────────────────────────────────────────────────────

    // Schedule a delayed capture of the given window. The delay allows the
    // window to finish repainting after the resize. Once captured, the
    // bitmap is scaled to the target preset size and dispatched to the
    // configured destinations (file and/or clipboard).
    public static void CaptureAfterResize(WindowInfo window, PresetSize targetSize, int delayMs = 500)
    {
        var store = SettingsStore.Shared;

        // Bail out early if screenshots are disabled
        if (!store.ScreenshotEnabled)
            return;

        // Use a one-shot timer to let the window repaint before capturing
        var timer = new System.Windows.Forms.Timer { Interval = delayMs };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();

            try
            {
                using var rawCapture = CaptureWindowBitmap(window.Handle);
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
                if (store.ScreenshotSaveToFile && !string.IsNullOrEmpty(store.ScreenshotSaveFolderPath))
                    SaveScreenshotToFile(scaled, window, store.ScreenshotSaveFolderPath);

                if (store.ScreenshotCopyToClipboard)
                    Clipboard.SetImage(scaled);
            }
            catch { }
        };
        timer.Start();
    }

    // ── Private helpers ──────────────────────────────────────────────────

    // Capture the window's visual content into a Bitmap using native GDI.
    // Temporarily switches the thread to Per-Monitor V2 DPI awareness so
    // that GetWindowRect returns physical-pixel dimensions, avoiding the
    // quarter-capture bug under DPI virtualisation.
    private static Bitmap? CaptureWindowBitmap(IntPtr hWnd)
    {
        IntPtr prevDpiContext = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        try
        {
            if (!GetWindowRect(hWnd, out RECT rect))
                return null;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0)
                return null;

            // Create a native GDI memory DC compatible with the window's DC,
            // then ask PrintWindow to render into it.
            IntPtr windowDC = GetDC(hWnd);
            IntPtr memoryDC = CreateCompatibleDC(windowDC);
            IntPtr hBitmap = CreateCompatibleBitmap(windowDC, width, height);
            IntPtr previousBitmap = SelectObject(memoryDC, hBitmap);

            bool success = PrintWindow(hWnd, memoryDC, PW_RENDERFULLCONTENT);
            Bitmap? result = success ? Image.FromHbitmap(hBitmap) : null;

            // Release all GDI resources in reverse order
            SelectObject(memoryDC, previousBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memoryDC);
            ReleaseDC(hWnd, windowDC);

            return result;
        }
        finally
        {
            // Always restore the previous DPI context
            SetThreadDpiAwarenessContext(prevDpiContext);
        }
    }

    // Persist the bitmap as a PNG file using the naming convention
    // MMddHHmmss_ProcessName_WindowTitle.png, truncating overly long
    // titles to avoid filesystem path-length issues.
    private static void SaveScreenshotToFile(Bitmap bitmap, WindowInfo window, string folderPath)
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
