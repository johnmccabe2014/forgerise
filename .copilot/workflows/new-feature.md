# Workflow: new-feature

1. **Intake** — user states goal. Planner clarifies until acceptance criteria are concrete.
2. **Plan** — Planner produces `templates/plan.md`. Security agent reviews the plan if any sensitive surface is touched.
3. **Slice** — break into the smallest end-to-end vertical slice that delivers value.
4. **Loop** — run `feedback-loop.md` for slice 1.
5. **Demo & decide** — show working slice; decide whether to continue, pivot, or stop.
6. **Repeat** for next slice.

Definition of feature-done: every acceptance criterion is covered by an automated test that fails before the code change and passes after.
