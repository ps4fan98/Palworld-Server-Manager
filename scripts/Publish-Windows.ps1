[CmdletBinding()]
param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$publishDirectory = Join-Path $root "artifacts\publish\$Runtime"

if (Test-Path $publishDirectory) {
    Remove-Item $publishDirectory -Recurse -Force
}

dotnet publish `
    ".\src\PalworldServerManager.Web\PalworldServerManager.Web.csproj" `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDirectory

Write-Host ""
Write-Host "Published to: $publishDirectory" -ForegroundColor Green
