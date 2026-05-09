"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import {
  READINESS_LABELS,
  type ReadinessCategory,
} from "@/types/welfare";

interface SubmittedCheckIn {
  id: string;
  category: number;
  submittedBySelf: boolean;
}

const CATEGORY_TO_KEY: Record<number, ReadinessCategory> = {
  0: "ready",
  1: "monitor",
  2: "modify",
  3: "recovery",
};

const SCALE = [1, 2, 3, 4, 5] as const;

export interface SelfCheckInFormProps {
  playerId: string;
}

/**
 * Lets a player submit their own daily wellness check-in. Raw scores stay on
 * the server; only the derived readiness category is shown back to the
 * player on success.
 */
export function SelfCheckInForm({ playerId }: SelfCheckInFormProps) {
  const router = useRouter();
  const [sleepHours, setSleepHours] = useState("");
  const [soreness, setSoreness] = useState<number | "">("");
  const [mood, setMood] = useState<number | "">("");
  const [stress, setStress] = useState<number | "">("");
  const [fatigue, setFatigue] = useState<number | "">("");
  const [notes, setNotes] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ReadinessCategory | null>(null);

  function reset() {
    setSleepHours("");
    setSoreness("");
    setMood("");
    setStress("");
    setFatigue("");
    setNotes("");
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setResult(null);
    try {
      const body: Record<string, unknown> = {};
      if (sleepHours) body.sleepHours = Number(sleepHours);
      if (soreness !== "") body.sorenessScore = soreness;
      if (mood !== "") body.moodScore = mood;
      if (stress !== "") body.stressScore = stress;
      if (fatigue !== "") body.fatigueScore = fatigue;
      if (notes.trim()) body.injuryNotes = notes.trim();

      const res = await fetch(`/api/proxy/me/players/${playerId}/checkins`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) {
        if (res.status === 401) setError("Please sign in again.");
        else if (res.status === 403)
          setError("You don't have access to that profile.");
        else setError("Could not submit your check-in. Please try again.");
        return;
      }
      const saved = (await res.json()) as SubmittedCheckIn;
      const key = CATEGORY_TO_KEY[saved.category] ?? "monitor";
      setResult(key);
      reset();
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
      aria-label="Submit self check-in"
      className="space-y-4"
    >
      <div>
        <label
          htmlFor={`sleep-${playerId}`}
          className="block text-sm font-medium text-slate"
        >
          Sleep last night (hours)
        </label>
        <input
          id={`sleep-${playerId}`}
          type="number"
          inputMode="decimal"
          min={0}
          max={24}
          step={0.5}
          value={sleepHours}
          onChange={(e) => setSleepHours(e.target.value)}
          className="mt-1 w-32 rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
        />
      </div>

      {(
        [
          ["soreness", "Soreness", soreness, setSoreness],
          ["mood", "Mood", mood, setMood],
          ["stress", "Stress", stress, setStress],
          ["fatigue", "Fatigue", fatigue, setFatigue],
        ] as const
      ).map(([key, label, value, setter]) => (
        <fieldset key={key}>
          <legend className="text-sm font-medium text-slate">
            {label} <span className="text-slate/60">(1 = best, 5 = worst)</span>
          </legend>
          <div role="radiogroup" className="mt-1 flex gap-2">
            {SCALE.map((n) => {
              const checked = value === n;
              return (
                <label
                  key={n}
                  className={`cursor-pointer rounded-xl border px-3 py-2 text-sm ${
                    checked
                      ? "border-forge-navy bg-forge-navy text-white"
                      : "border-slate/30 bg-white text-deep-charcoal"
                  }`}
                >
                  <input
                    type="radio"
                    name={`${key}-${playerId}`}
                    value={n}
                    checked={checked}
                    onChange={() => setter(n)}
                    className="sr-only"
                  />
                  {n}
                </label>
              );
            })}
          </div>
        </fieldset>
      ))}

      <div>
        <label
          htmlFor={`notes-${playerId}`}
          className="block text-sm font-medium text-slate"
        >
          Notes / niggles{" "}
          <span className="text-slate/60">(optional, only your coach sees these)</span>
        </label>
        <textarea
          id={`notes-${playerId}`}
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          maxLength={2000}
          rows={3}
          className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
        />
      </div>

      {error && (
        <p role="alert" className="text-sm text-readiness-recovery">
          {error}
        </p>
      )}
      {result && (
        <p
          role="status"
          className="rounded-card bg-mist-grey p-3 text-sm text-deep-charcoal"
        >
          Submitted. Today&apos;s readiness:{" "}
          <span className="font-medium">{READINESS_LABELS[result]}</span>.
        </p>
      )}

      <button
        type="submit"
        disabled={busy}
        className="rounded-xl bg-forge-navy px-4 py-2 text-white font-heading shadow-soft hover:bg-forge-navy/90 disabled:opacity-60"
      >
        {busy ? "Submitting…" : "Submit check-in"}
      </button>
    </form>
  );
}
