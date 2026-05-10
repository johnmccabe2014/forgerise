import Link from "next/link";
import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import { DrillPreferenceToggle } from "@/components/DrillPreferenceToggle";
import { DrillPrefsImport } from "@/components/DrillPrefsImport";

interface DrillCataloguePrefDto {
  drillId: string;
  title: string;
  description: string;
  durationMinutes: number;
  tags: string[];
  status: "favourite" | "exclude" | null;
  updatedAt?: string | null;
  lastChangedByDisplayName?: string | null;
}

function fmtDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    day: "2-digit",
    month: "short",
  });
}

export const dynamic = "force-dynamic";
export const metadata = { title: "Drill preferences — ForgeRise" };

export default async function DrillPreferencesPage({
  params,
  searchParams,
}: {
  params: Promise<{ teamId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { teamId } = await params;
  const sp = await searchParams;
  const pickStr = (v: string | string[] | undefined) =>
    Array.isArray(v) ? v[0] ?? "" : v ?? "";
  const q = pickStr(sp.q).trim();
  const qLower = q.toLowerCase();
  const tag = pickStr(sp.tag).trim();
  const statusFilter = pickStr(sp.status).trim() as
    | ""
    | "favourite"
    | "exclude"
    | "set"
    | "unset";

  const resp = await serverFetchApi<DrillCataloguePrefDto[]>(
    `/teams/${teamId}/drill-preferences`,
  );
  if (!resp.ok) {
    if (resp.status === 401) redirect("/login");
    redirect(`/teams/${teamId}`);
  }
  const drills = resp.data;
  const favouriteCount = drills.filter((d) => d.status === "favourite").length;
  const excludeCount = drills.filter((d) => d.status === "exclude").length;

  // Tags are sourced from the loaded catalogue so the dropdown can never
  // offer a tag that isn't in scope. Sorted alphabetically for stable UI.
  const allTags = Array.from(
    new Set(drills.flatMap((d) => d.tags)),
  ).sort((a, b) => a.localeCompare(b));

  const filtered = drills.filter((d) => {
    if (tag && !d.tags.includes(tag)) return false;
    if (statusFilter === "favourite" && d.status !== "favourite") return false;
    if (statusFilter === "exclude" && d.status !== "exclude") return false;
    if (statusFilter === "set" && d.status === null) return false;
    if (statusFilter === "unset" && d.status !== null) return false;
    if (qLower) {
      const hay = `${d.title} ${d.description} ${d.tags.join(" ")}`.toLowerCase();
      if (!hay.includes(qLower)) return false;
    }
    return true;
  });

  const hasFilters = Boolean(q || tag || statusFilter);

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

      <section className="mx-auto max-w-3xl px-6 py-10 space-y-6">
        <div>
          <p className="text-xs uppercase tracking-widest text-rise-copper">
            Session planning
          </p>
          <h1 className="mt-1 font-heading text-3xl text-forge-navy">
            Drill preferences
          </h1>
          <p className="mt-2 text-sm text-slate">
            Tune the recommender for this team. Favourites get prioritised in
            generated plans; excluded drills are filtered out entirely. Tap
            again to clear a preference.
          </p>
          <p
            className="mt-2 text-xs text-deep-charcoal"
            data-testid="prefs-summary"
          >
            {favouriteCount} favourite{favouriteCount === 1 ? "" : "s"} ·{" "}
            {excludeCount} excluded
          </p>
        </div>

        <DrillPrefsImport teamId={teamId} />

        <form
          method="GET"
          data-testid="drill-prefs-filters"
          className="rounded-card bg-white p-4 shadow-soft grid gap-3 sm:grid-cols-2 lg:grid-cols-4"
        >
          <label className="text-xs text-slate flex flex-col gap-1 lg:col-span-2">
            Search
            <input
              type="search"
              name="q"
              defaultValue={q}
              placeholder="Title, description, or tag"
              className="rounded border border-slate/30 bg-white px-2 py-1 text-sm text-deep-charcoal"
            />
          </label>
          <label className="text-xs text-slate flex flex-col gap-1">
            Tag
            <select
              name="tag"
              defaultValue={tag}
              className="rounded border border-slate/30 bg-white px-2 py-1 text-sm text-deep-charcoal"
            >
              <option value="">All tags</option>
              {allTags.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </label>
          <label className="text-xs text-slate flex flex-col gap-1">
            Preference
            <select
              name="status"
              defaultValue={statusFilter}
              className="rounded border border-slate/30 bg-white px-2 py-1 text-sm text-deep-charcoal"
            >
              <option value="">Any</option>
              <option value="favourite">Favourites only</option>
              <option value="exclude">Excluded only</option>
              <option value="set">Any preference set</option>
              <option value="unset">No preference yet</option>
            </select>
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
                href={`/teams/${teamId}/drill-preferences`}
                className="text-xs text-rise-copper hover:underline"
              >
                Clear
              </Link>
            )}
            <span
              data-testid="drill-prefs-count"
              className="ml-auto text-xs text-slate"
            >
              {filtered.length} of {drills.length} drills
            </span>
          </div>
        </form>

        {filtered.length === 0 ? (
          <div className="rounded-card bg-white p-6 shadow-soft text-slate text-sm">
            No drills match those filters.
          </div>
        ) : (
          <ul className="space-y-2">
            {filtered.map((d) => (
            <li
              key={d.drillId}
              className="rounded-card bg-white p-4 shadow-soft flex items-start justify-between gap-4"
            >
              <div className="min-w-0">
                <p className="text-sm font-medium text-deep-charcoal">
                  {d.title}
                  <span className="ml-2 text-xs text-slate">
                    {d.durationMinutes} min
                  </span>
                </p>
                <p className="text-xs text-slate mt-1">{d.description}</p>
                <p className="text-xs text-slate mt-1">
                  {d.tags.join(" · ")}
                </p>
                {d.status && d.updatedAt && (
                  <p
                    data-testid={`pref-meta-${d.drillId}`}
                    className="text-xs text-slate/80 mt-1"
                  >
                    {d.status === "favourite" ? "Favourited" : "Excluded"}{" "}
                    {fmtDate(d.updatedAt)}
                    {d.lastChangedByDisplayName
                      ? ` by ${d.lastChangedByDisplayName}`
                      : ""}
                  </p>
                )}
              </div>
              <DrillPreferenceToggle
                teamId={teamId}
                drillId={d.drillId}
                current={d.status}
              />
            </li>
          ))}
          </ul>
        )}
      </section>
    </main>
  );
}
