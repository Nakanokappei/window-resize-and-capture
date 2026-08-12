using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
#if WINDOWS10_0_17763_0_OR_GREATER
using Windows.ApplicationModel;
#endif

namespace WindowResizeCapture;

// Nine-position snap anchor for placing a window after resize.
[JsonConverter(typeof(JsonStringEnumConverter<WindowPosition>))]
public enum WindowPosition
{
    TopLeft, Top, TopRight,
    Left, Center, Right,
    BottomLeft, Bottom, BottomRight
}

// How far a window stands off the screen edge it is sent to.
//
// Only the edges. A window sent to the middle of the screen touches no edge, so
// nothing is held away from anything there, whichever of these is chosen. The
// same goes for the one coordinate a side position leaves alone: a window at the
// top of the screen is centred left to right, and only its top edge is held off.
[JsonConverter(typeof(JsonStringEnumConverter<ScreenEdgeMargin>))]
public enum ScreenEdgeMargin
{
    None,
    Taskbar,
    TitleBar
}

// Thread-safe singleton that persists all user preferences to a JSON file
// in %APPDATA%/WindowsResizeCapture/settings.json. Also manages the
// "launch at login" registration via either the Windows registry (standalone
// EXE) or the UWP StartupTask API (MSIX Store distribution).
public partial class SettingsStore
{
    private static readonly Lazy<SettingsStore> _instance = new(() => new SettingsStore());
    public static SettingsStore Shared => _instance.Value;

    private readonly string _settingsPath;
    private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    // These three keep the misspelled "Windows" of the original executable
    // name on purpose. They address state that already exists on a user's
    // machine: the registry value that registers auto-start, the StartupTask
    // declared in the package manifest, and the settings directory. Renaming
    // any of them would silently discard the user's preferences or leave an
    // orphaned auto-start entry pointing at an executable that no longer
    // exists. Nothing here is ever shown in the UI - the name the app shows a
    // person is App.Name, and these are not it.
    private const string RegistryRunValueName = "WindowsResizeCapture";
    private const string StartupTaskId = "WindowsResizeCaptureStartup";
    private const string SettingsDirectoryName = "WindowsResizeCapture";

    public List<PresetSize> CustomSizes { get; private set; } = new();

    // Window behavior settings
    public bool BringToFront { get; set; } = true;
    public WindowPosition? Position { get; set; }
    public bool MoveToMainScreen { get; set; }

    // How far the position above holds a window off the edge it sends it to.
    // None by default, which is where every window has landed until now.
    public ScreenEdgeMargin EdgeMargin { get; set; }

    // When true, preset dimensions size the window's client area (content)
    // rather than its outer frame, so the visible content matches the number.
    public bool ResizeClientArea { get; set; }

    // True when any post-resize positioning feature is enabled, which
    // determines whether the "Current Size" menu item should appear.
    public bool IsPositioningActive =>
        BringToFront || Position != null || MoveToMainScreen;

    // Capture destination settings with smart auto-toggle logic:
    //  - Enabling captures with no destination auto-enables clipboard.
    //  - Disabling all destinations auto-disables the master toggle.

    private bool _captureEnabled;
    public bool CaptureEnabled
    {
        get => _captureEnabled;
        set
        {
            _captureEnabled = value;

            // If enabling with no output selected, default to clipboard
            if (value && !CaptureSaveToFile && !CaptureCopyToClipboard)
                CaptureCopyToClipboard = true;
        }
    }

    private bool _captureSaveToFile = true;
    public bool CaptureSaveToFile
    {
        get => _captureSaveToFile;
        set
        {
            _captureSaveToFile = value;

            // Turn off the master toggle when no destination remains
            if (!value && !CaptureCopyToClipboard)
                _captureEnabled = false;
        }
    }

    public string CaptureSaveFolderPath { get; set; } = "";

