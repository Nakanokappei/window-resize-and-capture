using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
#if WINDOWS10_0_17763_0_OR_GREATER
using Windows.ApplicationModel;
#endif

namespace WindowsResizeCapture;

// Nine-position snap anchor for placing a window after resize.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WindowPosition
{
    TopLeft, Top, TopRight,
    Left, Center, Right,
    BottomLeft, Bottom, BottomRight
}

// Thread-safe singleton that persists all user preferences to a JSON file
// in %APPDATA%/WindowsResizeCapture/settings.json. Also manages the
// "launch at login" registration via either the Windows registry (standalone
// EXE) or the UWP StartupTask API (MSIX Store distribution).
public class SettingsStore
{
    private static readonly Lazy<SettingsStore> _instance = new(() => new SettingsStore());
    public static SettingsStore Shared => _instance.Value;

    private readonly string _settingsPath;
    private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WindowsResizeCapture";
    private const string StartupTaskId = "WindowsResizeCaptureStartup";

    public List<PresetSize> CustomSizes { get; private set; } = new();

    // Window behaviour settings
    public bool BringToFront { get; set; } = true;
    public WindowPosition? Position { get; set; }
    public bool MoveToMainScreen { get; set; }

    // True when any post-resize positioning feature is enabled, which
    // determines whether the "Current Size" menu item should appear.
    public bool IsPositioningActive =>
        BringToFront || Position != null || MoveToMainScreen;

    // Screenshot destination settings with smart auto-toggle logic:
    //  - Enabling screenshots with no destination auto-enables clipboard.
    //  - Disabling all destinations auto-disables the master toggle.

    private bool _screenshotEnabled;
    public bool ScreenshotEnabled
    {
        get => _screenshotEnabled;
        set
        {
            _screenshotEnabled = value;

            // If enabling with no output selected, default to clipboard
            if (value && !ScreenshotSaveToFile && !ScreenshotCopyToClipboard)
                ScreenshotCopyToClipboard = true;
        }
    }

    private bool _screenshotSaveToFile = true;
    public bool ScreenshotSaveToFile
    {
        get => _screenshotSaveToFile;
        set
        {
            _screenshotSaveToFile = value;

            // Turn off the master toggle when no destination remains
            if (!value && !ScreenshotCopyToClipboard)
                _screenshotEnabled = false;
        }
    }

    public string ScreenshotSaveFolderPath { get; set; } = "";

    private bool _screenshotCopyToClipboard;
    public bool ScreenshotCopyToClipboard
    {
        get => _screenshotCopyToClipboard;
        set
        {
            _screenshotCopyToClipboard = value;

            // Turn off the master toggle when no destination remains
            if (!value && !ScreenshotSaveToFile)
                _screenshotEnabled = false;
        }
    }

    // Language override: "system" or an IETF tag (e.g. "ja", "zh-Hans").
    public string AppLanguage { get; set; } = "system";

    // All languages shipped with the app, shown in the settings language picker.
    public static readonly (string Code, string NativeName)[] SupportedLanguages =
    {
        ("en", "English"),
        ("ja", "日本語"),
        ("zh-Hans", "简体中文"),
        ("zh-Hant", "繁體中文"),
        ("ko", "한국어"),
        ("es", "Español"),
        ("fr", "Français"),
        ("de", "Deutsch"),
        ("it", "Italiano"),
        ("pt", "Português"),
        ("ru", "Русский"),
        ("ar", "العربية"),
        ("hi", "हिन्दी"),
        ("id", "Bahasa Indonesia"),
        ("vi", "Tiếng Việt"),
        ("th", "ไทย"),
    };

