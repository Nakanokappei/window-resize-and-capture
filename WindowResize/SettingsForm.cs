using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsResizeCapture;

// The Settings window. Built entirely in code (no designer). A three-tab
// layout: General (preset sizes, launch at login), Screenshot (capture
// destinations), and Behaviour (post-resize window handling). Hides
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
    private CheckBox _launchAtLoginCheck = null!;

    // Screenshot tab controls
    private CheckBox _screenshotEnabledCheck = null!;
    private Panel _screenshotOptionsPanel = null!;
    private CheckBox _screenshotSaveToFileCheck = null!;
    private CheckBox _screenshotCopyToClipboardCheck = null!;
    private Button _chooseFolderButton = null!;
    private Label _folderPathLabel = null!;

    // Behaviour tab controls. The position tiles are checkbox-styled
    // buttons so UI Automation exposes their checked state to screen
    // readers (a plain Button has no toggle state).
    private CheckBox _bringToFrontCheck = null!;
    private CheckBox _moveToMainScreenCheck = null!;
    private CheckBox[] _positionButtons = null!;

    // Wingdings 3 arrow glyphs mapped to the 3x3 position grid
    // (TL, T, TR, L, C, R, BL, B, BR)
    private static readonly string[] PositionGlyphs = { "å", "ã", "æ", "á", "é", "â", "ç", "ä", "è" };

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
        Text = Strings.SettingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        // Scale the fixed pixel layout with the monitor DPI so the window
        // stays usable at 200%+ display scaling. The layout below is
        // designed at 96 DPI.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(420, 356);

        var tabs = new TabControl
        {
            Location = new Point(8, 8),
            Size = new Size(404, 340)
        };

        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildScreenshotTab());
        tabs.TabPages.Add(BuildBehaviourTab());
        Controls.Add(tabs);
    }

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

        // ── Launch at login ──
        _launchAtLoginCheck = new CheckBox
        {
            Text = Strings.SettingsLaunchAtLogin,
            Location = new Point(12, 276),
            AutoSize = true,
            Checked = _store.LaunchAtLogin
        };
        _launchAtLoginCheck.CheckedChanged += (_, _) =>
            _store.LaunchAtLogin = _launchAtLoginCheck.Checked;
        tab.Controls.Add(_launchAtLoginCheck);

        return tab;
    }

    // Screenshot tab: master toggle plus a panel of destination options
    // that hides while screenshots are disabled.
    private TabPage BuildScreenshotTab()
    {
        var tab = new TabPage(Strings.SettingsScreenshot);

        // Master screenshot toggle
        _screenshotEnabledCheck = new CheckBox
        {
            Text = Strings.SettingsScreenshotEnabled,
            Location = new Point(12, 12),
            AutoSize = true,
            Checked = _store.ScreenshotEnabled
        };
        _screenshotEnabledCheck.CheckedChanged += (_, _) =>
        {
            _store.ScreenshotEnabled = _screenshotEnabledCheck.Checked;
            _store.SaveAndNotify();
            SynchronizeScreenshotControls();
            _screenshotOptionsPanel.Visible = _store.ScreenshotEnabled;
        };
        tab.Controls.Add(_screenshotEnabledCheck);

        // Panel for screenshot destination options, hidden when disabled
        _screenshotOptionsPanel = new Panel
        {
            Location = new Point(0, 40),
            Size = new Size(396, 86),
            Visible = _store.ScreenshotEnabled
        };

        int panelY = 0;

        // Save-to-file checkbox
        _screenshotSaveToFileCheck = new CheckBox
        {
            Text = Strings.SettingsScreenshotSaveToFile,
            Location = new Point(28, panelY),
            AutoSize = true,
            Checked = _store.ScreenshotSaveToFile
        };
        _screenshotSaveToFileCheck.CheckedChanged += (_, _) =>
        {
            _store.ScreenshotSaveToFile = _screenshotSaveToFileCheck.Checked;
            _store.SaveAndNotify();
            _chooseFolderButton.Enabled = _store.ScreenshotSaveToFile;
            SynchronizeScreenshotControls();
        };
        _screenshotOptionsPanel.Controls.Add(_screenshotSaveToFileCheck);
        panelY += 26;

        // Folder chooser button and path label
        _chooseFolderButton = new Button
        {
            Text = Strings.SettingsScreenshotChooseFolder,
            Location = new Point(44, panelY),
            AutoSize = true,
            Enabled = _store.ScreenshotSaveToFile
        };
        _chooseFolderButton.Click += OnChooseScreenshotFolder;
        _screenshotOptionsPanel.Controls.Add(_chooseFolderButton);

        // GrayText keeps at least AA contrast in the default theme and
        // adapts to high-contrast themes, unlike a hard-coded gray
        _folderPathLabel = new Label
        {
            Text = FormatFolderPath(),
            Location = new Point(_chooseFolderButton.Right + 8, panelY + 4),
            Size = new Size(220, 20),
            ForeColor = SystemColors.GrayText,
            AutoEllipsis = true
        };
        _screenshotOptionsPanel.Controls.Add(_folderPathLabel);
        panelY += 30;

        // Copy-to-clipboard checkbox
        _screenshotCopyToClipboardCheck = new CheckBox
        {
            Text = Strings.SettingsScreenshotCopyToClipboard,
            Location = new Point(28, panelY),
            AutoSize = true,
            Checked = _store.ScreenshotCopyToClipboard
        };
        _screenshotCopyToClipboardCheck.CheckedChanged += (_, _) =>
        {
            _store.ScreenshotCopyToClipboard = _screenshotCopyToClipboardCheck.Checked;
            _store.SaveAndNotify();
            SynchronizeScreenshotControls();
        };
        _screenshotOptionsPanel.Controls.Add(_screenshotCopyToClipboardCheck);
        tab.Controls.Add(_screenshotOptionsPanel);

        return tab;
    }

    // Behaviour tab: post-resize options and the 3x3 snap-position grid.
    private TabPage BuildBehaviourTab()
    {
        var tab = new TabPage(Strings.SettingsBehaviour);

        // Bring to front
        _bringToFrontCheck = new CheckBox
        {
            Text = Strings.SettingsBringToFront,
            Location = new Point(12, 12),
            AutoSize = true,
            Checked = _store.BringToFront
        };
        _bringToFrontCheck.CheckedChanged += (_, _) =>
        {
            _store.BringToFront = _bringToFrontCheck.Checked;
            _store.SaveAndNotify();
        };
        tab.Controls.Add(_bringToFrontCheck);

        // Move to main screen
        _moveToMainScreenCheck = new CheckBox
        {
            Text = Strings.SettingsMoveToMainScreen,
            Location = new Point(12, 40),
            AutoSize = true,
            Checked = _store.MoveToMainScreen
        };
        _moveToMainScreenCheck.CheckedChanged += (_, _) =>
        {
            _store.MoveToMainScreen = _moveToMainScreenCheck.Checked;
            _store.SaveAndNotify();
        };
        tab.Controls.Add(_moveToMainScreenCheck);

        // Position-after-resize label with a 3x3 tile grid below it
        tab.Controls.Add(new Label
        {
            Text = Strings.SettingsWindowPosition,
            Location = new Point(12, 72),
            AutoSize = true
        });

        // Screen readers cannot pronounce the Wingdings glyphs, so each
        // tile carries a localized position name as its UIA name
        string[] positionNames =
        {
            Strings.SettingsPositionTopLeft, Strings.SettingsPositionTop, Strings.SettingsPositionTopRight,
            Strings.SettingsPositionLeft, Strings.SettingsPositionCenter, Strings.SettingsPositionRight,
            Strings.SettingsPositionBottomLeft, Strings.SettingsPositionBottom, Strings.SettingsPositionBottomRight
        };

        int gridTop = 96;
        int buttonSize = 32;
        int buttonGap = 2;
        _positionButtons = new CheckBox[9];

        var wingdings3 = new Font("Wingdings 3", 10f);
        for (int i = 0; i < 9; i++)
        {
            // Arrange the nine tiles as three rows of three. AutoCheck is
            // off because the checked state mirrors the store: clicking
            // raises Click, the store updates, and the refresh below sets
            // Checked on every tile (only one may be active).
            int col = i % 3;
            int row = i / 3;
            var btn = new CheckBox
            {
                Appearance = Appearance.Button,
                AutoCheck = false,
                Size = new Size(buttonSize, buttonSize),
                Location = new Point(
                    12 + col * (buttonSize + buttonGap),
                    gridTop + row * (buttonSize + buttonGap)),
                FlatStyle = FlatStyle.Flat,
                Tag = PositionOrder[i],
                Font = wingdings3,
                Text = PositionGlyphs[i],
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = Padding.Empty,
                AccessibleName = positionNames[i]
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.Click += OnPositionButtonClick;
            _positionButtons[i] = btn;
            tab.Controls.Add(btn);
        }

        RefreshPositionButtonHighlights();
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
        if (int.TryParse(_widthBox.Text, out int w) &&
            int.TryParse(_heightBox.Text, out int h) &&
            w > 0 && h > 0)
        {
            string name = _nameBox.Text.Trim();
            _store.AddSize(new PresetSize(w, h, name.Length > 0 ? name : null));
            _widthBox.Clear();
            _heightBox.Clear();
            _nameBox.Clear();
            RefreshCustomList();
        }
    }

    // Remove the currently selected custom preset.
    private void OnRemovePreset(object? sender, EventArgs e)
    {
        int index = _customList.SelectedIndex;
        if (index >= 0 && index < _store.CustomSizes.Count)
        {
            _store.RemoveSize(_store.CustomSizes[index]);
            RefreshCustomList();
        }
    }

    // Toggle the selected snap position. Clicking the already-active
    // position clears it (no snap).
    private void OnPositionButtonClick(object? sender, EventArgs e)
    {
        if (sender is not CheckBox btn || btn.Tag is not WindowPosition pos)
            return;

        _store.Position = (_store.Position == pos) ? null : pos;
        _store.SaveAndNotify();
        RefreshPositionButtonHighlights();
    }

    // Open a folder browser to choose the screenshot save location.
    private void OnChooseScreenshotFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (!string.IsNullOrEmpty(_store.ScreenshotSaveFolderPath))
            dialog.SelectedPath = _store.ScreenshotSaveFolderPath;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _store.ScreenshotSaveFolderPath = dialog.SelectedPath;
            _store.SaveAndNotify();
            _folderPathLabel.Text = FormatFolderPath();
        }
    }

    // ── UI synchronisation helpers ───────────────────────────────────────

    // After the store's auto-enable/disable logic fires, push the
    // canonical state back into the checkboxes without re-triggering events.
    private void SynchronizeScreenshotControls()
    {
        if (_screenshotEnabledCheck.Checked != _store.ScreenshotEnabled)
            _screenshotEnabledCheck.Checked = _store.ScreenshotEnabled;
        if (_screenshotSaveToFileCheck.Checked != _store.ScreenshotSaveToFile)
            _screenshotSaveToFileCheck.Checked = _store.ScreenshotSaveToFile;
        if (_screenshotCopyToClipboardCheck.Checked != _store.ScreenshotCopyToClipboard)
            _screenshotCopyToClipboardCheck.Checked = _store.ScreenshotCopyToClipboard;
    }

    // Highlight the currently selected position tile and reset the rest.
    // Checked feeds the UIA toggle state; the colours are the visual cue.
    private void RefreshPositionButtonHighlights()
    {
        foreach (var btn in _positionButtons)
        {
            if (btn.Tag is not WindowPosition pos) continue;
            bool selected = _store.Position == pos;
            btn.Checked = selected;
            btn.BackColor = selected ? SelectedTileColor : SystemColors.Control;
            btn.ForeColor = selected ? Color.White : SystemColors.ControlText;
        }
    }

    // Return the folder path for display, or a placeholder if none is set.
    private string FormatFolderPath()
    {
        return string.IsNullOrEmpty(_store.ScreenshotSaveFolderPath)
            ? Strings.SettingsScreenshotNoFolderSelected
            : _store.ScreenshotSaveFolderPath;
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
