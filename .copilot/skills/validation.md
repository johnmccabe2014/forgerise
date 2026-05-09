# Skill: validation

How to prove a change works before claiming it does.

## Layers (run in order; stop at first failure and fix)
1. **Static**: type-check, lint, format.
2. **Build**: compiles / bundles cleanly with no new warnings.
3. **Unit & integration tests**: green, with coverage delta non-negative.
4. **Contract checks**: API schemas, DB migrations dry-run.
5. **E2E / smoke**: critical paths only.
6. **Security scan** (skill: `security-scan`): no new high/critical.
7. **Manual sanity check** when UI / behaviour visibly changes (screenshot / curl / repro of the original bug).

## Evidence requirements
Every claim of "works" must include:
- Command(s) run.
- Exit codes / pass counts.
- Coverage delta.
- Anything skipped + why.

If you cannot run a step, say so explicitly. Do not infer success.

## Red flags that block validation
- "All tests pass" without naming the suite.
- Coverage drops on changed files.
- New `// TODO`, `// FIXME`, `eslint-disable`, `# type: ignore` without justification.
- Silenced or skipped tests.
- New warnings ignored.
