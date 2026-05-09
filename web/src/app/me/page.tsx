import Link from "next/link";
import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import { ClaimPlayerForm } from "@/components/ClaimPlayerForm";
import { SelfCheckInForm } from "@/components/SelfCheckInForm";
import { SelfIncidentForm } from "@/components/SelfIncidentForm";
import { ReadinessBadge } from "@/components/ReadinessBadge";
import { type ReadinessCategory } from "@/types/welfare";

export const metadata = { title: "My profile — ForgeRise" };
export const dynamic = "force-dynamic";

interface MeDto {
  id: string;
  email: string;
  displayName: string;
}

interface MyLinkedPlayerDto {
  playerId: string;
  playerDisplayName: string;
  teamId: string;
  teamName: string;
  claimedAt: string;
}

interface MyCheckInDto {
  id: string;
  asOf: string;
  category: number;
  categoryLabel: string;
  submittedBySelf: boolean;
}

interface MyIncidentDto {
  id: string;
  occurredAt: string;
  severity: number;
  summary: string;
  acknowledgedAt?: string | null;
  acknowledgedByDisplayName?: string | null;
}

const SEVERITY_LABELS: Record<number, string> = {
  0: "Low",
  1: "Medium",
  2: "High",
};

function fmtWhen(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  });
}

const CATEGORY_TO_KEY: Record<number, ReadinessCategory> = {
  0: "ready",
  1: "monitor",
  2: "modify",
  3: "recovery",
};

function fmtDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    day: "2-digit",
    month: "short",
  });
}

