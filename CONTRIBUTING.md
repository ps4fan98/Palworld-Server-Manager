# Contributing

Palworld Server Manager uses a pull-request-only workflow.

## Development flow

1. Synchronize `main`.
2. Create a focused branch:
   `git switch -c feature/short-description`
3. Make one coherent change.
4. Run restore, vulnerability audit, build, and focused tests.
5. Push the branch and open a draft pull request.
6. Resolve all review conversations and CI failures.
7. Squash merge after validation.

Do not push directly to `main`, force-push shared branches, commit secrets, or
disable security checks to merge a change.

## Local validation

```powershell
dotnet restore .\PalworldServerManager.slnx
dotnet list .\PalworldServerManager.slnx package --vulnerable --include-transitive --no-restore
dotnet build .\PalworldServerManager.slnx -c Release --no-restore
```

For interactive testing:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Run-Development.ps1
```

## Reporting bugs

Include:

- Windows version
- .NET SDK version
- Palworld dedicated-server version
- Reproduction steps
- Expected and actual behavior
- Sanitized logs
- Whether the server was imported or manager-installed

Never post server passwords, administrator passwords, tokens, public IP
addresses, world saves, or personally identifiable player data.