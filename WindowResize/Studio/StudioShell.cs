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

    // How tall this machine's taskbar is, in the display's own pixels: 48 at 100
    // per cent, 96 at 200. That is the number the set draws with, because the set
    // measures itself in the same pixels the picture is counted in.
    //
    // The scaling used to be divided back out of it, to leave a number every
    // machine agrees on. It left the band at half the height of the taskbar
    // standing on the same desktop, and nothing else in the picture is drawn that
    // way: the menu and the settings window are laid out at this machine's DPI
    // like they are for its user.
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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconEx(
        string file, int index, IntPtr[]? large, IntPtr[]? small, int count);

    // The sync client's cloud is drawn from the shell's own icon font rather
    // than lifted out of OneDrive.exe.
    //
    // Lifting it was tried. OneDrive ships dozens of icons in one file with no
    // name to ask for, so the least colorful was taken to be the tray's flat
    // cloud - and on this machine that rule chose a grey folder, which went into
    // every listing picture looking like a folder in the notification area. The
    // shape a reader has to recognize is a monochrome cloud, and the font has
    // one.

    // Measure the real taskbar, and take its color from the theme rather than
    // from the screen.
    //
    // Sampling the pixels was wrong. The Windows 11 taskbar is translucent, so
    // the wallpaper shows through it: on a machine with a sunset picture the
    // sampled band came out warm pink, and every listing picture would have
    // carried whatever wallpaper the operator happened to be using. The theme
    // is what a reader recognizes as a taskbar.
    // Measure the taskbar as this display draws it.
    //
    // The studio runs DPI aware, so the rectangle comes back in the display's own
    // pixels - the same pixels the picture is counted in - and no conversion is
    // wanted. A machine with no taskbar to find falls back to the 48 pixels
    // Windows 11 uses at 100 per cent, which is thin on a scaled display and
    // plain to see in the picture.
    private static (int height, (Color top, Color fillTop, Color fillBottom) band) MeasureTaskbar()
    {
        var taskbar = FindWindow(TaskbarClass, null);

        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out RECT bounds))
            return (FallbackHeight, ThemeColors());

        return (Math.Max(bounds.Bottom - bounds.Top, 1), ThemeColors());
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
