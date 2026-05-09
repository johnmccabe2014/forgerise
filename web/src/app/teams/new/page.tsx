import Link from "next/link";
import { TeamCreateForm } from "@/components/TeamCreateForm";

export const metadata = { title: "Create team — ForgeRise" };

export default function NewTeamPage() {
  return (
    <main className="min-h-screen bg-mist-grey px-6 py-10">
      <div className="mx-auto max-w-md">
        <Link href="/dashboard" className="text-sm text-slate underline">
          ← Back to dashboard
        </Link>
        <h1 className="mt-4 mb-6 font-heading text-3xl text-forge-navy">
          Create a team
        </h1>
        <TeamCreateForm />
      </div>
    </main>
  );
}
