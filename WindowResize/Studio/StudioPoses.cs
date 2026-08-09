using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowResizeCapture.Studio;

// Arranges the set for one named pose.
//
// A pose is made by calling what a click calls. Drawing a picture of a menu,
// rather than opening the menu, produces a photograph of a screen the app
// cannot actually show, and nobody notices until a user complains that the
// listing does not match the app.
internal static class StudioPoses
{
    // What a pose leaves on screen, so the session can take it down again.
    internal sealed class Arrangement : IDisposable
    {
        internal ContextMenuStrip? Menu { get; init; }
        internal Form? Window { get; init; }

        public void Dispose()
        {
            Menu?.Close();
            Menu?.Dispose();
            Window?.Close();
            Window?.Dispose();
        }
    }

    internal static async Task<Arrangement> Arrange(string view, StudioSetForm set)
    {
        return view switch
        {
            "tray-menu" => await OpenTrayMenu(set),
            "settings-general" => await OpenSettings(set, tabIndex: 0),
            "settings-capture" => await OpenSettings(set, tabIndex: 1),
            "settings-behavior" => await OpenSettings(set, tabIndex: 2),
            _ => throw new ArgumentException($"unknown view: {view}"),
        };
    }

    // The real tray menu, opened over the set at the point its own tray icon
    // sits. ContextMenuStrip.Show takes any screen coordinate, so the menu and
    // its shadow land in the picture without any compositing afterwards.
    private static async Task<Arrangement> OpenTrayMenu(StudioSetForm set)
    {
        var menu = TrayApplicationContext.BuildStudioMenu();

        var anchor = set.TrayIconAnchor;
        menu.Show(anchor, ToolStripDropDownDirection.AboveLeft);

        await StudioCamera.Settle(400);
        return new Arrangement { Menu = menu };
    }

    // The real settings window, centered on the set and switched to one tab.
    private static async Task<Arrangement> OpenSettings(StudioSetForm set, int tabIndex)
    {
        var settings = new SettingsForm();
        settings.StartPosition = FormStartPosition.Manual;
        settings.TopMost = true;
        settings.Show();

        // Center it on the set rather than on the screen: the set is the
        // picture, and the screen is usually larger.
        settings.Location = new System.Drawing.Point(
            set.Left + (set.Width - settings.Width) / 2,
            set.Top + (set.Height - settings.Height) / 2);

        SelectTab(settings, tabIndex);

        await StudioCamera.Settle(400);
        return new Arrangement { Window = settings };
    }

    // Switch tabs the way a click does, so a tab that fails to build shows up
    // in the picture instead of being skipped.
    private static void SelectTab(Form settings, int tabIndex)
    {
        foreach (Control control in settings.Controls)
        {
            if (control is TabControl tabs && tabIndex < tabs.TabPages.Count)
            {
                tabs.SelectedIndex = tabIndex;
                return;
            }
        }
    }
}
