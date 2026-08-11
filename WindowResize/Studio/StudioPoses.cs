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
    // What a pose leaves on screen, so the session can take it down again, and
    // why it cannot be photographed when that is how it ended.
    //
    // A failed pose is reported rather than thrown, so that what it opened is
    // still handed back and still closed. Thrown, the menu was left open, and an
    // open menu keeps a message filter that swallows Application.Exit: the run
    // that failed on the Chinese picture stayed alive afterwards, holding its own
    // binary and its zh-Hant resources locked against the next build.
    internal sealed class Arrangement : IDisposable
    {
        internal ContextMenuStrip? Menu { get; init; }
        internal Form? Window { get; init; }
        internal string? Failure { get; init; }

        // Every window this pose put on the set, in the order it opened them.
        // The camera lifts them in front of the set again before the shutter and
        // refuses the picture if one of them is not there.
        internal IReadOnlyList<Control> Opened { get; init; } = Array.Empty<Control>();

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
        // then the sizes offered for it. Every level turns the same way as the
        // one above it, so the four panels stand side by side in the picture.
        var inward = set.SubmenuDirection;

        var resize = menu.Items[0] as ToolStripMenuItem;
        if (!await Open(resize, inward))
            return new Arrangement { Menu = menu, Failure = "the resize menu did not open" };

        // The grouped item is the only one with a count after its name.
        var browser = FindItem(resize!, name => name.EndsWith(")", StringComparison.Ordinal));
        if (!await Open(browser, inward))
            return new Arrangement { Menu = menu, Failure = "the grouped windows did not open" };

        var window = browser!.DropDownItems.Count > 0
            ? browser.DropDownItems[0] as ToolStripMenuItem
            : null;
        if (!await Open(window, inward))
            return new Arrangement { Menu = menu, Failure = "the sizes for the window did not open" };

        await StudioCamera.Settle(300);

        // Now that every level is open, tell the set which parts of itself the
        // menu covers, so the marketing line keeps clear of them.
        var levels = OpenLevels(menu);
        set.Reserve(Areas(levels));

        await StudioCamera.Settle(150);

        // Then, because the set repaints itself for that reservation and the
        // menu is what has to be in front when the shutter opens.
        string? failure = await LiftAboveSet(set, levels)
            ?? StackedLevel(levels);
        if (failure != null)
            return new Arrangement { Menu = menu, Opened = levels, Failure = failure };

        // The highlight goes on last of all. It leaves the pointer's choice
        // sitting under it, so the picture catches the moment before the click
        // rather than after it - and raising the levels takes the selection off
        // whatever was carrying it. Set before that, one picture in five came
        // out showing a list of sizes instead of a size being chosen.
        var wanted = FindSize(window!, "XGA");
        wanted?.Select();
        await StudioCamera.Settle(150);

        if (wanted != null && !wanted.Selected)
            return new Arrangement
            {
                Menu = menu,
                Opened = levels,
                Failure = "the size being chosen lost its highlight",
            };

        return new Arrangement { Menu = menu, Opened = levels };
    }

    // Every level of the menu that is open, outermost first.
    //
    // Each level is a window of its own, which is why the top level's bounds
    // describe a strip and say nothing about the four panels beside it.
    private static List<ToolStripDropDown> OpenLevels(ToolStripDropDown menu)
    {
        var levels = new List<ToolStripDropDown> { menu };

        void Walk(ToolStripItemCollection items)
        {
            foreach (ToolStripItem child in items)
            {
                if (child is not ToolStripMenuItem item || !item.HasDropDownItems)
                    continue;

                if (item.DropDown.Visible)
                {
                    levels.Add(item.DropDown);
                    Walk(item.DropDownItems);
                }
            }
        }

        Walk(menu.Items);
        return levels;
    }

    // Where on screen each window the pose opened stands. Kept apart rather than
    // unioned: an open menu is a staircase, and the box around it claims room
    // beside the tall level that nothing is standing in.
    private static System.Drawing.Rectangle[] Areas(IReadOnlyList<Control> opened)
    {
        var areas = new System.Drawing.Rectangle[opened.Count];
        for (int index = 0; index < opened.Count; index++)
            areas[index] = opened[index].Bounds;
        return areas;
    }

    // Put what the pose opened back in front of the set. Returns null once it is
    // there, or why the picture cannot be taken.
    //
    // The set and every window a pose opens are all topmost, and the set is
    // activated once more while the levels are being opened - often enough to
    // matter. The levels opened before that moment end up behind it: one picture
    // in three came out with the top two levels of the menu missing, and nothing
    // downstream could tell. The file is exactly the right size and holds no
    // window belonging to another program, which is all the shutter checks.
    //
    // So raise them again in the order they were opened, then look at the screen
    // to see which window is really there. Raising once was not always enough:
    // a full shoot of sixteen languages lost the Chinese picture to this. Try a
    // few times, a moment apart, and only then give up - a shoot that stops
    // costs the operator the rest of the run.
    private static async Task<string?> LiftAboveSet(
        StudioSetForm set, IReadOnlyList<Control> opened)
    {
        Control? behind = null;

        for (int attempt = 0; attempt < 4; attempt++)
        {
            foreach (var window in opened)
                StudioCamera.Raise(window.Handle);

            await StudioCamera.Settle(120);

            behind = StudioCamera.FirstCoveredBy(set.Handle, opened);
            if (behind == null)
                return null;
        }

        // The rectangle is in the message because it is the only way to tell a
        // window that is really behind the set from one that reported a place
        // nobody could see it.
        return $"the set stayed in front of the {behind!.GetType().Name} " +
            $"the pose opened at {behind.Bounds}";
    }

    // Why the picture cannot be taken when one level of the menu has opened on
    // top of another, or null when the four of them stand side by side.
    //
    // Every level is a panel beside its parent, so a level over a level means one
    // of them opened somewhere it was not asked to and is hiding what is under
    // it. This is how the Russian picture came out of a full shoot: its list of
    // windows was underneath its list of sizes, and every other check passed.
    //
    // Panels that merely touch are not stacked. The tolerance is what a shared
    // edge and a shadow come to; a level that is really hidden overlaps its
    // neighbour by its whole width.
    private static string? StackedLevel(IReadOnlyList<Control> levels)
    {
        const int touching = 8;

        for (int outer = 0; outer < levels.Count; outer++)
        {
            for (int inner = outer + 1; inner < levels.Count; inner++)
            {
                var shared = System.Drawing.Rectangle.Intersect(
                    levels[outer].Bounds, levels[inner].Bounds);

                if (shared.Width > touching && shared.Height > touching)
                    return "two levels of the menu opened on top of each other";
            }
        }

        return null;
    }

    private static async Task<bool> Open(
        ToolStripMenuItem? item, ToolStripDropDownDirection direction)
    {
        if (item == null)
            return false;

        item.Select();
        item.DropDownDirection = direction;
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

    // Draw the save folder in a typeface that spells a path the way the language
    // being photographed spells it.
    //
    // A Japanese typeface draws the path separator as a yen sign. That is not a
    // fault: it is the convention Japanese Windows has always followed, and a
    // Japanese user sees it in Explorer too. But the shoot runs on one machine,
    // and this app takes the font Windows hands it, so fifteen listings came out
    // reading C:\Users\Alex\Pictures\Captures with yen signs in it - the
    // operator's own machine showing through a picture meant for somebody else's.
    //
    // Only this one label, and only its typeface. The path itself is ASCII, so a
    // Latin face has every character it needs, and the Japanese picture is left
    // exactly as a Japanese user would see it.
    private static void ShowThePathAsThatLanguageWould(Form settings)
    {
        if (System.Globalization.CultureInfo.CurrentUICulture
            .TwoLetterISOLanguageName == "ja")
            return;

        var found = settings.Controls.Find(SettingsForm.FolderPathName, searchAllChildren: true);
        if (found.Length == 0)
            return;

        var label = found[0];
        label.Font = new System.Drawing.Font(
            "Segoe UI", label.Font.Size, label.Font.Style, label.Font.Unit);
    }

    // The real settings window, centered on the set and switched to one tab.
    private static async Task<Arrangement> OpenSettings(StudioSetForm set, int tabIndex)
    {
        var settings = new SettingsForm();
        settings.StartPosition = FormStartPosition.Manual;
        settings.TopMost = true;
        settings.Show();

        ShowThePathAsThatLanguageWould(settings);

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

        // After the tab is chosen, because choosing one puts the focus rectangle
        // back on the tab strip.
        StudioCamera.HideFocusCues(settings.Handle);

        // Keep the marketing line clear of the window, the same way the menu
        // pose does.
        set.Reserve(new[] { settings.Bounds });

        await StudioCamera.Settle(400);

        // And in front of the set, for the same reason the menu has to be.
        string? failure = await LiftAboveSet(set, new[] { settings });

        return new Arrangement
        {
            Window = settings,
            Opened = new[] { settings },
            Failure = failure,
        };
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
