import Link from "next/link";
import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";

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
  acknowledgedByDisplayName: string | null;
}

const SEVERITY_LABEL: Record<number, string> = {
  0: "Low",
  1: "Medium",
  2: "High",
};

const SEVERITY_STYLE: Record<number, string> = {
  0: "bg-readiness-ready/10 text-readiness-ready border-readiness-ready/30",
  1: "bg-readiness-monitor/10 text-readiness-monitor border-readiness-monitor/30",
  2: "bg-readiness-recovery/10 text-readiness-recovery border-readiness-recovery/30",
};

const STATUSES = ["unread", "acknowledged", "all"] as const;
type Status = (typeof STATUSES)[number];

const STATUS_LABEL: Record<Status, string> = {
  unread: "Unread",
  acknowledged: "Acknowledged",
  all: "All",
};

function fmtWhen(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    weekday: "short",
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export const dynamic = "force-dynamic";
export const metadata = { title: "Incidents — ForgeRise" };

export default async function IncidentsHistoryPage({
  params,
  searchParams,
}: {
  params: Promise<{ teamId: string }>;
  searchParams: Promise<{ status?: string }>;
}) {
  const { teamId } = await params;
  const sp = await searchParams;
  const status: Status = STATUSES.includes(sp.status as Status)
    ? (sp.status as Status)
    : "acknowledged";

  const teamResp = await serverFetchApi<TeamDto>(`/teams/${teamId}`);
  if (!teamResp.ok) {
    if (teamResp.status === 401) redirect("/login");
    redirect("/dashboard");
  }

  const [incidentsResp, playersResp] = await Promise.all([
    serverFetchApi<IncidentRow[]>(`/teams/${teamId}/incidents?status=${status}`),
    serverFetchApi<PlayerLite[]>(`/teams/${teamId}/players`),
  ]);
  const rows =
    incidentsResp.ok && Array.isArray(incidentsResp.data)
      ? incidentsResp.data
      : [];
  const roster =
    playersResp.ok && Array.isArray(playersResp.data) ? playersResp.data : [];
  const nameById = new Map(roster.map((p) => [p.id, p.displayName]));

  return (
    <main className="min-h-screen bg-mist-grey">
      <header className="bg-white border-b border-slate/10">
        <div className="mx-auto max-w-3xl px-6 py-4 flex items-center justify-between">
          <Link
            href={`/teams/${teamId}/welfare`}
            className="text-sm text-slate underline"
          >
            ← Welfare
          </Link>
          <p className="font-heading text-forge-navy">ForgeRise</p>
        </div>
      </header>

      <section className="mx-auto max-w-3xl px-6 py-10 space-y-6">
        <div>
          <p className="text-xs uppercase tracking-widest text-rise-copper">
            Welfare
          </p>
          <h1 className="mt-1 font-heading text-3xl text-forge-navy">
            Incident history
          </h1>
          <p className="text-sm text-slate">
            {teamResp.data.name} — provenance and acknowledgement audit trail.
            Detailed notes stay on the player profile.
          </p>
        </div>

        <nav
          aria-label="Incident filter"
          className="inline-flex rounded-card border border-slate/20 bg-white p-1 text-sm shadow-soft"
        >
          {STATUSES.map((s) => {
            const active = s === status;
            return (
              <Link
                key={s}
                href={`/teams/${teamId}/welfare/incidents?status=${s}`}
                aria-current={active ? "page" : undefined}
                className={
                  active
                    ? "rounded-card bg-forge-navy px-3 py-1.5 text-white"
                    : "rounded-card px-3 py-1.5 text-slate hover:text-forge-navy"
                }
              >
                {STATUS_LABEL[s]}
              </Link>
            );
          })}
        </nav>

        {rows.length === 0 ? (
          <div className="rounded-card bg-white p-6 text-center text-sm text-slate shadow-soft">
            {status === "unread"
              ? "No unread player reports right now."
              : status === "acknowledged"
                ? "No acknowledged incidents yet."
                : "No incidents have been recorded for this team."}
          </div>
        ) : (
          <ul className="space-y-2">
            {rows.map((i) => {
              const sevStyle =
                SEVERITY_STYLE[i.severity] ??
                "bg-mist-grey text-slate border-slate/20";
              const playerName = nameById.get(i.playerId) ?? "Unknown player";
              return (
                <li
                  key={i.id}
                  className="rounded-card bg-white p-4 shadow-soft space-y-2"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-heading text-forge-navy">
                        <Link
                          href={`/teams/${teamId}/players/${i.playerId}`}
                          className="hover:underline"
                        >
                          {playerName}
                        </Link>
                      </p>
                      <p className="text-sm text-deep-charcoal">{i.summary}</p>
                    </div>
                    <span
                      className={`shrink-0 inline-flex items-center rounded-card border px-2 py-1 text-xs font-medium ${sevStyle}`}
                    >
                      {SEVERITY_LABEL[i.severity] ?? "—"}
                    </span>
                  </div>
                  <p className="text-xs text-slate">
                    {i.submittedBySelf
                      ? "Self-reported"
                      : "Coach-recorded"}{" "}
                    · occurred {fmtWhen(i.occurredAt)}
                  </p>
                  {i.acknowledgedAt ? (
                    <p className="text-xs text-readiness-ready">
                      Acknowledged {fmtWhen(i.acknowledgedAt)}
                      {i.acknowledgedByDisplayName
                        ? ` by ${i.acknowledgedByDisplayName}`
                        : ""}
                    </p>
                  ) : (
                    <p className="text-xs text-rise-copper">Needs review</p>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </section>
    </main>
  );
}
