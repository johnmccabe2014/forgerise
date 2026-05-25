import Link from "next/link";
import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import { BrandMark } from "@/components/BrandMark";
import { sessionTypeLabel } from "@/lib/sessionLabels";

interface SessionDto {
  id: string;
  teamId: string;
  scheduledAt: string;
  durationMinutes: number;
  type: number;
  location: string | null;
  focus: string | null;
  reviewedAt: string | null;
  sourceSessionPlanId?: string | null;
}

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
export const metadata = { title: "Session — ForgeRise" };

export default async function SessionDetailPage({
  params,
}: {
  params: Promise<{ teamId: string; sessionId: string }>;
}) {
  const { teamId, sessionId } = await params;
  const resp = await serverFetchApi<SessionDto>(
    `/teams/${teamId}/sessions/${sessionId}`,
  );
  if (!resp.ok) {
    if (resp.status === 401) redirect("/login");
    redirect(`/teams/${teamId}`);
  }
  const session = resp.data;
  const fromPlan = Boolean(session.sourceSessionPlanId);

  return (
    <main className="min-h-screen bg-mist-grey">
      <header className="bg-white border-b border-slate/10">
        <div className="mx-auto max-w-2xl px-6 py-4 flex items-center justify-between">
          <Link href={`/teams/${teamId}`} className="text-sm text-slate underline">
            ← Team
          </Link>
          <BrandMark />
        </div>
      </header>

      <section className="mx-auto max-w-2xl px-6 py-10 space-y-6">
        <div className="space-y-1">
          <p className="text-xs uppercase tracking-widest text-rise-copper">
            {sessionTypeLabel(session.type)}
          </p>
          <h1 className="font-heading text-3xl text-forge-navy">
            {session.focus ?? "Session"}
          </h1>
          <p className="text-sm text-slate">
            {fmtWhen(session.scheduledAt)} · {session.durationMinutes} min
            {session.location ? ` · ${session.location}` : ""}
          </p>
        </div>

        <div className="rounded-card bg-white p-4 shadow-soft space-y-3">
          {fromPlan ? (
            <>
              <p className="text-sm text-deep-charcoal">
                This session was adopted from a session plan. Press start and
                we’ll walk you through it block-by-block with a timer.
              </p>
              <Link
                href={`/teams/${teamId}/sessions/${sessionId}/run`}
                className="inline-block rounded-pill bg-rise-copper text-white px-6 py-3 font-medium"
                data-testid="start-session"
              >
                ▶ Start session
              </Link>
            </>
          ) : (
            <p className="text-sm text-slate">
              The guided run-through is only available for sessions adopted
              from a session plan. Generate a plan first, then adopt it as a
              session to enable the timer view.
            </p>
          )}
          {session.sourceSessionPlanId && (
            <p className="text-xs text-slate">
              <Link
                href={`/teams/${teamId}/session-plans/${session.sourceSessionPlanId}`}
                className="underline"
              >
                View source plan
              </Link>
            </p>
          )}
        </div>

        <div className="rounded-card bg-white p-4 shadow-soft space-y-2">
          <Link
            href={`/teams/${teamId}/sessions/${sessionId}/attendance`}
            className="block text-sm text-forge-navy underline"
          >
            Record attendance
          </Link>
        </div>
      </section>
    </main>
  );
}
