"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

interface Props {
  teamId: string;
  focus: string;
}

/**
 * Regenerate a session plan with the same focus, picking up any drill
 * preference edits the coach has made since the original was created.
 * Posts to the same endpoint the Plans index uses; on success routes
 * to the new plan's detail page.
 */
export function RegeneratePlanButton({ teamId, focus }: Props) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onClick() {
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(
        `/api/proxy/teams/${teamId}/session-plans/generate`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ focus }),
        },
      );
      if (!res.ok) {
        setError("Could not regenerate.");
        return;
      }
      const dto = (await res.json()) as { id: string };
      router.push(`/teams/${teamId}/session-plans/${dto.id}`);
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
        {busy ? "Regenerating…" : "Regenerate with current preferences"}
      </button>
      {error && (
        <p role="alert" className="text-xs text-red-600">
          {error}
        </p>
      )}
    </div>
  );
}
