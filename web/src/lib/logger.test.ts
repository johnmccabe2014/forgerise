import { describe, it, expect, vi, beforeEach } from "vitest";
import { logger } from "@/lib/logger";

describe("logger", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("emits a structured JSON line with correlation ID and redacts welfare", () => {
    const spy = vi.spyOn(console, "log").mockImplementation(() => {});

    logger.info("attendance.recorded", {
      correlationId: "c_abc123",
      playerId: "p_1",
      sleepHours: 5,
    });

    expect(spy).toHaveBeenCalledOnce();
    const line = spy.mock.calls[0][0] as string;
    const parsed = JSON.parse(line);

    expect(parsed.level).toBe("info");
    expect(parsed.msg).toBe("attendance.recorded");
    expect(parsed.correlationId).toBe("c_abc123");
    expect(parsed.playerId).toBe("p_1");
    expect(parsed.sleepHours).toBe("[REDACTED]");
    expect(parsed.ts).toMatch(/^\d{4}-\d{2}-\d{2}T/);
  });
});