    // Launch-at-login property that dispatches to the registry or
    // StartupTask API depending on the deployment model.
    public bool LaunchAtLogin
    {
        get
        {
            // For MSIX packages, query the StartupTask state;
            // for standalone EXE, check the registry Run key.
            if (IsPackaged())
            {
#if WINDOWS10_0_17763_0_OR_GREATER
                try
                {
                    var task = StartupTask.GetAsync(StartupTaskId).GetAwaiter().GetResult();
                    return task.State == StartupTaskState.Enabled;
                }
                catch { }
#endif
                return false;
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch { return false; }
        }
        set
        {
            // For MSIX packages, enable/disable via the StartupTask API;
            // for standalone EXE, write or remove a registry Run key.
            if (IsPackaged())
            {
#if WINDOWS10_0_17763_0_OR_GREATER
                try
                {
                    var task = StartupTask.GetAsync(StartupTaskId).GetAwaiter().GetResult();
                    if (value)
                    {
                        if (task.State == StartupTaskState.Disabled)
                            task.RequestEnableAsync().GetAwaiter().GetResult();
                    }
                    else
                    {
                        task.Disable();
                    }
                }
                catch { }
#endif
                return;
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
                if (key == null) return;

                if (value)
                {
                    string exePath = Environment.ProcessPath ?? "";
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch { }
        }
    }

    public static readonly List<PresetSize> BuiltInSizes = new()
    {
        new(3840, 2160, "4K UHD"),
        new(2560, 1440, "QHD"),
        new(1920, 1200, "WUXGA"),
        new(1920, 1080, "Full HD"),
        new(1680, 1050, "WSXGA+"),
        new(1600, 900,  "HD+"),
        new(1440, 900,  "WXGA+"),
        new(1366, 768,  "WXGA"),
        new(1280, 1024, "SXGA"),
        new(1280, 720,  "HD"),
        new(1024, 768,  "XGA"),
        new(800,  600,  "SVGA"),
    };

    // Merged view of built-in presets followed by user-defined custom sizes.
    public List<PresetSize> AllSizes
    {
        get
        {
            var combined = new List<PresetSize>(BuiltInSizes);
            combined.AddRange(CustomSizes);
            return combined;
        }
    }

    // Fired after any setting mutation so the UI can rebuild menus/controls.
    public event Action? SettingsChanged;

    // Private constructor: resolve the settings directory, ensure it exists,
    // and load persisted data.
    private SettingsStore()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appDir = Path.Combine(appData, "WindowsResizeCapture");
        Directory.CreateDirectory(appDir);
        _settingsPath = Path.Combine(appDir, "settings.json");
        Load();
    }

    // Add a user-defined preset size, persist, and notify listeners.
    public void AddSize(PresetSize size)
    {
        CustomSizes.Add(size);
        SaveAndNotify();
    }

    // Remove a user-defined preset size by ID, persist, and notify listeners.
    public void RemoveSize(PresetSize size)
    {
        CustomSizes.RemoveAll(s => s.Id == size.Id);
        SaveAndNotify();
    }

    // Persist the current state and fire the SettingsChanged event.
    // Called by public mutators after any setting change.
    public void SaveAndNotify()
    {
        Save();
        SettingsChanged?.Invoke();
    }

    // Apply a language override, persist, and update the resource culture
    // so that subsequent Strings.* lookups use the new language.
    public void ApplyLanguage(string languageCode)
    {
        AppLanguage = languageCode;
        Save();

        // "system" or empty means follow the OS locale
        if (languageCode == "system" || string.IsNullOrEmpty(languageCode))
            Strings.Culture = null;
        else
            Strings.Culture = new CultureInfo(languageCode);
    }

    // On startup, set the resource culture to the saved language so the
    // first UI strings already appear in the correct language.
    public void InitializeLanguage()
    {
        if (!string.IsNullOrEmpty(AppLanguage) && AppLanguage != "system")
            Strings.Culture = new CultureInfo(AppLanguage);
    }

    // Read settings from the JSON file into this instance's properties.
    // Uses backing fields for screenshot booleans to avoid triggering the
    // auto-enable/disable logic during deserialization.
    private void Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;

            string json = File.ReadAllText(_settingsPath);
            var data = JsonSerializer.Deserialize<SettingsData>(json);

            if (data?.CustomSizes != null)
                CustomSizes = data.CustomSizes;

            // Behaviour settings
            BringToFront = data?.BringToFront ?? true;
            Position = data?.Position;
            MoveToMainScreen = data?.MoveToMainScreen ?? false;

            // Screenshot settings (bypass property setters to avoid auto-logic)
            _screenshotEnabled = data?.ScreenshotEnabled ?? false;
            _screenshotSaveToFile = data?.ScreenshotSaveToFile ?? true;
            ScreenshotSaveFolderPath = data?.ScreenshotSaveFolderPath ?? "";
            _screenshotCopyToClipboard = data?.ScreenshotCopyToClipboard ?? false;

            // Language
            AppLanguage = data?.AppLanguage ?? "system";
        }
        catch { }
    }

    // Serialize all current settings to JSON and write to disk.
    private void Save()
    {
        try
        {
            var data = new SettingsData
            {
                CustomSizes = CustomSizes,
                BringToFront = BringToFront,
                Position = Position,
                MoveToMainScreen = MoveToMainScreen,
                ScreenshotEnabled = ScreenshotEnabled,
                ScreenshotSaveToFile = ScreenshotSaveToFile,
                ScreenshotSaveFolderPath = ScreenshotSaveFolderPath,
                ScreenshotCopyToClipboard = ScreenshotCopyToClipboard,
                AppLanguage = AppLanguage
            };
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    // Detect whether the app is running inside an MSIX package.
    // Package.Current throws when the process is not packaged.
    public static bool IsPackaged()
    {
#if WINDOWS10_0_17763_0_OR_GREATER
        try
        {
            _ = Package.Current.Id;
            return true;
        }
        catch { }
#endif
        return false;
    }

    // JSON-serializable DTO mirroring all persisted fields.
    private class SettingsData
    {
        public List<PresetSize>? CustomSizes { get; set; }
        public bool BringToFront { get; set; } = true;
        public WindowPosition? Position { get; set; }
        public bool MoveToMainScreen { get; set; }
        public bool ScreenshotEnabled { get; set; }
        public bool ScreenshotSaveToFile { get; set; } = true;
        public string ScreenshotSaveFolderPath { get; set; } = "";
        public bool ScreenshotCopyToClipboard { get; set; }
        public string AppLanguage { get; set; } = "system";
    }
}
