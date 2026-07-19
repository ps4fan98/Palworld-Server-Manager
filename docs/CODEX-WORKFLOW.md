# Codex pull-request workflow

## Assigning work

Give Codex one GitHub issue or one narrowly scoped outcome at a time. Require a
draft pull request and require it to run the repository validation commands.

## Reviewing a Codex pull request

```powershell
git switch main
git pull --ff-only
gh pr list
gh pr checkout <PR_NUMBER>

dotnet restore .\PalworldServerManager.slnx
dotnet list .\PalworldServerManager.slnx package --vulnerable --include-transitive --no-restore
dotnet build .\PalworldServerManager.slnx -c Release --no-restore

Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Run-Development.ps1
```

Test the feature, review the diff, and leave review comments rather than editing
the Codex branch locally unless deliberately taking over the change.

## After merge

```powershell
git switch main
git pull --ff-only
git branch --delete <LOCAL_FEATURE_BRANCH>
git remote prune origin
```

Use squash merge so each approved pull request becomes one understandable commit
on `main`.