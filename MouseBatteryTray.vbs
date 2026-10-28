' MouseBatteryTray 静默启动器：以隐藏窗口方式在 PowerShell 宿主中运行程序
' 适用于 Smart App Control 拦截未签名 exe 的机器，无需签名、无需关闭安全策略
Option Explicit

Dim shell, fso, scriptDir, ps1, cmd
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
ps1 = fso.BuildPath(scriptDir, "Start-MouseBatteryTray.ps1")

If Not fso.FileExists(ps1) Then
    MsgBox "找不到启动脚本：" & ps1, 16, "MouseBatteryTray"
    WScript.Quit 1
End If

cmd = "powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & ps1 & """"
shell.Run cmd, 0, False
