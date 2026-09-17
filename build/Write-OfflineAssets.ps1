# Offline restore shim for environments where GetRestoreSettingsTask crashes.
param(
    [Parameter(Mandatory = $true)][string]$ProjectDir,
    [Parameter(Mandatory = $true)][string]$ProjectName,
    [Parameter(Mandatory = $true)][string]$Tfm,
    [string]$PackagesPath = "C:\Users\winte\.nuget\packages",
    [switch]$WindowsDesktop
)

$ErrorActionPreference = "Stop"
$obj = Join-Path $ProjectDir "obj"
New-Item -ItemType Directory -Force -Path $obj | Out-Null

$projectPath = (Resolve-Path (Join-Path $ProjectDir "$ProjectName.csproj")).Path.Replace("\\", "/")
$objPath = $obj.Replace("\\", "/")
$pkgs = $PackagesPath.Replace("\\", "/")
$config = (Join-Path $ProjectDir "..\..\nuget.config")
if (-not (Test-Path $config)) { $config = "C:/Users/winte/.nuget/NuGet/NuGet.Config" }
$config = (Resolve-Path $config).Path.Replace("\\", "/")

$fwRefs = @(
  "Microsoft.NETCore.App"
)
if ($WindowsDesktop) { $fwRefs += "Microsoft.WindowsDesktop.App" }

$fwRefJson = ($fwRefs | ForEach-Object { "        `"$_`": {`n          `"privateAssets`": `"all`"`n        }" }) -join ",`n"

$assets = @"
{
  "version": 3,
  "targets": {
    "$Tfm": {}
  },
  "libraries": {},
  "projectFileDependencyGroups": {
    "$Tfm": []
  },
  "project": {
    "version": "1.0.0",
    "restore": {
      "projectUniqueName": "$projectPath",
      "projectName": "$ProjectName",
      "projectPath": "$projectPath",
      "packagesPath": "$pkgs/",
      "outputPath": "$objPath/",
      "projectStyle": "PackageReference",
      "configFilePaths": [
        "$config"
      ],
      "originalTargetFrameworks": [
        "$Tfm"
      ],
      "sources": {},
      "frameworks": {
        "$Tfm": {
          "targetAlias": "$Tfm",
          "projectReferences": {}
        }
      }
    },
    "frameworks": {
      "$Tfm": {
        "targetAlias": "$Tfm",
        "imports": [],
        "assetTargetFallback": true,
        "warn": true,
        "frameworkReferences": {
$fwRefJson
        }
      }
    }
  }
}
"@

Set-Content -Path (Join-Path $obj "project.assets.json") -Value $assets -Encoding UTF8
Set-Content -Path (Join-Path $obj "$ProjectName.csproj.nuget.g.props") -Value @"
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <NuGetPackageRoot>$($PackagesPath.Replace('\','/'))/</NuGetPackageRoot>
    <NuGetPackageFolders>$($PackagesPath.Replace('\','/'))</NuGetPackageFolders>
    <NuGetProjectStyle>PackageReference</NuGetProjectStyle>
    <NuGetToolVersion>6.11.1</NuGetToolVersion>
  </PropertyGroup>
</Project>
"@ -Encoding UTF8
Set-Content -Path (Join-Path $obj "$ProjectName.csproj.nuget.g.targets") -Value @"
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
</Project>
"@ -Encoding UTF8

Write-Output "Wrote offline restore assets for $ProjectName ($Tfm)"
