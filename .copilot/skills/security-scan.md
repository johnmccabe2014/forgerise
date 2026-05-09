# Skill: security-scan

Standard sweep run by the Security agent (and any agent touching sensitive areas).

## Automated layer
<!-- TODO: wire these to actual tools used in the repo. -->
- **SAST**: e.g. Semgrep, CodeQL.
- **Dependency scan**: e.g. `npm audit`, `pip-audit`, `osv-scanner`, Dependabot.
- **Secret scan**: e.g. gitleaks, trufflehog — pre-commit and in CI.
- **IaC scan**: e.g. Checkov, tfsec.
- **Container scan**: e.g. Trivy.
- **License check** for new deps.

## Manual layer (apply by inspection)
1. **Trust boundaries** — list them; confirm validation at each.
2. **AuthN/AuthZ** — every endpoint, every action.
3. **Data flow** — where does user input go? Each sink encoded?
4. **Secrets path** — config → app; nothing checked in; rotation possible.
5. **Logging** — no tokens, passwords, PII, full payloads of sensitive ops.
6. **Error responses** — generic to the client, detailed only in server logs.
7. **Dependencies** — new ones: maintained? popular? license? install scripts?
8. **CI/CD** — least-privilege tokens, no `pull_request_target` foot-guns, pinned actions/SHAs.

## Output format
For each finding:
```
[severity] <title>
location: <file:line or component>
evidence: <snippet / scan output>
impact:   <what an attacker can do>
fix:      <concrete remediation>
```
High and critical block merge.