    private bool _captureCopyToClipboard;
    public bool CaptureCopyToClipboard
    {
        get => _captureCopyToClipboard;
        set
        {
            _captureCopyToClipboard = value;

            // Turn off the master toggle when no destination remains
            if (!value && !CaptureSaveToFile)
                _captureEnabled = false;
        }
    }

    // When true, only the window's client area (content) is captured,
    // excluding the title bar and frame.
    public bool CaptureClientArea { get; set; }

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
                return key?.GetValue(RegistryRunValueName) != null;
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
                    key.SetValue(RegistryRunValueName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(RegistryRunValueName, false);
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
        // FWXGA, not WXGA. WXGA is 1280x800 (and 1280x768); 1366x768 is the
        // "full" one. The wrong name here would have shipped a size list that
        // disagrees with every table a user can look this up in.
        new(1366, 768,  "FWXGA"),
        new(1280, 1024, "SXGA"),
        new(1280, 800,  "WXGA"),
        new(1280, 720,  "HD"),
        new(1024, 768,  "XGA"),
        new(800,  600,  "SVGA"),
    };

    // The sizes whose box is cleared in the settings window. Only what was
    // cleared is written down, so a size added by a later version arrives shown.
    //
    // Kept as "width x height" rather than by Id, because a built-in size is
    // built at startup and takes a new Id with it every time: an Id written to
    // the file would match nothing on the next run. Two presets of the same
    // dimensions read as one line in the menu, and clearing the box hides both.
    private readonly HashSet<string> _sizesKeptOutOfTheMenu = new();

    private static string SizeKey(PresetSize size) => $"{size.Width}x{size.Height}";

    public bool ShowsInMenu(PresetSize size) => !_sizesKeptOutOfTheMenu.Contains(SizeKey(size));

    public void SetShowsInMenu(PresetSize size, bool shows)
    {
        if (shows)
            _sizesKeptOutOfTheMenu.Remove(SizeKey(size));
        else
            _sizesKeptOutOfTheMenu.Add(SizeKey(size));

        SaveAndNotify();
    }

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
        string appDir = Path.Combine(appData, SettingsDirectoryName);
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

