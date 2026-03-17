# Window Resize for Windows

A system tray application that resizes windows to preset sizes.

Windows port of [Window Resize (macOS)](https://github.com/Nakanokappei/window-resize).

## Features

- **System tray resident** — left-click or right-click the tray icon to open the menu
- **12 built-in preset sizes** — common Windows display resolutions
- **Custom sizes** — add your own width x height presets
- **Screenshot capture** — automatically capture a screenshot after resizing (save to file and/or clipboard)
- **Window icons** — app icons displayed in the menu for easy identification
- **Launch at login** — optional auto-start via Windows Registry
- **Single instance** — prevents duplicate processes
- **High DPI support** — works correctly on 125% / 150% / 200% scaled displays
- **16 languages** — English, Simplified Chinese, Spanish, Hindi, Arabic, Indonesian, Portuguese, French, Japanese, Russian, German, Vietnamese, Thai, Korean, Italian, Traditional Chinese

## Download

Download the latest release from [Releases](https://github.com/Nakanokappei/window-resize-and-capture-windows/releases).

No .NET runtime installation required — the exe is self-contained.

## Usage

1. Run `WindowResize.exe`
2. A splash screen appears briefly, then the app sits in the system tray
3. Click the tray icon to open the menu
4. Select **Resize** → choose a window → select a preset size
5. Open **Settings** to add custom sizes, enable launch at login, or configure screenshot capture

## System Requirements

- Windows 10 / 11 (x64)

## Preset Sizes

| Size | Label |
|------|-------|
| 3840 x 2160 | 4K UHD |
| 2560 x 1440 | QHD |
| 1920 x 1200 | WUXGA |
| 1920 x 1080 | Full HD |
| 1680 x 1050 | WSXGA+ |
| 1600 x 900 | HD+ |
| 1440 x 900 | WXGA+ |
| 1366 x 768 | WXGA |
| 1280 x 1024 | SXGA |
| 1280 x 720 | HD |
| 1024 x 768 | XGA |
| 800 x 600 | SVGA |

## Build from Source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
cd WindowResize
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output: `bin/Release/net8.0-windows/win-x64/publish/WindowResize.exe`

### Cross-compile from macOS

The project supports cross-compilation from macOS using `EnableWindowsTargeting`:

```bash
dotnet publish WindowResize.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Privacy Policy

[Privacy Policy](PRIVACY.md) — This app does not collect any personal data.

## License

[MIT](LICENSE)
