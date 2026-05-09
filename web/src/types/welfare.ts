/**
 * Welfare-safe categories — coach-facing only.
 *
 * Master prompt §9: coaches must NEVER see raw welfare data.
 * Anything coach-facing collapses to one of these four labels.
 */
export const READINESS_CATEGORIES = [
  "ready",
  "monitor",
  "modify",
  "recovery",
] as const;

export type ReadinessCategory = (typeof READINESS_CATEGORIES)[number];

export const READINESS_LABELS: Record<ReadinessCategory, string> = {
  ready: "Ready",
  monitor: "Monitor",
  modify: "Modify Load",
  recovery: "Recovery Focus",
};

/**
 * Field names that MUST NEVER appear in coach-facing payloads, logs, or
 * analytics events. Enforced by `redactWelfare()` and an api-side analyzer.
 */
export const RAW_WELFARE_FIELDS = [
  "sleepHours",
  "sorenessScore",
  "menstrualPhase",
  "menstrualSymptoms",
  "moodScore",
  "stressScore",
  "fatigueScore",
  "injuryNotes",
  "medicalNotes",
] as const;

export type RawWelfareField = (typeof RAW_WELFARE_FIELDS)[number];
