# Window Resize & Capture — Windows tray app

Resizes and captures the windows of other applications from the notification
area. It began as a port of the macOS app Window Resize; the two have since
diverged in both features and implementation.

C# on .NET 8 with WinForms, shipped as a self-contained single file so that no
.NET runtime is required. All window work goes through Win32 P/Invoke.

## Build

```bash
# Build and run from a checkout
dotnet build WindowResize/WindowResize.csproj

# The artifact that ships: self-contained, single file, compressed
dotnet publish WindowResize/WindowResize.csproj -c Release -r win-x64 \
  --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
# -> WindowResize/bin/Release/net8.0-windows10.0.17763.0/win-x64/publish/WindowResizeCapture.exe
```

The MSIX build is the same publish with `-p:PublishSingleFile=false`, because
the package layout needs the loose files.

**Never add `PublishTrimmed`.** WinForms reaches COM interop and UI Automation
types the trimmer cannot see, and the app dies with `TypeLoadException` at run
time (`UiaCore`, `ComponentManager`).

### Cross-compiling from macOS

`UseWindowsForms` needs the Windows Desktop SDK, which does not exist on macOS.
The project works around this with `EnableWindowsTargeting=true` plus a direct
`FrameworkReference`, so `dotnet publish -r win-x64` works from either OS.

## Source layout

| File | Responsibility |
|---|---|
| `Program.cs` | Entry point, single-instance mutex |
| `TrayApplicationContext.cs` | Tray icon, menu construction, resize + capture flow |
| `WindowManager.cs` | Win32 P/Invoke: enumeration, resize, positioning, foreground |
| `CaptureHelper.cs` | Window capture via `PrintWindow`, scaling, delivery |
| `SettingsStore.cs` | JSON persistence, launch-at-login, built-in preset list |
| `SettingsForm.cs` | Settings window (tabs: General, Capture, Behavior) |
| `SplashForm.cs` | Startup splash, and the place the version string is drawn |
| `PresetSize.cs` | Size model |
| `Package/` | MSIX manifest and Store assets |
| `Resources/` | `Strings.resx` (English) plus 15 translations, icon, splash |

The built-in preset sizes live in `SettingsStore.BuiltInSizes`. They are not
duplicated here on purpose: an earlier copy of that table in this file drifted
out of date and started contradicting the code.

## Things that will bite you

### Identifiers that deliberately keep a misspelling

The executable was `WindowsResizeCapture.exe` until 1.8.2 — plural "Windows",
a leftover from the macOS port. The file was renamed, but four identifiers
were **deliberately left with the old spelling**, because each names state that
already exists on a user's machine:

