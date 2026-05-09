"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";

export interface SelfIncidentFormProps {
  playerId: string;
}

const SEVERITY_OPTIONS: { value: number; label: string; hint: string }[] = [
  { value: 0, label: "Low", hint: "Niggle, soreness, manageable" },
  { value: 1, label: "Medium", hint: "Limits training; needs attention" },
];

/**
 * Lets a player file a self-reported injury or welfare incident. High
 * severity is intentionally not offered here — anything that serious must
 * go through the coach so triage isn't delegated to a form. The summary
 * field is short and headline-only; raw notes (private to the player) are
 * optional.
 */
export function SelfIncidentForm({ playerId }: SelfIncidentFormProps) {
  const router = useRouter();
  const [severity, setSeverity] = useState<number | "">("");
  const [summary, setSummary] = useState("");
  const [notes, setNotes] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (severity === "" || summary.trim().length === 0) {
      setError("Pick a severity and add a short summary.");
      return;
    }
    setBusy(true);
    setError(null);
    setSuccess(false);
    try {
      const res = await fetch(`/api/proxy/me/players/${playerId}/incidents`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          severity,
          summary: summary.trim(),
          notes: notes.trim() ? notes.trim() : null,
        }),
      });
      if (!res.ok) {
        setError("Could not log incident. Please try again or contact your coach.");
        return;
      }
      setSeverity("");
      setSummary("");
      setNotes("");
      setSuccess(true);
      router.refresh();
    } catch {
      setError("Network error. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={onSubmit} className="space-y-3" aria-label="Report an injury">
      <fieldset className="space-y-1">
        <legend className="text-sm font-heading text-deep-charcoal">
          How serious?
        </legend>
        <div className="flex flex-wrap gap-2">
          {SEVERITY_OPTIONS.map((opt) => (
            <label
              key={opt.value}
              className={`cursor-pointer rounded-card border px-3 py-2 text-xs ${
                severity === opt.value
                  ? "border-forge-navy bg-forge-navy/5 text-forge-navy"
                  : "border-slate/20 text-slate"
              }`}
            >
              <input
                type="radio"
                name="severity"
                value={opt.value}
                checked={severity === opt.value}
                onChange={() => setSeverity(opt.value)}
                className="sr-only"
              />
              <span className="font-medium">{opt.label}</span>
              <span className="block text-[11px] text-slate">{opt.hint}</span>
            </label>
          ))}
        </div>
        <p className="text-[11px] text-slate">
          For anything more serious, contact your coach directly — not via
          this form.
        </p>
      </fieldset>

      <label className="block space-y-1">
        <span className="text-sm font-heading text-deep-charcoal">
          What happened?
        </span>
        <input
          type="text"
          value={summary}
          onChange={(e) => setSummary(e.target.value)}
          maxLength={280}
          required
          className="w-full rounded-xl border border-slate/20 px-3 py-2 text-sm"
          placeholder="Tight hamstring after sprints"
        />
      </label>

      <label className="block space-y-1">
        <span className="text-sm font-heading text-deep-charcoal">
          Notes <span className="text-xs text-slate">(optional)</span>
        </span>
        <textarea
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          maxLength={4000}
          rows={3}
          className="w-full rounded-xl border border-slate/20 px-3 py-2 text-sm"
        />
      </label>

      {error && (
        <p role="alert" className="text-sm text-readiness-recovery">
          {error}
        </p>
      )}
      {success && (
        <p role="status" className="text-sm text-readiness-ready">
          Logged. Your coach will see this.
        </p>
      )}

      <button
        type="submit"
        disabled={busy}
        className="rounded-xl bg-forge-navy px-4 py-2 text-sm text-white font-heading disabled:opacity-60"
      >
        {busy ? "Logging…" : "Log incident"}
      </button>
    </form>
  );
}
