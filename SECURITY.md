# Security Policy

## Supported versions

The project is pre-release. Security fixes target the latest `main` branch until
the first stable release establishes a formal support window.

## Reporting a vulnerability

Do not publish exploitable security details in a public issue.

Use GitHub private vulnerability reporting when enabled. Until then, contact the
repository owner privately through the GitHub profile associated with this
repository.

Include:

- Affected commit or version
- Threat model and required access
- Reproduction steps
- Potential impact
- Suggested mitigation, when known

Do not include live credentials, world saves, player IP addresses, or tokens.

## High-risk areas

Security reports are especially important for:

- Authentication or authorization bypass
- Secret disclosure
- Arbitrary process execution
- Path traversal
- Unsafe archive extraction
- Firewall misconfiguration
- REST API or RCON exposure
- Cross-site request forgery
- Server-side request forgery
- Backup tampering
- Privilege escalation through the Windows service