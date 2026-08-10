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

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

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

    // How tall the taskbar would be on a display at 100 per cent, which is the
    // 48 pixels Windows 11 uses. The measurement itself is in the physical
    // pixels of whatever display the shoot runs on - 96 at 200 per cent - and a
    // picture drawn from that number came out with a different band on every
    // machine. Dividing the scaling back out is what makes two machines produce
    // the same picture.
    internal int HeightAt100Percent { get; private init; } = FallbackHeight;
    internal Color TopLine { get; private init; } = Color.FromArgb(178, 178, 178);
    internal Color FillTop { get; private init; } = Color.FromArgb(220, 220, 220);
    internal Color FillBottom { get; private init; } = Color.FromArgb(216, 216, 216);
    internal Icon[] PinnedIcons { get; private init; } = Array.Empty<Icon>();

    // The monochrome cloud OneDrive shows in the notification area, taken from
    // the program itself. Null when OneDrive is not installed, in which case
    // the set falls back to the shell's own cloud glyph.
    internal Icon? SyncIcon { get; private init; }

    internal static StudioShell Read()
    {
        var (height, band) = MeasureTaskbar();

        return new StudioShell
        {
            HeightAt100Percent = height,
            TopLine = band.top,
            FillTop = band.fillTop,
            FillBottom = band.fillBottom,
            PinnedIcons = ReadPinnedIcons(),
            SyncIcon = ReadSyncIcon(),
        };
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconEx(
        string file, int index, IntPtr[]? large, IntPtr[]? small, int count);

    // OneDrive ships dozens of icons in one executable: a colored one for the
    // desktop and, among the rest, the flat monochrome cloud it puts in the
    // tray. There is no name to ask for, so the one with the least color in it
    // is the one wanted. Choosing by measurement rather than by index means a
    // OneDrive update that reorders its resources does not silently produce a
    // bright blue cloud in every listing picture.
    private static Icon? ReadSyncIcon()
    {
        string path = OneDrivePath();
        if (string.IsNullOrEmpty(path))
            return null;

        int available = ExtractIconEx(path, -1, null, null, 0);
        if (available <= 0)
            return null;

        Icon? plainest = null;
        double leastColor = double.MaxValue;

        for (int index = 0; index < available; index++)
        {
            var handles = new IntPtr[1];
            if (ExtractIconEx(path, index, handles, null, 1) <= 0 || handles[0] == IntPtr.Zero)
                continue;

            try
            {
                using var candidate = Icon.FromHandle(handles[0]);
                double color = MeanSaturation(candidate);

                if (color < leastColor)
                {
                    leastColor = color;
                    plainest?.Dispose();
                    plainest = (Icon)candidate.Clone();
                }
            }
            catch (Exception)
            {
                // An icon that will not load is simply not a candidate.
            }
            finally
            {
                DestroyIcon(handles[0]);
            }
        }

        // Every icon in the file was colorful, so none of them is the tray's.
        return leastColor < 0.25 ? plainest : null;
    }

    private static double MeanSaturation(Icon icon)
    {
        using var image = icon.ToBitmap();
        double total = 0;
        int counted = 0;

        for (int x = 0; x < image.Width; x += 2)
        {
            for (int y = 0; y < image.Height; y += 2)
            {
                var pixel = image.GetPixel(x, y);
                if (pixel.A <= 40)
                    continue;

                total += pixel.GetSaturation();
                counted++;
            }
        }

        return counted == 0 ? double.MaxValue : total / counted;
    }

    private static string OneDrivePath()
    {
        foreach (var folder in new[]
        {
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
        })
        {
            string candidate = Path.Combine(
                Environment.GetFolderPath(folder), "Microsoft OneDrive", "OneDrive.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        string perUser = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "OneDrive", "OneDrive.exe");

        return File.Exists(perUser) ? perUser : "";
    }

    // Measure the real taskbar, and take its color from the theme rather than
    // from the screen.
    //
    // Sampling the pixels was wrong. The Windows 11 taskbar is translucent, so
    // the wallpaper shows through it: on a machine with a sunset picture the
    // sampled band came out warm pink, and every listing picture would have
    // carried whatever wallpaper the operator happened to be using. The theme
    // is what a reader recognizes as a taskbar.
    // Measure the taskbar and report its height as it would be at 100 per cent.
    //
    // The studio runs DPI aware, so the rectangle comes back in the display's
    // real pixels: the same taskbar measures 48 on one machine and 96 on
    // another. Asking Windows what scaling that window is drawn at, and
    // dividing it back out, leaves a number every machine agrees on.
    private static (int height, (Color top, Color fillTop, Color fillBottom) band) MeasureTaskbar()
    {
        var taskbar = FindWindow(TaskbarClass, null);

        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out RECT bounds))
            return (FallbackHeight, ThemeColors());

        int measured = Math.Max(bounds.Bottom - bounds.Top, 1);
        uint dpi = GetDpiForWindow(taskbar);

        return (dpi > 0 ? measured * 96 / (int)dpi : measured, ThemeColors());
    }

    // The colors Windows 11 paints its taskbar in. When the user has asked for
    // the accent color on the taskbar, that wins; otherwise it is the near
    // neutral that belongs to the light or dark theme.
    private static (Color top, Color fillTop, Color fillBottom) ThemeColors()
    {
        bool light = ReadDword(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme") == 1;

        bool accentOnTaskbar = ReadDword(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "ColorPrevalence") == 1;

        if (accentOnTaskbar)
        {
            int? accent = ReadDword(@"Software\Microsoft\Windows\DWM", "AccentColor");
            if (accent is int value)
            {
                // Stored as ABGR, not ARGB.
                var color = Color.FromArgb(value & 0xFF, (value >> 8) & 0xFF, (value >> 16) & 0xFF);
                return (Lighten(color, 0.12), color, color);
            }
        }

        return light
            ? (Color.FromArgb(229, 229, 229), Color.FromArgb(243, 243, 243), Color.FromArgb(243, 243, 243))
            : (Color.FromArgb(46, 46, 46), Color.FromArgb(32, 32, 32), Color.FromArgb(32, 32, 32));
    }

    private static Color Lighten(Color color, double amount) => Color.FromArgb(
        (int)Math.Min(255, color.R + 255 * amount),
        (int)Math.Min(255, color.G + 255 * amount),
        (int)Math.Min(255, color.B + 255 * amount));

    private static int? ReadDword(string subKey, string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey);
            return key?.GetValue(name) as int?;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // The icons a Windows desktop normally shows, taken from the programs
    // themselves rather than from a picture of a taskbar. A program that is
    // not installed simply contributes no icon.
    private static Icon[] ReadPinnedIcons()
    {
        var found = new System.Collections.Generic.List<Icon>();

        AddFileIcon(found, ExplorerPath());
        AddFileIcon(found, EdgePath());

        // The Store is a packaged app: there is no executable to point at, so
        // the icon comes from the shell's own list of applications.
        AddShellIcon(found, @"shell:AppsFolder\Microsoft.WindowsStore_8wekyb3d8bbwe!App");

        return found.ToArray();
    }

    private static void AddFileIcon(System.Collections.Generic.List<Icon> found, string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        try
        {
            var icon = Icon.ExtractAssociatedIcon(path);
            if (icon != null)
                found.Add(icon);
        }
        catch (Exception)
        {
            // An icon that cannot be read leaves a gap rather than stopping
            // the shoot.
        }
    }

    // Ask the shell for the icon behind one of its parsing names, which is the
    // only way to reach a packaged app's icon without reading its manifest out
    // of a protected folder.
    private static void AddShellIcon(System.Collections.Generic.List<Icon> found, string parsingName)
    {
        IntPtr list = IntPtr.Zero;
        try
        {
            if (SHParseDisplayName(parsingName, IntPtr.Zero, out list, 0, out _) != 0)
                return;

            var info = new SHFILEINFO();
            IntPtr result = SHGetFileInfo(
                list, 0, ref info, Marshal.SizeOf<SHFILEINFO>(),
                SHGFI_PIDL | SHGFI_ICON | SHGFI_LARGEICON);

            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
                return;

            try
            {
                // Clone, because the handle has to be freed either way.
                found.Add((Icon)Icon.FromHandle(info.hIcon).Clone());
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            if (list != IntPtr.Zero)
                Marshal.FreeCoTaskMem(list);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(
        string name, IntPtr bindContext, out IntPtr idList, uint attributes, out uint parsed);

    [DllImport("shell32.dll")]
    private static extern IntPtr SHGetFileInfo(
        IntPtr idList, uint attributes, ref SHFILEINFO info, int size, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_PIDL = 0x000000008;

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
