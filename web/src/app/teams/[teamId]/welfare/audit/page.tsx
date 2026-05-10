import Link from "next/link";
import { notFound } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import { BrandMark } from "@/components/BrandMark";

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

interface PlayerDto {
  id: string;
  displayName: string;
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

const ACTION_NAMES: { value: string; label: string }[] = [
  { value: "ReadRawCheckIn", label: "Read raw check-in" },
  { value: "ReadRawIncident", label: "Read raw incident" },
  { value: "PurgeRawCheckIn", label: "Purge raw check-in" },
  { value: "PurgeRawIncident", label: "Purge raw incident" },
  { value: "DeleteCheckIn", label: "Delete check-in" },
  { value: "DeleteIncident", label: "Delete incident" },
  { value: "SelfSubmitCheckIn", label: "Self-submit check-in" },
  { value: "SelfReportIncident", label: "Self-report incident" },
  { value: "AcknowledgeIncident", label: "Acknowledge incident" },
];

function fmtWhen(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function pickString(
  v: string | string[] | undefined,
): string | undefined {
  if (Array.isArray(v)) return v[0];
  return v;
}

export default async function WelfareAuditPage({
  params,
  searchParams,
}: {
  params: Promise<{ teamId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { teamId } = await params;
  const sp = await searchParams;
  const action = pickString(sp.action) ?? "";
  const playerId = pickString(sp.playerId) ?? "";
  const from = pickString(sp.from) ?? "";
  const to = pickString(sp.to) ?? "";
  const PAGE_SIZE = 50;
  const skipParam = Number.parseInt(pickString(sp.skip) ?? "0", 10);
  const skip = Number.isFinite(skipParam) && skipParam > 0 ? skipParam : 0;

  const qs = new URLSearchParams();
  if (action) qs.set("action", action);
  if (playerId) qs.set("playerId", playerId);
  if (from) qs.set("from", new Date(from).toISOString());
  if (to) qs.set("to", new Date(to).toISOString());
  qs.set("skip", String(skip));
  qs.set("take", String(PAGE_SIZE));
  const query = qs.toString();

  // Filter-only querystring used for Prev/Next links so we don't carry the
  // current page's skip into the new offset.
  const filterParams = new URLSearchParams();
  if (action) filterParams.set("action", action);
  if (playerId) filterParams.set("playerId", playerId);
  if (from) filterParams.set("from", from);
  if (to) filterParams.set("to", to);

  const [auditRes, playersRes] = await Promise.all([
    serverFetchApi<AuditEntryDto[]>(
      `/teams/${teamId}/welfare-audit${query ? `?${query}` : ""}`,
    ),
    serverFetchApi<PlayerDto[]>(`/teams/${teamId}/players`),
  ]);
  if (!auditRes.ok) notFound();
  const rows = Array.isArray(auditRes.data) ? auditRes.data : [];
  const players =
    playersRes.ok && Array.isArray(playersRes.data) ? playersRes.data : [];
  const hasFilters = Boolean(action || playerId || from || to);
  const hasMore = rows.length === PAGE_SIZE;
  const hasPrev = skip > 0;
  const prevSkip = Math.max(0, skip - PAGE_SIZE);
  const nextSkip = skip + PAGE_SIZE;

  function pageHref(nextSkipValue: number): string {
    const p = new URLSearchParams(filterParams);
    if (nextSkipValue > 0) p.set("skip", String(nextSkipValue));
    const qs = p.toString();
    return `/teams/${teamId}/welfare/audit${qs ? `?${qs}` : ""}`;
  }

  return (
    <main className="min-h-screen bg-mist-grey">
      <header className="bg-white border-b border-slate/10">
        <div className="mx-auto max-w-4xl px-6 py-4 flex items-center justify-between">
          <BrandMark />
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
            team, plus self-submissions and acknowledgements. Newest first,
            {" "}
            {PAGE_SIZE} per page.
          </p>
        </div>

        <form
          method="GET"
          data-testid="welfare-audit-filters"
          className="rounded-card bg-white p-4 shadow-soft grid gap-3 sm:grid-cols-2 lg:grid-cols-4"
        >
          <label className="text-xs text-slate flex flex-col gap-1">
            Action
            <select
              name="action"
              defaultValue={action}
              className="rounded border border-slate/30 bg-white px-2 py-1 text-sm text-deep-charcoal"
            >
              <option value="">All</option>
              {ACTION_NAMES.map((a) => (
                <option key={a.value} value={a.value}>
                  {a.label}
                </option>
              ))}
            </select>
          </label>

          <label className="text-xs text-slate flex flex-col gap-1">
            Player
            <select
              name="playerId"
              defaultValue={playerId}
              className="rounded border border-slate/30 bg-white px-2 py-1 text-sm text-deep-charcoal"
            >
              <option value="">All</option>
              {players.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.displayName}
                </option>
              ))}
            </select>
          </label>

          <label className="text-xs text-slate flex flex-col gap-1">
            From
            <input
              type="datetime-local"
              name="from"
              defaultValue={from}
              className="rounded border border-slate/30 bg-white px-2 py-1 text-sm text-deep-charcoal"
            />
          </label>

          <label className="text-xs text-slate flex flex-col gap-1">
            To
            <input
              type="datetime-local"
              name="to"
              defaultValue={to}
              className="rounded border border-slate/30 bg-white px-2 py-1 text-sm text-deep-charcoal"
            />
          </label>

          <div className="sm:col-span-2 lg:col-span-4 flex items-center gap-3">
            <button
              type="submit"
              className="rounded-pill bg-forge-navy px-4 py-1.5 text-sm font-medium text-white hover:bg-forge-navy/90"
            >
              Apply filters
            </button>
            {hasFilters && (
              <Link
                href={`/teams/${teamId}/welfare/audit`}
                className="text-xs text-rise-copper hover:underline"
              >
                Clear
              </Link>
            )}
            <a
              data-testid="welfare-audit-export"
              href={`/api/proxy/teams/${teamId}/welfare-audit/export.csv${
                filterParams.toString() ? `?${filterParams.toString()}` : ""
              }`}
              className="text-xs text-forge-navy underline hover:text-rise-copper"
            >
              Export CSV
            </a>
            <span className="ml-auto text-xs text-slate">
              showing {rows.length === 0 ? 0 : skip + 1}–{skip + rows.length}
            </span>
          </div>
        </form>

        {rows.length === 0 ? (
          <div className="rounded-card bg-white p-6 shadow-soft">
            <p className="text-slate">
              {hasFilters
                ? "No audit events match those filters."
                : "No audit events yet."}
            </p>
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

        {(hasPrev || hasMore) && (
          <nav
            data-testid="welfare-audit-pager"
            className="flex items-center justify-between text-sm"
            aria-label="Audit log pagination"
          >
            {hasPrev ? (
              <Link
                data-testid="welfare-audit-prev"
                href={pageHref(prevSkip)}
                className="rounded-pill bg-white px-4 py-1.5 text-forge-navy shadow-soft hover:bg-mist-grey"
              >
                ← Newer
              </Link>
            ) : (
              <span />
            )}
            {hasMore ? (
              <Link
                data-testid="welfare-audit-next"
                href={pageHref(nextSkip)}
                className="rounded-pill bg-white px-4 py-1.5 text-forge-navy shadow-soft hover:bg-mist-grey"
              >
                Older →
              </Link>
            ) : (
              <span />
            )}
          </nav>
        )}
      </section>
    </main>
  );
}
