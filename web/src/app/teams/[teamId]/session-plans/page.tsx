import Link from "next/link";
import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import { GeneratePlanForm } from "@/components/GeneratePlanForm";

interface TeamDto {
  id: string;
  name: string;
}

interface SessionPlanRow {
  id: string;
  teamId: string;
  generatedAt: string;
  basedOnSessionId: string | null;
  focus: string;
  summary: string;
  blocks: { intensity: string }[];
  readinessSnapshot: { playerId: string; category: number }[];
  pinnedAt?: string | null;
}

const INTENSITY_STYLE: Record<string, string> = {
  Standard: "bg-readiness-ready/10 text-readiness-ready border-readiness-ready/30",
  Reduced:
    "bg-readiness-modify/10 text-readiness-modify border-readiness-modify/30",
  "Recovery emphasis":
    "bg-readiness-recovery/10 text-readiness-recovery border-readiness-recovery/30",
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
export const metadata = { title: "Session plans — ForgeRise" };

export default async function SessionPlansListPage({
  params,
}: {
  params: Promise<{ teamId: string }>;
}) {
  const { teamId } = await params;
  const team = await serverFetchApi<TeamDto>(`/teams/${teamId}`);
  if (!team.ok) {
    if (team.status === 401) redirect("/login");
    redirect("/dashboard");
  }

  const plansResp = await serverFetchApi<SessionPlanRow[]>(
    `/teams/${teamId}/session-plans`,
  );
  const plans =
    plansResp.ok && Array.isArray(plansResp.data) ? plansResp.data : [];

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
        <div>
          <p className="text-xs uppercase tracking-widest text-rise-copper">
            Sessions
          </p>
          <h1 className="mt-1 font-heading text-3xl text-forge-navy">
            Session plans
          </h1>
          <p className="text-sm text-slate">
            {team.data.name} — heuristic plans built from the squad&apos;s
            current readiness mix and the last reviewed session.
          </p>
        </div>

        <section
          aria-labelledby="generate-heading"
          className="rounded-card bg-white p-4 shadow-soft space-y-3"
        >
          <h2
            id="generate-heading"
            className="font-heading text-xl text-deep-charcoal"
          >
            Generate a new plan
          </h2>
          <GeneratePlanForm teamId={teamId} />
        </section>

        <section aria-labelledby="recent-heading" className="space-y-3">
          <h2
            id="recent-heading"
            className="font-heading text-xl text-deep-charcoal"
          >
            Recent plans
          </h2>
          {plans.length === 0 ? (
            <div className="rounded-card bg-white p-6 shadow-soft text-slate">
              No plans yet. Generate one above to seed your next session.
            </div>
          ) : (
            <ul className="space-y-2">
              {plans.map((p) => {
                const intensity = p.blocks[0]?.intensity ?? "Standard";
                const style =
                  INTENSITY_STYLE[intensity] ??
                  "bg-mist-grey text-slate border-slate/20";
                return (
                  <li key={p.id}>
                    <Link
                      href={`/teams/${teamId}/session-plans/${p.id}`}
                      className="block rounded-card bg-white p-4 shadow-soft hover:shadow-md transition"
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="font-medium text-deep-charcoal truncate">
                            {p.pinnedAt && (
                              <span
                                aria-label="pinned"
                                data-testid={`plan-pinned-${p.id}`}
                                className="mr-1 text-rise-copper"
                              >
                                ★
                              </span>
                            )}
                            {p.focus}
                          </p>
                          <p className="text-xs text-slate">
                            {fmtWhen(p.generatedAt)} ·{" "}
                            {p.readinessSnapshot.length} reported
                          </p>
                        </div>
                        <span
                          className={`shrink-0 inline-flex items-center rounded-card border px-2 py-1 text-xs font-medium ${style}`}
                        >
                          {intensity}
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
