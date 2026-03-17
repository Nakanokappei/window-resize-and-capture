# Window Resize - Windows Tray App

## Overview
Windows で動作中のアプリウィンドウを既定サイズにリサイズするタスクトレイ常駐アプリ。
macOS 版 (Window Resize) の Windows 移植。

## Tech Stack
- **Language:** C# (.NET 8, WinForms)
- **Build:** `dotnet publish` でMac上からクロスコンパイル
- **配布:** 自己完結型の単一exe（.NETランタイム不要）
- **ウィンドウ操作:** Win32 API (P/Invoke) — `EnumWindows`, `SetWindowPos`

## Build & Run
```bash
# Windows上でのビルド
dotnet build WindowResize/WindowResize.csproj
dotnet publish WindowResize/WindowResize.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# macOS上でのクロスコンパイル
DOTNET=/opt/homebrew/Cellar/dotnet@8/8.0.123/libexec/dotnet
$DOTNET publish WindowResize.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# 出力先
# WindowResize/bin/Release/net8.0-windows10.0.17763.0/win-x64/publish/WindowsResizeCapture.exe
```

## Project Structure
```
Window Resize and Capture/       # リポジトリルート (W:\01_Active\Window Resize and Capture)
├── README.md                    # GitHub用README
├── LICENSE                      # MIT License
├── .gitignore
├── .github/workflows/msix.yml  # MSIX自動ビルド (v* タグ)
├── installer/
│   └── WindowResize.iss         # Inno Setup インストーラスクリプト
├── dist/                        # ビルド成果物 (.gitignore対象)
└── WindowResize/                # ソースコード
    ├── CLAUDE.md                # このファイル
    ├── WindowResize.csproj      # プロジェクト設定 (TFM: net8.0-windows10.0.17763.0)
    ├── Program.cs               # エントリポイント
    ├── TrayApplicationContext.cs # NotifyIcon・メニュー構築・リサイズ実行
    ├── WindowManager.cs         # Win32 P/Invoke（ウィンドウ列挙・リサイズ）
    ├── PresetSize.cs            # サイズモデル
    ├── SettingsStore.cs         # JSON永続化・自動起動 (Registry/StartupTask)
    ├── SettingsForm.cs          # WinForms設定画面
    ├── SplashForm.cs            # スプラッシュ画面 (バージョン表示)
    ├── ScreenshotHelper.cs      # スクリーンショットキャプチャ (PrintWindow API)
    ├── Package/
    │   ├── AppxManifest.xml     # MSIX マニフェスト
    │   └── Assets/*.png         # Store 用アセット (10ファイル)
    └── Resources/
        ├── Strings.resx         # 英語（デフォルト）+ 15言語の .resx
        ├── app.ico              # アプリアイコン
        └── splash.png           # スプラッシュ画像
```

## Key Architecture Decisions

### macOS版との差異
| 項目 | macOS | Windows |
|------|-------|---------|
| トレイ | NSStatusItem | NotifyIcon |
| メニュー | NSMenu | ContextMenuStrip |
| ウィンドウ列挙 | CGWindowListCopyWindowInfo | EnumWindows (P/Invoke) |
| リサイズ | AXUIElement | SetWindowPos (P/Invoke) |
| 権限 | Accessibility権限必要 | 不要 |
| 設定保存 | UserDefaults | JSON in AppData |
| 自動起動 | SMAppService | Registry Run key |
| スクリーンショット | SCScreenshotManager / CGWindowListCreateImage | PrintWindow (P/Invoke) |
| 多言語 | .lproj/Localizable.strings | .resx リソース |

### ウィンドウ列挙・リサイズ
- `EnumWindows` で全ウィンドウ列挙
- `DWMWA_CLOAKED` フィルタリングでUWP隠しウィンドウ除外
- `WS_CAPTION` + `WS_EX_TOOLWINDOW` でスタイルフィルタリング
- `SetWindowPos` でリサイズ実行（`SWP_NOMOVE | SWP_NOZORDER`）

### 設定永続化
- JSON ファイル: `%APPDATA%/WindowsResizeCapture/settings.json`
- 自動起動: `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`

### 多言語対応 (i18n)
- .NET .resx リソースファイル方式
- `Strings.PropertyName` で呼び出し（強く型付けされたリソース）
- プリセットサイズ名（"Full HD", "XGA" 等）は翻訳しない

### メニュー構造
- NotifyIcon + ContextMenuStrip
- 左クリック・右クリックどちらでもメニュー表示
- DropDownOpening イベントで遅延ウィンドウ一覧取得

### プリセットサイズ
| サイズ | ラベル |
|--------|--------|
| 3840 x 2160 | 4K UHD |
| 2560 x 1440 | QHD |
| 1920 x 1080 | Full HD |
| 1680 x 1050 | WSXGA+ |
| 1366 x 768 | HD+ |
| 1280 x 720 | HD |
| 1024 x 768 | XGA |
| 800 x 600 | SVGA |

### スクリーンショット
- `PrintWindow` API (P/Invoke) で対象ウィンドウをキャプチャ
- `PW_RENDERFULLCONTENT` フラグでDWM合成ウィンドウ対応
- リサイズ成功後500ms待機してからキャプチャ（Mac版と同じ）
- ファイル名形式: `MMddHHmmss_AppName_WindowTitle.png`
- 設定項目:
  - `ScreenshotEnabled` — マスタートグル
  - `ScreenshotSaveToFile` — ファイル保存
  - `ScreenshotSaveFolderPath` — 保存先フォルダ（FolderBrowserDialogで選択）
  - `ScreenshotCopyToClipboard` — クリップボードコピー

