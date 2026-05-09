"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import {
  RUGBY_POSITIONS,
  POSITION_NAMES,
  JERSEY_NUMBERS,
} from "@/lib/rugby";

export interface PlayerAddFormProps {
  teamId: string;
}

const FORWARD_POSITIONS = POSITION_NAMES.filter((name) =>
  RUGBY_POSITIONS.some((p) => p.name === name && p.group === "forward"),
);
const BACK_POSITIONS = POSITION_NAMES.filter((name) =>
  RUGBY_POSITIONS.some((p) => p.name === name && p.group === "back"),
);

export function PlayerAddForm({ teamId }: PlayerAddFormProps) {
  const router = useRouter();
  const [displayName, setDisplayName] = useState("");
  const [jersey, setJersey] = useState("");
  const [position, setPosition] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    if (!displayName.trim()) {
      setError("Player name is required.");
      return;
    }
    setBusy(true);
    try {
      const payload: Record<string, unknown> = { displayName: displayName.trim() };
      if (jersey.trim() !== "") {
        const n = Number(jersey);
        if (!Number.isInteger(n) || n < 0 || n > 999) {
          setError("Jersey number must be 0–999.");
          setBusy(false);
          return;
        }
        payload.jerseyNumber = n;
      }
      if (position.trim() !== "") payload.position = position.trim();

      const res = await fetch(`/api/proxy/teams/${teamId}/players`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(payload),
      });
      if (!res.ok) {
        setError(await humaniseProblem(res));
        return;
      }
      setDisplayName("");
      setJersey("");
      setPosition("");
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
      aria-label="Add player"
      className="grid grid-cols-1 sm:grid-cols-[1fr_auto_auto_auto] gap-3 items-end"
    >
      <div>
        <label htmlFor="p-name" className="block text-xs font-medium text-slate">
          Name
        </label>
        <input
          id="p-name"
          required
          value={displayName}
          onChange={(e) => setDisplayName(e.target.value)}
          maxLength={120}
          className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
        />
      </div>
      <div className="w-24">
        <label htmlFor="p-jersey" className="block text-xs font-medium text-slate">
          #
        </label>
        <select
          id="p-jersey"
          value={jersey}
          onChange={(e) => setJersey(e.target.value)}
          className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 text-deep-charcoal focus:outline-none focus:ring-2 focus:ring-rise-copper"
        >
          <option value="">—</option>
          {JERSEY_NUMBERS.map((n) => (
            <option key={n} value={n}>
              {n}
            </option>
          ))}
        </select>
      </div>
      <div className="w-56">
        <label htmlFor="p-pos" className="block text-xs font-medium text-slate">
          Position
        </label>
        <select
          id="p-pos"
          value={position}
          onChange={(e) => setPosition(e.target.value)}
          className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 text-deep-charcoal focus:outline-none focus:ring-2 focus:ring-rise-copper"
        >
          <option value="">—</option>
          <optgroup label="Forwards">
            {FORWARD_POSITIONS.map((name) => (
              <option key={`f-${name}`} value={name}>
                {name}
              </option>
            ))}
          </optgroup>
          <optgroup label="Backs">
            {BACK_POSITIONS.map((name) => (
              <option key={`b-${name}`} value={name}>
                {name}
              </option>
            ))}
          </optgroup>
        </select>
      </div>
      <button
        type="submit"
        disabled={busy}
        className="rounded-xl bg-forge-navy px-4 py-2 text-white font-heading disabled:opacity-60"
      >
        {busy ? "Adding…" : "Add"}
      </button>

      {error && (
        <p role="alert" className="sm:col-span-4 text-sm text-readiness-recovery">
          {error}
        </p>
      )}
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
  return "Could not add the player. Please try again.";
}
