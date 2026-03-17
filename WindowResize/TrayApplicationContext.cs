using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WindowResize;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly SettingsStore _store = SettingsStore.Shared;
    private SettingsForm? _settingsForm;

    public TrayApplicationContext()
    {
        // Apply saved language before building any UI
        _store.InitializeLanguage();

        _contextMenu = new ContextMenuStrip { ShowImageMargin = true };
        BuildMenu();

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            ContextMenuStrip = _contextMenu,
            Visible = true,
            Text = "Window Resize"
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                // Show context menu on left click too
                var mi = typeof(NotifyIcon).GetMethod("ShowContextMenu",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                mi?.Invoke(_notifyIcon, null);
            }
        };

        _store.SettingsChanged += () =>
        {
            _contextMenu.Items.Clear();
            BuildMenu();
        };

        // Show splash screen briefly
        var splash = new SplashForm();
        splash.ShowSplash(1500);
    }

    // Construct the tray context menu: Resize submenu, Settings, Quit.
    private void BuildMenu()
    {
        var resizeItem = new ToolStripMenuItem(Strings.MenuResize);

        // Lazy-load the window list each time the submenu opens
        resizeItem.DropDownOpening += (_, _) =>
        {
            resizeItem.DropDownItems.Clear();
            PopulateWindowList(resizeItem);
        };

        // Placeholder item so WinForms renders the submenu arrow
        resizeItem.DropDownItems.Add(new ToolStripMenuItem(Strings.MenuLoading) { Enabled = false });
        _contextMenu.Items.Add(resizeItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        // Settings
        var settingsItem = new ToolStripMenuItem(Strings.MenuSettings);
        settingsItem.Click += (_, _) => OpenSettings();
        _contextMenu.Items.Add(settingsItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Quit
        var quitItem = new ToolStripMenuItem(Strings.MenuQuit);
        quitItem.Click += (_, _) =>
        {
            _notifyIcon.Visible = false;
            Application.Exit();
        };
        _contextMenu.Items.Add(quitItem);
    }

    // Enumerate visible windows and add each as a submenu item with its app icon.
    // When 3+ windows belong to the same app, group them under an app-level submenu.
    private void PopulateWindowList(ToolStripMenuItem parent)
    {
        var windows = WindowManager.ListWindows();

        if (windows.Count == 0)
        {
            parent.DropDownItems.Add(new ToolStripMenuItem(Strings.MenuNoWindows) { Enabled = false });
            return;
        }

        // Limit menu item width to 1/4 of the primary screen width (matches macOS behaviour)
        var menuFont = SystemFonts.MenuFont ?? new Font("Segoe UI", 9);
        float maxMenuWidth = Screen.PrimaryScreen!.Bounds.Width / 4.0f;

        // Group windows by process ID
        var groups = windows.GroupBy(w => w.ProcessId).ToList();

        // Determine whether any app has 3+ windows (triggers grouping mode)
        bool useGrouping = groups.Any(g => g.Count() >= 3);

        if (useGrouping)
        {
            foreach (var group in groups)
            {
                var appWindows = group.ToList();
                string appName = appWindows[0].ProcessName;

                if (appWindows.Count >= 3)
                {
                    // Create an app-level parent item with window count
                    string groupLabel = $"{appName} ({appWindows.Count})";
                    var groupItem = new ToolStripMenuItem(groupLabel);

                    // Use the first window's icon for the group
                    if (appWindows[0].AppIcon is { } groupIcon)
                    {
                        try
                        {
                            groupItem.Image = groupIcon.ToBitmap();
                            groupItem.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                        }
                        catch { }
                    }

                    // Add each window as a child
                    foreach (var win in appWindows)
                    {
                        string displayName = string.IsNullOrEmpty(win.Title) ? Strings.MenuUntitled : win.Title;
                        string title = TruncateToFit(displayName, menuFont, maxMenuWidth);
                        var windowItem = new ToolStripMenuItem(title);

                        PopulateSizeList(windowItem, win);
                        groupItem.DropDownItems.Add(windowItem);
                    }

                    parent.DropDownItems.Add(groupItem);
                }
                else
                {
                    // Fewer than 3 windows from this app: show flat
                    foreach (var win in appWindows)
                    {
                        AddFlatWindowItem(parent, win, menuFont, maxMenuWidth);
                    }
                }
            }
        }
        else
        {
            // No grouping needed: show all windows flat
            foreach (var win in windows)
            {
                AddFlatWindowItem(parent, win, menuFont, maxMenuWidth);
            }
        }
    }

    // Add a single window as a flat menu item with icon and process name tag.
    private void AddFlatWindowItem(ToolStripMenuItem parent, WindowInfo win, Font menuFont, float maxMenuWidth)
    {
        string displayName = string.IsNullOrEmpty(win.Title) ? Strings.MenuUntitled : win.Title;
        string title = TruncateToFit(displayName, menuFont, maxMenuWidth);

        var windowItem = new ToolStripMenuItem(title);
        windowItem.ShortcutKeyDisplayString = win.ProcessName;

        // Display the application's icon beside the menu item
        if (win.AppIcon != null)
        {
            try
            {
                windowItem.Image = win.AppIcon.ToBitmap();
                windowItem.ImageScaling = ToolStripItemImageScaling.SizeToFit;
            }
            catch { }
        }

        // Attach the available preset sizes as a submenu
        PopulateSizeList(windowItem, win);

        parent.DropDownItems.Add(windowItem);
    }

    // Add a size menu item for each preset; disable sizes that exceed the window's screen.
    // When positioning features are active, add a "Current Size" item at the top.
    private void PopulateSizeList(ToolStripMenuItem parent, WindowInfo win)
    {
        // Determine which display contains this window and get its resolution
        var screenSize = GetScreenSizeForWindow(win);

        // If any positioning feature is active, add a "current size" item
        // that repositions/brings-to-front without resizing
        if (_store.HasActivePositioningFeatures)
        {
            var currentSize = new PresetSize(win.Width, win.Height, Strings.MenuCurrentSize);
            var currentItem = new ToolStripMenuItem($"{win.Width} x {win.Height}");
            currentItem.ShortcutKeyDisplayString = Strings.MenuCurrentSize;
            currentItem.Click += (_, _) => PerformResize(win, currentSize);
            parent.DropDownItems.Add(currentItem);
            parent.DropDownItems.Add(new ToolStripSeparator());
        }

        foreach (var size in _store.AllSizes)
        {
            string text = size.DisplayName;

            bool exceedsScreen = size.Width > screenSize.Width || size.Height > screenSize.Height;

            var sizeItem = new ToolStripMenuItem(text);
            if (!string.IsNullOrEmpty(size.Label))
                sizeItem.ShortcutKeyDisplayString = size.Label;
            sizeItem.Enabled = !exceedsScreen;

            if (!exceedsScreen)
            {
                sizeItem.Click += (_, _) => PerformResize(win, size);
            }

            parent.DropDownItems.Add(sizeItem);
        }
    }

    // Execute the resize operation with all behaviour settings applied.
    private void PerformResize(WindowInfo win, PresetSize size)
    {
        bool success = WindowManager.ResizeWindow(
            win, size,
            bringToFront: _store.BringToFront,
            position: _store.Position,
            moveToMainScreen: _store.MoveToMainScreen
        );

        if (success)
        {
            // Capture a screenshot now that the resize succeeded
            ScreenshotHelper.CaptureAfterResize(win, size);
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

    /// <summary>
    /// Truncates text with "..." so its rendered width does not exceed maxWidth.
    /// </summary>
    private static string TruncateToFit(string text, Font font, float maxWidth)
    {
        if (TextRenderer.MeasureText(text, font).Width <= maxWidth)
            return text;

        // Shorten one character at a time until the text plus ellipsis fits.
        for (int length = text.Length - 1; length >= 10; length--)
        {
            string candidate = text[..length] + "\u2026";
            if (TextRenderer.MeasureText(candidate, font).Width <= maxWidth)
                return candidate;
        }

        return text[..10] + "\u2026";
    }

    /// <summary>
    /// Returns the screen size of the display containing the window's center point.
    /// </summary>
    private static Size GetScreenSizeForWindow(WindowInfo win)
    {
        var centerPoint = new Point(
            win.Left + win.Width / 2,
            win.Top + win.Height / 2
        );

        var screen = Screen.FromPoint(centerPoint);
        return screen.Bounds.Size;
    }

    private void OpenSettings()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm();
        }

        _settingsForm.Show();
        _settingsForm.BringToFront();
        _settingsForm.Activate();
    }

    // Load the tray icon from embedded resources, with a drawn fallback.
    private static Icon LoadTrayIcon()
    {
        // Load icon from embedded resource
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream("WindowResize.Resources.app.ico");
        if (stream != null)
        {
            return new Icon(stream);
        }

        // Fallback: draw a simple resize icon
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

        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
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