#### DPIスケーリング対応
- **問題:** DPI仮想化環境（Parallels+Retina Mac等、200%スケーリング）で左上1/4しかキャプチャされない
- **原因:** GDI+ `Graphics.FromImage()` 経由のHDCがDPIスケーリングの影響を受け、`GetWindowRect`も論理ピクセルを返す
- **対策:**
  1. `SetThreadDpiAwarenessContext(PER_MONITOR_AWARE_V2)` で物理ピクセルサイズを取得
  2. ネイティブGDI (`CreateCompatibleDC` + `CreateCompatibleBitmap`) でウィンドウDCと互換なメモリDCを作成し `PrintWindow` に渡す
  3. キャプチャ後、ユーザー指定の `PresetSize` (800x600等) に `HighQualityBicubic` でリサイズ
- **注意:** `SetResolution(96, 96)` はGDI+メタデータのみでGDI HDCには影響しないため効果なし

### クロスコンパイル注意事項
- macOS上では `UseWindowsForms` SDK が利用不可
- `EnableWindowsTargeting=true` + `FrameworkReference` で代替
- `dotnet publish -r win-x64` でクロスビルド可能

## Microsoft Store 公開

### 公開方式
Desktop Bridge (MSIX + `runFullTrust`)。Win32 P/Invoke を多用するため `runFullTrust` が必須。

### Publisher 情報
- **アプリ名:** Window Resize for Windows
- **Publisher ID:** `CN=CBBEB0B6-F2F8-4A20-93BF-7BB185208944`

### 実装状況
- [x] Partner Center 登録 & アプリ名予約
- [x] AppxManifest.xml 作成 — `WindowResize/Package/AppxManifest.xml`
- [x] Store 用アセット作成 — `WindowResize/Package/Assets/` (10 PNGs)
- [x] SettingsStore.cs 修正 — `IsPackaged()` で自動起動方式を分岐
- [x] csproj 修正 — TFM を `net8.0-windows10.0.17763.0` に変更
- [x] GitHub Actions — `.github/workflows/msix.yml` (タグ `v*` でトリガー)
- [ ] Store 提出 — MSIX アップロード → 審査

### MSIX ビルド方法
```bash
# 1. Publish
dotnet publish WindowResize/WindowResize.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=false

# 2. Prepare layout (copy publish → msix_layout, move Package/* to root)
# 3. MakeAppx
makeappx.exe pack /d msix_layout /p WindowsResizeCapture.msix /o

# Store 提出時は Microsoft が署名するため自己署名不要
# ローカルテスト時は自己署名 + 開発者モード有効化が必要
```

### デュアル配布アーキテクチャ
同一コードベースで EXE 直接配布と Store MSIX 配布の両方に対応:
- `SettingsStore.IsPackaged()` で実行環境を判定
- **パッケージ環境 (MSIX):** `Windows.ApplicationModel.StartupTask` API で自動起動
- **非パッケージ環境 (EXE):** Registry Run key で自動起動

### 注意事項
- MSIX 環境では Registry Run key が使えない → StartupTask で代替
- Global Mutex はパッケージ環境でも動作する（ただし名前空間が分離される可能性あり）
- Store 提出時は Microsoft が署名するため自己署名不要
- `runFullTrust` 申請理由: ウィンドウ列挙・リサイズ・スクリーンショットのための Win32 API アクセス

## 配布方法

### 1. ZIP (ポータブル)
`dist/WindowsResizeCapture-Windows-v{VERSION}.zip` — EXE + README.md + LICENSE を同梱
```bash
dotnet publish WindowResize/WindowResize.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
# ZIP に WindowsResizeCapture.exe, README.md, LICENSE を含める
```

### 2. インストーラ (Inno Setup)
`dist/WindowsResizeCapture-Setup-v{VERSION}.exe` — 9言語選択ダイアログ付き
```bash
# Inno Setup パス: C:\Users\nakanokappei\AppData\Local\Programs\Inno Setup 6\ISCC.exe
ISCC.exe installer/WindowResize.iss
```
- インストーラにも README.md, LICENSE を同梱
- `ShowLanguageDialog=yes` で言語選択

### 3. MSIX (Microsoft Store)
Store 提出時は Microsoft が署名。ローカルテストは自己署名 + 開発者モード必要。

### リリース手順
1. バージョン更新: `SplashForm.cs`, `AppxManifest.xml`, `WindowResize.iss`
2. ビルド: `dotnet publish` → ZIP作成 → `ISCC.exe` でインストーラ
3. コミット & タグ: `git tag v{VERSION}`
4. GitHub Release: `gh release create v{VERSION} dist/*.zip dist/*.exe`
   - gh.exe パス: `C:\Program Files\GitHub CLI\gh.exe`

## Conventions
- リソースキーは PascalCase: `MenuResize`, `SettingsWidth`, `AlertResizeFailedTitle`
- SettingsForm は閉じるとき Hide（破棄しない）
- メニューは SettingsChanged イベントで再構築
- メニュータグの右寄せは `ShortcutKeyDisplayString` プロパティで実現
- メニュータイトルは画面幅の1/4に制限 (`TruncateToFit`)
