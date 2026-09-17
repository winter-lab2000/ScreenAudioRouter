# Build a clean release zip for GitHub Releases
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:NUGET_PACKAGES = "C:\Users\winte\.nuget\packages"

$proj = Join-Path $root "src\ScreenAudioRouter"
$publish = Join-Path $proj "bin\publish\win-x64"
$dist = Join-Path $root "dist"
$version = "0.1.0"

# Offline restore assets with both TFM and RID keys
$obj = Join-Path $proj "obj"
New-Item -ItemType Directory -Force -Path $obj | Out-Null
Set-Content -Path (Join-Path $obj "project.assets.json") -Value @'
{
  "version": 3,
  "targets": {
    "net8.0-windows": {},
    "net8.0-windows/win-x64": {}
  },
  "libraries": {},
  "projectFileDependencyGroups": {
    "net8.0-windows": [],
    "net8.0-windows/win-x64": []
  },
  "project": {
    "version": "1.0.0",
    "restore": {
      "projectUniqueName": "C:/Users/winte/Desktop/mimo/ScreenAudioRouter/src/ScreenAudioRouter/ScreenAudioRouter.csproj",
      "projectName": "ScreenAudioRouter",
      "projectPath": "C:/Users/winte/Desktop/mimo/ScreenAudioRouter/src/ScreenAudioRouter/ScreenAudioRouter.csproj",
      "packagesPath": "C:/Users/winte/.nuget/packages/",
      "outputPath": "C:/Users/winte/Desktop/mimo/ScreenAudioRouter/src/ScreenAudioRouter/obj/",
      "projectStyle": "PackageReference",
      "configFilePaths": ["C:/Users/winte/Desktop/mimo/ScreenAudioRouter/nuget.config"],
      "originalTargetFrameworks": ["net8.0-windows"],
      "sources": {},
      "frameworks": {
        "net8.0-windows": {
          "targetAlias": "net8.0-windows",
          "projectReferences": {},
          "rids": ["win-x64"]
        }
      }
    },
    "frameworks": {
      "net8.0-windows": {
        "targetAlias": "net8.0-windows",
        "imports": [],
        "assetTargetFallback": true,
        "warn": true,
        "frameworkReferences": {
          "Microsoft.NETCore.App": { "privateAssets": "all" },
          "Microsoft.WindowsDesktop.App": { "privateAssets": "all" }
        }
      }
    }
  }
}
'@ -Encoding UTF8
Set-Content -Path (Join-Path $obj "ScreenAudioRouter.csproj.nuget.g.props") -Value @'
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <NuGetPackageRoot>C:/Users/winte/.nuget/packages/</NuGetPackageRoot>
    <NuGetPackageFolders>C:/Users/winte/.nuget/packages</NuGetPackageFolders>
    <NuGetProjectStyle>PackageReference</NuGetProjectStyle>
  </PropertyGroup>
</Project>
'@ -Encoding UTF8
Set-Content -Path (Join-Path $obj "ScreenAudioRouter.csproj.nuget.g.targets") -Value @'
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003"></Project>
'@ -Encoding UTF8

# Icons
$ico = Join-Path $proj "Assets\ScreenAudioRouter.ico"
if (-not (Test-Path $ico)) {
    $py = if ($env:MIMO_PYTHON) { $env:MIMO_PYTHON } else { "python" }
    try { & $py (Join-Path $PSScriptRoot "make_icons.py") } catch { }
}

Push-Location $root
try {
    Write-Host "Build Release..."
    dotnet build (Join-Path $proj "ScreenAudioRouter.csproj") -c Release --no-restore -v q
    $publishOk = $false
    if ($LASTEXITCODE -eq 0) {
        Write-Host "dotnet publish (portable)..."
        dotnet publish (Join-Path $proj "ScreenAudioRouter.csproj") -c Release --self-contained false -o $publish --no-restore -v q
        if ($LASTEXITCODE -eq 0 -and (Test-Path (Join-Path $publish "ScreenAudioRouter.exe"))) {
            $publishOk = $true
        }
    }
    if (-not $publishOk) {
        Write-Host "Using Debug bin as release payload" -ForegroundColor Yellow
        $dbg = Join-Path $proj "bin\Debug\net8.0-windows"
        if (-not (Test-Path (Join-Path $dbg "ScreenAudioRouter.exe"))) {
            dotnet build (Join-Path $proj "ScreenAudioRouter.csproj") -c Debug --no-restore -v q
        }
        if (Test-Path $publish) { Remove-Item $publish -Recurse -Force -ErrorAction SilentlyContinue }
        New-Item -ItemType Directory -Force -Path $publish | Out-Null
        Copy-Item (Join-Path $dbg "*") $publish -Recurse -Force
    }
}
finally { Pop-Location }

if (-not (Test-Path (Join-Path $publish "ScreenAudioRouter.exe"))) {
    throw "No ScreenAudioRouter.exe to package"
}

$stage = Join-Path $dist "stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item (Join-Path $publish "*") $stage -Recurse -Force
Copy-Item (Join-Path $root "installer\install.ps1") $stage -Force
Copy-Item (Join-Path $root "README.md") $stage -Force
Copy-Item (Join-Path $root "LICENSE") $stage -Force

Get-ChildItem $stage -Recurse -Include *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

$zip = Join-Path $dist "ScreenAudioRouter-v$version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
Write-Output "RELEASE_ZIP=$zip"
Get-Item $zip | Format-List FullName, Length
