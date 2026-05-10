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
}: {
  params: Promise<{ teamId: string }>;
}) {
  const { teamId } = await params;
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

        <ul className="space-y-2">
          {drills.map((d) => (
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
      </section>
    </main>
  );
}
