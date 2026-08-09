; Inno Setup script for MouseCursorSupporter.
; Built by GitHub Actions (see .github/workflows/release.yml), which runs:
;   dotnet publish -c Release -r win-x64 --self-contained true ^
;     -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
;   ISCC installer\installer.iss /DMyAppVersion=<version>
; Local test builds without /DMyAppVersion fall back to 0.0.0-dev below.

#define MyAppName "マウスカーソル自動切替"
#define MyAppExeName "MouseCursorSupporter.exe"
#define MyAppPublisher "Minakami1124"
#define MyAppURL "https://github.com/Minakami1124/MouseCursorSupporter"

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif

[Setup]
AppId={{7C6C6A9E-6A0B-4B9E-9B36-2B7B7B6F0E11}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
; Per-user install under %LocalAppData% so no administrator rights are required -
; this matches the app's own design (it only ever touches HKCU, never HKLM).
DefaultDirName={localappdata}\Programs\MouseCursorSupporter
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=MouseCursorSupporterSetup-{#MyAppVersion}
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにアイコンを作成する"; GroupDescription: "追加のアイコン:"; Flags: unchecked

[Files]
Source: "..\publish\MouseCursorSupporter.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Make sure the tray app isn't holding the exe open when we try to delete it.
Filename: "{cmd}"; Parameters: "/C taskkill /F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillMouseCursorSupporter"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    // Close a currently running instance before overwriting its exe (upgrade scenario).
    Exec(ExpandConstant('{cmd}'), '/C taskkill /F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // The app manages this Run key entry itself (Settings > 全般); clean it up so
    // uninstalling doesn't leave a dangling logon entry pointing at a deleted exe.
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MouseCursorSupporter');
  end;
end;
