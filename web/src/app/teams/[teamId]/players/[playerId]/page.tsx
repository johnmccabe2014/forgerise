import Link from "next/link";
import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import { ReadinessBadge } from "@/components/ReadinessBadge";
import {
  attendanceStatusLabel,
  sessionTypeLabel,
} from "@/lib/sessionLabels";
import type { ReadinessCategory } from "@/types/welfare";

interface PlayerDto {
  id: string;
  teamId: string;
  displayName: string;
  jerseyNumber: number | null;
  birthYear: number | null;
  position: string | null;
  isActive: boolean;
  createdAt: string;
}

interface AttendanceRowDto {
  sessionId: string;
  scheduledAt: string;
  type: number;
  location: string | null;
  focus: string | null;
  status: number;
  note: string | null;
}

// Coach-safe summaries only — never raw scores or notes (master prompt §9).
interface CheckInSummaryDto {
  id: string;
  playerId: string;
  asOf: string;
  category: number; // SafeCategory: 0 Ready, 1 Monitor, 2 ModifyLoad, 3 RecoveryFocus
  categoryLabel: string;
}

interface IncidentSummaryDto {
  id: string;
  playerId: string;
  occurredAt: string;
  severity: number; // 0 Low, 1 Medium, 2 High
  summary: string;
}

const SAFE_CATEGORY_TO_READINESS: Record<number, ReadinessCategory> = {
  0: "ready",
  1: "monitor",
  2: "modify",
  3: "recovery",
};

const SEVERITY_LABEL: Record<number, string> = {
  0: "Low",
  1: "Medium",
  2: "High",
};

const SEVERITY_STYLE: Record<number, string> = {
  0: "bg-readiness-monitor/10 text-readiness-monitor border-readiness-monitor/30",
  1: "bg-readiness-modify/10 text-readiness-modify border-readiness-modify/30",
  2: "bg-readiness-recovery/10 text-readiness-recovery border-readiness-recovery/30",
};

const ATTENDANCE_STYLE: Record<number, string> = {
  0: "text-readiness-recovery", // Absent
  1: "text-readiness-ready", // Present
  2: "text-readiness-modify", // Late
  3: "text-slate", // Excused
};

function fmtDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    weekday: "short",
    day: "2-digit",
    month: "short",
    year: "numeric",
  });
}

function attendanceSummary(rows: AttendanceRowDto[]) {
  const counts = { present: 0, late: 0, excused: 0, absent: 0 };
  for (const r of rows) {
    if (r.status === 1) counts.present++;
    else if (r.status === 2) counts.late++;
    else if (r.status === 3) counts.excused++;
    else counts.absent++;
  }
  // "Eligible" excludes excused absences when computing the rate so a
  // player who skipped a session for a legit reason isn't penalised.
  const eligible = counts.present + counts.late + counts.absent;
  const ratePct =
    eligible === 0
      ? null
      : Math.round(((counts.present + counts.late) / eligible) * 100);
  return { ...counts, ratePct };
}

export const dynamic = "force-dynamic";
export const metadata = { title: "Player profile — ForgeRise" };

