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

## First-run owner authentication

On a new manager database, opening the local management UI at
`http://127.0.0.1:8213` redirects to `/setup`. The setup flow creates the single
owner account for this phase with a username and Identity-validated password;
there are no default credentials, hidden reset query parameters, fallback
passwords, or public registration.

After setup, owners sign in at `/login`. Signing out uses a POST-only logout
form that validates antiforgery data, clears the authentication cookie, and
returns the browser to `/login`. Password reset, email recovery, MFA, and
multiple-user administration are not available in this phase, so losing the
owner password requires restoring from a trusted backup or a future recovery
feature.

Authentication data is stored by ASP.NET Core Identity in the existing SQLite
manager database alongside the manager audit data. Startup continues to use the
repository's `EnsureCreated` database initialization approach so new databases
receive all current tables and existing Milestone 1 databases are not deleted or
recreated.

The security boundary remains local-only: the default listen URL is still
`http://127.0.0.1:8213`, and authentication alone is not sufficient for LAN or
WAN exposure. The dedicated owner cookie is HTTP-only, SameSite-protected, has a
finite sliding expiration, and is compatible with localhost HTTP development.
When a future HTTPS milestone enables network exposure, the cookie secure policy
must be changed to HTTPS-only before LAN use.
