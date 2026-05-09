"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";

interface GeneratedPlan {
  id: string;
}

export interface GeneratePlanFormProps {
  teamId: string;
  /** When set, the new plan page is the navigation target after success. */
  redirectToDetail?: boolean;
}

/**
 * Lets a coach kick off a new session plan. The API picks the most recent
 * reviewed session as the basis when no override is provided, so the only
 * input here is an optional focus hint.
 */
export function GeneratePlanForm({
  teamId,
  redirectToDetail = true,
}: GeneratePlanFormProps) {
  const router = useRouter();
  const [focus, setFocus] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(
        `/api/proxy/teams/${teamId}/session-plans/generate`,
        {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({
            focus: focus.trim() || null,
          }),
        },
      );
      if (!res.ok) {
        if (res.status === 401) setError("Please sign in again.");
        else if (res.status === 403)
          setError("You don't have access to this team.");
        else setError("Could not generate a plan. Please try again.");
        return;
      }
      const created = (await res.json()) as GeneratedPlan;
      setFocus("");
      if (redirectToDetail) {
        router.replace(`/teams/${teamId}/session-plans/${created.id}`);
      }
      router.refresh();
    } catch {
      setError("Network error. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <form
      onSubmit={onSubmit}
      aria-label="Generate session plan"
      className="space-y-3"
    >
      <div>
        <label
          htmlFor="plan-focus"
          className="block text-sm font-medium text-slate"
        >
          Focus override <span className="text-slate/60">(optional)</span>
        </label>
        <input
          id="plan-focus"
          type="text"
          maxLength={200}
          value={focus}
          onChange={(e) => setFocus(e.target.value)}
          placeholder="Lineout pods, midfield D, …"
          className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
        />
        <p className="mt-1 text-xs text-slate/80">
          Leave blank to reuse the last reviewed session&apos;s focus.
        </p>
      </div>
      {error && (
        <p role="alert" className="text-sm text-readiness-recovery">
          {error}
        </p>
      )}
      <button
        type="submit"
        disabled={busy}
        className="rounded-xl bg-forge-navy px-4 py-2 text-white font-heading shadow-soft hover:bg-forge-navy/90 disabled:opacity-60"
      >
        {busy ? "Generating…" : "Generate plan"}
      </button>
    </form>
  );
}
