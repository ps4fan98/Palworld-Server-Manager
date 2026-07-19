[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$currentIdentity =
    [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()

if (-not $currentIdentity.IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an Administrator PowerShell window."
}

$serviceName = "PalworldServerManager"
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

if (-not $service) {
    Write-Host "The service is not installed." -ForegroundColor Yellow
    exit 0
}

if ($service.Status -ne "Stopped") {
    Stop-Service -Name $serviceName -Force
}

sc.exe delete $serviceName | Out-Null

Write-Host "Service removed. ProgramData and published files were preserved." -ForegroundColor Green
