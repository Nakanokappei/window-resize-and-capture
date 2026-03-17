using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace WindowsResizeCapture;

// The Settings window. Built entirely in code (no designer). Provides
// controls for preset sizes, launch-at-login, window behaviour options,
// screenshot destinations, and language selection. Hides instead of
// closing so it can be reused without reconstruction.
public class SettingsForm : Form
{
    private readonly SettingsStore _store = SettingsStore.Shared;
    private ListBox _builtInList = null!;
    private ListBox _customList = null!;
    private TextBox _widthBox = null!;
    private TextBox _heightBox = null!;
    private Button _addButton = null!;
    private Button _removeButton = null!;
    private CheckBox _launchAtLoginCheck = null!;

    // Behaviour controls
    private CheckBox _bringToFrontCheck = null!;
    private Button[] _positionButtons = null!;
    private CheckBox _moveToMainScreenCheck = null!;

    // Screenshot controls
    private CheckBox _screenshotEnabledCheck = null!;
    private Panel _screenshotOptionsPanel = null!;
    private CheckBox _screenshotSaveToFileCheck = null!;
    private CheckBox _screenshotCopyToClipboardCheck = null!;
    private Button _chooseFolderButton = null!;
    private Label _folderPathLabel = null!;

    // Language controls
    private ComboBox _languageCombo = null!;

    // Y-coordinate where the collapsible screenshot options panel begins
    private int _screenshotOptionsPanelTop;

    // Wingdings 3 arrow glyphs mapped to the 3x3 position grid
    // (TL, T, TR, L, C, R, BL, B, BR)
    private static readonly string[] PositionGlyphs = { "å", "ã", "æ", "á", "é", "â", "ç", "ä", "è" };

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

