[CmdletBinding()]
param(
    [scriptblock]$DotnetCommand = {
        param([string[]]$Arguments)

        & dotnet @Arguments | Out-Host
        $exitCode = $LASTEXITCODE
        return $exitCode
    },

    [scriptblock]$DotnetVersionCommand = {
        & dotnet --version
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
    $exitCodeResult = @(& $DotnetCommand $Arguments)

    if ($exitCodeResult.Count -ne 1 -or $exitCodeResult[0] -isnot [int]) {
        throw "Dotnet command adapter for 'dotnet $($Arguments -join ' ')' must return exactly one integer exit code."
    }

    $exitCode = $exitCodeResult[0]
    if ($exitCode -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $exitCode."
    }
}

$versionResult = @(& $DotnetVersionCommand)
if ($versionResult.Count -lt 2 -or $versionResult[-1] -isnot [int]) {
    throw "Dotnet version adapter must return version output followed by one integer exit code."
}

$versionExitCode = $versionResult[-1]
if ($versionExitCode -ne 0) {
    throw ".NET SDK was not found. Install the .NET 10 SDK."
}

$versionText = [string]$versionResult[0]
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

$previousAspNetEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT

try {
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:DOTNET_ENVIRONMENT = "Development"

    Invoke-CheckedDotnetCommand `
        -Description "Running..." `
        -Arguments @(
            "run",
            "--project",
            ".\src\PalworldServerManager.Web\PalworldServerManager.Web.csproj",
            "--no-build")
}
finally {
    if ($null -eq $previousAspNetEnvironment) {
        Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
    }
    else {
        $env:ASPNETCORE_ENVIRONMENT = $previousAspNetEnvironment
    }

    if ($null -eq $previousDotnetEnvironment) {
        Remove-Item Env:DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue
    }
    else {
        $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
    }
}
