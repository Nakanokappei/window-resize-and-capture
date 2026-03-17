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

namespace WindowResize;

// 9-position snap anchor for post-resize window placement
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WindowPosition
{
    TopLeft, Top, TopRight,
    Left, Center, Right,
    BottomLeft, Bottom, BottomRight
}

public class SettingsStore
{
    private static readonly Lazy<SettingsStore> _instance = new(() => new SettingsStore());
    public static SettingsStore Shared => _instance.Value;

    private readonly string _settingsPath;
    private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WindowResize";

    public List<PresetSize> CustomSizes { get; private set; } = new();

    // Window behaviour settings
    public bool BringToFront { get; set; } = true;
    public WindowPosition? Position { get; set; }
    public bool MoveToMainScreen { get; set; }

    // Whether any positioning feature is active (used to show "Current Size" menu item)
    public bool HasActivePositioningFeatures =>
        BringToFront || Position != null || MoveToMainScreen;

    // Screenshot settings with smart auto-enable/disable logic
    private bool _screenshotEnabled;
    public bool ScreenshotEnabled
    {
        get => _screenshotEnabled;
        set
        {
            _screenshotEnabled = value;
            // Auto-enable clipboard if no destination is selected
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
            // Auto-disable master toggle if no destination remains
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
            // Auto-disable master toggle if no destination remains
            if (!value && !ScreenshotSaveToFile)
                _screenshotEnabled = false;
        }
    }

    // Language override (empty or "system" = system default)
    public string AppLanguage { get; set; } = "system";

    // Supported languages for the language picker
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

    public bool LaunchAtLogin
    {
        get => GetLaunchAtLogin();
        set => SetLaunchAtLogin(value);
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

    public List<PresetSize> AllSizes
    {
        get
        {
            var all = new List<PresetSize>(BuiltInSizes);
            all.AddRange(CustomSizes);
            return all;
        }
    }

    public event Action? SettingsChanged;

    private SettingsStore()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appDir = Path.Combine(appData, "WindowResize");
        Directory.CreateDirectory(appDir);
        _settingsPath = Path.Combine(appDir, "settings.json");
        Load();
    }

    public void AddSize(PresetSize size)
    {
        CustomSizes.Add(size);
        Save();
        SettingsChanged?.Invoke();
    }

    public void RemoveSize(PresetSize size)
    {
        CustomSizes.RemoveAll(s => s.Id == size.Id);
        Save();
        SettingsChanged?.Invoke();
    }

    public void SaveScreenshotSettings()
    {
        Save();
        SettingsChanged?.Invoke();
    }

    // Save behaviour settings (bring-to-front, position, move-to-main-screen)
    public void SaveBehaviourSettings()
    {
        Save();
        SettingsChanged?.Invoke();
    }

    // Apply language override and restart the app
    public void ApplyLanguage(string languageCode)
    {
        AppLanguage = languageCode;
        Save();

        if (languageCode == "system" || string.IsNullOrEmpty(languageCode))
        {
            Strings.Culture = null;
        }
        else
        {
            Strings.Culture = new CultureInfo(languageCode);
        }
    }

    // Set the resource culture on startup based on saved language
    public void InitializeLanguage()
    {
        if (!string.IsNullOrEmpty(AppLanguage) && AppLanguage != "system")
        {
            Strings.Culture = new CultureInfo(AppLanguage);
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                var data = JsonSerializer.Deserialize<SettingsData>(json);
                if (data?.CustomSizes != null)
                    CustomSizes = data.CustomSizes;

                // Load behaviour settings
                BringToFront = data?.BringToFront ?? true;
                Position = data?.Position;
                MoveToMainScreen = data?.MoveToMainScreen ?? false;

                // Load screenshot settings (use backing fields to avoid auto-logic during load)
                _screenshotEnabled = data?.ScreenshotEnabled ?? false;
                _screenshotSaveToFile = data?.ScreenshotSaveToFile ?? true;
                ScreenshotSaveFolderPath = data?.ScreenshotSaveFolderPath ?? "";
                _screenshotCopyToClipboard = data?.ScreenshotCopyToClipboard ?? false;

                // Load language
                AppLanguage = data?.AppLanguage ?? "system";
            }
        }
        catch { }
    }

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

    /// <summary>
    /// Detects whether the app is running as a packaged MSIX app.
    /// </summary>
    public static bool IsPackaged()
    {
#if WINDOWS10_0_17763_0_OR_GREATER
        try
        {
            // Package.Current throws if not packaged
            _ = Package.Current.Id;
            return true;
        }
        catch { }
#endif
        return false;
    }

    private bool GetLaunchAtLogin()
    {
        if (IsPackaged())
            return GetLaunchAtLoginPackaged();
        return GetLaunchAtLoginRegistry();
    }

    private void SetLaunchAtLogin(bool enabled)
    {
        if (IsPackaged())
            SetLaunchAtLoginPackaged(enabled);
        else
            SetLaunchAtLoginRegistry(enabled);
    }

    // --- Registry-based (non-packaged / EXE distribution) ---

    private bool GetLaunchAtLoginRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch { return false; }
    }

    private void SetLaunchAtLoginRegistry(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
            if (key == null) return;

            if (enabled)
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

    // --- StartupTask-based (packaged / MSIX Store distribution) ---

    private bool GetLaunchAtLoginPackaged()
    {
#if WINDOWS10_0_17763_0_OR_GREATER
        try
        {
            var task = StartupTask.GetAsync("WindowResizeStartup").GetAwaiter().GetResult();
            return task.State == StartupTaskState.Enabled;
        }
        catch { }
#endif
        return false;
    }

    private void SetLaunchAtLoginPackaged(bool enabled)
    {
#if WINDOWS10_0_17763_0_OR_GREATER
        try
        {
            var task = StartupTask.GetAsync("WindowResizeStartup").GetAwaiter().GetResult();
            if (enabled)
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
    }

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
