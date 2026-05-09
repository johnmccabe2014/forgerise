# Skill: code-review

Apply this checklist on every diff before claiming review-ready.

## Read order
1. The plan / acceptance criteria.
2. The tests.
3. The public API / contract changes.
4. The implementation.
5. The migrations / config / infra.

## Checklist
- [ ] Diff scope matches the plan; no drive-by changes.
- [ ] Names reveal intent; no abbreviations needing a glossary.
- [ ] Functions do one thing; cyclomatic complexity reasonable.
- [ ] No duplication introduced; no premature abstraction.
- [ ] Errors handled at the right layer; no swallowed exceptions.
- [ ] Inputs validated at boundaries; outputs encoded for sinks.
- [ ] Comments explain _why_, not _what_.
- [ ] Public APIs documented; breaking changes flagged.
- [ ] Tests assert behaviour, not implementation.
- [ ] No dead code, commented-out blocks, or stray debug logs.
- [ ] Performance: no obvious O(n²), N+1, or unbounded resource use.
- [ ] Concurrency: shared state guarded; idempotency where mutating.
- [ ] Observability: logs/metrics/traces appropriate, no PII.
- [ ] Backwards compatibility considered.

## Tone
Critique the code, not the author. Suggest, don't dictate, except on Definition-of-Done items, which are non-negotiable.