    // Read settings from the JSON file into this instance's properties.
    // Uses backing fields for capture booleans to avoid triggering the
    // auto-enable/disable logic during deserialization.
    private void Load()
    {
        if (!File.Exists(_settingsPath))
            return;

        try
        {
            string json = File.ReadAllText(_settingsPath);
            var data = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.SettingsData);

            if (data?.CustomSizes != null)
                CustomSizes = data.CustomSizes;

            _sizesKeptOutOfTheMenu.Clear();
            if (data?.HiddenSizes != null)
                foreach (string key in data.HiddenSizes)
                    _sizesKeptOutOfTheMenu.Add(key);

            // Behavior settings
            BringToFront = data?.BringToFront ?? true;
            Position = data?.Position;
            MoveToMainScreen = data?.MoveToMainScreen ?? false;
            EdgeMargin = data?.EdgeMargin ?? ScreenEdgeMargin.None;
            ResizeClientArea = data?.ResizeClientArea ?? false;

            // Capture settings (bypass property setters to avoid auto-logic).
            // Each falls back to the pre-rename key so that upgrading from
            // v1.8.1 or earlier preserves the user's choices.
            _captureEnabled = data?.CaptureEnabled ?? data?.LegacyCaptureEnabled ?? false;
            _captureSaveToFile = data?.CaptureSaveToFile ?? data?.LegacyCaptureSaveToFile ?? true;
            CaptureSaveFolderPath =
                data?.CaptureSaveFolderPath ?? data?.LegacyCaptureSaveFolderPath ?? "";
            _captureCopyToClipboard =
                data?.CaptureCopyToClipboard ?? data?.LegacyCaptureCopyToClipboard ?? false;
            CaptureClientArea = data?.CaptureClientArea ?? false;
        }
        catch (Exception failure)
        {
            // A file that cannot be read leaves this instance on its defaults,
            // and the first setting the user changes writes over it. Everything
            // they had chosen would be gone with no way back and nothing to
            // look at, so the file is kept and what went wrong is written
            // beside it.
            KeepUnreadableSettings(failure);
        }
    }

    // Move the unreadable file aside, with a note saying why, and leave the
    // reason where a person looking for their settings will find it: next to
    // them. Overwrites an earlier copy, because the useful one is the file that
    // was in place when the settings were last lost.
    private void KeepUnreadableSettings(Exception failure)
    {
        try
        {
            string kept = _settingsPath + ".unreadable";
            File.Copy(_settingsPath, kept, overwrite: true);
            File.WriteAllText(
                _settingsPath + ".unreadable.txt",
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
                $"{Path.GetFileName(_settingsPath)} could not be read, so the app " +
                $"started with its default settings and has kept the file as " +
                $"{Path.GetFileName(kept)}.{Environment.NewLine}" +
                $"{failure.GetType().Name}: {failure.Message}{Environment.NewLine}");
        }
        catch
        {
            // Nothing further to try: the app still runs on its defaults.
        }
    }

    // Serialize all current settings to JSON and write to disk.
    private void Save()
    {
        try
        {
            var data = new SettingsData
            {
                CustomSizes = CustomSizes,
                HiddenSizes = new List<string>(_sizesKeptOutOfTheMenu),
                BringToFront = BringToFront,
                Position = Position,
                MoveToMainScreen = MoveToMainScreen,
                EdgeMargin = EdgeMargin,
                ResizeClientArea = ResizeClientArea,
                CaptureEnabled = CaptureEnabled,
                CaptureSaveToFile = CaptureSaveToFile,
                CaptureSaveFolderPath = CaptureSaveFolderPath,
                CaptureCopyToClipboard = CaptureCopyToClipboard,
                CaptureClientArea = CaptureClientArea
            };
            string json = JsonSerializer.Serialize(data, SettingsJsonContext.Default.SettingsData);
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

    // JSON-serializable DTO mirroring all persisted fields. The capture
    // fields are nullable so that "key absent" is distinguishable from a
    // stored false, which is what lets the pre-rename keys below take over.
    private class SettingsData
    {
        public List<PresetSize>? CustomSizes { get; set; }

        // The sizes whose box is cleared in the settings window, as
        // "width x height". Written as the exceptions rather than as the whole
        // list, so a size the app gains later is shown without being listed.
        public List<string>? HiddenSizes { get; set; }

        public bool BringToFront { get; set; } = true;
        public WindowPosition? Position { get; set; }
        public bool MoveToMainScreen { get; set; }
        public ScreenEdgeMargin EdgeMargin { get; set; }
        public bool ResizeClientArea { get; set; }
        public bool? CaptureEnabled { get; set; }
        public bool? CaptureSaveToFile { get; set; }
        public string? CaptureSaveFolderPath { get; set; }
        public bool? CaptureCopyToClipboard { get; set; }
        public bool CaptureClientArea { get; set; }

        // Key names written by v1.8.1 and earlier, when the feature was
        // called "screenshot". They are read so that an existing install
        // keeps its preferences, and never written, so the file converts to
        // the current names the first time a setting changes.
        [JsonPropertyName("ScreenshotEnabled")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? LegacyCaptureEnabled { get; set; }

        [JsonPropertyName("ScreenshotSaveToFile")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? LegacyCaptureSaveToFile { get; set; }

        [JsonPropertyName("ScreenshotSaveFolderPath")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LegacyCaptureSaveFolderPath { get; set; }

        [JsonPropertyName("ScreenshotCopyToClipboard")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? LegacyCaptureCopyToClipboard { get; set; }
    }

    // Source-generated JSON serializer context for trim-safe serialization.
    // Eliminates reflection-based type discovery that the trimmer cannot analyze.
    [JsonSerializable(typeof(SettingsData))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    private partial class SettingsJsonContext : JsonSerializerContext { }
}
