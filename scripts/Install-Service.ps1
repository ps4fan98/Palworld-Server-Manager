[CmdletBinding()]
param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$currentIdentity =
    [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()

if (-not $currentIdentity.IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an Administrator PowerShell window."
}

$serviceName = "PalworldServerManager"
$displayName = "Palworld Server Manager"
$root = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $root "artifacts\publish\$Runtime"
$executablePath =
    Join-Path $publishDirectory "PalworldServerManager.Web.exe"

if (-not (Test-Path $executablePath)) {
    throw "Published executable not found. Run Publish-Windows.ps1 first."
}

$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

if ($existingService) {
    throw "Service '$serviceName' already exists. Uninstall it before reinstalling."
}

New-Service `
    -Name $serviceName `
    -BinaryPathName "`"$executablePath`"" `
    -DisplayName $displayName `
    -Description "Local Palworld dedicated-server management service." `
    -StartupType Automatic

sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/15000/none/0 | Out-Null
sc.exe failureflag $serviceName 1 | Out-Null

Start-Service -Name $serviceName

Write-Host ""
Write-Host "Service installed and started." -ForegroundColor Green
Write-Host "Manager URL: http://127.0.0.1:8213" -ForegroundColor Yellow
