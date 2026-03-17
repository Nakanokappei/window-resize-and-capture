using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace WindowResize;

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

    // Top Y coordinate of the collapsible screenshot options panel
    private int _screenshotOptionsPanelTop;

    // Position button labels: Wingdings 3 directional arrows
    // Wingdings 3 mapping: á=← â=→ ã=↑ ä=↓ å=↖ æ=↗ ç=↙ è=↘
    // Reordered to match PositionOrder: TL, T, TR, L, C, R, BL, B, BR
    private static readonly string[] PositionLabels = { "å", "ã", "æ", "á", "é", "â", "ç", "ä", "è" };

    // Map WindowPosition enum values to grid indices
    private static readonly WindowPosition[] PositionOrder =
    {
        WindowPosition.TopLeft, WindowPosition.Top, WindowPosition.TopRight,
        WindowPosition.Left, WindowPosition.Center, WindowPosition.Right,
        WindowPosition.BottomLeft, WindowPosition.Bottom, WindowPosition.BottomRight
    };

    public SettingsForm()
    {
        InitializeComponents();
        LoadData();
    }

    private void InitializeComponents()
    {
        Text = Strings.SettingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;

        int y = 12;

        // Preset Sizes header
        var headerLabel = new Label
        {
            Text = Strings.SettingsPresetSizes,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(12, y),
            AutoSize = true
        };
        Controls.Add(headerLabel);
        y += 28;

        // Built-in group (5 visible rows)
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

        // Custom group (3 visible rows)
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

        // Remove button, placed to the right of the custom sizes list
        _removeButton = new Button
        {
            Text = Strings.SettingsRemove,
            Location = new Point(296, 20),
            Size = new Size(76, 28),
            Enabled = false
        };
        _removeButton.Click += RemoveButton_Click;
        customGroup.Controls.Add(_removeButton);

        _customList.SelectedIndexChanged += (_, _) =>
        {
            _removeButton.Enabled = _customList.SelectedIndex >= 0;
        };

        // Add row
        var addPanel = new Panel
        {
            Location = new Point(8, 80),
            Size = new Size(364, 30)
        };

        var widthLabel = new Label
        {
            Text = Strings.SettingsWidth,
            Location = new Point(0, 5),
            AutoSize = true
        };
        addPanel.Controls.Add(widthLabel);

        _widthBox = new TextBox
        {
            Location = new Point(50, 2),
            Size = new Size(70, 23)
        };
        addPanel.Controls.Add(_widthBox);

        var sepLabel = new Label
        {
            Text = Strings.SettingsDimensionSeparator,
            Location = new Point(125, 5),
            AutoSize = true
        };
        addPanel.Controls.Add(sepLabel);

        var heightLabel = new Label
        {
            Text = Strings.SettingsHeight,
            Location = new Point(140, 5),
            AutoSize = true
        };
        addPanel.Controls.Add(heightLabel);

        _heightBox = new TextBox
        {
            Location = new Point(190, 2),
            Size = new Size(70, 23)
        };
        addPanel.Controls.Add(_heightBox);

        _addButton = new Button
        {
            Text = Strings.SettingsAdd,
            Location = new Point(270, 1),
            Size = new Size(60, 25)
        };
        _addButton.Click += AddButton_Click;
        addPanel.Controls.Add(_addButton);

        customGroup.Controls.Add(addPanel);

        Controls.Add(customGroup);
        y += 128;

        // Launch at Login
        _launchAtLoginCheck = new CheckBox
        {
            Text = Strings.SettingsLaunchAtLogin,
            Location = new Point(12, y),
            AutoSize = true,
            Checked = _store.LaunchAtLogin
        };
        _launchAtLoginCheck.CheckedChanged += (_, _) =>
        {
            _store.LaunchAtLogin = _launchAtLoginCheck.Checked;
        };
        Controls.Add(_launchAtLoginCheck);
        y += 32;

        // --- Behaviour section ---
        var behaviourHeader = new Label
        {
            Text = Strings.SettingsBehaviour,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(12, y),
            AutoSize = true
        };
        Controls.Add(behaviourHeader);
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
            _store.SaveBehaviourSettings();
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
            _store.SaveBehaviourSettings();
        };
        Controls.Add(_moveToMainScreenCheck);
        y += 28;

        // Position after resize label
        var posLabel = new Label
        {
            Text = Strings.SettingsWindowPosition,
            Location = new Point(12, y + 2),
            AutoSize = true
        };
        Controls.Add(posLabel);

        // Single row of 9 position buttons (compact)
        int gridLeft = 140;
        int btnSize = 26;
        int btnGap = 1;
        _positionButtons = new Button[9];

        var wingdings3 = new Font("Wingdings 3", 10f);
        for (int i = 0; i < 9; i++)
        {
            var btn = new Button
            {
                Size = new Size(btnSize, btnSize),
                Location = new Point(gridLeft + i * (btnSize + btnGap), y - 2),
                FlatStyle = FlatStyle.Flat,
                Tag = PositionOrder[i],
                Font = wingdings3,
                Text = PositionLabels[i],
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = Padding.Empty
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.Click += PositionButton_Click;

            _positionButtons[i] = btn;
            Controls.Add(btn);
        }

        UpdatePositionButtonStates();
        y += btnSize + 8;

        // --- Screenshot section ---
        var screenshotHeader = new Label
        {
            Text = Strings.SettingsScreenshot,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(12, y),
            AutoSize = true
        };
        Controls.Add(screenshotHeader);
        y += 24;

        // Screenshot enabled
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
            _store.SaveScreenshotSettings();
            SyncScreenshotCheckboxes();
            UpdateScreenshotOptionsVisibility();
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

        int py = 0;

        // Save to file
        _screenshotSaveToFileCheck = new CheckBox
        {
            Text = Strings.SettingsScreenshotSaveToFile,
            Location = new Point(28, py),
            AutoSize = true,
            Checked = _store.ScreenshotSaveToFile
        };
        _screenshotSaveToFileCheck.CheckedChanged += (_, _) =>
        {
            _store.ScreenshotSaveToFile = _screenshotSaveToFileCheck.Checked;
            _store.SaveScreenshotSettings();
            _chooseFolderButton.Enabled = _store.ScreenshotSaveToFile;
            SyncScreenshotCheckboxes();
        };
        _screenshotOptionsPanel.Controls.Add(_screenshotSaveToFileCheck);
        py += 26;

        // Choose folder button + path label
        _chooseFolderButton = new Button
        {
            Text = Strings.SettingsScreenshotChooseFolder,
            Location = new Point(44, py),
            AutoSize = true,
            Enabled = _store.ScreenshotSaveToFile
        };
        _chooseFolderButton.Click += ChooseFolderButton_Click;
        _screenshotOptionsPanel.Controls.Add(_chooseFolderButton);

        _folderPathLabel = new Label
        {
            Text = GetFolderDisplayText(),
            Location = new Point(_chooseFolderButton.Right + 8, py + 4),
            Size = new Size(240, 20),
            ForeColor = Color.Gray,
            AutoEllipsis = true
        };
        _screenshotOptionsPanel.Controls.Add(_folderPathLabel);
        py += 30;

        // Copy to clipboard
        _screenshotCopyToClipboardCheck = new CheckBox
        {
            Text = Strings.SettingsScreenshotCopyToClipboard,
            Location = new Point(28, py),
            AutoSize = true,
            Checked = _store.ScreenshotCopyToClipboard
        };
        _screenshotCopyToClipboardCheck.CheckedChanged += (_, _) =>
        {
            _store.ScreenshotCopyToClipboard = _screenshotCopyToClipboardCheck.Checked;
            _store.SaveScreenshotSettings();
            SyncScreenshotCheckboxes();
        };
        _screenshotOptionsPanel.Controls.Add(_screenshotCopyToClipboardCheck);

        Controls.Add(_screenshotOptionsPanel);

        // --- Language section ---
        // Positioned after the screenshot panel (dynamically adjusted)
        int langY = _screenshotOptionsPanelTop + _screenshotOptionsPanel.Height + 8;

        var langHeader = new Label
        {
            Text = Strings.SettingsLanguage,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(12, langY),
            AutoSize = true,
            Name = "_langHeader"
        };
        Controls.Add(langHeader);
        langY += 24;

        _languageCombo = new ComboBox
        {
            Location = new Point(12, langY),
            Size = new Size(250, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Name = "_languageCombo"
        };

        // Populate with system default + all supported languages
        _languageCombo.Items.Add(Strings.SettingsLanguageSystem);
        foreach (var (_, nativeName) in SettingsStore.SupportedLanguages)
        {
            _languageCombo.Items.Add(nativeName);
        }

        // Select current language
        if (_store.AppLanguage == "system" || string.IsNullOrEmpty(_store.AppLanguage))
        {
            _languageCombo.SelectedIndex = 0;
        }
        else
        {
            int langIndex = Array.FindIndex(SettingsStore.SupportedLanguages, l => l.Code == _store.AppLanguage);
            _languageCombo.SelectedIndex = langIndex >= 0 ? langIndex + 1 : 0;
        }

        _languageCombo.SelectedIndexChanged += LanguageCombo_SelectedIndexChanged;
        Controls.Add(_languageCombo);

        // Set initial panel visibility based on current setting
        UpdateScreenshotOptionsVisibility();
    }

    // Sync screenshot checkboxes after the smart auto-enable/disable logic in SettingsStore
    private void SyncScreenshotCheckboxes()
    {
        if (_screenshotEnabledCheck.Checked != _store.ScreenshotEnabled)
            _screenshotEnabledCheck.Checked = _store.ScreenshotEnabled;
        if (_screenshotSaveToFileCheck.Checked != _store.ScreenshotSaveToFile)
            _screenshotSaveToFileCheck.Checked = _store.ScreenshotSaveToFile;
        if (_screenshotCopyToClipboardCheck.Checked != _store.ScreenshotCopyToClipboard)
            _screenshotCopyToClipboardCheck.Checked = _store.ScreenshotCopyToClipboard;
    }

    // Handle position button click: toggle selection
    private void PositionButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not WindowPosition pos)
            return;

        // Toggle: clicking the already-selected position clears it
        _store.Position = (_store.Position == pos) ? null : pos;
        _store.SaveBehaviourSettings();
        UpdatePositionButtonStates();
    }

    // Refresh all position button appearances using background/foreground colors
    private void UpdatePositionButtonStates()
    {
        foreach (var btn in _positionButtons)
        {
            if (btn.Tag is not WindowPosition pos) continue;
            bool selected = _store.Position == pos;
            btn.BackColor = selected ? Color.DodgerBlue : SystemColors.Control;
            btn.ForeColor = selected ? Color.White : SystemColors.ControlText;
        }
    }

    // Handle language selection change
    private void LanguageCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        int idx = _languageCombo.SelectedIndex;
        string newLang = idx == 0 ? "system" : SettingsStore.SupportedLanguages[idx - 1].Code;

        if (newLang == _store.AppLanguage)
            return;

        _store.ApplyLanguage(newLang);

        // Notify user that restart is needed for full effect
        var result = MessageBox.Show(
            Strings.SettingsLanguageRestartBody,
            Strings.SettingsLanguageRestartTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result == DialogResult.Yes)
        {
            // Restart application
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                System.Diagnostics.Process.Start(exePath);
                Application.Exit();
            }
        }
    }

    /// <summary>
    /// Toggle screenshot options visibility and auto-adjust form height.
    /// Also repositions the language section below.
    /// </summary>
    private void UpdateScreenshotOptionsVisibility()
    {
        bool show = _store.ScreenshotEnabled;
        _screenshotOptionsPanel.Visible = show;

        // Calculate where language section goes
        int langY = show
            ? _screenshotOptionsPanelTop + _screenshotOptionsPanel.Height + 8
            : _screenshotOptionsPanelTop + 8;

        // Move language controls
        var langHeader = Controls.Find("_langHeader", false);
        if (langHeader.Length > 0)
            langHeader[0].Location = new Point(12, langY);

        _languageCombo.Location = new Point(12, langY + 24);

        // Recalculate form height
        int contentBottom = langY + 24 + _languageCombo.Height + 12;

        // Set client area; WinForms accounts for the title bar automatically
        ClientSize = new Size(404, contentBottom);
    }

    private void LoadData()
    {
        // Built-in sizes
        _builtInList.Items.Clear();
        foreach (var size in SettingsStore.BuiltInSizes)
        {
            string display = size.DisplayName;
            if (!string.IsNullOrEmpty(size.Label))
                display += $"    {size.Label}";
            _builtInList.Items.Add(display);
        }

        // Custom sizes
        RefreshCustomList();
    }

    private void RefreshCustomList()
    {
        _customList.Items.Clear();
        foreach (var size in _store.CustomSizes)
        {
            _customList.Items.Add(size.DisplayName);
        }

        if (_store.CustomSizes.Count == 0)
        {
            _customList.Items.Add(Strings.SettingsNoCustomSizes);
            _customList.Enabled = false;
        }
        else
        {
            _customList.Enabled = true;
        }

        _removeButton.Enabled = false;
    }

    private void AddButton_Click(object? sender, EventArgs e)
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

    private void RemoveButton_Click(object? sender, EventArgs e)
    {
        int idx = _customList.SelectedIndex;
        if (idx >= 0 && idx < _store.CustomSizes.Count)
        {
            _store.RemoveSize(_store.CustomSizes[idx]);
            RefreshCustomList();
        }
    }

    private void ChooseFolderButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (!string.IsNullOrEmpty(_store.ScreenshotSaveFolderPath))
            dialog.SelectedPath = _store.ScreenshotSaveFolderPath;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _store.ScreenshotSaveFolderPath = dialog.SelectedPath;
            _store.SaveScreenshotSettings();
            _folderPathLabel.Text = GetFolderDisplayText();
        }
    }

    private string GetFolderDisplayText()
    {
        return string.IsNullOrEmpty(_store.ScreenshotSaveFolderPath)
            ? Strings.SettingsScreenshotNoFolderSelected
            : _store.ScreenshotSaveFolderPath;
    }

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
