import Link from "next/link";
import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import { PlayerAddForm } from "@/components/PlayerAddForm";
import { PlayerRow } from "@/components/PlayerRow";
import { CoachesPanel, type CoachRow } from "@/components/CoachesPanel";
import { InvitesPanel, type InviteRow } from "@/components/InvitesPanel";
import { sessionTypeLabel } from "@/lib/sessionLabels";
import { classifyPosition } from "@/lib/rugby";

interface TeamDto {
  id: string;
  name: string;
  code: string;
  createdAt: string;
  playerCount: number;
  myRole?: "owner" | "coach";
  coachCount?: number;
}

interface MeDto {
  id: string;
  email: string;
  displayName: string;
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

  const me = await serverFetchApi<MeDto>("/auth/me");
  const myUserId = me.ok ? me.data.id : "";
  const myRole: "owner" | "coach" = team.data.myRole ?? "owner";

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

  const coachesResp = await serverFetchApi<CoachRow[]>(
    `/teams/${params.teamId}/coaches`,
  );
  const coaches =
    coachesResp.ok && Array.isArray(coachesResp.data) ? coachesResp.data : [];

  // Invites are owner-only — skip the round-trip for coaches (the API would 403).
  const invitesResp =
    myRole === "owner"
      ? await serverFetchApi<InviteRow[]>(`/teams/${params.teamId}/invites`)
      : null;
  const invites =
    invitesResp && invitesResp.ok && Array.isArray(invitesResp.data)
      ? invitesResp.data
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
            {typeof team.data.coachCount === "number" && (
              <>
                {" · "}
                {team.data.coachCount} coach
                {team.data.coachCount === 1 ? "" : "es"}
              </>
            )}
            {" · "}
            <span className="text-rise-copper font-medium">
              {myRole === "owner" ? "You: Owner" : "You: Coach"}
            </span>
          </p>
        </div>

        <section aria-labelledby="coaches-heading" className="space-y-3">
          <h2
            id="coaches-heading"
            className="font-heading text-xl text-deep-charcoal"
          >
            Coaches
          </h2>
          <CoachesPanel
            teamId={team.data.id}
            myRole={myRole}
            myUserId={myUserId}
            coaches={coaches}
          />
          {myRole === "owner" && (
            <div className="rounded-card bg-white p-4 shadow-soft space-y-3">
              <div>
                <h3 className="font-heading text-sm text-forge-navy">
                  Invite codes
                </h3>
                <p className="text-xs text-slate">
                  Share an invite code so another coach can join this team.
                  Codes expire after 7 days.
                </p>
              </div>
              <InvitesPanel teamId={team.data.id} invites={invites} />
            </div>
          )}
        </section>

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
            (() => {
              // Split the squad by rugby unit so forwards-specific and
              // backs-specific training sessions map cleanly to selection.
              const forwards = roster.filter(
                (p) => classifyPosition(p.position) === "forward",
              );
              const backs = roster.filter(
                (p) => classifyPosition(p.position) === "back",
              );
              const unassigned = roster.filter(
                (p) => classifyPosition(p.position) === null,
              );
              const sortByJersey = (a: PlayerDto, b: PlayerDto) =>
                (a.jerseyNumber ?? 99) - (b.jerseyNumber ?? 99);
              forwards.sort(sortByJersey);
              backs.sort(sortByJersey);

              return (
                <div className="space-y-6">
                  <RosterGroup
                    title="Forwards"
                    subtitle="The pack — typically jerseys 1–8"
                    teamId={team.data.id}
                    players={forwards}
                    emptyHint="No forwards on the roster yet."
                  />
                  <RosterGroup
                    title="Backs"
                    subtitle="The backline — typically jerseys 9–15"
                    teamId={team.data.id}
                    players={backs}
                    emptyHint="No backs on the roster yet."
                  />
                  {unassigned.length > 0 && (
                    <RosterGroup
                      title="Unassigned"
                      subtitle="Players without a rugby position set"
                      teamId={team.data.id}
                      players={unassigned}
                    />
                  )}
                </div>
              );
            })()
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

interface RosterGroupProps {
  title: string;
  subtitle?: string;
  teamId: string;
  players: PlayerDto[];
  emptyHint?: string;
}

function RosterGroup({
  title,
  subtitle,
  teamId,
  players,
  emptyHint,
}: RosterGroupProps) {
  const headingId = `roster-${title.toLowerCase()}`;
  return (
    <section aria-labelledby={headingId} className="space-y-2">
      <div className="flex items-baseline justify-between gap-3">
        <h3
          id={headingId}
          className="font-heading text-lg text-forge-navy"
        >
          {title}
          <span className="ml-2 text-sm text-slate font-normal">
            ({players.length})
          </span>
        </h3>
        {subtitle && (
          <p className="text-xs text-slate/80 truncate">{subtitle}</p>
        )}
      </div>
      {players.length === 0 ? (
        <div className="rounded-card bg-white p-4 shadow-soft text-sm text-slate">
          {emptyHint ?? "No players in this group yet."}
        </div>
      ) : (
        <ul className="space-y-2">
          {players.map((p) => (
            <PlayerRow
              key={p.id}
              teamId={teamId}
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
  );
}
