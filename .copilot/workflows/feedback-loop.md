# Workflow: feedback-loop

The default heartbeat. Every task runs through this.

```
            ┌──────────────────────────────────────────────────────┐
            │                                                      │
            ▼                                                      │
   ┌────────────────┐    ┌─────────────┐    ┌─────────────┐        │
   │   1. Plan      │───►│  2. Build   │───►│  3. Test    │        │
   │   (Planner)    │    │ (Frontend / │    │  (Tester)   │        │
   └────────────────┘    │  Backend)   │    └──────┬──────┘        │
                         └─────────────┘           │               │
                                                   ▼               │
                                          ┌──────────────────┐     │
                                          │  4. Validate     │     │
                                          │  (skill: validation,│  │
                                          │   security-scan) │     │
                                          └──────┬───────────┘     │
                                                 ▼                 │
                                          ┌──────────────────┐     │
                                          │  5. Review       │     │
                                          │ (Principal Rev.) │     │
                                          └──────┬───────────┘     │
                                                 │                 │
                                  approve  ◄─────┼─────►  changes  │
                                       │                  │        │
                                       ▼                  └────────┘
                                    merge
```

## Rules
1. **No skipping steps.** Even a one-line change goes through 1–5; the work just shrinks.
2. **Loop until clean.** Each pass must close every must-fix from the previous review.
3. **Each loop must improve a measurable.** Coverage, complexity, security findings, perf, or review score. The Principal Reviewer records the metric in `templates/review.md`.
4. **Two regressions on the same dimension across loops** → escalate to the user; the approach may be wrong.
5. **Stop conditions**:
   - Approved by Principal Reviewer → merge.
   - 3 loops without convergence → return to Planner; the plan is the problem.

## State file
Each task keeps a small log:

```
.copilot/state/<task-id>.md
- iteration 1: built X; tests green; review score 22/40; must-fix: A, B, C
- iteration 2: addressed A, B, C; new test for X edge case; score 31/40; must-fix: D
- iteration 3: addressed D; score 37/40; approved.
```

Create the `state/` folder lazily on first task.
