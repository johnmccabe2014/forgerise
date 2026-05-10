import type { ReadinessCategory } from "@/types/welfare";

// Coach-safe readiness trend: a strip of category-coloured tiles, oldest
// (left) → newest (right). We render purely from SafeCategory values; no
// raw scores ever reach this component (master prompt §9).
//
// Why tiles, not a chart? Categories are ordinal but the gaps aren't
// numeric, so plotting them on a y-axis would imply false precision.
// A discrete strip lets a coach scan "have we been drifting from Ready
// toward ModifyLoad?" without inviting score-shaped questions.

const CATEGORY_TO_READINESS: Record<number, ReadinessCategory> = {
  0: "ready",
  1: "monitor",
  2: "modify",
  3: "recovery",
};

const TILE_BG: Record<ReadinessCategory, string> = {
  ready: "bg-readiness-ready",
  monitor: "bg-readiness-monitor",
  modify: "bg-readiness-modify",
  recovery: "bg-readiness-recovery",
};

const CATEGORY_LABEL: Record<ReadinessCategory, string> = {
  ready: "Ready",
  monitor: "Monitor",
  modify: "Modify Load",
  recovery: "Recovery Focus",
};

interface CheckInPoint {
  id: string;
  asOf: string;
  category: number;
}

export interface ReadinessTrendProps {
  /** Newest-first list (matches API order). */
  checkins: CheckInPoint[];
  /** Max tiles to show. Defaults to 12. */
  windowSize?: number;
}

function fmtTooltip(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    day: "2-digit",
    month: "short",
    year: "numeric",
  });
}

export function ReadinessTrend({
  checkins,
  windowSize = 12,
}: ReadinessTrendProps) {
  // API gives us newest-first; flip so the eye reads left→right as time forward.
  const window = checkins.slice(0, windowSize).reverse();

  if (window.length === 0) {
    return (
      <p
        data-testid="readiness-trend-empty"
        className="text-sm text-slate"
      >
        Not enough check-ins to plot a trend yet.
      </p>
    );
  }

  return (
    <div
      data-testid="readiness-trend"
      className="space-y-2"
      aria-label="Readiness trend, oldest to newest"
    >
      <div className="flex gap-1">
        {window.map((c) => {
          const cat = CATEGORY_TO_READINESS[c.category] ?? "monitor";
          const label = `${CATEGORY_LABEL[cat]} on ${fmtTooltip(c.asOf)}`;
          return (
            <span
              key={c.id}
              data-testid="readiness-trend-tile"
              data-category={cat}
              title={label}
              aria-label={label}
              className={`h-6 flex-1 min-w-[8px] rounded-sm ${TILE_BG[cat]}`}
            />
          );
        })}
      </div>
      <div className="flex items-center justify-between text-[11px] text-slate">
        <span>{fmtTooltip(window[0]!.asOf)}</span>
        <span className="flex items-center gap-3" aria-hidden>
          {(["ready", "monitor", "modify", "recovery"] as const).map((c) => (
            <span key={c} className="flex items-center gap-1">
              <span className={`h-2 w-2 rounded-sm ${TILE_BG[c]}`} />
              {CATEGORY_LABEL[c]}
            </span>
          ))}
        </span>
        <span>{fmtTooltip(window[window.length - 1]!.asOf)}</span>
      </div>
    </div>
  );
}
