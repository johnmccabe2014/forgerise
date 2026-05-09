// Rugby Union 15-a-side reference data.
// Keep in one place so the player form, roster split, and any future
// tactical views all read from a single source of truth.

export type PositionGroup = "forward" | "back";

export interface RugbyPosition {
  /** Default jersey number for a starting XV (1-15). */
  number: number;
  /** Stored on the player record (e.g. "Tighthead Prop"). */
  name: string;
  /** Short label for compact UI (e.g. "TH"). */
  short: string;
  group: PositionGroup;
}

/**
 * Rugby Union starting XV. Replacements (16-23) are not tied to a single
 * position so we don't enumerate them here — the jersey-number dropdown
 * still goes up to 23 to allow squad numbers.
 */
export const RUGBY_POSITIONS: readonly RugbyPosition[] = [
  { number: 1, name: "Loosehead Prop", short: "LH", group: "forward" },
  { number: 2, name: "Hooker", short: "H", group: "forward" },
  { number: 3, name: "Tighthead Prop", short: "TH", group: "forward" },
  { number: 4, name: "Lock", short: "L", group: "forward" },
  { number: 5, name: "Lock", short: "L", group: "forward" },
  { number: 6, name: "Blindside Flanker", short: "BF", group: "forward" },
  { number: 7, name: "Openside Flanker", short: "OF", group: "forward" },
  { number: 8, name: "Number 8", short: "N8", group: "forward" },
  { number: 9, name: "Scrum-half", short: "SH", group: "back" },
  { number: 10, name: "Fly-half", short: "FH", group: "back" },
  { number: 11, name: "Left Wing", short: "LW", group: "back" },
  { number: 12, name: "Inside Centre", short: "IC", group: "back" },
  { number: 13, name: "Outside Centre", short: "OC", group: "back" },
  { number: 14, name: "Right Wing", short: "RW", group: "back" },
  { number: 15, name: "Fullback", short: "FB", group: "back" },
] as const;

/** De-duplicated list of position names for use in dropdowns. */
export const POSITION_NAMES: readonly string[] = Array.from(
  new Set(RUGBY_POSITIONS.map((p) => p.name)),
);

/** Squad jersey numbers — starting XV plus an 8-strong bench. */
export const JERSEY_NUMBERS: readonly number[] = Array.from(
  { length: 23 },
  (_, i) => i + 1,
);

const FORWARD_NAMES: readonly string[] = RUGBY_POSITIONS
  .filter((p) => p.group === "forward")
  .map((p) => p.name);
const BACK_NAMES: readonly string[] = RUGBY_POSITIONS
  .filter((p) => p.group === "back")
  .map((p) => p.name);

/**
 * Classify a stored player position into "forward", "back", or null when
 * the position is missing or doesn't match a Rugby Union slot. Comparison
 * is case-insensitive and trims whitespace so legacy free-text positions
 * still group correctly.
 */
export function classifyPosition(
  position: string | null | undefined,
): PositionGroup | null {
  if (!position) return null;
  const normalized = position.trim();
  if (FORWARD_NAMES.includes(normalized)) return "forward";
  if (BACK_NAMES.includes(normalized)) return "back";
  // Case-insensitive fallback for legacy data.
  const lower = normalized.toLowerCase();
  if (FORWARD_NAMES.some((n) => n.toLowerCase() === lower)) return "forward";
  if (BACK_NAMES.some((n) => n.toLowerCase() === lower)) return "back";
  return null;
}
