# AGENTS.md

## Mission

Build a free, open-source, security-conscious Palworld dedicated-server
management platform that can replace the routine control-panel features sold by
commercial hosting providers.

## Repository workflow

- Never commit directly to `main`.
- Use one narrowly scoped branch and pull request per change.
- Draft pull requests are preferred until all acceptance criteria pass.
- Do not combine unrelated refactors, dependency updates, and feature work.
- Describe the root cause, security impact, validation, and rollback concerns in
  every pull request.
- Do not suppress compiler warnings, NuGet vulnerability warnings, or failing
  checks merely to make CI green.

## Required validation

Run these commands from the repository root:

```powershell
dotnet restore .\PalworldServerManager.slnx
dotnet list .\PalworldServerManager.slnx package --vulnerable --include-transitive --no-restore
dotnet build .\PalworldServerManager.slnx -c Release --no-restore
```

Add and run focused tests for any behavior introduced or changed.

## Architectural boundaries

- `PalworldServerManager.Core` contains contracts, domain models, and business
  rules without Windows, database, web, or process-specific implementations.
- `PalworldServerManager.Infrastructure` implements persistence, Windows process
  control, SteamCMD, firewall, filesystem, and Palworld adapter concerns.
- `PalworldServerManager.Web` contains the ASP.NET Core host, local manager API,
  authentication boundary, and Blazor UI.
- Keep service operations behind interfaces. Razor components must not directly
  launch processes, edit INI files, call Windows Firewall, or handle secrets.

## Security invariants

- The unfinished manager must remain bound to `127.0.0.1`.
- Never expose Palworld's native REST API or RCON directly to the WAN.
- LAN or WAN exposure requires authentication, HTTPS, explicit warnings, and
  narrowly scoped firewall rules.
- Never write server passwords, admin passwords, API keys, session tokens, or
  decrypted secrets to logs, audit records, exceptions, JSON exports, or test
  fixtures.
- Destructive actions require explicit UI language and audit entries.
- Force-stop is an emergency action. Normal shutdown must save the world and use
  Palworld's graceful-shutdown operation.
- Never weaken path validation or accept arbitrary executable paths from an
  unauthenticated request.
- Do not introduce telemetry, cloud dependencies, advertisements, or paid
  feature gates.

## Product conventions

- Default managed server location: `C:\Servers\Palworld`.
- Default development panel: `http://127.0.0.1:8213`.
- Windows Server 2025 and Windows 11 are primary supported hosts.
- The web interface must remain usable from desktop and mobile browsers.
- Prefer readable administrative output over raw JSON, but retain expandable raw
  details for diagnostics.
- Every manager-created firewall, service, backup, or configuration change must
  be attributable and reversible.

## Dependency policy

- Centralize NuGet versions in `Directory.Packages.props`.
- Prefer stable servicing releases compatible with the selected .NET version.
- Do not ignore a vulnerability advisory without documenting why it is
  non-exploitable and receiving explicit maintainer approval.
- Keep `TreatWarningsAsErrors` enabled.

## Pull request size

Prefer fewer than 500 changed lines per pull request unless the change is a
generated migration, initial scaffold, or isolated UI asset. Split large
features into backend contract, implementation, UI, and tests where practical.