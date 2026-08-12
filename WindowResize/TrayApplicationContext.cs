using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WindowResizeCapture;

// The core application context: hosts the system-tray NotifyIcon and builds
// the context menu that lets users pick a window and resize it to a preset.
// Also manages the settings form lifecycle and splash screen.
public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private SettingsForm? _settingsForm;

    // Initialize the tray icon, build the menu, show the splash screen,
    // and subscribe to settings changes for live menu rebuilds.
    public TrayApplicationContext()
    {
        _contextMenu = NewMenu();
        BuildMenu();

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            ContextMenuStrip = _contextMenu,
            Visible = true,
            Text = App.Name
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
        SettingsStore.Shared.SettingsChanged += () =>
        {
            _contextMenu.Items.Clear();
            BuildMenu();
        };

        // Brief splash screen on startup
        new SplashForm().ShowSplash(1500);
    }

    // ── Menu construction ────────────────────────────────────────────────

    // An empty menu, laid out for the language the app is showing. In a language
    // that reads right to left the items align that way, the image column moves
    // to the right, and the mark that says an item has a submenu moves to the
    // left of it and points that way.
    //
    // The mark belongs on the left even where the submenu opens to the right,
    // which is what happens on an Arabic desktop: the taskbar is mirrored, so the
    // tray is at the left end of it and a menu opened there unfolds rightward.
    // Windows' own menus were checked on Arabic Windows - the desktop's context
    // menu and Explorer's "open with" - and both put it on the left. Do not
    // "correct" it: a picture of this menu from 1.8.1, before any of this, shows
    // it on the right because that build laid the menu out left to right.
    //
    // WinForms does all of it from the property below. Nothing here places the
    // mark.
    private static ContextMenuStrip NewMenu() => new()
    {
        ShowImageMargin = true,
        RightToLeft = App.ReadsRightToLeft ? RightToLeft.Yes : RightToLeft.No,
    };

    // Build the top-level context menu: Resize submenu, Settings, Quit.
    private void BuildMenu()
    {
        AddTopLevelItems(
            _contextMenu,
            populateWindows: parent => PopulateWindowList(parent, staged: null),
            onSettings: ShowSettingsForm,
            onQuit: () =>
            {
                _notifyIcon.Visible = false;
                Application.Exit();
            });
    }

    // The three items every menu starts with. Kept in one place because the
    // studio that takes the store pictures shows this same menu; a second
    // copy built for photographs would drift away from the real one and the
    // listing would advertise a menu the app does not have.
    private static void AddTopLevelItems(
        ContextMenuStrip menu,
        Action<ToolStripMenuItem> populateWindows,
        Action onSettings,
        Action onQuit)
    {
        // The Resize submenu lazily discovers windows when opened
        var resizeItem = new ToolStripMenuItem(Strings.MenuResize);
        resizeItem.DropDownOpening += (_, _) =>
        {
            resizeItem.DropDownItems.Clear();
            populateWindows(resizeItem);
        };

        // Placeholder so WinForms renders the submenu arrow before first open
        resizeItem.DropDownItems.Add(new ToolStripMenuItem(Strings.MenuLoading) { Enabled = false });
        menu.Items.Add(resizeItem);
        menu.Items.Add(new ToolStripSeparator());

        // Settings item
        var settingsItem = new ToolStripMenuItem(Strings.MenuSettings);
        settingsItem.Click += (_, _) => onSettings();
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());

        // Quit item. Its label carries the app name, whose ampersand a menu
        // item would otherwise consume as a mnemonic prefix.
        var quitItem = new ToolStripMenuItem(EscapeMenuMnemonics(Strings.MenuQuit));
        quitItem.Click += (_, _) => onQuit();
        menu.Items.Add(quitItem);
    }

#if DEBUG
    // The menu the studio photographs. It is the menu above, with the actions
    // left inert: a picture is taken of it, never clicked.
    //
    // A caller may supply the windows to list. The real enumeration returns
    // whatever the operator has open, which makes a listing picture different
    // every time it is taken; the studio hands in a written cast instead. The
    // menu itself is built the same way either way.
    //
    // Debug only, with the studio it belongs to.
    internal static ContextMenuStrip BuildStudioMenu(
        IReadOnlyList<WindowInfo>? windows = null)
    {
        var menu = NewMenu();
        AddTopLevelItems(
            menu,
            populateWindows: parent => PopulateWindowList(parent, windows),
            onSettings: () => { },
            onQuit: () => { });
        return menu;
    }
