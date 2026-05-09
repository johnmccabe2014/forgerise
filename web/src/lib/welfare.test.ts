import { describe, it, expect } from "vitest";
import { redactWelfare } from "@/lib/welfare";

describe("redactWelfare", () => {
  it("redacts top-level raw welfare fields", () => {
    const input = {
      playerId: "p_1",
      sleepHours: 4,
      sorenessScore: 7,
      menstrualPhase: "luteal",
      readiness: "monitor",
    };

    expect(redactWelfare(input)).toEqual({
      playerId: "p_1",
      sleepHours: "[REDACTED]",
      sorenessScore: "[REDACTED]",
      menstrualPhase: "[REDACTED]",
      readiness: "monitor",
    });
  });

  it("redacts nested + array shapes without mutating the input", () => {
    const input = {
      team: "u18s",
      players: [
        { id: "p_1", moodScore: 3, readiness: "ready" },
        { id: "p_2", injuryNotes: "tweak in knee", readiness: "recovery" },
      ],
    };
    const before = JSON.stringify(input);

    const out = redactWelfare(input) as typeof input;

    expect(out.players[0]).toMatchObject({ moodScore: "[REDACTED]" });
    expect(out.players[1]).toMatchObject({ injuryNotes: "[REDACTED]" });
    // Original input unchanged.
    expect(JSON.stringify(input)).toBe(before);
  });

  it("passes through primitives and unrelated structures", () => {
    expect(redactWelfare(42)).toBe(42);
    expect(redactWelfare("hello")).toBe("hello");
    expect(redactWelfare(null)).toBe(null);
    expect(redactWelfare({ a: 1, b: { c: 2 } })).toEqual({ a: 1, b: { c: 2 } });
  });
});
