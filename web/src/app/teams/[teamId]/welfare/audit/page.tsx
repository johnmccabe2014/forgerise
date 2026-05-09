import Link from "next/link";
import { notFound } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";

export const metadata = { title: "Welfare audit — ForgeRise" };
export const dynamic = "force-dynamic";

interface AuditEntryDto {
  id: string;
  actorUserId: string;
  playerId: string;
  subjectId: string | null;
  action: number;
  at: string;
  actorDisplayName: string | null;
  playerDisplayName: string | null;
}

const ACTION_LABELS: Record<number, string> = {
  0: "Read raw check-in",
  1: "Read raw incident",
  2: "Purge raw check-in",
  3: "Purge raw incident",
  4: "Delete check-in",
  5: "Delete incident",
  6: "Self-submit check-in",
  7: "Self-report incident",
  8: "Acknowledge incident",
};

function fmtWhen(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export default async function WelfareAuditPage({
  params,
}: {
  params: Promise<{ teamId: string }>;
}) {
  const { teamId } = await params;
  const res = await serverFetchApi<AuditEntryDto[]>(
    `/teams/${teamId}/welfare-audit`,
  );
  if (!res.ok) notFound();
  const rows = Array.isArray(res.data) ? res.data : [];

  return (
    <main className="min-h-screen bg-mist-grey">
      <header className="bg-white border-b border-slate/10">
        <div className="mx-auto max-w-4xl px-6 py-4 flex items-center justify-between">
          <p className="font-heading text-forge-navy">ForgeRise</p>
          <Link
            href={`/teams/${teamId}`}
            className="text-sm text-slate underline"
          >
            ← Team
          </Link>
        </div>
      </header>

      <section className="mx-auto max-w-4xl px-6 py-10 space-y-6">
        <div>
          <h1 className="font-heading text-3xl text-forge-navy">
            Welfare audit log
          </h1>
          <p className="mt-2 text-slate">
            Append-only record of every access to raw welfare data on this
            team, plus self-submissions and acknowledgements. Most recent 500
            events shown, newest first.
          </p>
        </div>

        {rows.length === 0 ? (
          <div className="rounded-card bg-white p-6 shadow-soft">
            <p className="text-slate">No audit events yet.</p>
          </div>
        ) : (
          <div
            data-testid="welfare-audit-rows"
            className="rounded-card bg-white shadow-soft overflow-hidden"
          >
            <table className="w-full text-sm">
              <thead className="bg-mist-grey/60 text-deep-charcoal">
                <tr>
                  <th className="text-left px-4 py-2 font-medium">When</th>
                  <th className="text-left px-4 py-2 font-medium">Actor</th>
                  <th className="text-left px-4 py-2 font-medium">Action</th>
                  <th className="text-left px-4 py-2 font-medium">Player</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate/10">
                {rows.map((r) => (
                  <tr key={r.id}>
                    <td className="px-4 py-2 text-slate whitespace-nowrap">
                      {fmtWhen(r.at)}
                    </td>
                    <td className="px-4 py-2 text-deep-charcoal">
                      {r.actorDisplayName ?? r.actorUserId.slice(0, 8)}
                    </td>
                    <td className="px-4 py-2 text-deep-charcoal">
                      {ACTION_LABELS[r.action] ?? `Action ${r.action}`}
                    </td>
                    <td className="px-4 py-2 text-deep-charcoal">
                      {r.playerDisplayName ?? r.playerId.slice(0, 8)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  );
}
