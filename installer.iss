; MouseBatteryTray installer script (Inno Setup 6)
; Build: ISCC.exe /DAppVersion=0.7.0 installer.iss

#define AppName "MouseBatteryTray"
#define AppPublisher "levil94"
#define AppUrl "https://github.com/levil94/MouseBatteryTray"

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{8C3F5B7A-5D2E-4A3B-9C1D-2E7F4A6B8C90}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename=MouseBatteryTray-{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\MouseBatteryTray.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "autostart"; Description: "Start automatically when Windows starts"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "MouseBatteryTray.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "MouseBatteryTray.vbs"; DestDir: "{app}"; Flags: ignoreversion
Source: "Start-MouseBatteryTray.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "hidapi.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "*.cs"; DestDir: "{app}\src"; Flags: ignoreversion
Source: "docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\MouseBatteryTray.vbs"; IconFilename: "{app}\MouseBatteryTray.exe"; Comment: "Show mouse battery in the system tray"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\MouseBatteryTray.vbs"; IconFilename: "{app}\MouseBatteryTray.exe"; Tasks: desktopicon

[Registry]
; Autostart via the signed-host launcher (works even with Smart App Control enforced)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MouseBatteryTray"; ValueData: """{app}\MouseBatteryTray.vbs"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\MouseBatteryTray.vbs"; Description: "Launch {#AppName} now"; Flags: postinstall nowait shellexec skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\src"
