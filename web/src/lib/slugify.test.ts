import { describe, it, expect } from "vitest";
import { slugify } from "@/lib/slugify";

describe("slugify", () => {
  it("returns empty for empty input", () => {
    expect(slugify("")).toBe("");
  });

  it("lowercases and replaces spaces", () => {
    expect(slugify("Riverside Roar")).toBe("riverside-roar");
  });

  it("strips diacritics", () => {
    expect(slugify("Coraçaõ U16")).toBe("coracao-u16");
  });

  it("collapses repeated separators and trims edges", () => {
    expect(slugify("  --Hello___World!!  ")).toBe("hello-world");
  });

  it("caps at 40 characters", () => {
    const long = "a".repeat(60);
    expect(slugify(long)).toHaveLength(40);
  });
});
