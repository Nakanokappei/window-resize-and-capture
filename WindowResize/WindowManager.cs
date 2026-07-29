using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace WindowsResizeCapture;

// Result of a resize attempt, so the UI can explain a failure the user did
// not cause (rather than looking like a bug in this app).
public enum ResizeOutcome
{
    Success,
    // The target window is owned by an elevated (administrator) process,
    // which Windows forbids a non-elevated app from resizing.
    NeedsElevation,
    // The window rejected the resize for another reason (fixed size,
    // full-screen, or it closed mid-operation).
    Failed
}

// Metadata for a single visible application window discovered by enumeration.
public class WindowInfo
{
    public IntPtr Handle { get; set; }
    public uint ProcessId { get; set; }
    public string ProcessName { get; set; } = "";
    public string Title { get; set; } = "";
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    // Client-area (content) dimensions, excluding the title bar and frame.
    public int ClientWidth { get; set; }
    public int ClientHeight { get; set; }
    public Icon? AppIcon { get; set; }
}

// Provides Win32 P/Invoke operations for enumerating visible desktop windows,
// resizing them to preset dimensions, repositioning them on a target screen,
// and forcibly bringing them to the foreground from a tray-app context.
public static class WindowManager
{
    // ── Win32 API declarations ───────────────────────────────────────────

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    // SendMessageTimeout (not SendMessage) so that querying a window whose
    // owning thread is hung cannot freeze our UI thread: it aborts quickly
    // instead of blocking until the system hang timeout, which was surfacing
    // as a HANG_QUIESCE crash bucket.
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    // ── Constants ────────────────────────────────────────────────────────

    private const uint WM_GETICON = 0x007F;
    private const IntPtr ICON_SMALL = 0;
    private const IntPtr ICON_BIG = 1;
    private const IntPtr ICON_SMALL2 = 2;
    private const int GCLP_HICONSM = -34;
    private const int GCLP_HICON = -14;

    // SendMessageTimeout: abort immediately if the target window is hung,
    // and cap the wait for a merely-busy window at this many milliseconds.
    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private const uint IconMessageTimeoutMs = 200;

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const long WS_CAPTION = 0x00C00000;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_APPWINDOW = 0x00040000;
    private const int DWMWA_CLOAKED = 14;

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const int ERROR_ACCESS_DENIED = 5;

    // Full query access; a medium-integrity process is denied this on a
    // higher-integrity (elevated) target, which is how we detect elevation.
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;
    private const int SW_RESTORE = 9;

    // ── Structs ──────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    // ── Public API ───────────────────────────────────────────────────────