export default async function PlayerProfilePage({
  params,
}: {
  params: Promise<{ teamId: string; playerId: string }>;
}) {
  const { teamId, playerId } = await params;

  const playerResp = await serverFetchApi<PlayerDto>(
    `/teams/${teamId}/players/${playerId}`,
  );
  if (!playerResp.ok) {
    if (playerResp.status === 401) redirect("/login");
    redirect(`/teams/${teamId}`);
  }
  const player = playerResp.data;

  const [attendanceResp, checkinsResp, incidentsResp] = await Promise.all([
    serverFetchApi<AttendanceRowDto[]>(
      `/teams/${teamId}/players/${playerId}/attendance`,
    ),
    serverFetchApi<CheckInSummaryDto[]>(
      `/teams/${teamId}/players/${playerId}/checkins`,
    ),
    serverFetchApi<IncidentSummaryDto[]>(
      `/teams/${teamId}/players/${playerId}/incidents`,
    ),
  ]);

  const attendance =
    attendanceResp.ok && Array.isArray(attendanceResp.data)
      ? attendanceResp.data
      : [];
  const checkins =
    checkinsResp.ok && Array.isArray(checkinsResp.data)
      ? checkinsResp.data
      : [];
  const incidents =
    incidentsResp.ok && Array.isArray(incidentsResp.data)
      ? incidentsResp.data
      : [];

  const summary = attendanceSummary(attendance);
  const latestCheckIn = checkins[0];

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
          <p className="font-heading text-forge-navy">ForgeRise</p>
        </div>
      </header>

      <section className="mx-auto max-w-3xl px-6 py-10 space-y-8">
        <div className="flex items-start gap-4">
          <span
            aria-hidden
            className="inline-flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-white text-xl font-heading text-forge-navy shadow-soft"
          >
            {player.jerseyNumber ?? "—"}
          </span>
          <div className="flex-1 min-w-0">
            <p className="text-xs uppercase tracking-widest text-rise-copper">
              Player
            </p>
            <h1 className="mt-1 font-heading text-3xl text-forge-navy truncate">
              {player.displayName}
            </h1>
            <p className="text-sm text-slate">
              {player.position ?? "Position TBD"}
              {player.birthYear && ` · ${player.birthYear}`}
              {!player.isActive && " · Inactive"}
            </p>
          </div>
          {latestCheckIn && (
            <div className="shrink-0">
              <ReadinessBadge
                category={SAFE_CATEGORY_TO_READINESS[latestCheckIn.category]}
              />
              <p className="mt-1 text-right text-xs text-slate">
                {fmtDate(latestCheckIn.asOf)}
              </p>
            </div>
          )}
        </div>

        <section
          aria-labelledby="attendance-heading"
          className="rounded-card bg-white p-4 shadow-soft space-y-3"
        >
          <div className="flex items-end justify-between gap-4">
            <h2
              id="attendance-heading"
              className="font-heading text-xl text-deep-charcoal"
            >
              Attendance
            </h2>
            <p className="text-xs text-slate">
              {summary.ratePct === null
                ? "No sessions yet"
                : `${summary.ratePct}% turn-up rate · ${summary.present} present, ${summary.late} late, ${summary.absent} absent, ${summary.excused} excused`}
            </p>
          </div>
          {attendance.length === 0 ? (
            <p className="text-sm text-slate">
              This team hasn&apos;t scheduled any sessions yet.
            </p>
          ) : (
            <ul className="divide-y divide-slate/10">
              {attendance.slice(0, 20).map((row) => (
                <li
                  key={row.sessionId}
                  className="flex items-center justify-between py-2"
                >
                  <div className="min-w-0">
                    <p className="text-sm text-deep-charcoal truncate">
                      {sessionTypeLabel(row.type)}
                      {row.focus && ` · ${row.focus}`}
                    </p>
                    <p className="text-xs text-slate">
                      {fmtDate(row.scheduledAt)}
                      {row.location && ` · ${row.location}`}
                    </p>
                  </div>
                  <span
                    className={`text-sm font-medium ${ATTENDANCE_STYLE[row.status] ?? "text-slate"}`}
                  >
                    {attendanceStatusLabel(row.status)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section
          aria-labelledby="welfare-heading"
          className="rounded-card bg-white p-4 shadow-soft space-y-3"
        >
          <h2
            id="welfare-heading"
            className="font-heading text-xl text-deep-charcoal"
          >
            Welfare check-ins
          </h2>
          <p className="text-xs text-slate">
            Coach-safe summaries only. Raw scores and notes are gated behind a
            separate audited view.
          </p>
          {checkins.length === 0 ? (
            <p className="text-sm text-slate">No check-ins recorded yet.</p>
          ) : (
            <ul className="divide-y divide-slate/10">
              {checkins.slice(0, 12).map((c) => (
                <li
                  key={c.id}
                  className="flex items-center justify-between py-2"
                >
                  <p className="text-xs text-slate">{fmtDate(c.asOf)}</p>
                  <ReadinessBadge
                    category={SAFE_CATEGORY_TO_READINESS[c.category]}
                  />
                </li>
              ))}
            </ul>
          )}
        </section>

        <section
          aria-labelledby="incidents-heading"
          className="rounded-card bg-white p-4 shadow-soft space-y-3"
        >
          <h2
            id="incidents-heading"
            className="font-heading text-xl text-deep-charcoal"
          >
            Incidents
          </h2>
          {incidents.length === 0 ? (
            <p className="text-sm text-slate">
              No injury or welfare incidents on record.
            </p>
          ) : (
            <ul className="divide-y divide-slate/10">
              {incidents.map((i) => (
                <li
                  key={i.id}
                  className="flex items-center justify-between gap-4 py-2"
                >
                  <div className="min-w-0">
                    <p className="text-sm text-deep-charcoal truncate">
                      {i.summary}
                    </p>
                    <p className="text-xs text-slate">{fmtDate(i.occurredAt)}</p>
                  </div>
                  <span
                    className={`shrink-0 inline-flex items-center rounded-card border px-2 py-1 text-xs font-medium ${SEVERITY_STYLE[i.severity] ?? ""}`}
                  >
                    {SEVERITY_LABEL[i.severity] ?? "—"}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </section>
    </main>
  );
}
