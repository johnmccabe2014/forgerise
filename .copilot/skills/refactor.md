# Skill: refactor

Refactor = change structure without changing behaviour. If behaviour changes, it's a feature, not a refactor.

## Pre-conditions
- Tests covering the area exist and are green. If not, write characterisation tests **first**.
- A clear, named smell to address (duplication, long function, primitive obsession, feature envy, etc.).

## Procedure
1. Commit the green baseline.
2. Apply one mechanical refactoring (extract, rename, inline, move).
3. Run tests. Green? Commit. Red? Revert.
4. Repeat.

## Stop conditions
- Smell removed.
- Diminishing returns.
- Scope creeping into behaviour change → stop, open a separate plan.

## Anti-patterns
- "While I'm here" rewrites.
- Refactor + feature in one PR.
- Refactor without tests.
