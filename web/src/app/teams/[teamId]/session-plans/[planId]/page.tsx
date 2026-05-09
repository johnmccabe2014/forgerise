import Link from "next/link";
import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import { READINESS_LABELS, type ReadinessCategory } from "@/types/welfare";

interface PlanBlockDto {
  block: string;
  title: string;
  durationMinutes: number;
  intent: string;
  intensity: string;
}

interface ReadinessRow {
  playerId: string;
  category: number;
}

interface SessionPlanDto {
  id: string;
  teamId: string;
  generatedAt: string;
  basedOnSessionId: string | null;
  focus: string;
  summary: string;
  blocks: PlanBlockDto[];
  readinessSnapshot: ReadinessRow[];
}

const SAFE_CATEGORY_TO_READINESS: Record<number, ReadinessCategory> = {
  0: "ready",
  1: "monitor",
  2: "modify",
  3: "recovery",
};

const INTENSITY_STYLE: Record<string, string> = {
  Standard: "bg-readiness-ready/10 text-readiness-ready border-readiness-ready/30",
  Reduced:
    "bg-readiness-modify/10 text-readiness-modify border-readiness-modify/30",
  "Recovery emphasis":
    "bg-readiness-recovery/10 text-readiness-recovery border-readiness-recovery/30",
};

const BLOCK_LABEL: Record<string, string> = {
  warmup: "Warm-up",
  technical: "Technical",
  game: "Game",
  decision: "Decision",
  cooldown: "Cool-down",
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
export const metadata = { title: "Session plan — ForgeRise" };

export default async function SessionPlanDetailPage({
  params,
}: {
  params: Promise<{ teamId: string; planId: string }>;
}) {
  const { teamId, planId } = await params;
  const planResp = await serverFetchApi<SessionPlanDto>(
    `/teams/${teamId}/session-plans/${planId}`,
  );
  if (!planResp.ok) {
    if (planResp.status === 401) redirect("/login");
    redirect(`/teams/${teamId}/session-plans`);
  }
  const plan = planResp.data;

  const intensity = plan.blocks[0]?.intensity ?? "Standard";
  const intensityStyle =
    INTENSITY_STYLE[intensity] ?? "bg-mist-grey text-slate border-slate/20";

  const totalMinutes = plan.blocks.reduce(
    (sum, b) => sum + b.durationMinutes,
    0,
  );

  // Bucket the snapshot by SafeCategory for an at-a-glance summary.
  const counts: Record<ReadinessCategory, number> = {
    ready: 0,
    monitor: 0,
    modify: 0,
    recovery: 0,
  };
  for (const r of plan.readinessSnapshot) {
    const key = SAFE_CATEGORY_TO_READINESS[r.category];
    if (key) counts[key]++;
  }

  return (
    <main className="min-h-screen bg-mist-grey">
      <header className="bg-white border-b border-slate/10">
        <div className="mx-auto max-w-3xl px-6 py-4 flex items-center justify-between">
          <Link
            href={`/teams/${teamId}/session-plans`}
            className="text-sm text-slate underline"
          >
            ← Plans
          </Link>
          <p className="font-heading text-forge-navy">ForgeRise</p>
        </div>
      </header>

      <section className="mx-auto max-w-3xl px-6 py-10 space-y-8">
        <div className="space-y-2">
          <p className="text-xs uppercase tracking-widest text-rise-copper">
            Session plan
          </p>
          <div className="flex items-start justify-between gap-3">
            <h1 className="font-heading text-3xl text-forge-navy">
              {plan.focus}
            </h1>
            <span
              className={`shrink-0 inline-flex items-center rounded-card border px-3 py-2 text-sm font-medium ${intensityStyle}`}
            >
              {intensity}
            </span>
          </div>
          <p className="text-sm text-slate">
            Generated {fmtWhen(plan.generatedAt)} · {totalMinutes} min total ·{" "}
            {plan.readinessSnapshot.length} player
            {plan.readinessSnapshot.length === 1 ? "" : "s"} reported
          </p>
        </div>

        <section
          aria-labelledby="summary-heading"
          className="rounded-card bg-white p-4 shadow-soft space-y-3"
        >
          <h2
            id="summary-heading"
            className="font-heading text-xl text-deep-charcoal"
          >
            Coach summary
          </h2>
          <p className="text-sm text-deep-charcoal whitespace-pre-line">
            {plan.summary}
          </p>
          {plan.readinessSnapshot.length > 0 && (
            <dl className="grid grid-cols-4 gap-2 pt-2 text-center">
              {(["ready", "monitor", "modify", "recovery"] as const).map(
                (cat) => (
                  <div
                    key={cat}
                    className="rounded-card border border-slate/10 p-2"
                  >
                    <dt className="text-[11px] uppercase tracking-widest text-slate">
                      {READINESS_LABELS[cat]}
                    </dt>
                    <dd className="font-heading text-xl text-forge-navy">
                      {counts[cat]}
                    </dd>
                  </div>
                ),
              )}
            </dl>
          )}
        </section>

        <section aria-labelledby="blocks-heading" className="space-y-3">
          <h2
            id="blocks-heading"
            className="font-heading text-xl text-deep-charcoal"
          >
            Plan blocks
          </h2>
          {plan.blocks.length === 0 ? (
            <div className="rounded-card bg-white p-6 shadow-soft text-slate">
              This plan has no blocks recorded.
            </div>
          ) : (
            <ol className="space-y-2">
              {plan.blocks.map((b, i) => (
                <li
                  key={`${b.block}-${i}`}
                  className="rounded-card bg-white p-4 shadow-soft"
                >
                  <div className="flex items-baseline justify-between gap-3">
                    <p className="font-heading text-forge-navy truncate">
                      {b.title}
                    </p>
                    <span className="shrink-0 text-xs text-slate">
                      {b.durationMinutes} min
                    </span>
                  </div>
                  <p className="text-xs uppercase tracking-widest text-rise-copper">
                    {BLOCK_LABEL[b.block] ?? b.block}
                  </p>
                  <p className="mt-2 text-sm text-deep-charcoal">{b.intent}</p>
                </li>
              ))}
            </ol>
          )}
        </section>
      </section>
    </main>
  );
}
