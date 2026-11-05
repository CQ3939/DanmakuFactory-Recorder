param([string]$OutputDir="release")
$ErrorActionPreference="Stop"
Set-Location (Join-Path $PSScriptRoot "../..")
New-Item -ItemType Directory -Force $OutputDir | Out-Null
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /platform:x64 /target:winexe "/out:$OutputDir\弹幕转换.exe" /win32icon:gui\VBGUI\DanmakuFactory\bitbug_favicon.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Xml.Linq.dll gui\RecorderWindows\AssemblyInfo.cs gui\RecorderWindows\Window.cs gui\RecorderWindows\ConvertFile.cs gui\RecorderWindows\SettingsDialog.cs
exit $LASTEXITCODE




