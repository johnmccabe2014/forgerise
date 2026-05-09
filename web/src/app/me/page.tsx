import Link from "next/link";
import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import { ClaimPlayerForm } from "@/components/ClaimPlayerForm";
import { SelfCheckInForm } from "@/components/SelfCheckInForm";

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

export default async function MePage() {
  const me = await serverFetchApi<MeDto>("/auth/me");
  if (!me.ok) {
    redirect("/login");
  }
  const linked = await serverFetchApi<MyLinkedPlayerDto[]>("/me/players");
  const players = linked.ok && Array.isArray(linked.data) ? linked.data : [];

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
              {players.map((p) => (
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
                </li>
              ))}
            </ul>
          )}
        </section>
      </section>
    </main>
  );
}
