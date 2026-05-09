import Link from "next/link";
import { SessionCreateForm } from "@/components/SessionCreateForm";

export const metadata = { title: "New session — ForgeRise" };

export default function NewSessionPage({
  params,
}: {
  params: { teamId: string };
}) {
  return (
    <main className="min-h-screen bg-mist-grey">
      <header className="bg-white border-b border-slate/10">
        <div className="mx-auto max-w-3xl px-6 py-4 flex items-center justify-between">
          <Link
            href={`/teams/${params.teamId}`}
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
            Session
          </p>
          <h1 className="mt-1 font-heading text-3xl text-forge-navy">
            Schedule a session
          </h1>
          <p className="text-sm text-slate">
            Pick the basics. You can record attendance immediately after.
          </p>
        </div>

        <div className="rounded-card bg-white p-6 shadow-soft">
          <SessionCreateForm teamId={params.teamId} />
        </div>
      </section>
    </main>
  );
}
