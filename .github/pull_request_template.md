## Summary

<!-- What changed? -->

## Reason

<!-- Why is this change needed? Include the root cause for fixes. -->

## Security impact

<!-- State "None" or describe changes to trust boundaries, authentication,
secrets, networking, process execution, filesystem access, or firewall rules. -->

## Validation

- [ ] `dotnet restore .\PalworldServerManager.slnx`
- [ ] `dotnet list .\PalworldServerManager.slnx package --vulnerable --include-transitive --no-restore`
- [ ] `dotnet build .\PalworldServerManager.slnx -c Release --no-restore`
- [ ] Focused tests added or updated
- [ ] Tested on Windows when Windows-specific behavior changed

## Operational checks

- [ ] Manager remains localhost-only unless this PR explicitly implements the
      authenticated HTTPS exposure milestone
- [ ] No secrets or personal data were added to logs, fixtures, screenshots, or
      repository files
- [ ] Destructive actions remain clearly labeled and audited
- [ ] Configuration and system changes are reversible

## Screenshots

<!-- Add screenshots for visible UI changes. -->

## Rollback

<!-- Explain how to safely revert this change. -->