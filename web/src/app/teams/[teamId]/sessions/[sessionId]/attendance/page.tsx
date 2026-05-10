import Link from "next/link";
import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import {
  AttendanceForm,
  type AttendanceRow,
} from "@/components/AttendanceForm";
import { sessionTypeLabel } from "@/lib/sessionLabels";
import { BrandMark } from "@/components/BrandMark";

interface SessionDto {
  id: string;
  teamId: string;
  scheduledAt: string;
  durationMinutes: number;
  type: number;
  location: string | null;
  focus: string | null;
  reviewNotes: string | null;
  reviewedAt: string | null;
  createdAt: string;
}

export const dynamic = "force-dynamic";
export const metadata = { title: "Attendance — ForgeRise" };

export default async function AttendancePage({
  params,
}: {
  params: Promise<{ teamId: string; sessionId: string }>;
}) {
  const { teamId, sessionId } = await params;
  const session = await serverFetchApi<SessionDto>(
    `/teams/${teamId}/sessions/${sessionId}`,
  );
  if (!session.ok) {
    if (session.status === 401) redirect("/login");
    redirect(`/teams/${teamId}`);
  }

  const rows = await serverFetchApi<AttendanceRow[]>(
    `/teams/${teamId}/sessions/${sessionId}/attendance`,
  );
  if (!rows.ok) {
    if (rows.status === 401) redirect("/login");
    redirect(`/teams/${teamId}`);
  }

  const initialRows = Array.isArray(rows.data) ? rows.data : [];
  const scheduled = new Date(session.data.scheduledAt);

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
          <BrandMark />
        </div>
      </header>

      <section className="mx-auto max-w-3xl px-6 py-10 space-y-6">
        <div>
          <p className="text-xs uppercase tracking-widest text-rise-copper">
            {sessionTypeLabel(session.data.type)}
          </p>
          <h1 className="mt-1 font-heading text-3xl text-forge-navy">
            Attendance
          </h1>
          <p className="text-sm text-slate">
            {scheduled.toLocaleString(undefined, {
              weekday: "short",
              day: "numeric",
              month: "short",
              hour: "2-digit",
              minute: "2-digit",
            })}{" "}
            · {session.data.durationMinutes} min
            {session.data.location ? ` · ${session.data.location}` : ""}
          </p>
          {session.data.focus && (
            <p className="mt-1 text-sm text-deep-charcoal">
              Focus: {session.data.focus}
            </p>
          )}
        </div>

        <AttendanceForm
          teamId={teamId}
          sessionId={sessionId}
          initialRows={initialRows}
        />
      </section>
    </main>
  );
}
