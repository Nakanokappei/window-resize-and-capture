using System;
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
    private ListBox _builtInList = null!;
    private ListBox _customList = null!;
    private TextBox _widthBox = null!;
    private TextBox _heightBox = null!;
    private TextBox _nameBox = null!;
    private Button _addButton = null!;
    private Button _removeButton = null!;
    private CheckBox _resizeClientAreaCheck = null!;
    private CheckBox _launchAtLoginCheck = null!;

    // Capture tab controls
    private CheckBox _captureEnabledCheck = null!;
    private Panel _captureOptionsPanel = null!;
    private CheckBox _captureSaveToFileCheck = null!;
    private CheckBox _captureCopyToClipboardCheck = null!;
    private CheckBox _captureClientAreaCheck = null!;
    private Button _chooseFolderButton = null!;
    private Label _folderPathLabel = null!;

    // Behavior tab controls. The position tiles are checkbox-styled
    // buttons so UI Automation exposes their checked state to screen
    // readers (a plain Button has no toggle state).
    private CheckBox _bringToFrontCheck = null!;
    private CheckBox _moveToMainScreenCheck = null!;
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
        Text = Strings.SettingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        ClientSize = new Size(420, 384);

        var tabs = new TabControl
        {
            Location = new Point(8, 8),
            Size = new Size(404, 368)
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

    // How much larger the display draws things than the 96 DPI every number in
    // this form is written for. Anything measured from the running font has to
    // be divided by this to become a design number.
    private float DesignScale => Math.Max(DeviceDpi / 96f, 1f);

    // General tab: built-in preset list, custom size editor, launch at login.
    private TabPage BuildGeneralTab()
    {
        var tab = new TabPage(Strings.SettingsGeneral);

        // ── Built-in sizes group ──
        var builtInGroup = new GroupBox
        {
            Text = Strings.SettingsBuiltIn,
            Location = new Point(8, 8),
            Size = new Size(380, 100)
        };

        _builtInList = new ListBox
        {
            Location = new Point(8, 20),
            Size = new Size(364, 70),
            SelectionMode = SelectionMode.None,
            BorderStyle = BorderStyle.None,
            AccessibleName = Strings.SettingsBuiltIn
        };
        builtInGroup.Controls.Add(_builtInList);
        tab.Controls.Add(builtInGroup);

        // ── Custom sizes group ──
        var customGroup = new GroupBox
        {
            Text = Strings.SettingsCustom,
            Location = new Point(8, 116),
            Size = new Size(380, 150)
        };

        _customList = new ListBox
        {
            Location = new Point(8, 20),
            Size = new Size(280, 55),
            BorderStyle = BorderStyle.FixedSingle,
            AccessibleName = Strings.SettingsCustom
        };
        customGroup.Controls.Add(_customList);

        // Remove button beside the custom list
        _removeButton = new Button
        {
            Text = Strings.SettingsRemove,
            Location = new Point(292, 20),
            Size = new Size(80, 28),
            Enabled = false
        };
        _removeButton.Click += OnRemovePreset;
        customGroup.Controls.Add(_removeButton);

        // Enable the remove button only when a custom size is selected
        _customList.SelectedIndexChanged += (_, _) =>
            _removeButton.Enabled = _customList.SelectedIndex >= 0;

        // ── Add-size rows: width × height, then optional name + Add ──
        customGroup.Controls.Add(new Label
        {
            Text = Strings.SettingsWidth,
            Location = new Point(8, 87),
            AutoSize = true
        });

        _widthBox = new TextBox
        {
            Location = new Point(64, 84),
            Size = new Size(60, 23),
            AccessibleName = Strings.SettingsWidth
        };
        customGroup.Controls.Add(_widthBox);

        customGroup.Controls.Add(new Label
        {
            Text = Strings.SettingsDimensionSeparator,
            Location = new Point(130, 87),
            AutoSize = true
        });

        customGroup.Controls.Add(new Label
        {
            Text = Strings.SettingsHeight,
            Location = new Point(146, 87),
            AutoSize = true
        });

        _heightBox = new TextBox
        {
            Location = new Point(204, 84),
            Size = new Size(60, 23),
            AccessibleName = Strings.SettingsHeight
        };
        customGroup.Controls.Add(_heightBox);

        customGroup.Controls.Add(new Label
        {
            Text = Strings.SettingsName,
            Location = new Point(8, 119),
            AutoSize = true
        });

        _nameBox = new TextBox
        {
            Location = new Point(64, 116),
            Size = new Size(200, 23),
            AccessibleName = Strings.SettingsName
        };
        customGroup.Controls.Add(_nameBox);

        _addButton = new Button
        {
            Text = Strings.SettingsAdd,
            Location = new Point(292, 114),
            Size = new Size(80, 26)
        };
        _addButton.Click += OnAddPreset;
        customGroup.Controls.Add(_addButton);

        tab.Controls.Add(customGroup);

        // ── Size by client area ──
        _resizeClientAreaCheck = AddSettingCheck(
            tab, Strings.SettingsResizeClientArea, new Point(12, 274),
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
        _launchAtLoginCheck = AddSettingCheck(
            tab, Strings.SettingsLaunchAtLogin, new Point(12, 300),
            _store.LaunchAtLogin,
            on => _store.LaunchAtLogin = on);

        return tab;
    }

    // One setting shown as a check box: labelled, placed, filled in from the
    // store and writing straight back to it.
    //
    // Every check box on these three tabs is this same shape. Written out once
    // per setting, the shape is what a new setting is copied from, and the
    // copy that forgets to save looks exactly like the ones that do not.
    private static CheckBox AddSettingCheck(
        Control parent, string label, Point at, bool value, Action<bool> apply)
    {
        var check = new CheckBox
        {
            Text = label,
            Location = at,
            AutoSize = true,
            Checked = value
        };
        check.CheckedChanged += (_, _) => apply(check.Checked);
        parent.Controls.Add(check);
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
            Location = new Point(44, panelY),
            Size = new Size(chooseFolderWidth, 28),
            Enabled = _store.CaptureSaveToFile
        };
        _chooseFolderButton.Click += OnChooseCaptureFolder;
        _captureOptionsPanel.Controls.Add(_chooseFolderButton);

        // GrayText keeps at least AA contrast in the default theme and
        // adapts to high-contrast themes, unlike a hard-coded gray
        //
        // The width is what is left of the panel rather than a fixed 220: the
        // button beside it is as wide as its own label, so in a language with a
        // longer word for "Choose Folder" a fixed width ran the path off the
        // panel and cut it without even an ellipsis.
        _folderPathLabel = new Label
        {
            Text = FormatFolderPath(),
            Location = new Point(_chooseFolderButton.Right + 8, panelY + 4),
            Size = new Size(
                Math.Max(_captureOptionsPanel.Width - _chooseFolderButton.Right - 16, 120), 20),
            ForeColor = SystemColors.GrayText,
            AutoEllipsis = true
        };
        _captureOptionsPanel.Controls.Add(_folderPathLabel);
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
        _captureClientAreaCheck = AddSettingCheck(
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
        _bringToFrontCheck = AddSettingCheck(
            tab, Strings.SettingsBringToFront, new Point(12, 12),
            _store.BringToFront,
            on =>
            {
                _store.BringToFront = on;
                _store.SaveAndNotify();
            });

        // Move to main screen
        _moveToMainScreenCheck = AddSettingCheck(
            tab, Strings.SettingsMoveToMainScreen, new Point(12, 40),
            _store.MoveToMainScreen,
            on =>
            {
                _store.MoveToMainScreen = on;
                _store.SaveAndNotify();
            });

        // Position-after-resize label with a 3x3 tile grid below it
        tab.Controls.Add(new Label
        {
            Text = Strings.SettingsWindowPosition,
            Location = new Point(12, 72),
            AutoSize = true
        });

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
                Location = new Point(
                    12 + col * (tileSize + tileGap),
                    gridTop + row * (tileSize + tileGap)),
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
            tab.Controls.Add(tile);
        }

        RefreshPositionTiles();
        return tab;
    }

    // ── Data population ──────────────────────────────────────────────────

    // Fill the built-in and custom size list boxes from the store.
    private void PopulateLists()
    {
        // Built-in sizes (read-only display)
        _builtInList.Items.Clear();
        foreach (var size in SettingsStore.BuiltInSizes)
            _builtInList.Items.Add(FormatSize(size));

        RefreshCustomList();
    }

    // Rebuild the custom sizes list and show a placeholder when empty.
    private void RefreshCustomList()
    {
        _customList.Items.Clear();

        if (_store.CustomSizes.Count == 0)
        {
            _customList.Items.Add(Strings.SettingsNoCustomSizes);
            _customList.Enabled = false;
        }
        else
        {
            foreach (var size in _store.CustomSizes)
                _customList.Items.Add(FormatSize(size));
            _customList.Enabled = true;
        }

        _removeButton.Enabled = false;
    }

    // Render a preset as "W x H" followed by its label when one is set.
    private static string FormatSize(PresetSize size)
    {
        string display = size.DisplayName;
        if (!string.IsNullOrEmpty(size.Label))
            display += $"    {size.Label}";
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
