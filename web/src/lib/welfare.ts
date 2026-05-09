/**
 * Welfare redaction — last line of defence before any structured object
 * leaves the trust boundary (logs, analytics, AI prompts, coach-facing API).
 *
 * Master prompt §9, §11.
 */
import { RAW_WELFARE_FIELDS } from "@/types/welfare";

const RAW_SET = new Set<string>(RAW_WELFARE_FIELDS);

export function redactWelfare<T>(value: T): T {
  return walk(value) as T;
}

function walk(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(walk);
  if (value && typeof value === "object") {
    const out: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value as Record<string, unknown>)) {
      out[k] = RAW_SET.has(k) ? "[REDACTED]" : walk(v);
    }
    return out;
  }
  return value;
}
