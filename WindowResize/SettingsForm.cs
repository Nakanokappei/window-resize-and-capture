using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WindowResizeCapture;

// The Settings window. Built entirely in code (no designer). A three-tab
// layout: General (preset sizes, launch at login), Capture (capture
// destinations), and Behavior (post-resize window handling). Hides
// instead of closing so it can be reused without reconstruction.
public class SettingsForm : Form
{
    private readonly SettingsStore _store = SettingsStore.Shared;

    // General tab controls
    private CheckedListBox _builtInList = null!;
    private CheckedListBox _customList = null!;

    // Set while the lists are being filled, because SetItemChecked raises
    // ItemCheck as if a person had clicked the box.
    private bool _fillingTheLists;
    private TextBox _widthBox = null!;
    private TextBox _heightBox = null!;
    private TextBox _nameBox = null!;
    private Button _removeButton = null!;

    // Capture tab controls. Only the ones something later has to reach are
    // kept: a control that is placed and never looked at again is a local in
    // the method that builds it, and its parent holds on to it.
    private CheckBox _captureEnabledCheck = null!;
    private Panel _captureOptionsPanel = null!;
    private CheckBox _captureSaveToFileCheck = null!;
    private CheckBox _captureCopyToClipboardCheck = null!;
    private Button _chooseFolderButton = null!;
    private Label _folderPathLabel = null!;

    // How the studio finds the path label among the controls of a built form.
    internal const string FolderPathName = "captureSaveFolderPath";

    // Behavior tab controls. The position tiles are checkbox-styled
    // buttons so UI Automation exposes their checked state to screen
    // readers (a plain Button has no toggle state).
    private CheckBox[] _positionTiles = null!;

    // Geometric glyphs for the 3x3 position grid (TL, T, TR, L, C, R, BL, B,
    // BR): filled triangles pointing/leaning toward each edge or corner, and
    // a filled circle for center. All render in a standard Windows font.
    private static readonly string[] PositionGlyphs =
        { "◤", "▲", "◥", "◀", "●", "▶", "◣", "▼", "◢" };

    // Selected-tile background: the Windows accent blue, which keeps the
    // white glyph above the 4.5:1 contrast threshold (DodgerBlue did not).
    private static readonly Color SelectedTileColor = Color.FromArgb(0, 99, 177);

    // WindowPosition enum values in the same grid order as the glyphs
    private static readonly WindowPosition[] PositionOrder =
    {
        WindowPosition.TopLeft, WindowPosition.Top, WindowPosition.TopRight,
        WindowPosition.Left, WindowPosition.Center, WindowPosition.Right,
        WindowPosition.BottomLeft, WindowPosition.Bottom, WindowPosition.BottomRight
    };

    public SettingsForm()
    {
        BuildLayout();
        PopulateLists();
    }

    // ── Layout construction ──────────────────────────────────────────────

    // Construct the window chrome and the three-tab layout. Each tab is
    // built by its own helper so the sections stay readable.
    private void BuildLayout()
    {
        // Every size and position below is written for a 96 DPI display, so
        // tell WinForms that and let it scale the whole form when it is built
        // on a higher-DPI one. Both lines are needed together: setting the
        // mode alone grows the font and leaves the controls where they were,
        // which is how an earlier attempt produced a window with clipped
        // labels and a list two rows tall.
        //
        // This has no effect on the shipping app, which runs DPI unaware and
        // is therefore always told its display is 96 DPI. It is what lets the
        // studio photograph this window from a DPI-aware process.
        //
        // In a language that reads right to left, Windows mirrors a window's
        // whole layout: the tabs run from the right, a check box keeps its box
        // on the side the reading starts, a label its text. Both properties are
        // needed - the first turns the text around, the second the layout - and
        // they are set before anything is built. Every coordinate below stays
        // written left to right; WinForms does the mirroring.
        if (App.ReadsRightToLeft)
        {
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
        }

        Text = Strings.SettingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        // Tall enough for the General tab, which is the longest of the three:
        // both size lists at their full height, the editor under them and two
        // check boxes under that.
        ClientSize = new Size(420, 554);

        var tabs = new TabControl
        {
            Location = new Point(8, 8),
            Size = new Size(404, 538),

            // The form's own RightToLeftLayout mirrors what sits directly on it
            // and stops at a tab control, which carries its own. Without this
            // one the tabs ran from the right while everything on their pages
            // stayed left-aligned - the text turned around, the layout did not.
            RightToLeftLayout = App.ReadsRightToLeft,
        };

        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildCaptureTab());
        tabs.TabPages.Add(BuildBehaviorTab());
        Controls.Add(tabs);

