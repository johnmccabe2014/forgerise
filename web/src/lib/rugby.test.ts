import { describe, it, expect } from "vitest";
import {
  RUGBY_POSITIONS,
  POSITION_NAMES,
  JERSEY_NUMBERS,
  classifyPosition,
} from "@/lib/rugby";

describe("rugby reference data", () => {
  it("has 15 starting positions, 8 forwards and 7 backs", () => {
    expect(RUGBY_POSITIONS).toHaveLength(15);
    expect(RUGBY_POSITIONS.filter((p) => p.group === "forward")).toHaveLength(8);
    expect(RUGBY_POSITIONS.filter((p) => p.group === "back")).toHaveLength(7);
  });

  it("dedupes Lock so the position dropdown lists each role once", () => {
    const locks = POSITION_NAMES.filter((n) => n === "Lock");
    expect(locks).toHaveLength(1);
  });

  it("offers jersey numbers 1 through 23", () => {
    expect(JERSEY_NUMBERS[0]).toBe(1);
    expect(JERSEY_NUMBERS[JERSEY_NUMBERS.length - 1]).toBe(23);
    expect(JERSEY_NUMBERS).toHaveLength(23);
  });
});

describe("classifyPosition", () => {
  it("classifies pack roles as forwards", () => {
    expect(classifyPosition("Hooker")).toBe("forward");
    expect(classifyPosition("Number 8")).toBe("forward");
    expect(classifyPosition("Tighthead Prop")).toBe("forward");
  });

  it("classifies back-line roles as backs", () => {
    expect(classifyPosition("Scrum-half")).toBe("back");
    expect(classifyPosition("Fullback")).toBe("back");
    expect(classifyPosition("Inside Centre")).toBe("back");
  });

  it("is tolerant of legacy free-text casing and whitespace", () => {
    expect(classifyPosition("  hooker  ")).toBe("forward");
    expect(classifyPosition("FLY-HALF")).toBe("back");
  });

  it("returns null for missing or unknown positions", () => {
    expect(classifyPosition(null)).toBeNull();
    expect(classifyPosition(undefined)).toBeNull();
    expect(classifyPosition("")).toBeNull();
    expect(classifyPosition("Goalkeeper")).toBeNull();
  });
});
