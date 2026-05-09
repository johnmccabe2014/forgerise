"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

interface Props {
  teamId: string;
  planId: string;
}

/**
 * Inline form to adopt a plan as a real Session. The plan's focus and
 * recommended-drills digest are copied onto the new session server-side;
 * here we only collect the schedule + location.
 */
export function AdoptPlanForm({ teamId, planId }: Props) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [scheduledAt, setScheduledAt] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    d.setHours(18, 0, 0, 0);
    return new Date(d.getTime() - d.getTimezoneOffset() * 60_000)
      .toISOString()
      .slice(0, 16);
  });
  const [durationMinutes, setDurationMinutes] = useState(75);
  const [location, setLocation] = useState("");

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(
        `/api/proxy/teams/${teamId}/session-plans/${planId}/adopt`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            scheduledAt: new Date(scheduledAt).toISOString(),
            durationMinutes,
            type: 0,
            location: location || null,
          }),
        },
      );
      if (!res.ok) {
        setError("Could not adopt plan.");
        return;
      }
      setOpen(false);
      router.refresh();
    } catch {
      setError("Network error.");
    } finally {
      setBusy(false);
    }
  }

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="rounded-pill bg-rise-copper px-3 py-1.5 text-xs font-medium text-white"
      >
        Mark as used →
      </button>
    );
  }

  return (
    <form
      onSubmit={onSubmit}
      data-testid="adopt-plan-form"
      className="rounded-card border border-slate/20 bg-white p-3 space-y-2"
    >
      <label className="block text-xs text-slate">
        When
        <input
          type="datetime-local"
          required
          value={scheduledAt}
          onChange={(e) => setScheduledAt(e.target.value)}
          className="mt-1 w-full rounded-card border border-slate/30 px-2 py-1 text-sm"
        />
      </label>
      <label className="block text-xs text-slate">
        Duration (minutes)
        <input
          type="number"
          min={5}
          max={480}
          value={durationMinutes}
          onChange={(e) => setDurationMinutes(Number(e.target.value))}
          className="mt-1 w-full rounded-card border border-slate/30 px-2 py-1 text-sm"
        />
      </label>
      <label className="block text-xs text-slate">
        Location (optional)
        <input
          type="text"
          maxLength={120}
          value={location}
          onChange={(e) => setLocation(e.target.value)}
          className="mt-1 w-full rounded-card border border-slate/30 px-2 py-1 text-sm"
        />
      </label>
      <div className="flex items-center gap-2 pt-1">
        <button
          type="submit"
          disabled={busy}
          className="rounded-pill bg-rise-copper px-3 py-1.5 text-xs font-medium text-white disabled:opacity-60"
        >
          {busy ? "Adopting…" : "Adopt as session"}
        </button>
        <button
          type="button"
          onClick={() => setOpen(false)}
          disabled={busy}
          className="rounded-pill border border-slate/30 px-3 py-1.5 text-xs text-slate"
        >
          Cancel
        </button>
      </div>
      {error && (
        <p role="alert" className="text-xs text-red-600">
          {error}
        </p>
      )}
    </form>
  );
}
