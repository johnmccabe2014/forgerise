import { READINESS_LABELS, type ReadinessCategory } from "@/types/welfare";

const styles: Record<ReadinessCategory, string> = {
  ready: "bg-readiness-ready/10 text-readiness-ready border-readiness-ready/30",
  monitor:
    "bg-readiness-monitor/10 text-readiness-monitor border-readiness-monitor/30",
  modify:
    "bg-readiness-modify/10 text-readiness-modify border-readiness-modify/30",
  recovery:
    "bg-readiness-recovery/10 text-readiness-recovery border-readiness-recovery/30",
};

export interface ReadinessBadgeProps {
  category: ReadinessCategory;
}

export function ReadinessBadge({ category }: ReadinessBadgeProps) {
  return (
    <span
      role="status"
      aria-label={`Readiness: ${READINESS_LABELS[category]}`}
      className={[
        "inline-flex items-center justify-center",
        "rounded-card border px-3 py-2 text-sm font-medium shadow-soft",
        styles[category],
      ].join(" ")}
    >
      {READINESS_LABELS[category]}
    </span>
  );
}
