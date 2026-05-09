# Skill: testing

A reusable playbook any agent can apply when writing or evaluating tests.

## When to use
Whenever code changes behaviour, fixes a bug, or touches a critical path.

## Procedure
1. **Identify behaviours**, not functions. One test per observable behaviour.
2. **Pick the level** (unit / integration / e2e) using the test pyramid.
3. **Write the test first** when feasible (red → green → refactor).
4. **Cover edge cases**: empty, null/undefined, max/min, unicode, concurrency, partial failure, retries, timeouts.
5. **Make failures informative** — assert on values, not booleans.
6. **Run the suite** locally; ensure it passes 3× in a row (flake check) for new tests.

## Bug-fix rule
A bug fix without a regression test is rejected. Write the failing test first; watch it fail; then fix.

## Tools
<!-- TODO: list project test runner, assertion lib, mock lib, e2e tool, coverage tool. -->

## Anti-patterns to reject
- Tests that mock the unit under test.
- Tests that assert implementation details (private methods, call counts on internals).
- Snapshot tests for non-trivial structures without a human reviewing the snapshot.
- `sleep`-based synchronisation.
