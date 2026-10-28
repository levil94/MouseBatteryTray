# MouseBatteryTray launcher - runs the app inside the signed PowerShell host.
# Used on machines where Smart App Control blocks freshly compiled unsigned exe files.
param(
    [switch]$Settings
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$probeLog = Join-Path $root "launcher.log"

function Write-LauncherLog([string]$text) {
    try {
        $line = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss") + "  " + $text + [Environment]::NewLine
        [IO.File]::AppendAllText($probeLog, $line, [Text.Encoding]::UTF8)
    } catch { }
}

$srcDirs = @($root, (Join-Path $root "src")) | Where-Object { Test-Path $_ }
$sources = @()
foreach ($d in $srcDirs) {
    $sources += Get-ChildItem -Path $d -Filter *.cs -File |
        Where-Object { $_.Name -notmatch '^(AssemblyInfo)' } |
        Sort-Object Name |
        ForEach-Object { $_.FullName }
}

if ($sources.Count -eq 0) {
    Write-LauncherLog "no .cs sources found in $root"
    exit 1
}

foreach ($f in $sources) {
    if (-not (Test-Path $f)) {
        Write-LauncherLog "missing source: $f"
        exit 1
    }
}
Write-LauncherLog ("sources: " + ($sources.Count) + " files")

# 让程序把数据写在本目录，并能在本目录找到 hidapi.dll
$env:MOUSEBATTERYTRAY_DATA = $root
$env:PATH = $root + ";" + $env:PATH
[Environment]::CurrentDirectory = $root
try {
    Add-Type -Name K32 -Namespace Native -MemberDefinition '[DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern bool SetDllDirectory(string path);'
    [void][Native.K32]::SetDllDirectory($root)
} catch { }

try {
    Add-Type -Path $sources -ReferencedAssemblies @(
        "System.dll",
        "System.Drawing.dll",
        "System.Windows.Forms.dll",
        "System.Core.dll"
    ) -ErrorAction Stop
    Write-LauncherLog "compiled OK"
}
catch {
    Write-LauncherLog ("compile failed: " + $_.Exception.Message)
    exit 1
}

$appArgs = @()
if ($Settings) { $appArgs = @("--settings") }

try {
    [System.Windows.Forms.Application]::EnableVisualStyles()
    $flags = [Reflection.BindingFlags]"NonPublic,Static,Public"
    $entry = [MouseBatteryTray.Program].GetMethod("Main", $flags)
    if ($null -eq $entry) { throw "entry point not found" }
    $entry.Invoke($null, @(, [string[]]$appArgs)) | Out-Null
    Write-LauncherLog "exited normally"
}
catch {
    Write-LauncherLog ("run failed: " + $_.Exception.ToString())
    exit 1
}
