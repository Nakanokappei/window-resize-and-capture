using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WindowsResizeCapture;

// The core application context: hosts the system-tray NotifyIcon and builds
// the context menu that lets users pick a window and resize it to a preset.
// Also manages the settings form lifecycle and splash screen.
public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly SettingsStore _store = SettingsStore.Shared;
    private SettingsForm? _settingsForm;

    // Initialise the tray icon, build the menu, show the splash screen,
    // and subscribe to settings changes for live menu rebuilds.
    public TrayApplicationContext()
    {
        // Apply the saved language before any UI strings are resolved
        _store.InitializeLanguage();

        _contextMenu = new ContextMenuStrip { ShowImageMargin = true };
        BuildMenu();

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            ContextMenuStrip = _contextMenu,
            Visible = true,
            Text = "Window Resize & Capture"
        };

        // Show the context menu on left-click as well (default is right-click only)
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                var showMethod = typeof(NotifyIcon).GetMethod("ShowContextMenu",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                showMethod?.Invoke(_notifyIcon, null);
            }
        };

        // Rebuild the menu whenever settings change (e.g. new preset added)
        _store.SettingsChanged += () =>
        {
            _contextMenu.Items.Clear();
            BuildMenu();
        };

        // Brief splash screen on startup
        new SplashForm().ShowSplash(1500);
    }

    // ── Menu construction ────────────────────────────────────────────────

    // Build the top-level context menu: Resize submenu, Settings, Quit.
    private void BuildMenu()
    {
        // The Resize submenu lazily discovers windows when opened
        var resizeItem = new ToolStripMenuItem(Strings.MenuResize);
        resizeItem.DropDownOpening += (_, _) =>
        {
            resizeItem.DropDownItems.Clear();
            PopulateWindowList(resizeItem);
        };

        // Placeholder so WinForms renders the submenu arrow before first open
        resizeItem.DropDownItems.Add(new ToolStripMenuItem(Strings.MenuLoading) { Enabled = false });
        _contextMenu.Items.Add(resizeItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        // Settings item
        var settingsItem = new ToolStripMenuItem(Strings.MenuSettings);
        settingsItem.Click += (_, _) => ShowSettingsForm();
        _contextMenu.Items.Add(settingsItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        // Quit item
        var quitItem = new ToolStripMenuItem(Strings.MenuQuit);
        quitItem.Click += (_, _) =>
        {
            _notifyIcon.Visible = false;
            Application.Exit();
        };
        _contextMenu.Items.Add(quitItem);
    }

    // Enumerate visible windows and add each as a submenu item with its
    // app icon. When three or more windows belong to the same process,
    // group them under an app-level parent item.
    private void PopulateWindowList(ToolStripMenuItem parent)
    {
        var windows = WindowManager.DiscoverWindows();

        if (windows.Count == 0)
        {
            parent.DropDownItems.Add(new ToolStripMenuItem(Strings.MenuNoWindows) { Enabled = false });
            return;
        }

        // Layout constants for truncation
        var menuFont = SystemFonts.MenuFont ?? new Font("Segoe UI", 9);
        float maxMenuWidth = Screen.PrimaryScreen!.Bounds.Width / 4.0f;

        // Group windows by owning process
        var groups = windows.GroupBy(w => w.ProcessId).ToList();
        bool useGrouping = groups.Any(g => g.Count() >= 3);

        if (!useGrouping)
        {
            // Flat list — every window gets its own top-level item
            foreach (var window in windows)
                AddFlatWindowItem(parent, window, menuFont, maxMenuWidth);
            return;
        }

        // Grouped mode — cluster windows by process
        foreach (var group in groups)
        {
            var appWindows = group.ToList();

            if (appWindows.Count < 3)
            {
                // Too few windows to justify a group — show flat
                foreach (var window in appWindows)
                    AddFlatWindowItem(parent, window, menuFont, maxMenuWidth);
                continue;
            }

            // Create an app-level parent with window count badge
            string groupLabel = $"{appWindows[0].ProcessName} ({appWindows.Count})";
            var groupItem = new ToolStripMenuItem(groupLabel);

            // Use the first window's icon for the group header
            if (appWindows[0].AppIcon is { } groupIcon)
            {
                try
                {
                    groupItem.Image = groupIcon.ToBitmap();
                    groupItem.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                }
                catch { }
            }

            // Each window becomes a child of the group
            foreach (var window in appWindows)
            {
                string displayTitle = string.IsNullOrEmpty(window.Title) ? Strings.MenuUntitled : window.Title;
                string truncatedTitle = TruncateToFit(displayTitle, menuFont, maxMenuWidth);
                var windowItem = new ToolStripMenuItem(truncatedTitle);
                BuildSizeSubmenu(windowItem, window);
                groupItem.DropDownItems.Add(windowItem);
            }

            parent.DropDownItems.Add(groupItem);
        }
    }

    // Create a flat menu item for a single window, showing its icon and
    // the owning process name as a right-aligned tag.
    private void AddFlatWindowItem(
        ToolStripMenuItem parent, WindowInfo window, Font menuFont, float maxMenuWidth)
    {
        string displayTitle = string.IsNullOrEmpty(window.Title) ? Strings.MenuUntitled : window.Title;
        string truncatedTitle = TruncateToFit(displayTitle, menuFont, maxMenuWidth);

        var item = new ToolStripMenuItem(truncatedTitle)
        {
            ShortcutKeyDisplayString = window.ProcessName
        };

        // Display the application's icon beside the menu item
        if (window.AppIcon != null)
        {
            try
            {
                item.Image = window.AppIcon.ToBitmap();
                item.ImageScaling = ToolStripItemImageScaling.SizeToFit;
            }
            catch { }
        }

        BuildSizeSubmenu(item, window);
        parent.DropDownItems.Add(item);
    }

    // Attach preset-size children to a window menu item. Sizes that
    // exceed the window's current screen are shown but disabled.
    // When positioning features are active, a "Current Size" item is
    // prepended to allow repositioning without changing dimensions.
    private void BuildSizeSubmenu(ToolStripMenuItem parent, WindowInfo window)
    {
        // Determine the resolution of the display containing this window
        var screenBounds = ScreenBoundsForWindow(window);

        // If any positioning feature is active, offer a "reposition only" item
        if (_store.IsPositioningActive)
        {
            var currentSize = new PresetSize(window.Width, window.Height, Strings.MenuCurrentSize);
            var currentItem = new ToolStripMenuItem($"{window.Width} x {window.Height}")
            {
                ShortcutKeyDisplayString = Strings.MenuCurrentSize
            };
            currentItem.Click += (_, _) => PerformResize(window, currentSize);
            parent.DropDownItems.Add(currentItem);
            parent.DropDownItems.Add(new ToolStripSeparator());
        }

        // Add every preset size, disabling those larger than the screen
        foreach (var size in _store.AllSizes)
        {
            bool exceedsScreen = size.Width > screenBounds.Width || size.Height > screenBounds.Height;

            var sizeItem = new ToolStripMenuItem(size.DisplayName) { Enabled = !exceedsScreen };

            if (!string.IsNullOrEmpty(size.Label))
                sizeItem.ShortcutKeyDisplayString = size.Label;

            if (!exceedsScreen)
                sizeItem.Click += (_, _) => PerformResize(window, size);

            parent.DropDownItems.Add(sizeItem);
        }
    }

    // ── Actions ──────────────────────────────────────────────────────────

    // Execute the resize with all configured behaviour options, then
    // capture a screenshot if successful, or show an error dialog.
    private void PerformResize(WindowInfo window, PresetSize size)
    {
        bool success = WindowManager.ResizeWindow(
            window, size,
            bringToFront: _store.BringToFront,
            position: _store.Position,
            moveToMainScreen: _store.MoveToMainScreen);

        if (success)
        {
            ScreenshotHelper.CaptureAfterResize(window, size);
        }
        else
        {
            MessageBox.Show(
                Strings.AlertResizeFailedBody,
                Strings.AlertResizeFailedTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    // Show the settings form, creating it on first use. Reuses the
    // existing instance (which hides instead of closing) when possible.
    private void ShowSettingsForm()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
            _settingsForm = new SettingsForm();

        _settingsForm.Show();
        _settingsForm.BringToFront();
        _settingsForm.Activate();
    }

    // ── Utility ──────────────────────────────────────────────────────────

    // Shorten text with an ellipsis so its rendered width stays within
    // maxWidth pixels. Preserves at least 10 characters before giving up.
    private static string TruncateToFit(string text, Font font, float maxWidth)
    {
        if (TextRenderer.MeasureText(text, font).Width <= maxWidth)
            return text;

        for (int length = text.Length - 1; length >= 10; length--)
        {
            string candidate = text[..length] + "\u2026";
            if (TextRenderer.MeasureText(candidate, font).Width <= maxWidth)
                return candidate;
        }

        return text[..10] + "\u2026";
    }

    // Return the pixel dimensions of the display that contains the
    // centre point of the given window.
    private static Size ScreenBoundsForWindow(WindowInfo window)
    {
        var centre = new Point(
            window.Left + window.Width / 2,
            window.Top + window.Height / 2);
        return Screen.FromPoint(centre).Bounds.Size;
    }

    // Load the tray icon from the embedded resource. If the resource is
    // missing, draw a minimal fallback resize icon.
    private static Icon LoadTrayIcon()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream("WindowsResizeCapture.Resources.app.ico");

        if (stream != null)
            return new Icon(stream);

        // Fallback: a simple hand-drawn resize icon
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            using var pen = new Pen(Color.White, 1);
            g.DrawRectangle(pen, 2, 2, 11, 11);
            g.DrawLine(pen, 8, 6, 12, 6);
            g.DrawLine(pen, 12, 6, 12, 2);
            g.DrawLine(pen, 3, 9, 7, 9);
            g.DrawLine(pen, 3, 9, 3, 13);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
            _settingsForm?.Dispose();
        }
        base.Dispose(disposing);
    }
}