        // Scale the finished layout explicitly rather than leaving it to
        // AutoScaleMode. Auto-scaling runs on every Controls.Add and writes
        // the current dimensions back over the design ones, so a form built
        // in code compares 192 against 192 and scales by one. The system font
        // already grows with the display, which is what left the text too big
        // for its controls; only the bounds need this.
        if (DesignScale > 1f)
            Scale(new SizeF(DesignScale, DesignScale));
    }

    // Turning the layout around waits for the window to have a handle.
    //
    // A tab page added to a tab control that has not been created yet reports the
    // size of a bare control rather than the page rectangle: it said 208 pixels
    // wide where the page is 396. Both passes below divide that width, so both
    // came out wrong in a way only an Arabic picture showed. A group box that
    // fills its page was mirrored to x=-188, half of it outside the window, and
    // the room left for a check box label was so narrow that the longest Arabic
    // setting wrapped to five lines and pushed the setting under it off the
    // bottom of the page. The build no longer claims those sizes are final; the
    // handle is what makes them final.
    private bool _turnedAround;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Once only. This window hides on close and is shown again, and a handle
        // is created again with it.
        if (_turnedAround || !App.ReadsRightToLeft)
            return;

        _turnedAround = true;
        WrapWideSettings(this);
        MirrorLayout(this);
        MirrorPositionGrid();
    }

    // The width a child of this container is turned around inside.
    //
    // A tab page cannot be asked for it. Until the tab control has laid its pages
    // out - which it has still not done when the window is handed its handle - a
    // page reports the size of a bare control, 400 by 200, and mirroring a
    // full-width group box against that put half of it outside the window. The
    // tab control knows the page rectangle as soon as it has a handle of its own,
    // so ask the control instead of the page.
    private static int RoomInside(Control container) =>
        container is TabPage page && page.Parent is TabControl tabs
            ? tabs.DisplayRectangle.Width
            : container.ClientSize.Width;

    // How much larger the display draws things than the 96 DPI every number in
    // this form is written for. Anything measured from the running font has to
    // be divided by this to become a design number.
    private float DesignScale => Math.Max(DeviceDpi / 96f, 1f);

    // General tab: built-in preset list, custom size editor, launch at login.
    private TabPage BuildGeneralTab()
    {
        var tab = new TabPage(Strings.SettingsGeneral);

        // ── Built-in sizes group ──
        //
        // Tall enough for every built-in size at once. Four rows of thirteen
        // showed while the list was something to read; it is something to clear
        // check boxes in now, and nine sizes hidden under a scroll bar is nine
        // sizes a person has to go looking for. Everything below is placed from
        // this number, so the group can be resized here alone.
        // Generous rather than exact: a list box trims its own height to a whole
        // number of rows, so slack disappears and a shortfall of a pixel costs a
        // whole row. 228 showed twelve of the thirteen.
        int builtInHeight = 248;

        var builtInGroup = new GroupBox
        {
            Text = Strings.SettingsBuiltIn,
            Size = new Size(380, builtInHeight + 32)
        };
        Place(tab, builtInGroup, 8, 8);

        _builtInList = new CheckedListBox
        {
            Size = new Size(364, builtInHeight),
            BorderStyle = BorderStyle.None,
            CheckOnClick = true,
            AccessibleName = Strings.SettingsBuiltIn
        };
        _builtInList.ItemCheck += (_, e) => OnSizeChecked(SettingsStore.BuiltInSizes, e);
        Place(builtInGroup, _builtInList, 8, 20);

        // ── Custom sizes group ──
        int customTop = 8 + builtInHeight + 32 + 8;
        var customGroup = new GroupBox
        {
            Text = Strings.SettingsCustom,
            Size = new Size(380, 150)
        };
        Place(tab, customGroup, 8, customTop);

        _customList = new CheckedListBox
        {
            Size = new Size(280, 55),
            BorderStyle = BorderStyle.FixedSingle,
            CheckOnClick = true,
            AccessibleName = Strings.SettingsCustom
        };
        _customList.ItemCheck += (_, e) => OnSizeChecked(_store.CustomSizes, e);
        Place(customGroup, _customList, 8, 20);

        // Remove button beside the custom list
        _removeButton = new Button
        {
            Text = Strings.SettingsRemove,
            Size = new Size(80, 28),
            Enabled = false
        };
        _removeButton.Click += OnRemovePreset;
        Place(customGroup, _removeButton, 292, 20);

        // Enable the remove button only when a custom size is selected
        _customList.SelectedIndexChanged += (_, _) =>
            _removeButton.Enabled = _customList.SelectedIndex >= 0;

        // ── Add-size rows: width × height, then optional name + Add ──
        Place(customGroup, new Label
        {
            Text = Strings.SettingsWidth,
            AutoSize = true
        }, 8, 87);

        _widthBox = new TextBox
        {
            Size = new Size(60, 23),
            AccessibleName = Strings.SettingsWidth
        };
        Place(customGroup, _widthBox, 64, 84);

        Place(customGroup, new Label
        {
            Text = Strings.SettingsDimensionSeparator,
            AutoSize = true
        }, 130, 87);

        Place(customGroup, new Label
        {
            Text = Strings.SettingsHeight,
            AutoSize = true
        }, 146, 87);

        _heightBox = new TextBox
        {
            Size = new Size(60, 23),
            AccessibleName = Strings.SettingsHeight
        };
        Place(customGroup, _heightBox, 204, 84);

        Place(customGroup, new Label
        {
            Text = Strings.SettingsName,
            AutoSize = true
        }, 8, 119);

        _nameBox = new TextBox
        {
            Size = new Size(200, 23),
            AccessibleName = Strings.SettingsName
        };
        Place(customGroup, _nameBox, 64, 116);

        var addButton = new Button
        {
            Text = Strings.SettingsAdd,
            Size = new Size(80, 26)
        };
        addButton.Click += OnAddPreset;
        Place(customGroup, addButton, 292, 114);

        // ── Size by client area ──
        AddSettingCheck(
            tab, Strings.SettingsResizeClientArea, new Point(12, customTop + 158),
            _store.ResizeClientArea,
            on =>
            {
                _store.ResizeClientArea = on;
                _store.SaveAndNotify();
            });

        // ── Launch at login ──
        // The only setting that is not kept in the settings file, so it is
        // also the only one that does not save and notify: writing it registers
        // the app with Windows itself.
        AddSettingCheck(
            tab, Strings.SettingsLaunchAtLogin, new Point(12, customTop + 184),
            _store.LaunchAtLogin,
            on => _store.LaunchAtLogin = on);

        return tab;
    }

    // ── Placing controls ─────────────────────────────────────────────────

    // Put a control at a position written for a left-to-right layout.
    private static void Place(Control parent, Control child, int x, int y)
    {
        child.Location = new Point(x, y);
        parent.Controls.Add(child);
    }

    // Let a setting whose label is wider than the room it has wrap onto a second
    // line, and push the rows below it down by what it grew.
    //
    // Arabic has the long labels. Left to right they simply run to the edge of
    // the tab and stop, but mirrored they run off the side the reading starts
    // at, where a sentence loses its beginning instead of its end - and the
    // widest of them disappeared from the picture altogether.
    private void WrapWideSettings(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            WrapWideSettings(child);

            if (child is not CheckBox check || Array.IndexOf(_positionTiles, check) >= 0)
                continue;

            int room = RoomInside(parent) - check.Left - 8;
            if (check.Width <= room)
                continue;

            int grew = WrapLabel(check, room);
            foreach (Control below in parent.Controls)
            {
                if (below != check && below.Top > check.Top)
                    below.Top += grew;
            }
        }
    }

    // Hold the check box to the width it has and let its label take the lines it
    // needs. Returns how much taller it came out.
    private static int WrapLabel(CheckBox check, int room)
    {
        int before = check.Height;

        // What the box and its padding take, so only the text has to be fitted.
        int furniture = check.Width - TextRenderer.MeasureText(check.Text, check.Font).Width;
        var text = TextRenderer.MeasureText(
            check.Text, check.Font,
            new Size(Math.Max(room - furniture, 40), int.MaxValue),
            TextFormatFlags.WordBreak);

        check.AutoSize = false;
        check.Size = new Size(room, Math.Max(text.Height + 4, before));

        return check.Height - before;
    }

    // Turn a hand-written layout around for a language that reads right to left.
    //
    // WinForms turns the text inside a control around on its own, and mirrors
    // what sits directly on a form or a tab strip, but not where a control sits
    // inside a tab page or a panel - and every position in this window is
    // written out by hand. Setting RightToLeft alone therefore produced an
    // Arabic window whose labels read right to left while every row stayed
    // pinned to the left edge.
    //
    // Done in one pass at the end rather than control by control, because a tab
    // page has no width worth measuring until the tab control has it, and the
    // page is filled before it is added.
    private void MirrorLayout(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            // The position tiles are moved together, further down, and must not
            // be turned around one at a time.
            if (child is CheckBox tile && Array.IndexOf(_positionTiles, tile) >= 0)
                continue;

            // A tab page is positioned by the tab control, not from here.
            if (child is not TabPage)
                child.Left = RoomInside(parent) - child.Left - child.Width;

            MirrorLayout(child);
        }
    }

    // The grid of position tiles moves as a block, keeping its columns in order.
    // It stands for places on a screen, and the tile that means "top left" is
    // the reader's own top left whichever way the language runs.
    private void MirrorPositionGrid()
    {
        var parent = _positionTiles[0].Parent;
        if (parent == null)
            return;

        int left = int.MaxValue;
        int right = 0;
        foreach (var tile in _positionTiles)
        {
            left = Math.Min(left, tile.Left);
            right = Math.Max(right, tile.Right);
        }

        int shift = RoomInside(parent) - right - left;
        foreach (var tile in _positionTiles)
            tile.Left += shift;
    }

    // One setting shown as a check box: labelled, placed, filled in from the
    // store and writing straight back to it.
    //
    // Every check box on these three tabs is this same shape. Written out once
    // per setting, the shape is what a new setting is copied from, and the
    // copy that forgets to save looks exactly like the ones that do not.
    private CheckBox AddSettingCheck(
        Control parent, string label, Point at, bool value, Action<bool> apply)
    {
        var check = new CheckBox
        {
            Text = label,
            AutoSize = true,
            Checked = value
        };
        check.CheckedChanged += (_, _) => apply(check.Checked);
        Place(parent, check, at.X, at.Y);
        return check;
    }

    // Capture tab: master toggle plus a panel of destination options
    // that hides while captures are disabled.
    private TabPage BuildCaptureTab()
    {
        var tab = new TabPage(Strings.SettingsCapture);

        // Master capture toggle
        _captureEnabledCheck = AddSettingCheck(
            tab, Strings.SettingsCaptureEnabled, new Point(12, 12),
            _store.CaptureEnabled,
            on =>
            {
                _store.CaptureEnabled = on;
                _store.SaveAndNotify();
                SynchronizeCaptureControls();
                _captureOptionsPanel.Visible = _store.CaptureEnabled;
            });

        // Panel for capture destination options, hidden when disabled
        _captureOptionsPanel = new Panel
        {
            Location = new Point(0, 40),
            Size = new Size(396, 112),
            Visible = _store.CaptureEnabled
        };

        int panelY = 0;

        // Save-to-file checkbox
        _captureSaveToFileCheck = AddSettingCheck(
            _captureOptionsPanel, Strings.SettingsCaptureSaveToFile, new Point(28, panelY),
            _store.CaptureSaveToFile,
            on =>
            {
                _store.CaptureSaveToFile = on;
                _store.SaveAndNotify();
                _chooseFolderButton.Enabled = _store.CaptureSaveToFile;
                SynchronizeCaptureControls();
            });
        panelY += 26;

        // Folder chooser button and path label
        //
        // The button is measured here rather than left to AutoSize. Every number
        // in this form is written for 96 DPI and the whole layout is scaled once
        // at the end, but a control that sizes itself has already measured the
        // display's own font, and scaling that again drew this button twice the
        // size of the ones beside it. Measuring the text and dividing the
        // display's scaling back out puts it in the same units as its
        // neighbours, and still fits whichever language labels it.
        string chooseFolder = Strings.SettingsCaptureChooseFolder;
        int chooseFolderWidth =
            (int)(TextRenderer.MeasureText(chooseFolder, Font).Width / DesignScale) + 20;

        _chooseFolderButton = new Button
        {
            Text = chooseFolder,
            Size = new Size(chooseFolderWidth, 28),
            Enabled = _store.CaptureSaveToFile
        };
        _chooseFolderButton.Click += OnChooseCaptureFolder;
        Place(_captureOptionsPanel, _chooseFolderButton, 44, panelY);

        // GrayText keeps at least AA contrast in the default theme and
        // adapts to high-contrast themes, unlike a hard-coded gray
        //
        // The width is what is left of the panel rather than a fixed 220: the
        // button beside it is as wide as its own label, so in a language with a
        // longer word for "Choose Folder" a fixed width ran the path off the
        // panel and cut it without even an ellipsis.
        // Measured from where the button was asked to go rather than from where
        // it ended up: Place may have mirrored it, and these are the numbers a
        // left-to-right layout is written in.
        int pathLeft = 44 + chooseFolderWidth + 8;

        _folderPathLabel = new Label
        {
            // Named so that the studio taking the store pictures can find it.
            // The path is the one place in this window where a character is
            // drawn differently depending on the language Windows itself is
            // running in, and a listing picture has to show the language it is
            // for rather than the machine the picture was taken on.
            Name = FolderPathName,
            Text = FormatFolderPath(),
            Size = new Size(Math.Max(_captureOptionsPanel.Width - pathLeft - 8, 120), 20),
            ForeColor = SystemColors.GrayText,
            AutoEllipsis = true
        };
        Place(_captureOptionsPanel, _folderPathLabel, pathLeft, panelY + 4);
        panelY += _chooseFolderButton.Height + 2;

        // Copy-to-clipboard checkbox
        _captureCopyToClipboardCheck = AddSettingCheck(
            _captureOptionsPanel, Strings.SettingsCaptureCopyToClipboard, new Point(28, panelY),
            _store.CaptureCopyToClipboard,
            on =>
            {
                _store.CaptureCopyToClipboard = on;
                _store.SaveAndNotify();
                SynchronizeCaptureControls();
            });
        panelY += 26;

        // Capture client area only
        AddSettingCheck(
            _captureOptionsPanel, Strings.SettingsCaptureClientArea, new Point(28, panelY),
            _store.CaptureClientArea,
            on =>
            {
                _store.CaptureClientArea = on;
                _store.SaveAndNotify();
            });
        tab.Controls.Add(_captureOptionsPanel);

        return tab;
    }

    // Behavior tab: post-resize options and the 3x3 snap-position grid.
    private TabPage BuildBehaviorTab()
    {
        var tab = new TabPage(Strings.SettingsBehavior);

        // Bring to front
        AddSettingCheck(
            tab, Strings.SettingsBringToFront, new Point(12, 12),
            _store.BringToFront,
            on =>
            {
                _store.BringToFront = on;
                _store.SaveAndNotify();
            });

        // Move to main screen
        AddSettingCheck(
            tab, Strings.SettingsMoveToMainScreen, new Point(12, 40),
            _store.MoveToMainScreen,
            on =>
            {
                _store.MoveToMainScreen = on;
                _store.SaveAndNotify();
            });

        // Position-after-resize label with a 3x3 tile grid below it
        Place(tab, new Label
        {
            Text = Strings.SettingsWindowPosition,
            AutoSize = true
        }, 12, 72);

        // Screen readers cannot pronounce the geometric glyphs, so each
        // tile carries a localized position name as its UIA name
        string[] positionNames =
        {
            Strings.SettingsPositionTopLeft, Strings.SettingsPositionTop, Strings.SettingsPositionTopRight,
            Strings.SettingsPositionLeft, Strings.SettingsPositionCenter, Strings.SettingsPositionRight,
            Strings.SettingsPositionBottomLeft, Strings.SettingsPositionBottom, Strings.SettingsPositionBottomRight
        };

        int gridTop = 96;
        int tileSize = 32;
        int tileGap = 2;
        _positionTiles = new CheckBox[9];

        var glyphFont = new Font("Segoe UI Symbol", 10f);
        for (int i = 0; i < 9; i++)
        {
            // Arrange the nine tiles as three rows of three. AutoCheck is
            // off because the checked state mirrors the store: clicking
            // raises Click, the store updates, and the refresh below sets
            // Checked on every tile (only one may be active).
            int col = i % 3;
            int row = i / 3;
            var tile = new CheckBox
            {
                Appearance = Appearance.Button,
                AutoCheck = false,
                Size = new Size(tileSize, tileSize),
                FlatStyle = FlatStyle.Flat,
                Tag = PositionOrder[i],
                Font = glyphFont,
                Text = PositionGlyphs[i],
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = Padding.Empty,
                AccessibleName = positionNames[i]
            };
            tile.FlatAppearance.BorderSize = 1;
            tile.Click += OnPositionTileClick;
            _positionTiles[i] = tile;

            Place(tab, tile,
                12 + col * (tileSize + tileGap),
                gridTop + row * (tileSize + tileGap));
        }

        // How far the position above holds the window off the edge it sends it
        // to. Under the grid, because it says something about what the grid does
        // and means nothing without it.
        int marginTop = gridTop + 3 * (tileSize + tileGap) + 12;
        Place(tab, new Label
        {
            Text = Strings.SettingsEdgeMargin,
            AutoSize = true
        }, 12, marginTop);

        // No margin first, then the two measures in ascending height, so that
        // reading down the list is reading a widening gap. A title bar is always
        // the shorter of the two: 23 pixels against the taskbar's 48 at 96 dpi,
        // and both grow with the scaling.
        (string label, ScreenEdgeMargin margin)[] choices =
        {
            (Strings.SettingsEdgeMarginNone, ScreenEdgeMargin.None),
            (Strings.SettingsEdgeMarginTitleBar, ScreenEdgeMargin.TitleBar),
            (Strings.SettingsEdgeMarginTaskbar, ScreenEdgeMargin.Taskbar),
        };

        for (int i = 0; i < choices.Length; i++)
        {
            var choice = new RadioButton
            {
                Text = choices[i].label,
                AutoSize = true,
                Tag = choices[i].margin,
                Checked = _store.EdgeMargin == choices[i].margin
            };

            // CheckedChanged arrives twice for one click - once for the button
            // being cleared and once for the button being set - so only the one
            // being set writes to the store.
            choice.CheckedChanged += (sender, _) =>
            {
                if (sender is RadioButton picked && picked.Checked &&
                    picked.Tag is ScreenEdgeMargin margin)
                {
                    _store.EdgeMargin = margin;
                    _store.SaveAndNotify();
                }
            };

            Place(tab, choice, 12, marginTop + 24 + i * 24);
        }

        RefreshPositionTiles();
        return tab;
    }

    // ── Data population ──────────────────────────────────────────────────

    // Fill the built-in and custom size list boxes from the store. A checked
    // box means the size is offered in the menu.
    private void PopulateLists()
    {
        _fillingTheLists = true;

        _builtInList.Items.Clear();
        foreach (var size in SettingsStore.BuiltInSizes)
            _builtInList.Items.Add(FormatSize(size), _store.ShowsInMenu(size));

        _fillingTheLists = false;

        RefreshCustomList();
    }

    // Rebuild the custom sizes list and show a placeholder when empty.
    private void RefreshCustomList()
    {
        _fillingTheLists = true;
        _customList.Items.Clear();

        if (_store.CustomSizes.Count == 0)
        {
            _customList.Items.Add(Strings.SettingsNoCustomSizes);
            _customList.Enabled = false;
        }
        else
        {
            foreach (var size in _store.CustomSizes)
                _customList.Items.Add(FormatSize(size), _store.ShowsInMenu(size));
            _customList.Enabled = true;
        }

        _fillingTheLists = false;
        _removeButton.Enabled = false;
    }

    // Render a preset as "W x H" followed by its label when one is set.
    private static string FormatSize(PresetSize size)
    {
        string display = size.DisplayDimensions;
        if (!string.IsNullOrEmpty(size.Label))
            display += $"    {size.DisplayLabel}";
        return display;
    }

    // ── Event handlers ───────────────────────────────────────────────────

    // Parse the width/height inputs and add a new custom preset with an
    // optional user-supplied name.
    private void OnAddPreset(object? sender, EventArgs e)
    {
        // A size has to be two positive numbers. Anything else leaves the
        // boxes as they are, so the typing is not thrown away.
        if (!int.TryParse(_widthBox.Text, out int width) ||
            !int.TryParse(_heightBox.Text, out int height) ||
            width <= 0 || height <= 0)
        {
            return;
        }

        string name = _nameBox.Text.Trim();
        _store.AddSize(new PresetSize(width, height, name.Length > 0 ? name : null));
        _widthBox.Clear();
        _heightBox.Clear();
        _nameBox.Clear();
        RefreshCustomList();
    }

    // Clearing a box keeps that size out of the menu. ItemCheck arrives before
    // the box has changed, so the new state is the one being asked for.
    //
    // Both lists come here with the sizes they were filled from, because the
    // rule is the same for a built-in size and one the person added. Checking
    // the index against that list also disposes of the placeholder line
    // standing in for an empty custom list: it is not a size, and there are no
    // sizes for it to be the first of.
    private void OnSizeChecked(IReadOnlyList<PresetSize> sizes, ItemCheckEventArgs e)
    {
        if (_fillingTheLists || e.Index < 0 || e.Index >= sizes.Count)
            return;

        _store.SetShowsInMenu(sizes[e.Index], e.NewValue == CheckState.Checked);
    }

    // Remove the currently selected custom preset.
    private void OnRemovePreset(object? sender, EventArgs e)
    {
        int index = _customList.SelectedIndex;
        if (index < 0 || index >= _store.CustomSizes.Count)
            return;

        _store.RemoveSize(_store.CustomSizes[index]);
        RefreshCustomList();
    }

    // Toggle the selected snap position. Clicking the already-active
    // position clears it (no snap).
    private void OnPositionTileClick(object? sender, EventArgs e)
    {
        if (sender is not CheckBox tile || tile.Tag is not WindowPosition position)
            return;

        _store.Position = (_store.Position == position) ? null : position;
        _store.SaveAndNotify();
        RefreshPositionTiles();
    }

    // Open a folder browser to choose the capture save location.
    private void OnChooseCaptureFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (!string.IsNullOrEmpty(_store.CaptureSaveFolderPath))
            dialog.SelectedPath = _store.CaptureSaveFolderPath;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _store.CaptureSaveFolderPath = dialog.SelectedPath;
            _store.SaveAndNotify();
            _folderPathLabel.Text = FormatFolderPath();
        }
    }

    // ── UI synchronisation helpers ───────────────────────────────────────

    // After the store's auto-enable/disable logic fires, push the
    // canonical state back into the checkboxes without re-triggering events.
    private void SynchronizeCaptureControls()
    {
        if (_captureEnabledCheck.Checked != _store.CaptureEnabled)
            _captureEnabledCheck.Checked = _store.CaptureEnabled;
        if (_captureSaveToFileCheck.Checked != _store.CaptureSaveToFile)
            _captureSaveToFileCheck.Checked = _store.CaptureSaveToFile;
        if (_captureCopyToClipboardCheck.Checked != _store.CaptureCopyToClipboard)
            _captureCopyToClipboardCheck.Checked = _store.CaptureCopyToClipboard;
    }

    // Highlight the currently selected position tile and reset the rest.
    // Checked feeds the UIA toggle state; the colours are the visual cue.
    private void RefreshPositionTiles()
    {
        foreach (var tile in _positionTiles)
        {
            if (tile.Tag is not WindowPosition position) continue;
            bool selected = _store.Position == position;
            tile.Checked = selected;
            tile.BackColor = selected ? SelectedTileColor : SystemColors.Control;
            tile.ForeColor = selected ? Color.White : SystemColors.ControlText;
        }
    }

    // Return the folder path for display, or a placeholder if none is set.
    private string FormatFolderPath()
    {
        return string.IsNullOrEmpty(_store.CaptureSaveFolderPath)
            ? Strings.SettingsCaptureNoFolderSelected
            : _store.CaptureSaveFolderPath;
    }

    // Hide instead of closing so the form can be reused without rebuilding.
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }
}
