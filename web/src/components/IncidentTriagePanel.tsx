"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";

export interface UnacknowledgedIncident {
  id: string;
  playerId: string;
  playerDisplayName: string;
  occurredAt: string;
  severity: number; // 0 Low, 1 Medium (High blocked at API for self-reports)
  summary: string;
}

export interface IncidentTriagePanelProps {
  teamId: string;
  incidents: UnacknowledgedIncident[];
}

const SEVERITY_LABEL: Record<number, string> = {
  0: "Low",
  1: "Medium",
  2: "High",
};

function fmt(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    day: "2-digit",
    month: "short",
  });
}

/**
 * Coach-side triage list for player-submitted incident reports that
 * haven't been acknowledged yet. One-click ack records the actor in the
 * welfare audit log via the API.
 */
export function IncidentTriagePanel({
  teamId,
  incidents,
}: IncidentTriagePanelProps) {
  const router = useRouter();
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function acknowledge(playerId: string, id: string) {
    setBusy(id);
    setError(null);
    try {
      const res = await fetch(
        `/api/proxy/teams/${teamId}/players/${playerId}/incidents/${id}/acknowledge`,
        { method: "POST" },
      );
      if (!res.ok) {
        setError("Could not acknowledge. Please try again.");
        return;
      }
      router.refresh();
    } catch {
      setError("Network error. Please try again.");
    } finally {
      setBusy(null);
    }
  }

  if (incidents.length === 0) {
    return (
      <p className="text-xs text-slate">
        No unread player reports. New self-submitted incidents land here.
      </p>
    );
  }

  return (
    <div className="space-y-3">
      {error && (
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      )}
      <ul className="divide-y divide-slate/10">
        {incidents.map((i) => (
          <li
            key={i.id}
            className="flex flex-col gap-2 py-3 sm:flex-row sm:items-center sm:justify-between"
          >
            <div className="min-w-0 space-y-1">
              <div className="flex items-center gap-2 text-sm">
                <span className="rounded-full bg-rise-copper/10 px-2 py-0.5 text-xs font-medium text-rise-copper">
                  {SEVERITY_LABEL[i.severity] ?? "—"}
                </span>
                <Link
                  href={`/teams/${teamId}/players/${i.playerId}`}
                  className="truncate font-medium text-deep-charcoal hover:underline"
                >
                  {i.playerDisplayName}
                </Link>
                <span className="shrink-0 text-xs text-slate">
                  {fmt(i.occurredAt)}
                </span>
              </div>
              <p className="text-sm text-slate truncate">{i.summary}</p>
            </div>
            <button
              type="button"
              onClick={() => acknowledge(i.playerId, i.id)}
              disabled={busy === i.id}
              className="shrink-0 rounded-pill bg-forge-navy px-3 py-1.5 text-xs font-medium text-white disabled:opacity-60"
            >
              {busy === i.id ? "Acknowledging…" : "Acknowledge"}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
