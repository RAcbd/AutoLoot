# Creates the Marketplace release zip from DLLs already in this folder.
# Run from monorepo after build, or use ..\..\..\scripts\Release-OriathPlugin.ps1 for full build + zip.

param(
    [Parameter(Mandatory)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$Plugin = 'AutoLoot'
$deployDir = Split-Path $PSScriptRoot -Parent
$zipPath = Join-Path $deployDir "$Plugin-$Version.zip"
$staging = Join-Path $env:TEMP "oriath-release-$Plugin-$Version"

foreach ($file in @("$Plugin.dll", 'OriathPlugins.Common.dll', 'config\settings.json.example', 'data\currency-names.json')) {
    if (-not (Test-Path (Join-Path $deployDir $file))) {
        throw "Missing required file: $file"
    }
}

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
$pluginStage = Join-Path $staging $Plugin
New-Item -ItemType Directory -Force -Path (Join-Path $pluginStage 'config'), (Join-Path $pluginStage 'data') | Out-Null
Copy-Item (Join-Path $deployDir "$Plugin.dll") $pluginStage
Copy-Item (Join-Path $deployDir 'OriathPlugins.Common.dll') $pluginStage
Copy-Item (Join-Path $deployDir 'config\settings.json.example') (Join-Path $pluginStage 'config\')
Copy-Item (Join-Path $deployDir 'data\currency-names.json') (Join-Path $pluginStage 'data\')

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $pluginStage -DestinationPath $zipPath
Remove-Item $staging -Recurse -Force

Write-Host "Created $zipPath"
tar -tf $zipPath
