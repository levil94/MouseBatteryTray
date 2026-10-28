; MouseBatteryTray installer (Inno Setup 6)
; Build: ISCC.exe /DAppVersion=0.7.0 installer.iss
; 中文界面通过下方 [Messages] 覆盖实现，允许用户自选安装目录

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
DisableDirPage=no
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

[Messages]
SetupAppTitle=安装向导
SetupWindowTitle=%1 安装向导
WelcomeLabel1=欢迎安装 [name]
WelcomeLabel2=即将在你的电脑上安装 [name/ver]。%n%n本程序会在任务栏托盘显示罗技无线鼠标电量，支持明暗双主题、历史曲线与低电量提醒。%n%n点击“下一步”继续。
WizardSelectDir=选择安装位置
SelectDirDesc=程序将安装到下面的文件夹
SelectDirLabel3=安装程序会将 [name] 安装到以下文件夹。
SelectDirBrowseLabel=如需安装到其他位置，点击“浏览”选择。
DiskSpaceMBLabel=至少需要 [mb] MB 可用磁盘空间。
SelectComponentsDesc=选择需要安装的组件
SelectComponentsLabel2=选择要安装的组件，取消勾选不需要的部分。
SelectTasksDesc=选择附加任务
SelectTasksLabel2=安装 [name] 时需要执行哪些附加任务？
SelectProgramGroupDesc=选择开始菜单文件夹
ReadyLabel1=安装程序已准备好，可以开始安装 [name]。
ReadyLabel2a=点击“安装”开始安装；如需检查或更改设置，点击“上一步”。
ReadyLabel2b=点击“安装”开始安装。
PreparingDesc=安装程序正在准备安装 [name]。
InstallingLabel=正在安装 [name]，请稍候……
FinishedHeadingLabel=[name] 安装完成
FinishedLabel=安装完成。可通过开始菜单中的快捷方式启动 [name]。
FinishedLabelNoIcons=安装完成。
ClickFinish=点击“完成”退出安装程序。
ButtonBack=< 上一步(&B)
ButtonNext=下一步(&N) >
ButtonInstall=安装(&I)
ButtonCancel=取消
ButtonFinish=完成(&F)
ButtonBrowse=浏览(&B)…
ButtonYes=是(&Y)
ButtonNo=否(&N)
ButtonOK=确定
ConfirmUninstall=确定要卸载 %1 及其所有组件吗？
UninstalledAll=%1 已成功卸载。
UninstalledMost=%1 卸载完成。%n%n部分文件未能删除，可手动清理。
UninstallAppFullTitle=卸载 %1
StatusUninstalling=正在卸载 %1……
LanguageDialogOK=确定
LanguageDialogCancel=取消

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: unchecked
Name: "autostart"; Description: "开机时自动启动"; GroupDescription: "启动选项："; Flags: unchecked

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
Name: "{group}\{#AppName}"; Filename: "{app}\MouseBatteryTray.vbs"; IconFilename: "{app}\MouseBatteryTray.exe"; Comment: "在托盘显示鼠标电量"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\MouseBatteryTray.vbs"; IconFilename: "{app}\MouseBatteryTray.exe"; Tasks: desktopicon

[Registry]
; 通过宿主启动器设置自启，兼容 Smart App Control 强制模式
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MouseBatteryTray"; ValueData: """{app}\MouseBatteryTray.vbs"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\MouseBatteryTray.vbs"; Description: "立即启动 {#AppName}"; Flags: postinstall nowait shellexec skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\src"