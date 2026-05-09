"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { SESSION_TYPE } from "@/lib/sessionLabels";

interface CreatedSession {
  id: string;
}

export function SessionCreateForm({ teamId }: { teamId: string }) {
  const router = useRouter();
  // Default the date input to today (local) and time to 18:00 — most grassroots
  // training is evenings — so the coach typically just confirms.
  const today = new Date();
  const yyyy = today.getFullYear();
  const mm = String(today.getMonth() + 1).padStart(2, "0");
  const dd = String(today.getDate()).padStart(2, "0");

  const [date, setDate] = useState(`${yyyy}-${mm}-${dd}`);
  const [time, setTime] = useState("18:00");
  const [duration, setDuration] = useState("60");
  const [type, setType] = useState<number>(SESSION_TYPE.Training);
  const [location, setLocation] = useState("");
  const [focus, setFocus] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);

    if (!date || !time) {
      setError("Pick a date and time for the session.");
      return;
    }
    const durationNum = Number.parseInt(duration, 10);
    if (!Number.isFinite(durationNum) || durationNum < 5 || durationNum > 480) {
      setError("Duration must be between 5 and 480 minutes.");
      return;
    }
    // Compose a local-time ISO string — the browser produces a Date in the
    // user's tz, which we then send as a UTC ISO string the API can parse.
    const local = new Date(`${date}T${time}:00`);
    if (Number.isNaN(local.getTime())) {
      setError("That date and time don't look right.");
      return;
    }

    setBusy(true);
    try {
      const res = await fetch(`/api/proxy/teams/${teamId}/sessions`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          scheduledAt: local.toISOString(),
          durationMinutes: durationNum,
          type,
          location: location.trim() || null,
          focus: focus.trim() || null,
        }),
      });
      if (!res.ok) {
        setError(await humaniseProblem(res));
        return;
      }
      const created = (await res.json()) as CreatedSession;
      router.replace(`/teams/${teamId}/sessions/${created.id}/attendance`);
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
      aria-label="Create session"
      className="w-full max-w-md space-y-4"
    >
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label
            htmlFor="session-date"
            className="block text-sm font-medium text-slate"
          >
            Date
          </label>
          <input
            id="session-date"
            type="date"
            required
            value={date}
            onChange={(e) => setDate(e.target.value)}
            className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
          />
        </div>
        <div>
          <label
            htmlFor="session-time"
            className="block text-sm font-medium text-slate"
          >
            Time
          </label>
          <input
            id="session-time"
            type="time"
            required
            value={time}
            onChange={(e) => setTime(e.target.value)}
            className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
          />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div>
          <label
            htmlFor="session-duration"
            className="block text-sm font-medium text-slate"
          >
            Duration (min)
          </label>
          <input
            id="session-duration"
            type="number"
            min={5}
            max={480}
            required
            value={duration}
            onChange={(e) => setDuration(e.target.value)}
            className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
          />
        </div>
        <div>
          <label
            htmlFor="session-type"
            className="block text-sm font-medium text-slate"
          >
            Type
          </label>
          <select
            id="session-type"
            value={type}
            onChange={(e) => setType(Number(e.target.value))}
            className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
          >
            <option value={SESSION_TYPE.Training}>Training</option>
            <option value={SESSION_TYPE.Match}>Match</option>
            <option value={SESSION_TYPE.Other}>Other</option>
          </select>
        </div>
      </div>

      <div>
        <label
          htmlFor="session-location"
          className="block text-sm font-medium text-slate"
        >
          Location <span className="text-slate/60">(optional)</span>
        </label>
        <input
          id="session-location"
          value={location}
          onChange={(e) => setLocation(e.target.value)}
          maxLength={120}
          className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
        />
      </div>

      <div>
        <label
          htmlFor="session-focus"
          className="block text-sm font-medium text-slate"
        >
          Focus <span className="text-slate/60">(optional)</span>
        </label>
        <input
          id="session-focus"
          value={focus}
          onChange={(e) => setFocus(e.target.value)}
          maxLength={200}
          placeholder="e.g. Defensive line speed"
          className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
        />
      </div>

      {error && (
        <p role="alert" className="text-sm text-readiness-recovery">
          {error}
        </p>
      )}

      <button
        type="submit"
        disabled={busy}
        className="w-full rounded-xl bg-forge-navy py-3 text-white font-heading disabled:opacity-60"
      >
        {busy ? "Saving…" : "Schedule session"}
      </button>
    </form>
  );
}

async function humaniseProblem(res: Response): Promise<string> {
  if (res.status === 401) return "Please sign in again.";
  if (res.status === 403) return "You don't have access to this team.";
  try {
    const body = (await res.json()) as { title?: unknown };
    if (typeof body.title === "string" && body.title.length < 200) return body.title;
  } catch {
    /* ignore */
  }
  return "Could not save the session. Please try again.";
}