| Identifier | Renaming it would |
|---|---|
| `%APPDATA%\WindowsResizeCapture\` | discard every saved preference |
| Registry `Run` value `WindowsResizeCapture` | lose launch-at-login and strand a dead entry |
| MSIX `Application Id` / `StartupTask` TaskId | break taskbar and Start pins, reset auto-start |
| `Global\WindowsResizeCapture_SingleInstance_F7A3B2` | let an old and a new instance run at once |

The registry value name must stay in step with `installer/WindowResize.iss`,
and the TaskId with `Package/AppxManifest.xml`. None of them is ever shown in
the UI.

### Capture must not run on the UI thread

`PrintWindow` sends `WM_PRINT` synchronously to the target window and has **no
timeout parameter**. Capturing a busy or unresponsive window used to stall the
message pump until the system hang timeout, which Partner Center reported as
`MOAPPLICATION_HANG ... HANG_QUIESCE`. Since 1.8.1 the capture runs on a
thread-pool thread; only the clipboard write is marshalled back, because
`Clipboard.SetImage` requires an STA thread.

The same applies to icon extraction: `WM_GETICON` goes out through
`SendMessageTimeout` with `SMTO_ABORTIFHUNG` and a short cap, because the cap
applies per message across three icon sizes for every window in the list.

### Capture under DPI virtualization

Symptom: on a 200 %-scaled display (Parallels on a Retina Mac, for instance)
only the top-left quarter of the window is captured.

Cause: a HDC obtained through GDI+ `Graphics.FromImage()` is subject to DPI
scaling, and `GetWindowRect` returns logical pixels.

Fix, in this order:
1. `SetThreadDpiAwarenessContext(PER_MONITOR_AWARE_V2)` so `GetWindowRect`
   reports physical pixels.
2. Build the memory DC with native GDI (`CreateCompatibleDC` +
   `CreateCompatibleBitmap`) from the window's own DC and hand that to
   `PrintWindow`.
3. Scale the result down to the chosen `PresetSize` with `HighQualityBicubic`.

`SetResolution(96, 96)` does nothing here — it only touches GDI+ metadata, not
the GDI HDC.

### Settings written before the capture rename

Files written by 1.8.1 and earlier use `Screenshot*` keys. `SettingsData`
declares nullable `Capture*` properties plus read-only `Legacy*` properties
bound to the old names, so an absent key is distinguishable from a stored
`false`. The file converts to the new names the first time a setting changes.

### Packaged and unpackaged builds behave differently

One codebase serves both the direct EXE and the Store MSIX.
`SettingsStore.IsPackaged()` decides which path to take:

- **MSIX**: auto-start through the `Windows.ApplicationModel.StartupTask` API,
  because a registry `Run` key has no effect for a packaged app.
- **Plain EXE**: auto-start through the registry `Run` key.

A global mutex does work inside a package, though the namespace may be
isolated — do not assume cross-boundary exclusion.

### Window enumeration filters

`EnumWindows` returns far more than application windows. The list is narrowed
by `DWMWA_CLOAKED` (drops hidden UWP containers and virtual-desktop ghosts),
`WS_CAPTION`, and `WS_EX_TOOLWINDOW` unless `WS_EX_APPWINDOW` is also set.

## Microsoft Store

Published as a Desktop Bridge package: MSIX with `runFullTrust`, which is
required because the app is built on Win32 P/Invoke. Justify it in the
submission as access needed for window enumeration, resizing and capture.

| | |
|---|---|
| App name | Window Resize & Capture |
| Identity Name | `KappeiNakano.WindowResizeforWindows` (assigned at reservation; unchanged by renames) |
| Publisher ID | `CN=CBBEB0B6-F2F8-4A20-93BF-7BB185208944` |

Microsoft signs the package at submission, so no signing is needed here.
Testing an MSIX locally does need a self-signed certificate and developer mode.

`.github/workflows/msix.yml` builds the package on any `v*` tag and attaches it
to the GitHub release. It verifies that the manifest version matches the tag
and fails the build otherwise, so bump the manifest before tagging.

## Release procedure

1. Update the version in **four** places:
   `WindowResize.csproj` `<Version>`, `SplashForm.cs`, `Package/AppxManifest.xml`,
   `installer/WindowResize.iss`.
   Omitting the csproj one makes the executable report `1.0.0.0` in its file
   properties and in the installed-programs list.
2. Publish, then build the two local artifacts:
   - `dist/WindowResizeCapture-Windows-v{VERSION}.zip` — exe + README + LICENSE
   - `dist/WindowResizeCapture-Setup-v{VERSION}.exe` — `ISCC.exe installer/WindowResize.iss`
3. Commit, tag `v{VERSION}`, push. The workflow builds and publishes the MSIX.
4. Upload the ZIP and installer to the same release.
5. For a Store release, submit the MSIX from that release in Partner Center.

Tool paths on the current machine:
`C:\Users\nakanokappei\AppData\Local\Programs\Inno Setup 6\ISCC.exe`
(installable with `winget install JRSoftware.InnoSetup`), and
`C:\Program Files\GitHub CLI\gh.exe`.

Upgrading over an older install is the risky part of a release: the executable
rename means the installer carries an `[InstallDelete]` entry for the old file,
and the settings-key migration has only been exercised in isolation. Verify a
real upgrade before submitting to the Store.

## Conventions

- Name things after the UI. The capture feature is "Capture" everywhere —
  label, resource key, property, JSON key — because someone who reads the UI
  should be able to grep for it.
- Resource keys are PascalCase and mirror the UI wording: `MenuResize`,
  `SettingsWidth`, `AlertResizeFailedTitle`.
- Menu item text passes through `EscapeMenuMnemonics`: WinForms treats `&` as a
  mnemonic prefix, and both the app name and arbitrary window titles contain
  one. Escape after measuring text, never before.
- Preset size labels ("Full HD", "XGA") are not translated.
- `SettingsForm` hides on close rather than disposing.
- The menu is rebuilt from the `SettingsChanged` event.
- The window list is populated lazily on `DropDownOpening`.
- Right-aligned menu tags are drawn via `ShortcutKeyDisplayString`.
- Menu titles are truncated to a quarter of the screen width (`TruncateToFit`).