    // Walk all top-level windows via EnumWindows and return those that
    // represent visible, titled application windows (excluding this
    // process, cloaked UWP shells, and tool windows).
    public static List<WindowInfo> DiscoverWindows()
    {
        var windows = new List<WindowInfo>();
        int ownPid = Environment.ProcessId;

        EnumWindows((hWnd, _) =>
        {
            // Skip invisible windows early
            if (!IsWindowVisible(hWnd))
                return true;

            // Exclude cloaked windows (hidden UWP containers, virtual-desktop ghosts)
            DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int));
            if (cloaked != 0)
                return true;

            int style = GetWindowLong(hWnd, GWL_STYLE);
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            // Require a title bar — filters out background/system surfaces
            if ((style & (int)WS_CAPTION) != (int)WS_CAPTION)
                return true;

            // Exclude tool windows unless explicitly marked as app windows
            if ((exStyle & (int)WS_EX_TOOLWINDOW) != 0 && (exStyle & (int)WS_EX_APPWINDOW) == 0)
                return true;

            // Must have a non-empty title
            int titleLength = GetWindowTextLength(hWnd);
            if (titleLength == 0)
                return true;

            var titleBuffer = new StringBuilder(titleLength + 1);
            GetWindowText(hWnd, titleBuffer, titleBuffer.Capacity);

            // Skip our own process
            GetWindowThreadProcessId(hWnd, out uint processId);
            if ((int)processId == ownPid)
                return true;

            // Resolve the owning process name (best-effort)
            string processName = "";
            try { processName = Process.GetProcessById((int)processId).ProcessName; }
            catch { }

            // Compute pixel dimensions; skip zero-area windows
            GetWindowRect(hWnd, out RECT rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0)
                return true;

            // Client-area dimensions for the client-size resize/display mode
            GetClientRect(hWnd, out RECT clientRect);

            windows.Add(new WindowInfo
            {
                Handle = hWnd,
                ProcessId = processId,
                ProcessName = processName,
                Title = titleBuffer.ToString(),
                Left = rect.Left,
                Top = rect.Top,
                Width = width,
                Height = height,
                ClientWidth = clientRect.Right - clientRect.Left,
                ClientHeight = clientRect.Bottom - clientRect.Top,
                AppIcon = ExtractWindowIcon(hWnd)
            });

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    // Resize the given window to the preset dimensions, then optionally
    // reposition it on a target screen and bring it to the foreground.
    // Returns an outcome so the caller can explain why a resize was refused.
    public static ResizeOutcome ResizeWindow(WindowInfo window, PresetSize size,
        bool bringToFront = false, WindowPosition? position = null, bool moveToMainScreen = false,
        bool clientArea = false)
    {
        int targetWidth = size.Width;
        int targetHeight = size.Height;

        // When sizing by client area, grow the target by the window's current
        // non-client border (title bar + frame) so the content — not the outer
        // frame — ends up at the requested dimensions.
        if (clientArea &&
            GetWindowRect(window.Handle, out RECT outerRect) &&
            GetClientRect(window.Handle, out RECT clientRect))
        {
            targetWidth += (outerRect.Right - outerRect.Left) - (clientRect.Right - clientRect.Left);
            targetHeight += (outerRect.Bottom - outerRect.Top) - (clientRect.Bottom - clientRect.Top);
        }

        // Step 1 — resize without moving or changing Z-order
        bool resized = SetWindowPos(
            window.Handle, IntPtr.Zero,
            0, 0, targetWidth, targetHeight,
            SWP_NOMOVE | SWP_NOZORDER);

        // Distinguish an elevation block from other failures so the UI can
        // point the user at "Run as administrator".
        if (!resized)
        {
            return IsResizeBlockedByElevation(window.Handle)
                ? ResizeOutcome.NeedsElevation
                : ResizeOutcome.Failed;
        }

        // Step 2 — snap to a screen position if any positioning is requested
        if (position != null || moveToMainScreen)
        {
            var workArea = ResolveTargetWorkArea(window.Handle, moveToMainScreen);
            var anchor = position ?? WindowPosition.Center;
            var origin = CalculateSnapOrigin(anchor, targetWidth, targetHeight, workArea);

            SetWindowPos(
                window.Handle, IntPtr.Zero,
                origin.X, origin.Y, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER);
        }

        // Step 3 — force the window to the foreground if requested
        if (bringToFront)
            BringToForeground(window.Handle);

        return ResizeOutcome.Success;
    }

    // ── Private helpers ──────────────────────────────────────────────────

    // Decide whether a failed resize was blocked because the target window
    // runs at a higher integrity level (e.g. as administrator) than we do.
    // A medium-integrity process is denied PROCESS_QUERY_INFORMATION on an
    // elevated target but granted it on a same-integrity one, so an
    // access-denied here means the window is elevated.
    private static bool IsResizeBlockedByElevation(IntPtr hWnd)
    {
        // If we are already elevated, elevation is not the blocker.
        if (IsCurrentProcessElevated())
            return false;

        GetWindowThreadProcessId(hWnd, out uint processId);

        IntPtr handle = OpenProcess(PROCESS_QUERY_INFORMATION, false, processId);
        if (handle != IntPtr.Zero)
        {
            CloseHandle(handle);
            return false;
        }

        return Marshal.GetLastWin32Error() == ERROR_ACCESS_DENIED;
    }

    // Whether this process is running with an elevated (administrator) token.
    private static bool IsCurrentProcessElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    // Try multiple Win32 strategies to obtain the application icon for a
    // window: first WM_GETICON with decreasing size preference, then the
    // window-class registered icons.
    private static Icon? ExtractWindowIcon(IntPtr hWnd)
    {
        try
        {
            IntPtr iconHandle = IntPtr.Zero;

            // Try WM_GETICON: small2 → small → big. Use SendMessageTimeout so
            // a hung target window cannot block our UI thread; on timeout or
            // failure the result stays zero and we fall through to the class icon.
            foreach (var sizeHint in new[] { ICON_SMALL2, ICON_SMALL, ICON_BIG })
            {
                IntPtr sendResult = SendMessageTimeout(
                    hWnd, WM_GETICON, sizeHint, IntPtr.Zero,
                    SMTO_ABORTIFHUNG, IconMessageTimeoutMs, out iconHandle);

                if (sendResult != IntPtr.Zero && iconHandle != IntPtr.Zero) break;
                iconHandle = IntPtr.Zero;
            }

            // Fall back to the window-class icon: small → large
            if (iconHandle == IntPtr.Zero)
            {
                foreach (var classIndex in new[] { GCLP_HICONSM, GCLP_HICON })
                {
                    iconHandle = GetClassLongPtr(hWnd, classIndex);
                    if (iconHandle != IntPtr.Zero) break;
                }
            }

            if (iconHandle != IntPtr.Zero)
                return Icon.FromHandle(iconHandle);
        }
        catch { }

        return null;
    }

    // Return the taskbar-excluded work area of the target display.
    // When usePrimaryScreen is true, always pick the primary monitor;
    // otherwise pick whichever monitor currently contains the window.
    private static RECT ResolveTargetWorkArea(IntPtr hWnd, bool usePrimaryScreen)
    {
        IntPtr hMonitor = usePrimaryScreen
            ? MonitorFromWindow(IntPtr.Zero, MONITOR_DEFAULTTOPRIMARY)
            : MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(hMonitor, ref info);
        return info.rcWork;
    }

    // Compute the top-left pixel coordinate for a window of the given size
    // snapped to one of nine anchor positions within the work area.
    private static Point CalculateSnapOrigin(
        WindowPosition anchor, int windowWidth, int windowHeight, RECT workArea)
    {
        int areaWidth = workArea.Right - workArea.Left;
        int areaHeight = workArea.Bottom - workArea.Top;

        // Horizontal coordinate based on the anchor column
        int x = anchor switch
        {
            WindowPosition.TopLeft or WindowPosition.Left or WindowPosition.BottomLeft
                => workArea.Left,
            WindowPosition.Top or WindowPosition.Center or WindowPosition.Bottom
                => workArea.Left + (areaWidth - windowWidth) / 2,
            _ => workArea.Right - windowWidth,
        };

        // Vertical coordinate based on the anchor row
        int y = anchor switch
        {
            WindowPosition.TopLeft or WindowPosition.Top or WindowPosition.TopRight
                => workArea.Top,
            WindowPosition.Left or WindowPosition.Center or WindowPosition.Right
                => workArea.Top + (areaHeight - windowHeight) / 2,
            _ => workArea.Bottom - windowHeight,
        };

        return new Point(x, y);
    }

    // Force a window to the foreground even from a background/tray process.
    // Windows restricts SetForegroundWindow to the thread that owns the
    // current foreground window. Temporarily attaching our input thread to
    // that thread satisfies the requirement.
    private static void BringToForeground(IntPtr hWnd)
    {
        IntPtr foregroundHwnd = GetForegroundWindow();
        uint foregroundThread = GetWindowThreadProcessId(foregroundHwnd, out _);
        uint currentThread = GetCurrentThreadId();

        // Attach our input queue to the foreground thread so Windows allows
        // the focus switch
        bool attached = false;
        if (foregroundThread != currentThread)
            attached = AttachThreadInput(currentThread, foregroundThread, true);

        ShowWindow(hWnd, SW_RESTORE);
        BringWindowToTop(hWnd);
        SetForegroundWindow(hWnd);

        // Detach immediately to restore normal input processing
        if (attached)
            AttachThreadInput(currentThread, foregroundThread, false);
    }
}
