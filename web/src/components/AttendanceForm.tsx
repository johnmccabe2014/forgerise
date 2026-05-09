"use client";

import { useState, type FormEvent } from "react";
import {
  ATTENDANCE_STATUS_OPTIONS,
  type AttendanceStatusValue,
} from "@/lib/sessionLabels";

export interface AttendanceRow {
  playerId: string;
  playerDisplayName: string;
  status: number;
  note: string | null;
  recordedAt: string | null;
}

interface AttendanceFormProps {
  teamId: string;
  sessionId: string;
  initialRows: AttendanceRow[];
}

export function AttendanceForm({
  teamId,
  sessionId,
  initialRows,
}: AttendanceFormProps) {
  const [rows, setRows] = useState<AttendanceRow[]>(initialRows);
  const [error, setError] = useState<string | null>(null);
  const [savedAt, setSavedAt] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function setStatus(playerId: string, status: AttendanceStatusValue) {
    setRows((prev) =>
      prev.map((r) => (r.playerId === playerId ? { ...r, status } : r)),
    );
  }
  function setNote(playerId: string, note: string) {
    setRows((prev) =>
      prev.map((r) => (r.playerId === playerId ? { ...r, note } : r)),
    );
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSavedAt(null);
    setBusy(true);
    try {
      const res = await fetch(
        `/api/proxy/teams/${teamId}/sessions/${sessionId}/attendance`,
        {
          method: "PUT",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({
            items: rows.map((r) => ({
              playerId: r.playerId,
              status: r.status,
              note: r.note?.trim() ? r.note.trim() : null,
            })),
          }),
        },
      );
      if (!res.ok) {
        setError(await humaniseProblem(res));
        return;
      }
      const updated = (await res.json()) as AttendanceRow[];
      setRows(updated);
      setSavedAt(new Date().toLocaleTimeString());
    } catch {
      setError("Network error. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  if (rows.length === 0) {
    return (
      <p className="rounded-card bg-white p-6 shadow-soft text-slate">
        Add players to the team before recording attendance.
      </p>
    );
  }

  return (
    <form
      onSubmit={onSubmit}
      aria-label="Record attendance"
      className="space-y-4"
    >
      <ul className="space-y-2">
        {rows.map((r) => (
          <li
            key={r.playerId}
            className="rounded-card bg-white p-4 shadow-soft space-y-3"
          >
            <div className="flex items-center justify-between gap-3">
              <p className="font-medium text-deep-charcoal">
                {r.playerDisplayName}
              </p>
              <label
                htmlFor={`status-${r.playerId}`}
                className="sr-only"
              >
                Status for {r.playerDisplayName}
              </label>
              <select
                id={`status-${r.playerId}`}
                aria-label={`Status for ${r.playerDisplayName}`}
                value={r.status}
                onChange={(e) =>
                  setStatus(
                    r.playerId,
                    Number(e.target.value) as AttendanceStatusValue,
                  )
                }
                className="rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
              >
                {ATTENDANCE_STATUS_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </div>
            <input
              type="text"
              aria-label={`Note for ${r.playerDisplayName}`}
              placeholder="Optional note"
              value={r.note ?? ""}
              onChange={(e) => setNote(r.playerId, e.target.value)}
              maxLength={500}
              className="w-full rounded-xl border border-slate/20 bg-mist-grey px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-rise-copper"
            />
          </li>
        ))}
      </ul>

      {error && (
        <p role="alert" className="text-sm text-readiness-recovery">
          {error}
        </p>
      )}
      {savedAt && (
        <p
          role="status"
          aria-live="polite"
          className="text-sm text-readiness-ready"
        >
          Saved at {savedAt}.
        </p>
      )}

      <button
        type="submit"
        disabled={busy}
        className="w-full sm:w-auto rounded-xl bg-forge-navy px-6 py-3 text-white font-heading disabled:opacity-60"
      >
        {busy ? "Saving…" : "Save attendance"}
      </button>
    </form>
  );
}

async function humaniseProblem(res: Response): Promise<string> {
  if (res.status === 401) return "Please sign in again.";
  if (res.status === 403) return "You don't have access to this session.";
  try {
    const body = (await res.json()) as { title?: unknown };
    if (typeof body.title === "string" && body.title.length < 200) return body.title;
  } catch {
    /* ignore */
  }
  return "Could not save attendance. Please try again.";
}
