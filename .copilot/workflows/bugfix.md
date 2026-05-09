# Workflow: bugfix

1. **Reproduce** — write a failing automated test that reproduces the bug. Do not proceed without it.
2. **Diagnose** — find the root cause, not just a symptom. Note it in the PR description.
3. **Plan the smallest fix** — Planner approves anything beyond a one-file change.
4. **Fix** — change the minimum needed to make the failing test pass.
5. **Hunt siblings** — search for the same root cause elsewhere; add tests there too if found.
6. **Validate & review** — standard `feedback-loop.md`.
7. **Postmortem if severity ≥ high** — short note in `.copilot/state/postmortems/`: what, why missed, how prevented (test, lint rule, type, alarm).
