$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src\ScreenAudioRouter"
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:NUGET_PACKAGES = "C:\Users\winte\.nuget\packages"

& (Join-Path $PSScriptRoot "Write-OfflineAssets.ps1") -ProjectDir $proj -ProjectName "ScreenAudioRouter" -Tfm "net8.0-windows7.0" -WindowsDesktop

Push-Location $root
try {
    dotnet build src\ScreenAudioRouter\ScreenAudioRouter.csproj -c Debug --no-restore -v m
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
    $exe = Join-Path $proj "bin\Debug\net8.0-windows\ScreenAudioRouter.exe"
    if (Test-Path $exe) { Write-Output "BUILD_OK $exe" } else { Get-ChildItem (Join-Path $proj "bin") -Recurse -Filter *.exe | Select-Object FullName }
}
finally {
    Pop-Location
}
