# Window Resize for Windows — User Manual

## Table of Contents

1. [Getting Started](#getting-started)
2. [Resizing a Window](#resizing-a-window)
3. [Settings](#settings)
4. [Troubleshooting](#troubleshooting)

---

## Getting Started

1. Run **WindowsResizeCapture.exe**. A splash screen appears briefly.
2. The app icon appears in the **system tray** (notification area at the bottom-right of the taskbar).
3. Click the tray icon to open the menu.

> **Note:** No special permissions are required. The app works immediately after launch.

---

## Resizing a Window

### Step-by-Step

1. Click the **Window Resize icon** in the system tray.
2. Hover over **"Resize"** to open the window list.
3. All currently open windows are listed with their **application icon** and name as **[App Name] Window Title**. Long titles are automatically truncated to keep the menu readable.
4. Hover over a window to see the available preset sizes.
5. Click a size to resize the window immediately.

### How Sizes Are Displayed

Each size entry in the menu shows:

```
1920 x 1080          Full HD
```

- **Left:** Width x Height (in pixels)
- **Right:** Label (standard name), displayed in gray

### Sizes That Exceed the Screen

If a preset size is larger than the display where the window is located, that size will be **grayed out and unselectable**. This prevents you from resizing a window beyond the screen boundaries.

---

## Settings

Click the Window Resize tray icon, then select **"Settings..."** to open the settings window.

### Built-in Sizes

The app includes 12 built-in preset sizes:

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

Built-in sizes cannot be removed or edited.

### Custom Sizes

You can add your own sizes to the list:

1. Enter the **Width** and **Height** in pixels.
2. Click **"Add"**.
3. The new size appears in the custom list and is immediately available in the resize menu.

To remove a custom size, click the **"Remove"** button next to it.

### Launch at Login

Toggle **"Launch at Login"** to have Window Resize start automatically when you log in to Windows.

### Screenshot

Toggle **"Take screenshot after resize"** to automatically capture the window after resizing.

When enabled, the following options are available:

- **Save to file** — Save the screenshot as a PNG file to your chosen folder.
  > **Filename format:** `MMddHHmmss_AppName_WindowTitle.png` (e.g. `0227193012_chrome_Google.png`). Symbols are removed; only letters, digits, and underscores are used.
- **Copy to clipboard** — Copy the screenshot to the clipboard for pasting into other apps.

Both options can be enabled independently.

---

## Troubleshooting

### Resize Failed

If you see a "Resize Failed" alert, possible causes include:

- The target window does not support external resizing.
- The window is in **full-screen mode** (exit full-screen first by pressing **F11** or **Esc**).

### Window Not Appearing in the List

The resize menu only shows windows that are:

- Currently visible on screen
- Have a title bar
- Not the Window Resize app's own windows

Minimized windows will not appear in the list.

### Screenshot Not Working

If screenshots are not being captured:

- Ensure at least one of **"Save to file"** or **"Copy to clipboard"** is enabled in Settings.
- If saving to file, verify the save folder exists and is writable.
