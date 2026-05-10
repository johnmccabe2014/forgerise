"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";

interface ArchivePlanButtonProps {
  teamId: string;
  planId: string;
  archived: boolean;
  /** When true the plan is adopted; archive is forbidden by the API. */
  adopted: boolean;
}

export function ArchivePlanButton({
  teamId,
  planId,
  archived,
  adopted,
}: ArchivePlanButtonProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  async function onClick() {
    setError(null);
    const res = await fetch(
      `/api/proxy/teams/${teamId}/session-plans/${planId}/archive`,
      { method: "POST" },
    );
    if (!res.ok) {
      // The API returns ValidationProblem 400 when an adopted plan is
      // archive-targeted. Surface its message to the coach without
      // leaking the JSON shape.
      let msg = "Couldn’t update archive state. Please try again.";
      try {
        const body = (await res.json()) as {
          errors?: Record<string, string[]>;
        };
        const planErr = body?.errors?.plan?.[0];
        if (planErr) msg = planErr;
      } catch {
        // ignore JSON parse errors — keep generic message.
      }
      setError(msg);
      return;
    }
    startTransition(() => router.refresh());
  }

  // Hide the affordance on adopted plans rather than showing a doomed
  // button — the rule "adopted = source of truth" is a property of the
  // plan, not a per-click error.
  if (adopted && !archived) return null;

  return (
    <span className="inline-flex items-center gap-2">
      <button
        type="button"
        onClick={onClick}
        disabled={isPending}
        data-testid="plan-archive-button"
        aria-pressed={archived}
        className={`rounded-pill border px-3 py-1 text-xs font-medium transition ${
          archived
            ? "border-readiness-ready bg-readiness-ready/10 text-readiness-ready"
            : "border-slate/30 text-slate hover:bg-mist-grey"
        }`}
      >
        {archived ? "Restore" : "Archive"}
      </button>
      {error && (
        <span
          data-testid="plan-archive-error"
          className="text-xs text-readiness-modify"
        >
          {error}
        </span>
      )}
    </span>
  );
}
