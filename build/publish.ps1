$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:NUGET_PACKAGES = "C:\Users\winte\.nuget\packages"

$proj = Join-Path $root "src\ScreenAudioRouter"
$objAssets = Join-Path $proj "obj\project.assets.json"
if (-not (Test-Path $objAssets)) {
    & (Join-Path $PSScriptRoot "Write-OfflineAssets.ps1") -ProjectDir $proj -ProjectName "ScreenAudioRouter" -Tfm "net8.0-windows" -WindowsDesktop
}

# Ensure assets JSON uses correct TFM key
if (Test-Path $objAssets) {
    $raw = Get-Content $objAssets -Raw
    $raw = $raw -replace 'net8\.0-windows7\.0', 'net8.0-windows'
    Set-Content $objAssets -Value $raw -Encoding UTF8
}

$out = Join-Path $proj "bin\publish\win-x64"
Write-Host "Publishing self-contained → $out"
Push-Location $root
try {
    dotnet publish "$proj\ScreenAudioRouter.csproj" -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $out --no-restore -v m
    if ($LASTEXITCODE -ne 0) {
        Write-Host "publish with --no-restore failed, trying build first..." -ForegroundColor Yellow
        dotnet build "$proj\ScreenAudioRouter.csproj" -c Release --no-restore -v q
        dotnet publish "$proj\ScreenAudioRouter.csproj" -c Release -r win-x64 --self-contained true -o $out --no-restore -v m
        if ($LASTEXITCODE -ne 0) { throw "publish failed" }
    }
    Get-ChildItem $out | Select-Object Name, Length | Format-Table -AutoSize | Out-String | Write-Host
    Write-Output "PUBLISH_OK $out"
}
finally { Pop-Location }
