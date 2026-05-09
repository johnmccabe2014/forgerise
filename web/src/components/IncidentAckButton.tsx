"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

interface IncidentAckButtonProps {
  teamId: string;
  playerId: string;
  incidentId: string;
}

/**
 * Single-purpose acknowledge button for the incident history page. POSTs
 * to the same endpoint the welfare dashboard uses so the audit trail is
 * consistent — actor + timestamp recorded server-side.
 */
export function IncidentAckButton({
  teamId,
  playerId,
  incidentId,
}: IncidentAckButtonProps) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onClick() {
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(
        `/api/proxy/teams/${teamId}/players/${playerId}/incidents/${incidentId}/acknowledge`,
        { method: "POST" },
      );
      if (!res.ok) {
        setError("Could not acknowledge.");
        return;
      }
      router.refresh();
    } catch {
      setError("Network error.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-1">
      <button
        type="button"
        onClick={onClick}
        disabled={busy}
        className="rounded-pill bg-forge-navy px-3 py-1.5 text-xs font-medium text-white disabled:opacity-60"
      >
        {busy ? "Acknowledging…" : "Acknowledge"}
      </button>
      {error && (
        <p role="alert" className="text-xs text-red-600">
          {error}
        </p>
      )}
    </div>
  );
}
