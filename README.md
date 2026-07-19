# Palworld Server Manager

Milestone 1 establishes the safe, local-only foundation for an open-source
Palworld dedicated-server control plane.

## Current capabilities

- ASP.NET Core / Blazor management panel
- Runs interactively for development
- Can be published as a Windows service
- Binds to `127.0.0.1:8213` by default
- SQLite database initialized under ProgramData
- Detects an existing Palworld server process
- Starts `PalServer.exe`
- Provides a clearly labeled force-stop operation
- Captures output from processes launched by the manager
- Writes lifecycle operations to an audit table
- Exposes initial local REST endpoints

## Safety boundary

This milestone intentionally has no user authentication yet. For that reason,
the panel listens on localhost only. Do not change it to `0.0.0.0`, a LAN
address, or a public address until authentication and HTTPS are implemented.

The force-stop operation is not a graceful Palworld save/shutdown. Graceful
shutdown will be introduced with Palworld REST API integration in Milestone 2.

## Default paths

- Palworld installation: `C:\Servers\Palworld`
- Palworld executable: `C:\Servers\Palworld\PalServer.exe`
- Manager data:
  `C:\ProgramData\Palworld Server Manager`
- Manager URL: `http://127.0.0.1:8213`

Edit `src\PalworldServerManager.Web\appsettings.json` to change the Palworld
installation path.

## Prerequisites

- Windows 11 or Windows Server 2025
- .NET 10 SDK for development
- Administrator PowerShell only when installing the Windows service

## Run in development

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Run-Development.ps1
```

Then open:

```text
http://127.0.0.1:8213
```

## Build manually

```powershell
dotnet restore .\PalworldServerManager.slnx
dotnet build .\PalworldServerManager.slnx -c Debug
dotnet run --project .\src\PalworldServerManager.Web
```

## Publish and install as a Windows service

Run PowerShell as Administrator:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Publish-Windows.ps1
.\scripts\Install-Service.ps1
```

To remove the service:

```powershell
.\scripts\Uninstall-Service.ps1
```

## Initial local API

```text
GET  /api/v1/health
GET  /api/v1/server/status
GET  /api/v1/server/logs?take=100
POST /api/v1/server/start
POST /api/v1/server/force-stop
```

These endpoints are intentionally reachable only from the local machine while
the manager uses its default listen URL.

## Next milestone

Milestone 2 will add:

1. First-run owner account and authentication
2. Secure secret storage
3. Palworld REST API client
4. Graceful save and shutdown
5. Human-readable server metrics and player data
6. Configuration parser and revision history
