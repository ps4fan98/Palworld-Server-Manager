[CmdletBinding()]
param(
    [scriptblock]$DotnetCommand = {
        param([string[]]$Arguments)

        & dotnet @Arguments
        return $LASTEXITCODE
    }
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Invoke-CheckedDotnetCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host $Description -ForegroundColor Cyan
    $exitCode = & $DotnetCommand $Arguments

    if ($exitCode -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $exitCode."
    }
}

$versionText = (& dotnet --version)
if ($LASTEXITCODE -ne 0) {
    throw ".NET SDK was not found. Install the .NET 10 SDK."
}

$major = [int]($versionText.Split(".")[0])
if ($major -lt 10) {
    throw "The project requires .NET 10 or later. Detected: $versionText"
}

Invoke-CheckedDotnetCommand `
    -Description "Restoring packages..." `
    -Arguments @("restore", ".\PalworldServerManager.slnx")

Invoke-CheckedDotnetCommand `
    -Description "Auditing packages for known vulnerabilities..." `
    -Arguments @(
        "list",
        ".\PalworldServerManager.slnx",
        "package",
        "--vulnerable",
        "--include-transitive",
        "--no-restore")

Invoke-CheckedDotnetCommand `
    -Description "Building..." `
    -Arguments @("build", ".\PalworldServerManager.slnx", "-c", "Debug", "--no-restore")

Write-Host ""
Write-Host "Starting Palworld Server Manager..." -ForegroundColor Green
Write-Host "Open http://127.0.0.1:8213" -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop the manager." -ForegroundColor DarkGray
Write-Host ""

Invoke-CheckedDotnetCommand `
    -Description "Running..." `
    -Arguments @(
        "run",
        "--project",
        ".\src\PalworldServerManager.Web\PalworldServerManager.Web.csproj",
        "--no-build")
