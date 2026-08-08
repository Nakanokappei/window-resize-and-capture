# Window Resize & Capture

A system tray app for Windows. It resizes any app window to a preset size, and it can save a picture of that window at the same time.

The app started as a port of [Window Resize for macOS](https://github.com/Nakanokappei/window-resize). The two apps are now different in both features and code.

## Features

- **Lives in the system tray** — left-click or right-click the icon to open the menu
- **12 built-in sizes** — common Windows display resolutions
- **Custom sizes** — add your own width x height presets and give them names
- **Capture** — after a resize, save a picture of the window to a file, to the clipboard, or to both
- **App icons** — every window in the menu shows its app icon, so the one you want is easy to spot
- **Launch at login** — start the app automatically when you sign in to Windows
- **One copy at a time** — starting the app again does not create a second copy
- **High DPI** — looks correct on displays scaled to 125%, 150%, or 200%
- **16 languages** — the app follows your Windows language: English, Simplified Chinese, Spanish, Hindi, Arabic, Indonesian, Portuguese, French, Japanese, Russian, German, Vietnamese, Thai, Korean, Italian, Traditional Chinese

## Download

Get the latest version from [Releases](https://github.com/Nakanokappei/window-resize-and-capture/releases).

You do not need to install the .NET runtime. Everything the app needs is inside the .exe.

## How to use

1. Run `WindowResizeCapture.exe`.
2. A splash screen appears for a moment. Then the app icon appears in the system tray.
3. Click the icon to open the menu.
4. Select **Resize**, choose a window, then choose a size.
5. Open **Settings** to add your own sizes, turn on launch at login, or set up capture.

## System requirements

- Windows 10 or Windows 11 (64-bit)

## Built-in sizes

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

## Build from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
cd WindowResize
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The app is built here:

```
bin/Release/net8.0-windows10.0.17763.0/win-x64/publish/WindowResizeCapture.exe
```

You can run the same command on macOS. The project sets `EnableWindowsTargeting`, so the Windows build works there as well.

## Privacy

The app collects no personal data. See the [privacy policy](PRIVACY.md).

## License

[MIT](LICENSE)