#endif


    // Enumerate visible windows and add each as a submenu item with its
    // app icon. When three or more windows belong to the same process,
    // group them under an app-level parent item.
    private static void PopulateWindowList(
        ToolStripMenuItem parent, IReadOnlyList<WindowInfo>? staged)
    {
        var windows = staged ?? WindowManager.DiscoverWindows();

        // Staged windows mean a listing picture is being taken, and a listing
        // picture has to read the same whoever takes it. So the sizes offered
        // there are all of them, rather than the ones this display can hold and
        // the ones whose box the operator happens to have cleared.
        bool everySize = staged != null;

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
                AddFlatWindowItem(parent, window, menuFont, maxMenuWidth, everySize);
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
                    AddFlatWindowItem(parent, window, menuFont, maxMenuWidth, everySize);
                continue;
            }

            // Create an app-level parent with window count badge
            string groupLabel = $"{appWindows[0].ProcessName} ({appWindows.Count})";
            var groupItem = new ToolStripMenuItem(EscapeMenuMnemonics(groupLabel));

            // Use the first window's icon for the group header
            ShowAppIcon(groupItem, appWindows[0].AppIcon);

            // Each window becomes a child of the group. No icon here: the
            // group above already carries the app's.
            foreach (var window in appWindows)
                groupItem.DropDownItems.Add(WindowItem(window, menuFont, maxMenuWidth, everySize));

            parent.DropDownItems.Add(groupItem);
        }
    }

    // Create a flat menu item for a single window, showing its icon and
    // title. No process-name tag: the item always has a size submenu, so
    // WinForms shows the submenu arrow instead of the tag while still
    // reserving the tag's column width, which padded the menu out to a
    // fixed, oversized width. The app icon already identifies the app.
    private static void AddFlatWindowItem(
        ToolStripMenuItem parent, WindowInfo window, Font menuFont, float maxMenuWidth,
        bool everySize)
    {
        var item = WindowItem(window, menuFont, maxMenuWidth, everySize);
        ShowAppIcon(item, window.AppIcon);
        parent.DropDownItems.Add(item);
    }

    // One window as a menu item: its title, shortened to fit, with the sizes
    // it can be resized to beneath it. Built in one place because a window
    // reached through a group and one reached directly have to offer the same
    // thing; two copies of this drifted apart once already.
    private static ToolStripMenuItem WindowItem(
        WindowInfo window, Font menuFont, float maxMenuWidth, bool everySize)
    {
        string displayTitle = string.IsNullOrEmpty(window.Title) ? Strings.MenuUntitled : window.Title;
        string truncatedTitle = TruncateToFit(displayTitle, menuFont, maxMenuWidth);

        var item = new ToolStripMenuItem(EscapeMenuMnemonics(truncatedTitle));
        BuildSizeSubmenu(item, window, everySize);
        return item;
    }

    // Put an application's own icon beside a menu item. A window can hand
    // back an icon handle that will not become a bitmap, and a menu missing
    // one picture is better than no menu at all.
    private static void ShowAppIcon(ToolStripMenuItem item, Icon? icon)
    {
        if (icon == null)
            return;

        try
        {
            item.Image = icon.ToBitmap();
            item.ImageScaling = ToolStripItemImageScaling.SizeToFit;
        }
        catch { }
    }

    // Attach preset-size children to a window menu item. Sizes that
    // exceed the window's current screen are shown but disabled.
    // When positioning features are active, a "Current Size" item is
    // prepended to allow repositioning without changing dimensions.
    private static void BuildSizeSubmenu(
        ToolStripMenuItem parent, WindowInfo window, bool everySize)
    {
        // Determine the resolution of the display containing this window
        var screenBounds = ScreenBoundsForWindow(window);

        // If any positioning feature is active, offer a "reposition only" item.
        // Report the current size in the same coordinate system as the presets:
        // client dimensions when client-area sizing is on, outer dimensions
        // otherwise. Passing the matching value keeps this a pure reposition —
        // in client mode ResizeWindow re-adds the border to preserve the frame.
        if (SettingsStore.Shared.IsPositioningActive)
        {
            int currentWidth = SettingsStore.Shared.ResizeClientArea ? window.ClientWidth : window.Width;
            int currentHeight = SettingsStore.Shared.ResizeClientArea ? window.ClientHeight : window.Height;

            // The size this item offers writes itself. Spelled out here a second
            // time instead, it missed what PresetSize.DisplayName does for a
            // language that reads right to left, and the Arabic menu offered a
            // window "x 800 1280".
            var currentSize = new PresetSize(currentWidth, currentHeight, Strings.MenuCurrentSize);
            var currentItem = new ToolStripMenuItem(currentSize.DisplayName)
            {
                ShortcutKeyDisplayString = Strings.MenuCurrentSize
            };
            currentItem.Click += (_, _) => PerformResize(window, currentSize);
            parent.DropDownItems.Add(currentItem);
            parent.DropDownItems.Add(new ToolStripSeparator());
        }

        // A size is offered when it fits the display this window is on and its
        // box is checked in the settings window. Sizes that do not fit used to
        // be listed and greyed out, which spent the height of the menu on sizes
        // nobody on that display can pick.
        foreach (var size in SettingsStore.Shared.AllSizes)
        {
            if (!everySize && (size.Width > screenBounds.Width || size.Height > screenBounds.Height))
                continue;

            if (!everySize && !SettingsStore.Shared.ShowsInMenu(size))
                continue;

            var sizeItem = new ToolStripMenuItem(size.DisplayName);

            if (!string.IsNullOrEmpty(size.Label))
                sizeItem.ShortcutKeyDisplayString = size.Label;

            sizeItem.Click += (_, _) => PerformResize(window, size);

            parent.DropDownItems.Add(sizeItem);
        }
    }

    // ── Actions ──────────────────────────────────────────────────────────

    // Execute the resize with all configured behavior options, then
    // capture the window if successful, or show an error dialog.
    private static void PerformResize(WindowInfo window, PresetSize size)
    {
        var outcome = WindowManager.ResizeWindow(
            window, size,
            bringToFront: SettingsStore.Shared.BringToFront,
            position: SettingsStore.Shared.Position,
            moveToMainScreen: SettingsStore.Shared.MoveToMainScreen,
            clientArea: SettingsStore.Shared.ResizeClientArea);

        // On success capture the window; on failure explain the cause so
        // the user doesn't mistake a Windows restriction for an app bug.
        switch (outcome)
        {
            case ResizeOutcome.Success:
                CaptureHelper.CaptureAfterResize(window, size);
                break;

            case ResizeOutcome.NeedsElevation:
                Warn(Strings.AlertResizeElevatedBody);
                break;

            default:
                Warn(Strings.AlertResizeFailedBody);
                break;
        }
    }

    // Explain a resize the app was not allowed to make. Both refusals read the
    // same way, and a message box has to be told the reading direction at every
    // call because it does not inherit the app's own.
    private static void Warn(string body) => MessageBox.Show(
        body,
        Strings.AlertResizeFailedTitle,
        MessageBoxButtons.OK,
        MessageBoxIcon.Warning,
        MessageBoxDefaultButton.Button1,
        App.MessageReading);

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

    // Menu items read "&" as a mnemonic prefix: it disappears and the next
    // character gains an underline. Doubling it renders a literal ampersand.
    // Apply this after measuring text, since the doubled character is not
    // drawn and would otherwise skew the width.
    private static string EscapeMenuMnemonics(string text) => text.Replace("&", "&&");

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
    // center point of the given window.
    private static Size ScreenBoundsForWindow(WindowInfo window)
    {
        var center = new Point(
            window.Left + window.Width / 2,
            window.Top + window.Height / 2);
        return Screen.FromPoint(center).Bounds.Size;
    }

    // Load the tray icon from the embedded resource. If the resource is
    // missing, draw a minimal fallback resize icon.
    private static Icon LoadTrayIcon()
    {
        using var stream = App.OpenIcon();
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
