[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$versionText = (& dotnet --version)
if ($LASTEXITCODE -ne 0) {
    throw ".NET SDK was not found. Install the .NET 10 SDK."
}

$major = [int]($versionText.Split(".")[0])
if ($major -lt 10) {
    throw "The project requires .NET 10 or later. Detected: $versionText"
}

Write-Host "Restoring packages..." -ForegroundColor Cyan
dotnet restore ".\PalworldServerManager.slnx"

Write-Host "Building..." -ForegroundColor Cyan
dotnet build ".\PalworldServerManager.slnx" -c Debug --no-restore

Write-Host ""
Write-Host "Starting Palworld Server Manager..." -ForegroundColor Green
Write-Host "Open http://127.0.0.1:8213" -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop the manager." -ForegroundColor DarkGray
Write-Host ""

dotnet run `
    --project ".\src\PalworldServerManager.Web\PalworldServerManager.Web.csproj" `
    --no-build
