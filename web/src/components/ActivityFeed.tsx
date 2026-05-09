import Link from "next/link";

export interface ActivityEvent {
  kind: string;
  at: string;
  playerId: string;
  playerDisplayName: string;
  subjectId: string | null;
  category: number | null;
  categoryLabel: string | null;
  severity: number | null;
  summary: string | null;
  acknowledged: boolean | null;
}

const KIND_CHECKIN = "checkin_self_submitted";
const KIND_INCIDENT = "incident_self_reported";
const KIND_REDEEMED = "invite_redeemed";

const SEVERITY_LABEL: Record<number, string> = {
  0: "Low",
  1: "Medium",
  2: "High",
};

function fmtRelative(iso: string): string {
  const then = new Date(iso).getTime();
  const diffMs = Date.now() - then;
  const day = 24 * 60 * 60 * 1000;
  if (diffMs < 60 * 60 * 1000) {
    const mins = Math.max(1, Math.round(diffMs / 60000));
    return `${mins}m ago`;
  }
  if (diffMs < day) {
    const hrs = Math.round(diffMs / (60 * 60 * 1000));
    return `${hrs}h ago`;
  }
  const days = Math.round(diffMs / day);
  return days === 1 ? "yesterday" : `${days}d ago`;
}

interface ActivityFeedProps {
  teamId: string;
  events: ActivityEvent[];
}

/**
 * Read-only "what changed" feed for the team detail page. Shows the most
 * recent player-driven events: self check-ins, self-reported incidents,
 * and invite redemptions. Each row links to the relevant player profile.
 */
export function ActivityFeed({ teamId, events }: ActivityFeedProps) {
  if (events.length === 0) {
    return (
      <p className="text-xs text-slate">
        No recent player activity. Self check-ins, self-reported incidents,
        and invite redemptions show up here.
      </p>
    );
  }

  return (
    <ul className="divide-y divide-slate/10">
      {events.map((e, idx) => (
        <li key={`${e.kind}-${e.subjectId ?? e.playerId}-${idx}`} className="flex items-start justify-between gap-3 py-3">
          <div className="min-w-0 space-y-0.5">
            <p className="text-sm">
              <Link
                href={`/teams/${teamId}/players/${e.playerId}`}
                className="font-medium text-deep-charcoal hover:underline"
              >
                {e.playerDisplayName}
              </Link>{" "}
              <span className="text-slate">{describe(e)}</span>
            </p>
            {e.kind === KIND_INCIDENT && e.summary && (
              <p className="truncate text-xs text-slate">{e.summary}</p>
            )}
          </div>
          <span className="shrink-0 text-xs text-slate">
            {fmtRelative(e.at)}
          </span>
        </li>
      ))}
    </ul>
  );
}

function describe(e: ActivityEvent): string {
  switch (e.kind) {
    case KIND_CHECKIN:
      return e.categoryLabel
        ? `submitted a check-in — ${e.categoryLabel}`
        : "submitted a check-in";
    case KIND_INCIDENT: {
      const sev = e.severity !== null ? SEVERITY_LABEL[e.severity] : null;
      const acked = e.acknowledged ? "acknowledged" : "needs review";
      return sev
        ? `reported a ${sev.toLowerCase()} incident — ${acked}`
        : `reported an incident — ${acked}`;
    }
    case KIND_REDEEMED:
      return "claimed their roster spot";
    default:
      return e.kind;
  }
}
