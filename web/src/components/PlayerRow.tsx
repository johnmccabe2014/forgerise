"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

export interface PlayerRowProps {
  teamId: string;
  player: {
    id: string;
    displayName: string;
    jerseyNumber: number | null;
    position: string | null;
  };
}

export function PlayerRow({ teamId, player }: PlayerRowProps) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onRemove() {
    if (!confirm(`Remove ${player.displayName} from the roster?`)) return;
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/api/proxy/teams/${teamId}/players/${player.id}`, {
        method: "DELETE",
      });
      if (!res.ok && res.status !== 204) {
        setError("Could not remove player.");
        return;
      }
      router.refresh();
    } finally {
      setBusy(false);
    }
  }

  return (
    <li className="rounded-card bg-white p-4 shadow-soft flex items-center gap-4">
      <span
        aria-hidden
        className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-mist-grey text-sm font-heading text-forge-navy"
      >
        {player.jerseyNumber ?? "—"}
      </span>
      <div className="flex-1 min-w-0">
        <p className="font-heading text-forge-navy truncate">{player.displayName}</p>
        {player.position && (
          <p className="text-xs text-slate truncate">{player.position}</p>
        )}
        {error && (
          <p role="alert" className="text-xs text-readiness-recovery">
            {error}
          </p>
        )}
      </div>
      <button
        type="button"
        onClick={onRemove}
        disabled={busy}
        aria-label={`Remove ${player.displayName}`}
        className="text-sm text-slate hover:text-readiness-recovery disabled:opacity-60"
      >
        {busy ? "…" : "Remove"}
      </button>
    </li>
  );
}
