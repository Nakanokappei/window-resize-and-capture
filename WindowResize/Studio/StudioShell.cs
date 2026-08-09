using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WindowResizeCapture.Studio;

// Reads the taskbar from the machine the shoot runs on, so that no photograph
// of Windows has to be stored in this repository or shipped with the product.
//
// Everything here is measured or extracted at the moment a picture is taken:
// how tall the taskbar is, what color it is, and the icons of the programs a
// desktop normally shows. The studio then draws its own band from those
// values instead of pasting a captured strip.
internal sealed class StudioShell
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    // Windows 11 keeps its taskbar in a window of this class, the same one the
    // product's own window enumeration skips.
    private const string TaskbarClass = "Shell_TrayWnd";

    // Used only when the taskbar cannot be found, so a shoot on a machine with
    // an autohidden or replaced shell still produces a plausible picture.
    private const int FallbackHeight = 48;

    internal int Height { get; private init; } = FallbackHeight;
    internal Color TopLine { get; private init; } = Color.FromArgb(178, 178, 178);
    internal Color FillTop { get; private init; } = Color.FromArgb(220, 220, 220);
    internal Color FillBottom { get; private init; } = Color.FromArgb(216, 216, 216);
    internal Icon[] PinnedIcons { get; private init; } = Array.Empty<Icon>();

    internal static StudioShell Read()
    {
        var (height, band) = MeasureTaskbar();

        return new StudioShell
        {
            Height = height,
            TopLine = band.top,
            FillTop = band.fillTop,
            FillBottom = band.fillBottom,
            PinnedIcons = ReadPinnedIcons(),
        };
    }

    // Measure the real taskbar and sample three of its colors: the light line
    // along its top edge and the two ends of its vertical gradient. Sampling a
    // color is not the same as keeping a picture: three numbers travel out of
    // this method, and they are re-read on every machine that shoots.
    private static (int height, (Color top, Color fillTop, Color fillBottom) band) MeasureTaskbar()
    {
        var taskbar = FindWindow(TaskbarClass, null);
        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out RECT bounds))
            return (FallbackHeight, (Color.FromArgb(178, 178, 178),
                Color.FromArgb(220, 220, 220), Color.FromArgb(216, 216, 216)));

        int height = Math.Max(bounds.Bottom - bounds.Top, 1);

        // Read one column far from any icon, the same trick the plate used.
        const int quietColumn = 4;
        using var strip = new Bitmap(1, height);
        using (var canvas = Graphics.FromImage(strip))
        {
            canvas.CopyFromScreen(
                bounds.Left + quietColumn, bounds.Top, 0, 0, new Size(1, height));
        }

        return (height, (
            strip.GetPixel(0, 0),
            strip.GetPixel(0, Math.Min(1, height - 1)),
            strip.GetPixel(0, height - 1)));
    }

    // The icons a Windows desktop normally shows, taken from the programs
    // themselves rather than from a picture of a taskbar. A program that is
    // not installed simply contributes no icon.
    private static Icon[] ReadPinnedIcons()
    {
        var found = new System.Collections.Generic.List<Icon>();

        foreach (var path in new[] { ExplorerPath(), EdgePath(), StorePath() })
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                continue;

            try
            {
                var icon = Icon.ExtractAssociatedIcon(path);
                if (icon != null)
                    found.Add(icon);
            }
            catch (Exception)
            {
                // An icon that cannot be read leaves a gap rather than
                // stopping the shoot.
            }
        }

        return found.ToArray();
    }

    private static string ExplorerPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    // Edge registers itself here, which is more reliable than guessing at a
    // versioned folder under Program Files.
    private static string EdgePath() => AppPath("msedge.exe");

    // The Store is a packaged app, so it has no entry of its own to point at.
    // Its executable lives beside its package; when that cannot be reached the
    // icon is simply left out.
    private static string StorePath() => AppPath("WinStore.App.exe");

    private static string AppPath(string executable)
    {
        const string root = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(root + executable);
            return key?.GetValue(null) as string ?? "";
        }
        catch (Exception)
        {
            return "";
        }
    }
}
