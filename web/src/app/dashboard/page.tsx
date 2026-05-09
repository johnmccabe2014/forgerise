import Link from "next/link";
import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import { LogoutButton } from "@/components/LogoutButton";
import { JoinTeamForm } from "@/components/JoinTeamForm";

export const metadata = { title: "Dashboard — ForgeRise" };
export const dynamic = "force-dynamic";

interface MeDto {
  id: string;
  email: string;
  displayName: string;
}

interface TeamDto {
  id: string;
  name: string;
  code: string;
  myRole?: "owner" | "coach";
  coachCount?: number;
}

export default async function DashboardPage() {
  const me = await serverFetchApi<MeDto>("/auth/me");
  if (!me.ok) {
    redirect("/login");
  }
  const teams = await serverFetchApi<TeamDto[]>("/teams");
  const teamList = teams.ok && Array.isArray(teams.data) ? teams.data : [];

  return (
    <main className="min-h-screen bg-mist-grey">
      <header className="bg-white border-b border-slate/10">
        <div className="mx-auto max-w-3xl px-6 py-4 flex items-center justify-between">
          <p className="font-heading text-forge-navy">ForgeRise</p>
          <div className="flex items-center gap-4">
            <span className="text-sm text-slate">{me.data.displayName}</span>
            <LogoutButton />
          </div>
        </div>
      </header>

      <section className="mx-auto max-w-3xl px-6 py-10 space-y-8">
        <div>
          <h1 className="font-heading text-3xl text-forge-navy">
            What needs attention today?
          </h1>
          <p className="mt-2 text-slate">
            Welcome back, {me.data.displayName}. Pick a team to start.
          </p>
        </div>

        <section aria-labelledby="teams-heading" className="space-y-3">
          <h2
            id="teams-heading"
            className="font-heading text-xl text-deep-charcoal"
          >
            Your teams
          </h2>
          {teamList.length === 0 ? (
            <div className="rounded-card bg-white p-6 shadow-soft">
              <p className="text-slate">
                You haven&apos;t set up a team yet.
              </p>
              <Link
                href="/teams/new"
                className="mt-4 inline-block rounded-xl bg-forge-navy px-4 py-2 text-white font-heading"
              >
                Create your first team
              </Link>
            </div>
          ) : (
            <>
              <ul className="space-y-2">
                {teamList.map((t) => (
                  <li key={t.id}>
                    <Link
                      href={`/teams/${t.id}`}
                      className="block rounded-card bg-white p-4 shadow-soft flex items-center justify-between hover:shadow-md transition-shadow"
                    >
                      <div>
                        <p className="font-heading text-forge-navy">{t.name}</p>
                        <p className="text-xs text-slate">
                          code: {t.code}
                          {t.myRole && (
                            <>
                              {" · "}
                              <span
                                className={
                                  t.myRole === "owner"
                                    ? "text-rise-copper font-medium"
                                    : "text-slate"
                                }
                              >
                                {t.myRole === "owner" ? "Owner" : "Coach"}
                              </span>
                            </>
                          )}
                        </p>
                      </div>
                      <span aria-hidden className="text-slate">
                        →
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
              <Link
                href="/teams/new"
                className="inline-block text-sm text-forge-navy underline"
              >
                + Create another team
              </Link>
            </>
          )}
        </section>

        <section aria-labelledby="join-heading" className="space-y-3">
          <h2
            id="join-heading"
            className="font-heading text-xl text-deep-charcoal"
          >
            Join a team
          </h2>
          <div className="rounded-card bg-white p-4 shadow-soft">
            <p className="text-sm text-slate mb-3">
              Got an invite code from another coach? Paste it here to join
              their team.
            </p>
            <JoinTeamForm />
          </div>
        </section>
      </section>
    </main>
  );
}
