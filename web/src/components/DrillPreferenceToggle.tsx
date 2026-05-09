"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";

type Status = "favourite" | "exclude" | null;

interface Props {
  teamId: string;
  drillId: string;
  current: Status;
}

/**
 * Per-drill toggle. Three states: neutral (no row), favourite, exclude.
 * Cycles via PUT/DELETE on the team-scoped preferences endpoint and
 * refreshes the page so the recommender sees the new pool next time.
 */
export function DrillPreferenceToggle({ teamId, drillId, current }: Props) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  async function setStatus(next: Status) {
    setError(null);
    try {
      const res = next === null
        ? await fetch(`/api/proxy/teams/${teamId}/drill-preferences/${drillId}`, {
            method: "DELETE",
          })
        : await fetch(`/api/proxy/teams/${teamId}/drill-preferences/${drillId}`, {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ status: next }),
          });
      if (!res.ok) {
        setError("Could not save.");
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Network error.");
    }
  }

  function buttonClass(active: boolean) {
    return [
      "rounded-pill px-3 py-1.5 text-xs font-medium transition-colors",
      active
        ? "bg-forge-navy text-white"
        : "bg-white border border-slate/30 text-deep-charcoal hover:bg-mist-grey",
      pending ? "opacity-60" : "",
    ].join(" ");
  }

  return (
    <div className="flex flex-col items-end gap-1">
      <div className="flex items-center gap-2" data-testid={`pref-toggle-${drillId}`}>
        <button
          type="button"
          aria-label={`Mark ${drillId} as favourite`}
          aria-pressed={current === "favourite"}
          disabled={pending}
          onClick={() => setStatus(current === "favourite" ? null : "favourite")}
          className={buttonClass(current === "favourite")}
        >
          ★ Favourite
        </button>
        <button
          type="button"
          aria-label={`Exclude ${drillId} from recommendations`}
          aria-pressed={current === "exclude"}
          disabled={pending}
          onClick={() => setStatus(current === "exclude" ? null : "exclude")}
          className={buttonClass(current === "exclude")}
        >
          ✕ Exclude
        </button>
      </div>
      {error && (
        <p role="alert" className="text-xs text-red-600">
          {error}
        </p>
      )}
    </div>
  );
}
