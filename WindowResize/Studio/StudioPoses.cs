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
            "choose-a-size" => await ChooseASize(set),
            "settings-general" => await OpenSettings(set, tabIndex: 0),
            "settings-capture" => await OpenSettings(set, tabIndex: 1),
            "settings-behavior" => await OpenSettings(set, tabIndex: 2),
            _ => throw new ArgumentException($"unknown view: {view}"),
        };
    }

    // The whole point of the app in one picture: a browser with five windows
    // open, one of them picked out, and a size about to be chosen for it.
    //
    // Every menu here is opened the way a pointer opens it. Only the list of
    // windows is staged, because the real one shows whatever the operator has
    // running and would differ in every language's picture.
    private static async Task<Arrangement> ChooseASize(StudioSetForm set)
    {
        var menu = TrayApplicationContext.BuildStudioMenu(
            StudioWindows.Staged(System.Globalization.CultureInfo.CurrentUICulture));
        menu.Font = new System.Drawing.Font(menu.Font.FontFamily,
            menu.Font.Size * StudioSetForm.MenuMagnification);

        menu.Show(set.TrayIconAnchor, set.MenuDirection);
        await StudioCamera.Settle(250);

        // Resize, then the browser's group of five, then one of its windows,
        // then the sizes offered for it.
        var resize = menu.Items[0] as ToolStripMenuItem;
        if (!await Open(resize))
            return new Arrangement { Menu = menu };

        // The grouped item is the only one with a count after its name.
        var browser = FindItem(resize!, name => name.EndsWith(")", StringComparison.Ordinal));
        if (!await Open(browser))
            return new Arrangement { Menu = menu };

        var window = browser!.DropDownItems.Count > 0
            ? browser.DropDownItems[0] as ToolStripMenuItem
            : null;
        if (!await Open(window))
            return new Arrangement { Menu = menu };

        // Leave the pointer's choice sitting under the highlight, so the
        // picture catches the moment before the click rather than after it.
        var wanted = FindSize(window!, "XGA");
        wanted?.Select();

        await StudioCamera.Settle(300);

        // Now that every level is open, tell the set how much of itself the
        // menu covers, so the marketing line can wrap clear of it.
        set.Reserve(OpenArea(menu));

        await StudioCamera.Settle(150);
        return new Arrangement { Menu = menu };
    }

    // The rectangle the menu occupies on screen, counting every dropdown that
    // is currently open. Each level is its own window, so the top-level bounds
    // alone would describe a strip and miss the four panels beside it.
    private static System.Drawing.Rectangle OpenArea(ToolStripDropDown menu)
    {
        var area = menu.Bounds;

        void Walk(ToolStripItemCollection items)
        {
            foreach (ToolStripItem child in items)
            {
                if (child is not ToolStripMenuItem item || !item.HasDropDownItems)
                    continue;

                if (item.DropDown.Visible)
                {
                    area = System.Drawing.Rectangle.Union(area, item.DropDown.Bounds);
                    Walk(item.DropDownItems);
                }
            }
        }

        Walk(menu.Items);
        return area;
    }

    private static async Task<bool> Open(ToolStripMenuItem? item)
    {
        if (item == null)
            return false;

        item.Select();
        item.ShowDropDown();
        await StudioCamera.Settle(250);
        return item.DropDownItems.Count > 0;
    }

    private static ToolStripMenuItem? FindItem(
        ToolStripMenuItem parent, Func<string, bool> matches)
    {
        foreach (ToolStripItem child in parent.DropDownItems)
        {
            if (child is ToolStripMenuItem item && matches(item.Text ?? ""))
                return item;
        }
        return null;
    }

    // Preset sizes carry their label in the right-aligned tag rather than in
    // the text, which holds the dimensions.
    private static ToolStripMenuItem? FindSize(ToolStripMenuItem window, string label)
    {
        foreach (ToolStripItem child in window.DropDownItems)
        {
            if (child is ToolStripMenuItem item &&
                string.Equals(item.ShortcutKeyDisplayString, label, StringComparison.Ordinal))
            {
                return item;
            }
        }
        return null;
    }

    // The real settings window, centered on the set and switched to one tab.
    private static async Task<Arrangement> OpenSettings(StudioSetForm set, int tabIndex)
    {
        var settings = new SettingsForm();
        settings.StartPosition = FormStartPosition.Manual;
        settings.TopMost = true;
        settings.Show();

        // Grow with the rest of the set, on top of the scaling the form
        // already does for the display's own DPI, but never past the point
        // where the window stops fitting. At the full magnification the
        // settings window stood taller than the picture itself.
        //
        // The limit is a share of the picture rather than a fixed number, so
        // it still holds if the picture size changes.
        const float share = 0.62f;
        float room = set.ClientSize.Height * share / settings.Height;
        float scale = Math.Min(StudioSetForm.Magnification, room);

        if (scale > 1f)
            settings.Scale(new System.Drawing.SizeF(scale, scale));

        // Sit toward the bottom corner the tray lives in, leaving the opposite
        // top corner clear for the marketing line - which is the left on a
        // left-to-right set and the right on a mirrored one, the same corner the
        // reader's eye starts from in each.
        int margin = set.PictureMargin;
        settings.Location = new System.Drawing.Point(
            set.Mirrored ? set.Left + margin : set.Left + set.Width - settings.Width - margin,
            set.Top + set.Height - settings.Height - margin);

        SelectTab(settings, tabIndex);

        // Keep the marketing line clear of the window, the same way the menu
        // pose does.
        set.Reserve(settings.Bounds);

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
