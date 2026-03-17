; Inno Setup Script for Window Resize for Windows
; Build: iscc.exe WindowResize.iss

#define MyAppName "Window Resize & Capture"
#define MyAppVersion "1.6"
#define MyAppPublisher "Kappei Nakano"
#define MyAppURL "https://github.com/Nakanokappei/window-resize-and-capture"
#define MyAppExeName "WindowsResizeCapture.exe"

[Setup]
AppId={{B7A3F2E1-9C4D-4E5F-8A6B-1D2E3F4A5B6C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=WindowsResizeCapture-Setup-v{#MyAppVersion}
SetupIconFile=..\WindowResize\Resources\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=lowest
ShowLanguageDialog=yes
CloseApplications=force

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Types]
Name: "standard"; Description: "Standard"
Name: "full"; Description: "Full (all manual languages)"
Name: "custom"; Description: "Custom"; Flags: iscustom

[Components]
Name: "app"; Description: "Window Resize & Capture"; Types: standard full custom; Flags: fixed
Name: "manual"; Description: "User Manual"; Types: standard full
Name: "manual\en"; Description: "English"; Types: standard full
Name: "manual\ar"; Description: "العربية (Arabic)"; Types: full
Name: "manual\de"; Description: "Deutsch (German)"; Types: full
Name: "manual\es"; Description: "Español (Spanish)"; Types: full
Name: "manual\fr"; Description: "Français (French)"; Types: full
Name: "manual\hi"; Description: "हिन्दी (Hindi)"; Types: full
Name: "manual\id"; Description: "Bahasa Indonesia"; Types: full
Name: "manual\it"; Description: "Italiano (Italian)"; Types: full
Name: "manual\ja"; Description: "日本語 (Japanese)"; Types: full
Name: "manual\ko"; Description: "한국어 (Korean)"; Types: full
Name: "manual\pt"; Description: "Português (Portuguese)"; Types: full
Name: "manual\ru"; Description: "Русский (Russian)"; Types: full
Name: "manual\th"; Description: "ไทย (Thai)"; Types: full
Name: "manual\vi"; Description: "Tiếng Việt (Vietnamese)"; Types: full
Name: "manual\zh_hans"; Description: "简体中文 (Simplified Chinese)"; Types: full
Name: "manual\zh_hant"; Description: "繁體中文 (Traditional Chinese)"; Types: full

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "launchatlogin"; Description: "Start with Windows"; GroupDescription: "Other:"

[Files]
Source: "..\WindowResize\bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\WindowsResizeCapture.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
; User manuals
Source: "..\docs\manual-en.md"; DestDir: "{app}\docs"; Components: manual\en; Flags: ignoreversion
Source: "..\docs\manual-ar.md"; DestDir: "{app}\docs"; Components: manual\ar; Flags: ignoreversion
Source: "..\docs\manual-de.md"; DestDir: "{app}\docs"; Components: manual\de; Flags: ignoreversion
Source: "..\docs\manual-es.md"; DestDir: "{app}\docs"; Components: manual\es; Flags: ignoreversion
Source: "..\docs\manual-fr.md"; DestDir: "{app}\docs"; Components: manual\fr; Flags: ignoreversion
Source: "..\docs\manual-hi.md"; DestDir: "{app}\docs"; Components: manual\hi; Flags: ignoreversion
Source: "..\docs\manual-id.md"; DestDir: "{app}\docs"; Components: manual\id; Flags: ignoreversion
Source: "..\docs\manual-it.md"; DestDir: "{app}\docs"; Components: manual\it; Flags: ignoreversion
Source: "..\docs\manual-ja.md"; DestDir: "{app}\docs"; Components: manual\ja; Flags: ignoreversion
Source: "..\docs\manual-ko.md"; DestDir: "{app}\docs"; Components: manual\ko; Flags: ignoreversion
Source: "..\docs\manual-pt.md"; DestDir: "{app}\docs"; Components: manual\pt; Flags: ignoreversion
Source: "..\docs\manual-ru.md"; DestDir: "{app}\docs"; Components: manual\ru; Flags: ignoreversion
Source: "..\docs\manual-th.md"; DestDir: "{app}\docs"; Components: manual\th; Flags: ignoreversion
Source: "..\docs\manual-vi.md"; DestDir: "{app}\docs"; Components: manual\vi; Flags: ignoreversion
Source: "..\docs\manual-zh-Hans.md"; DestDir: "{app}\docs"; Components: manual\zh_hans; Flags: ignoreversion
Source: "..\docs\manual-zh-Hant.md"; DestDir: "{app}\docs"; Components: manual\zh_hant; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WindowsResizeCapture"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: launchatlogin

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  ManualAutoSelected: Boolean;

procedure CurPageChanged(CurPageID: Integer);
var
  ActiveLang, SearchStr: String;
  I: Integer;
begin
  if (CurPageID = wpSelectComponents) and not ManualAutoSelected then
  begin
    ManualAutoSelected := True;
    ActiveLang := ActiveLanguage;
    SearchStr := '';

    if ActiveLang = 'japanese' then SearchStr := 'Japanese'
    else if ActiveLang = 'german' then SearchStr := 'German'
    else if ActiveLang = 'french' then SearchStr := 'French'
    else if ActiveLang = 'spanish' then SearchStr := 'Spanish'
    else if ActiveLang = 'italian' then SearchStr := 'Italian'
    else if ActiveLang = 'portuguese' then SearchStr := 'Portuguese'
    else if ActiveLang = 'russian' then SearchStr := 'Russian'
    else if ActiveLang = 'korean' then SearchStr := 'Korean';

    if SearchStr <> '' then
    begin
      for I := 0 to WizardForm.ComponentsList.Items.Count - 1 do
      begin
        if Pos(SearchStr, WizardForm.ComponentsList.ItemCaption[I]) > 0 then
        begin
          WizardForm.ComponentsList.Checked[I] := True;
          Break;
        end;
      end;
    end;
  end;
end;
