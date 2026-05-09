import { describe, it, expect } from "vitest";
import {
  sessionTypeLabel,
  attendanceStatusLabel,
  ATTENDANCE_STATUS_OPTIONS,
} from "@/lib/sessionLabels";

describe("sessionTypeLabel", () => {
  it("maps each known value", () => {
    expect(sessionTypeLabel(0)).toBe("Training");
    expect(sessionTypeLabel(1)).toBe("Match");
    expect(sessionTypeLabel(2)).toBe("Other");
  });
  it("falls back for unknown values", () => {
    expect(sessionTypeLabel(99)).toBe("Session");
  });
});

describe("attendanceStatusLabel", () => {
  it("maps each known value", () => {
    expect(attendanceStatusLabel(0)).toBe("Absent");
    expect(attendanceStatusLabel(1)).toBe("Present");
    expect(attendanceStatusLabel(2)).toBe("Late");
    expect(attendanceStatusLabel(3)).toBe("Excused");
  });
  it("falls back for unknown values", () => {
    expect(attendanceStatusLabel(99)).toBe("Unknown");
  });
});

describe("ATTENDANCE_STATUS_OPTIONS", () => {
  it("offers all four statuses with Present first for low coach friction", () => {
    expect(ATTENDANCE_STATUS_OPTIONS.map((o) => o.label)).toEqual([
      "Present",
      "Late",
      "Excused",
      "Absent",
    ]);
  });
});
