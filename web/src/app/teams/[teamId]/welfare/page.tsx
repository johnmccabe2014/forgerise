import Link from "next/link";
import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import { ReadinessBadge } from "@/components/ReadinessBadge";
import { SelfSubmittedPill } from "@/components/SelfSubmittedPill";
import {
  IncidentTriagePanel,
  type UnacknowledgedIncident,
} from "@/components/IncidentTriagePanel";
import {
  READINESS_CATEGORIES,
  READINESS_LABELS,
  type ReadinessCategory,
} from "@/types/welfare";
import { BrandMark } from "@/components/BrandMark";

interface TeamDto {
  id: string;
  name: string;
}

interface PlayerLite {
  id: string;
  displayName: string;
}

interface IncidentRow {
  id: string;
  playerId: string;
  occurredAt: string;
  severity: number;
  summary: string;
  submittedBySelf: boolean;
  acknowledgedAt: string | null;
}

interface TeamReadinessRow {
  playerId: string;
  playerDisplayName: string;
  category: number; // SafeCategory: 0 Ready, 1 Monitor, 2 ModifyLoad, 3 RecoveryFocus
  categoryLabel: string;
  asOf: string;
  submittedBySelf: boolean;
}

const SAFE_CATEGORY_TO_READINESS: Record<number, ReadinessCategory> = {
  0: "ready",
  1: "monitor",
  2: "modify",
  3: "recovery",
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

export const dynamic = "force-dynamic";
export const metadata = { title: "Welfare — ForgeRise" };

export default async function WelfareDashboardPage({
  params,
}: {
  params: Promise<{ teamId: string }>;
}) {
  const { teamId } = await params;
  const teamResp = await serverFetchApi<TeamDto>(`/teams/${teamId}`);
  if (!teamResp.ok) {
    if (teamResp.status === 401) redirect("/login");
    redirect("/dashboard");
  }

  const [readinessResp, playersResp, incidentsResp] = await Promise.all([
    serverFetchApi<TeamReadinessRow[]>(`/teams/${teamId}/readiness`),
    serverFetchApi<PlayerLite[]>(`/teams/${teamId}/players`),
    serverFetchApi<IncidentRow[]>(`/teams/${teamId}/incidents`),
  ]);

  const rows =
    readinessResp.ok && Array.isArray(readinessResp.data)
      ? readinessResp.data
      : [];
  const roster =
    playersResp.ok && Array.isArray(playersResp.data) ? playersResp.data : [];
  const incidents =
    incidentsResp.ok && Array.isArray(incidentsResp.data)
      ? incidentsResp.data
      : [];
  const playerNameById = new Map(roster.map((p) => [p.id, p.displayName]));

  // Player-submitted incidents that haven't been triaged yet.
  const unacknowledged: UnacknowledgedIncident[] = incidents
    .filter((i) => i.submittedBySelf && i.acknowledgedAt === null)
    .map((i) => ({
      id: i.id,
      playerId: i.playerId,
      playerDisplayName: playerNameById.get(i.playerId) ?? "Unknown player",
      occurredAt: i.occurredAt,
      severity: i.severity,
      summary: i.summary,
    }));

  // Bucket reported players by readiness category.
  const buckets: Record<ReadinessCategory, TeamReadinessRow[]> = {
    ready: [],
    monitor: [],
    modify: [],
    recovery: [],
  };
  for (const row of rows) {
    const key = SAFE_CATEGORY_TO_READINESS[row.category];
    if (key) buckets[key].push(row);
  }
  // Within each bucket, sort by most recent check-in first.
  for (const key of READINESS_CATEGORIES) {
    buckets[key].sort(
      (a, b) => new Date(b.asOf).getTime() - new Date(a.asOf).getTime(),
    );
  }

  const reportedIds = new Set(rows.map((r) => r.playerId));
  const noCheckIn = roster.filter((p) => !reportedIds.has(p.id));

  return (
    <main className="min-h-screen bg-mist-grey">
      <header className="bg-white border-b border-slate/10">
        <div className="mx-auto max-w-3xl px-6 py-4 flex items-center justify-between">
          <Link
            href={`/teams/${teamId}`}
            className="text-sm text-slate underline"
          >
            ← Team
          </Link>
          <BrandMark />
        </div>
      </header>

      <section className="mx-auto max-w-3xl px-6 py-10 space-y-8">
        <div>
          <p className="text-xs uppercase tracking-widest text-rise-copper">
            Welfare
          </p>
          <h1 className="mt-1 font-heading text-3xl text-forge-navy">
            Readiness board
          </h1>
          <p className="text-sm text-slate">
            {teamResp.data.name} — {rows.length} of {roster.length} player
            {roster.length === 1 ? "" : "s"} reported.
          </p>
          <p className="mt-2 text-sm">
            <Link
              href={`/teams/${teamId}/welfare/incidents?status=acknowledged`}
              className="text-rise-copper hover:underline"
            >
              View incident history →
            </Link>
          </p>
        </div>

        <section
          aria-labelledby="triage-heading"
          className="rounded-card bg-white p-4 shadow-soft space-y-3"
        >
          <div className="flex items-center justify-between gap-2">
            <h2
              id="triage-heading"
              className="font-heading text-xl text-deep-charcoal"
            >
              Unread player reports
            </h2>
            {unacknowledged.length > 0 && (
              <span
                aria-label={`${unacknowledged.length} unacknowledged`}
                className="rounded-full bg-rise-copper px-2 py-0.5 text-xs font-medium text-white"
              >
                {unacknowledged.length}
              </span>
            )}
          </div>
          <IncidentTriagePanel teamId={teamId} incidents={unacknowledged} />
        </section>

        <ol className="grid gap-4 sm:grid-cols-2">
          {READINESS_CATEGORIES.map((cat) => {
            const items = buckets[cat];
            return (
              <li
                key={cat}
                className="rounded-card bg-white p-4 shadow-soft space-y-3"
              >
                <div className="flex items-center justify-between gap-2">
                  <ReadinessBadge category={cat} />
                  <span
                    aria-label={`${items.length} ${READINESS_LABELS[cat]}`}
                    className="font-heading text-xl text-forge-navy"
                  >
                    {items.length}
                  </span>
                </div>
                {items.length === 0 ? (
                  <p className="text-xs text-slate">No players in this state.</p>
                ) : (
                  <ul className="divide-y divide-slate/10">
                    {items.map((row) => (
                      <li
                        key={row.playerId}
                        className="flex items-center justify-between gap-2 py-2"
                      >
                        <Link
                          href={`/teams/${teamId}/players/${row.playerId}`}
                          className="flex min-w-0 items-center gap-2 text-sm text-deep-charcoal hover:underline"
                        >
                          <span className="truncate">
                            {row.playerDisplayName}
                          </span>
                          {row.submittedBySelf && <SelfSubmittedPill />}
                        </Link>
                        <span className="shrink-0 text-xs text-slate">
                          {fmtRelative(row.asOf)}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </li>
            );
          })}
        </ol>

        {noCheckIn.length > 0 && (
          <section
            aria-labelledby="no-checkin-heading"
            className="rounded-card bg-white p-4 shadow-soft space-y-3"
          >
            <h2
              id="no-checkin-heading"
              className="font-heading text-xl text-deep-charcoal"
            >
              No check-in yet
            </h2>
            <p className="text-xs text-slate">
              These players haven&apos;t submitted a wellness check-in. Coaches
              can record one from the player profile.
            </p>
            <ul className="divide-y divide-slate/10">
              {noCheckIn.map((p) => (
                <li
                  key={p.id}
                  className="flex items-center justify-between gap-2 py-2"
                >
                  <Link
                    href={`/teams/${teamId}/players/${p.id}`}
                    className="text-sm text-deep-charcoal hover:underline truncate"
                  >
                    {p.displayName}
                  </Link>
                  <span className="shrink-0 text-xs text-slate">—</span>
                </li>
              ))}
            </ul>
          </section>
        )}
      </section>
    </main>
  );
}
