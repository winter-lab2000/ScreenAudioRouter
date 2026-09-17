<#
.SYNOPSIS
  Install Screen Audio Router to a user-chosen directory.

.EXAMPLE
  .\install.ps1
  .\install.ps1 -InstallDir "D:\Tools\ScreenAudioRouter" -DesktopShortcut -StartApp
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\ScreenAudioRouter",
    [string]$PublishDir = "",
    [switch]$DesktopShortcut,
    [switch]$StartupShortcut,
    [switch]$StartApp,
    [switch]$Silent
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if (-not $PublishDir) {
    $PublishDir = Join-Path $root "src\ScreenAudioRouter\bin\publish\win-x64"
}

if (-not (Test-Path $PublishDir)) {
    throw "Publish folder not found: $PublishDir`n先运行: powershell -File build\publish.ps1"
}

if (-not $Silent -and -not $PSBoundParameters.ContainsKey("InstallDir")) {
    Write-Host "默认安装目录: $InstallDir" -ForegroundColor Cyan
    $answer = Read-Host "输入安装路径（回车使用默认）"
    if ($answer) { $InstallDir = $answer }
}

# Normalize
$InstallDir = [System.IO.Path]::GetFullPath($InstallDir)
Write-Host "安装到: $InstallDir" -ForegroundColor Green

# Stop running instance
Get-Process ScreenAudioRouter -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Path (Join-Path $PublishDir "*") -Destination $InstallDir -Recurse -Force

$exe = Join-Path $InstallDir "ScreenAudioRouter.exe"
if (-not (Test-Path $exe)) { throw "安装失败：未找到 $exe" }

# Start menu
$sm = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\ScreenAudioRouter"
New-Item -ItemType Directory -Force -Path $sm | Out-Null
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut((Join-Path $sm "Screen Audio Router.lnk"))
$sc.TargetPath = $exe
$sc.WorkingDirectory = $InstallDir
$sc.IconLocation = "$exe,0"
$sc.Save()

if ($DesktopShortcut) {
    $desktop = [Environment]::GetFolderPath("Desktop")
    $dsc = $ws.CreateShortcut((Join-Path $desktop "Screen Audio Router.lnk"))
    $dsc.TargetPath = $exe
    $dsc.WorkingDirectory = $InstallDir
    $dsc.IconLocation = "$exe,0"
    $dsc.Save()
}

if ($StartupShortcut) {
    $startup = [Environment]::GetFolderPath("Startup")
    $ssc = $ws.CreateShortcut((Join-Path $startup "Screen Audio Router.lnk"))
    $ssc.TargetPath = $exe
    $ssc.WorkingDirectory = $InstallDir
    $ssc.Save()
}

# Uninstall helper
$un = @"
@echo off
echo Uninstalling Screen Audio Router...
taskkill /IM ScreenAudioRouter.exe /F >nul 2>&1
rd /s /q "$InstallDir"
del "$sm\Screen Audio Router.lnk" >nul 2>&1
rd /s /q "$sm" >nul 2>&1
if exist "%USERPROFILE%\Desktop\Screen Audio Router.lnk" del "%USERPROFILE%\Desktop\Screen Audio Router.lnk"
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Screen Audio Router.lnk" >nul 2>&1
echo Done.
pause
"@
Set-Content -Path (Join-Path $InstallDir "uninstall.bat") -Value $un -Encoding ASCII

Write-Host ""
Write-Host "安装完成" -ForegroundColor Green
Write-Host "  程序: $exe"
Write-Host "  卸载: $(Join-Path $InstallDir 'uninstall.bat')"
Write-Host "  配置: $env:APPDATA\ScreenAudioRouter"

if ($StartApp) { Start-Process $exe }