    // Construct all controls top-to-bottom: preset sizes, launch-at-login,
    // behaviour options, screenshot options, and language picker.
    private void BuildLayout()
    {
        Text = Strings.SettingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;

        int y = 12;

        // ── Preset sizes header ──
        Controls.Add(new Label
        {
            Text = Strings.SettingsPresetSizes,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(12, y),
            AutoSize = true
        });
        y += 28;

        // ── Built-in sizes group ──
        var builtInGroup = new GroupBox
        {
            Text = Strings.SettingsBuiltIn,
            Location = new Point(12, y),
            Size = new Size(380, 100)
        };

        _builtInList = new ListBox
        {
            Location = new Point(8, 20),
            Size = new Size(364, 70),
            SelectionMode = SelectionMode.None,
            BorderStyle = BorderStyle.None
        };
        builtInGroup.Controls.Add(_builtInList);
        Controls.Add(builtInGroup);
        y += 108;

        // ── Custom sizes group ──
        var customGroup = new GroupBox
        {
            Text = Strings.SettingsCustom,
            Location = new Point(12, y),
            Size = new Size(380, 120)
        };

        _customList = new ListBox
        {
            Location = new Point(8, 20),
            Size = new Size(280, 55),
            BorderStyle = BorderStyle.FixedSingle
        };
        customGroup.Controls.Add(_customList);

        // Remove button beside the custom list
        _removeButton = new Button
        {
            Text = Strings.SettingsRemove,
            Location = new Point(296, 20),
            Size = new Size(76, 28),
            Enabled = false
        };
        _removeButton.Click += OnRemovePreset;
        customGroup.Controls.Add(_removeButton);

        // Enable the remove button only when a custom size is selected
        _customList.SelectedIndexChanged += (_, _) =>
            _removeButton.Enabled = _customList.SelectedIndex >= 0;

        // ── Add-size row (width × height + Add button) ──
        var addPanel = new Panel
        {
            Location = new Point(8, 80),
            Size = new Size(364, 30)
        };

        addPanel.Controls.Add(new Label
        {
            Text = Strings.SettingsWidth,
            Location = new Point(0, 5),
            AutoSize = true
        });

        _widthBox = new TextBox { Location = new Point(50, 2), Size = new Size(70, 23) };
        addPanel.Controls.Add(_widthBox);

        addPanel.Controls.Add(new Label
        {
            Text = Strings.SettingsDimensionSeparator,
            Location = new Point(125, 5),
            AutoSize = true
        });

        addPanel.Controls.Add(new Label
        {
            Text = Strings.SettingsHeight,
            Location = new Point(140, 5),
            AutoSize = true
        });

        _heightBox = new TextBox { Location = new Point(190, 2), Size = new Size(70, 23) };
        addPanel.Controls.Add(_heightBox);

        _addButton = new Button
        {
            Text = Strings.SettingsAdd,
            Location = new Point(270, 1),
            Size = new Size(60, 25)
        };
        _addButton.Click += OnAddPreset;
        addPanel.Controls.Add(_addButton);

        customGroup.Controls.Add(addPanel);
        Controls.Add(customGroup);
        y += 128;

        // ── Launch at login ──
        _launchAtLoginCheck = new CheckBox
        {
            Text = Strings.SettingsLaunchAtLogin,
            Location = new Point(12, y),
            AutoSize = true,
            Checked = _store.LaunchAtLogin
        };
        _launchAtLoginCheck.CheckedChanged += (_, _) =>
            _store.LaunchAtLogin = _launchAtLoginCheck.Checked;
        Controls.Add(_launchAtLoginCheck);
        y += 32;

        // ── Behaviour section ──
        Controls.Add(new Label
        {
            Text = Strings.SettingsBehaviour,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(12, y),
            AutoSize = true
        });
        y += 24;

        // Bring to front
        _bringToFrontCheck = new CheckBox
        {
            Text = Strings.SettingsBringToFront,
            Location = new Point(12, y),
            AutoSize = true,
            Checked = _store.BringToFront
        };
        _bringToFrontCheck.CheckedChanged += (_, _) =>
        {
            _store.BringToFront = _bringToFrontCheck.Checked;
            _store.SaveAndNotify();
        };
        Controls.Add(_bringToFrontCheck);
        y += 26;

        // Move to main screen
        _moveToMainScreenCheck = new CheckBox
        {
            Text = Strings.SettingsMoveToMainScreen,
            Location = new Point(12, y),
            AutoSize = true,
            Checked = _store.MoveToMainScreen
        };
        _moveToMainScreenCheck.CheckedChanged += (_, _) =>
        {
            _store.MoveToMainScreen = _moveToMainScreenCheck.Checked;
            _store.SaveAndNotify();
        };
        Controls.Add(_moveToMainScreenCheck);
        y += 28;

        // Position-after-resize label and 9-button grid
        Controls.Add(new Label
        {
            Text = Strings.SettingsWindowPosition,
            Location = new Point(12, y + 2),
            AutoSize = true
        });

        int gridLeft = 140;
        int buttonSize = 26;
        int buttonGap = 1;
        _positionButtons = new Button[9];

        var wingdings3 = new Font("Wingdings 3", 10f);
        for (int i = 0; i < 9; i++)
        {
            var btn = new Button
            {
                Size = new Size(buttonSize, buttonSize),
                Location = new Point(gridLeft + i * (buttonSize + buttonGap), y - 2),
                FlatStyle = FlatStyle.Flat,
                Tag = PositionOrder[i],
                Font = wingdings3,
                Text = PositionGlyphs[i],
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = Padding.Empty
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.Click += OnPositionButtonClick;
            _positionButtons[i] = btn;
            Controls.Add(btn);
        }

        RefreshPositionButtonHighlights();
        y += buttonSize + 8;

        // ── Screenshot section ──
        Controls.Add(new Label
        {
            Text = Strings.SettingsScreenshot,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(12, y),
            AutoSize = true
        });
        y += 24;

        // Master screenshot toggle
        _screenshotEnabledCheck = new CheckBox
        {
            Text = Strings.SettingsScreenshotEnabled,
            Location = new Point(12, y),
            AutoSize = true,
            Checked = _store.ScreenshotEnabled
        };
        _screenshotEnabledCheck.CheckedChanged += (_, _) =>
        {
            _store.ScreenshotEnabled = _screenshotEnabledCheck.Checked;
            _store.SaveAndNotify();
            SynchronizeScreenshotControls();
            AdjustScreenshotPanelVisibility();
        };
        Controls.Add(_screenshotEnabledCheck);
        y += 26;

        // Collapsible panel for screenshot destination options
        _screenshotOptionsPanelTop = y;
        _screenshotOptionsPanel = new Panel
        {
            Location = new Point(0, y),
            Size = new Size(420, 86)
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

        _folderPathLabel = new Label
        {
            Text = FormatFolderPath(),
            Location = new Point(_chooseFolderButton.Right + 8, panelY + 4),
            Size = new Size(240, 20),
            ForeColor = Color.Gray,
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
        Controls.Add(_screenshotOptionsPanel);

        // ── Language section ──
        int languageY = _screenshotOptionsPanelTop + _screenshotOptionsPanel.Height + 8;

        Controls.Add(new Label
        {
            Text = Strings.SettingsLanguage,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(12, languageY),
            AutoSize = true,
            Name = "_langHeader"
        });
        languageY += 24;

        _languageCombo = new ComboBox
        {
            Location = new Point(12, languageY),
            Size = new Size(250, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Name = "_languageCombo"
        };

        // Populate: "System default" first, then every shipped language
        _languageCombo.Items.Add(Strings.SettingsLanguageSystem);
        foreach (var (_, nativeName) in SettingsStore.SupportedLanguages)
            _languageCombo.Items.Add(nativeName);

        // Select the currently active language
        if (_store.AppLanguage == "system" || string.IsNullOrEmpty(_store.AppLanguage))
        {
            _languageCombo.SelectedIndex = 0;
        }
        else
        {
            int langIndex = Array.FindIndex(SettingsStore.SupportedLanguages,
                l => l.Code == _store.AppLanguage);
            _languageCombo.SelectedIndex = langIndex >= 0 ? langIndex + 1 : 0;
        }

        _languageCombo.SelectedIndexChanged += OnLanguageChanged;
        Controls.Add(_languageCombo);

        // Set initial screenshot panel visibility
        AdjustScreenshotPanelVisibility();
    }

    // ── Data population ──────────────────────────────────────────────────

    // Fill the built-in and custom size list boxes from the store.
    private void PopulateLists()
    {
        // Built-in sizes (read-only display)
        _builtInList.Items.Clear();
        foreach (var size in SettingsStore.BuiltInSizes)
        {
            string display = size.DisplayName;
            if (!string.IsNullOrEmpty(size.Label))
                display += $"    {size.Label}";
            _builtInList.Items.Add(display);
        }

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
                _customList.Items.Add(size.DisplayName);
            _customList.Enabled = true;
        }

        _removeButton.Enabled = false;
    }

    // ── Event handlers ───────────────────────────────────────────────────

    // Parse the width/height inputs and add a new custom preset.
    private void OnAddPreset(object? sender, EventArgs e)
    {
        if (int.TryParse(_widthBox.Text, out int w) &&
            int.TryParse(_heightBox.Text, out int h) &&
            w > 0 && h > 0)
        {
            _store.AddSize(new PresetSize(w, h));
            _widthBox.Clear();
            _heightBox.Clear();
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
        if (sender is not Button btn || btn.Tag is not WindowPosition pos)
            return;

        _store.Position = (_store.Position == pos) ? null : pos;
        _store.SaveAndNotify();
        RefreshPositionButtonHighlights();
    }

    // Apply the newly selected language and offer to restart the app
    // so that all UI strings update.
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        int index = _languageCombo.SelectedIndex;
        string newLanguage = index == 0 ? "system" : SettingsStore.SupportedLanguages[index - 1].Code;

        if (newLanguage == _store.AppLanguage)
            return;

        _store.ApplyLanguage(newLanguage);

        // Prompt the user to restart for the change to take full effect
        var result = MessageBox.Show(
            Strings.SettingsLanguageRestartBody,
            Strings.SettingsLanguageRestartTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result == DialogResult.Yes)
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                System.Diagnostics.Process.Start(exePath);
                Application.Exit();
            }
        }
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

    // Highlight the currently selected position button and reset the rest.
    private void RefreshPositionButtonHighlights()
    {
        foreach (var btn in _positionButtons)
        {
            if (btn.Tag is not WindowPosition pos) continue;
            bool selected = _store.Position == pos;
            btn.BackColor = selected ? Color.DodgerBlue : SystemColors.Control;
            btn.ForeColor = selected ? Color.White : SystemColors.ControlText;
        }
    }

    // Show or hide the screenshot options panel and reflow the language
    // section and form height accordingly.
    private void AdjustScreenshotPanelVisibility()
    {
        bool visible = _store.ScreenshotEnabled;
        _screenshotOptionsPanel.Visible = visible;

        // Calculate where the language section starts
        int languageY = visible
            ? _screenshotOptionsPanelTop + _screenshotOptionsPanel.Height + 8
            : _screenshotOptionsPanelTop + 8;

        // Reposition the language header and combo box
        var langHeader = Controls.Find("_langHeader", false);
        if (langHeader.Length > 0)
            langHeader[0].Location = new Point(12, languageY);
        _languageCombo.Location = new Point(12, languageY + 24);

        // Resize the form to fit the visible content
        int contentBottom = languageY + 24 + _languageCombo.Height + 12;
        ClientSize = new Size(404, contentBottom);
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
