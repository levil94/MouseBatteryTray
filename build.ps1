# 编译 MouseBatteryTray（不依赖 Visual Studio，用系统自带 csc.exe）
$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) { throw "找不到 csc.exe：$csc" }

$dll = Join-Path $dir "hidapi.dll"
if (-not (Test-Path $dll)) {
    Write-Host "hidapi.dll 不存在，正在下载官方预编译版本…"
    $zip = Join-Path $env:TEMP "hidapi-win.zip"
    Invoke-WebRequest -Uri "https://github.com/libusb/hidapi/releases/latest/download/hidapi-win.zip" `
        -OutFile $zip -UseBasicParsing
    $tmp = Join-Path $env:TEMP "hidapi-win-extract"
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $tmp -Force
    $arch = if ([Environment]::Is64BitOperatingSystem) { "x64" } else { "x86" }
    Copy-Item (Join-Path $tmp "$arch\hidapi.dll") $dll -Force
    Write-Host "hidapi.dll 下载完成（$arch）"
}

$out = Join-Path $dir "MouseBatteryTray.exe"
$sources = Get-ChildItem $dir -Filter *.cs | ForEach-Object { $_.FullName }
& $csc /nologo /target:winexe /optimize+ /out:$out `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    $sources

if (Test-Path $out) {
    "编译成功: $out  ({0:N0} 字节)" -f (Get-Item $out).Length
} else {
    throw "编译失败"
}