export default async function MePage() {
  const me = await serverFetchApi<MeDto>("/auth/me");
  if (!me.ok) {
    redirect("/login");
  }
  const linked = await serverFetchApi<MyLinkedPlayerDto[]>("/me/players");
  const players = linked.ok && Array.isArray(linked.data) ? linked.data : [];

  // Fan out to fetch recent check-ins per linked player. Linked-player count
  // is small (typically 1, occasionally a few siblings), so a parallel fetch
  // is cheaper than introducing a new batch endpoint.
  const histories = await Promise.all(
    players.map(async (p) => {
      const r = await serverFetchApi<MyCheckInDto[]>(
        `/me/players/${p.playerId}/checkins`,
      );
      return r.ok && Array.isArray(r.data) ? r.data.slice(0, 7) : [];
    }),
  );

  const incidents = await Promise.all(
    players.map(async (p) => {
      const r = await serverFetchApi<MyIncidentDto[]>(
        `/me/players/${p.playerId}/incidents`,
      );
      return r.ok && Array.isArray(r.data) ? r.data.slice(0, 5) : [];
    }),
  );

  return (
    <main className="min-h-screen bg-mist-grey">
      <header className="bg-white border-b border-slate/10">
        <div className="mx-auto max-w-3xl px-6 py-4 flex items-center justify-between">
          <p className="font-heading text-forge-navy">ForgeRise</p>
          <Link
            href="/dashboard"
            className="text-sm text-slate underline"
          >
            ← Dashboard
          </Link>
        </div>
      </header>

      <section className="mx-auto max-w-3xl px-6 py-10 space-y-8">
        <div>
          <h1 className="font-heading text-3xl text-forge-navy">My profile</h1>
          <p className="mt-2 text-slate">
            Hi {me.data.displayName}. Submit your daily check-in here so your
            coach can plan training around how you feel.
          </p>
        </div>

        <section
          aria-labelledby="claim-heading"
          className="space-y-3 rounded-card bg-white p-6 shadow-soft"
        >
          <h2
            id="claim-heading"
            className="font-heading text-xl text-deep-charcoal"
          >
            Claim a player profile
          </h2>
          <p className="text-sm text-slate">
            Got a player invite code from your coach? Enter it here to link your
            account to your spot on the roster.
          </p>
          <ClaimPlayerForm />
        </section>

        <section aria-labelledby="players-heading" className="space-y-3">
          <h2
            id="players-heading"
            className="font-heading text-xl text-deep-charcoal"
          >
            Linked profiles
          </h2>
          {players.length === 0 ? (
            <div className="rounded-card bg-white p-6 shadow-soft">
              <p className="text-slate">
                You haven&apos;t linked a profile yet. Use the invite code from
                your coach above to get started.
              </p>
            </div>
          ) : (
            <ul className="space-y-6">
              {players.map((p, idx) => {
                const history = histories[idx] ?? [];
                const myIncidents = incidents[idx] ?? [];
                return (
                <li
                  key={p.playerId}
                  className="rounded-card bg-white p-6 shadow-soft space-y-4"
                >
                  <div>
                    <p className="font-heading text-forge-navy">
                      {p.playerDisplayName}
                    </p>
                    <p className="text-xs text-slate">{p.teamName}</p>
                  </div>
                  <div className="border-t border-slate/10 pt-4">
                    <h3 className="font-heading text-deep-charcoal mb-3">
                      Today&apos;s check-in
                    </h3>
                    <SelfCheckInForm playerId={p.playerId} />
                  </div>
                  <div className="border-t border-slate/10 pt-4">
                    <h3 className="font-heading text-deep-charcoal mb-3">
                      Recent activity
                    </h3>
                    {(() => {
                      type TimelineItem =
                        | { kind: "checkin"; at: string; row: MyCheckInDto }
                        | { kind: "incident"; at: string; row: MyIncidentDto };
                      const items: TimelineItem[] = [
                        ...history.map((h) => ({
                          kind: "checkin" as const,
                          at: h.asOf,
                          row: h,
                        })),
                        ...myIncidents.map((inc) => ({
                          kind: "incident" as const,
                          at: inc.occurredAt,
                          row: inc,
                        })),
                      ].sort(
                        (a, b) =>
                          new Date(b.at).getTime() - new Date(a.at).getTime(),
                      );
                      if (items.length === 0) {
                        return (
                          <p className="text-sm text-slate">
                            No activity yet. Submit a check-in above to see
                            it here.
                          </p>
                        );
                      }
                      return (
                        <ul
                          data-testid={`my-timeline-${p.playerId}`}
                          className="divide-y divide-slate/10"
                        >
                          {items.map((item) =>
                            item.kind === "checkin" ? (
                              <li
                                key={`c-${item.row.id}`}
                                className="flex items-center justify-between gap-3 py-2"
                                data-testid={`timeline-checkin-${item.row.id}`}
                              >
                                <span className="text-sm text-deep-charcoal">
                                  {fmtDate(item.row.asOf)} · Check-in
                                </span>
                                <ReadinessBadge
                                  category={
                                    CATEGORY_TO_KEY[item.row.category] ??
                                    "monitor"
                                  }
                                />
                              </li>
                            ) : (
                              <li
                                key={`i-${item.row.id}`}
                                className="py-2 space-y-0.5"
                                data-testid={`timeline-incident-${item.row.id}`}
                              >
                                <div className="flex items-center justify-between gap-3">
                                  <span className="text-sm text-deep-charcoal">
                                    {fmtWhen(item.row.occurredAt)} · Report ·{" "}
                                    {SEVERITY_LABELS[item.row.severity] ??
                                      "Low"}
                                  </span>
                                  {item.row.acknowledgedAt ? (
                                    <span className="rounded-pill bg-readiness-ready/15 text-readiness-ready px-2 py-0.5 text-xs">
                                      Acknowledged
                                    </span>
                                  ) : (
                                    <span className="rounded-pill bg-readiness-monitor/15 text-readiness-monitor px-2 py-0.5 text-xs">
                                      Awaiting review
                                    </span>
                                  )}
                                </div>
                                <p className="text-sm text-slate">
                                  {item.row.summary}
                                </p>
                                {item.row.acknowledgedAt && (
                                  <p className="text-xs text-slate">
                                    Acknowledged{" "}
                                    {fmtWhen(item.row.acknowledgedAt)}
                                    {item.row.acknowledgedByDisplayName
                                      ? ` by ${item.row.acknowledgedByDisplayName}`
                                      : ""}
                                  </p>
                                )}
                              </li>
                            ),
                          )}
                        </ul>
                      );
                    })()}
                  </div>
                  <div className="border-t border-slate/10 pt-4">
                    <h3 className="font-heading text-deep-charcoal mb-3">
                      Report an injury or welfare issue
                    </h3>
                    <SelfIncidentForm playerId={p.playerId} />
                  </div>
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
