"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";

interface PinPlanButtonProps {
  teamId: string;
  planId: string;
  pinned: boolean;
}

export function PinPlanButton({ teamId, planId, pinned }: PinPlanButtonProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  async function onClick() {
    setError(null);
    const res = await fetch(
      `/api/proxy/teams/${teamId}/session-plans/${planId}/pin`,
      { method: "POST" },
    );
    if (!res.ok) {
      setError("Couldn’t toggle pin. Please try again.");
      return;
    }
    startTransition(() => router.refresh());
  }

  return (
    <span className="inline-flex items-center gap-2">
      <button
        type="button"
        onClick={onClick}
        disabled={isPending}
        data-testid="plan-pin-button"
        aria-pressed={pinned}
        className={`rounded-pill border px-3 py-1 text-xs font-medium transition ${
          pinned
            ? "border-rise-copper bg-rise-copper/10 text-rise-copper"
            : "border-slate/30 text-slate hover:bg-mist-grey"
        }`}
      >
        {pinned ? "★ Pinned" : "☆ Pin"}
      </button>
      {error && (
        <span data-testid="plan-pin-error" className="text-xs text-readiness-modify">
          {error}
        </span>
      )}
    </span>
  );
}
