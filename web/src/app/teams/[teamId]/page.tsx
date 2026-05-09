import Link from "next/link";
import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import { PlayerAddForm } from "@/components/PlayerAddForm";
import { PlayerRow } from "@/components/PlayerRow";
import { sessionTypeLabel } from "@/lib/sessionLabels";

interface TeamDto {
  id: string;
  name: string;
  code: string;
  createdAt: string;
  playerCount: number;
}

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

interface SessionDto {
  id: string;
  teamId: string;
  scheduledAt: string;
  durationMinutes: number;
  type: number;
  location: string | null;
  focus: string | null;
  reviewedAt: string | null;
}

export const dynamic = "force-dynamic";
export const metadata = { title: "Team — ForgeRise" };

export default async function TeamDetailPage({
  params,
}: {
  params: { teamId: string };
}) {
  const team = await serverFetchApi<TeamDto>(`/teams/${params.teamId}`);
  if (!team.ok) {
    if (team.status === 401) redirect("/login");
    redirect("/dashboard");
  }

  const players = await serverFetchApi<PlayerDto[]>(
    `/teams/${params.teamId}/players`,
  );
  const roster = players.ok && Array.isArray(players.data) ? players.data : [];

  const sessionsResp = await serverFetchApi<SessionDto[]>(
    `/teams/${params.teamId}/sessions`,
  );
  const sessions =
    sessionsResp.ok && Array.isArray(sessionsResp.data)
      ? sessionsResp.data.slice(0, 10)
      : [];

  return (
    <main className="min-h-screen bg-mist-grey">
      <header className="bg-white border-b border-slate/10">
        <div className="mx-auto max-w-3xl px-6 py-4 flex items-center justify-between">
          <Link href="/dashboard" className="text-sm text-slate underline">
            ← Dashboard
          </Link>
          <p className="font-heading text-forge-navy">ForgeRise</p>
        </div>
      </header>

      <section className="mx-auto max-w-3xl px-6 py-10 space-y-8">
        <div>
          <p className="text-xs uppercase tracking-widest text-rise-copper">
            Team
          </p>
          <h1 className="mt-1 font-heading text-3xl text-forge-navy">
            {team.data.name}
          </h1>
          <p className="text-sm text-slate">
            code: {team.data.code} · {team.data.playerCount} player
            {team.data.playerCount === 1 ? "" : "s"}
          </p>
        </div>

        <section aria-labelledby="add-player-heading" className="space-y-3">
          <h2
            id="add-player-heading"
            className="font-heading text-xl text-deep-charcoal"
          >
            Add player
          </h2>
          <div className="rounded-card bg-white p-4 shadow-soft">
            <PlayerAddForm teamId={team.data.id} />
          </div>
        </section>

        <section aria-labelledby="roster-heading" className="space-y-3">
          <h2
            id="roster-heading"
            className="font-heading text-xl text-deep-charcoal"
          >
            Roster
          </h2>
          {roster.length === 0 ? (
            <div className="rounded-card bg-white p-6 shadow-soft text-slate">
              No players yet. Add your first player above.
            </div>
          ) : (
            <ul className="space-y-2">
              {roster.map((p) => (
                <PlayerRow
                  key={p.id}
                  teamId={team.data.id}
                  player={{
                    id: p.id,
                    displayName: p.displayName,
                    jerseyNumber: p.jerseyNumber,
                    position: p.position,
                  }}
                />
              ))}
            </ul>
          )}
        </section>

        <section aria-labelledby="sessions-heading" className="space-y-3">
          <div className="flex items-center justify-between">
            <h2
              id="sessions-heading"
              className="font-heading text-xl text-deep-charcoal"
            >
              Sessions
            </h2>
            <Link
              href={`/teams/${team.data.id}/sessions/new`}
              className="text-sm text-rise-copper hover:underline"
            >
              + New session
            </Link>
          </div>
          {sessions.length === 0 ? (
            <div className="rounded-card bg-white p-6 shadow-soft text-slate">
              No sessions yet. Schedule one to start recording attendance.
            </div>
          ) : (
            <ul className="space-y-2">
              {sessions.map((s) => {
                const when = new Date(s.scheduledAt);
                return (
                  <li key={s.id}>
                    <Link
                      href={`/teams/${team.data.id}/sessions/${s.id}/attendance`}
                      className="block rounded-card bg-white p-4 shadow-soft hover:shadow-md transition"
                    >
                      <div className="flex items-center justify-between gap-3">
                        <div>
                          <p className="font-medium text-deep-charcoal">
                            {when.toLocaleString(undefined, {
                              weekday: "short",
                              day: "numeric",
                              month: "short",
                              hour: "2-digit",
                              minute: "2-digit",
                            })}
                          </p>
                          <p className="text-sm text-slate">
                            {sessionTypeLabel(s.type)} · {s.durationMinutes} min
                            {s.location ? ` · ${s.location}` : ""}
                          </p>
                          {s.focus && (
                            <p className="text-sm text-slate/80 truncate">
                              Focus: {s.focus}
                            </p>
                          )}
                        </div>
                        <span aria-hidden className="text-rise-copper">
                          →
                        </span>
                      </div>
                    </Link>
                  </li>
                );
              })}
            </ul>
          )}
        </section>
      </section>
    </main>
  );
}
