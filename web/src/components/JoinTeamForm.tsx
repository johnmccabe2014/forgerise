"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";

/**
 * Lets a logged-in coach paste an invite code to join an existing team.
 * On success it refreshes the parent server component so the team list updates.
 */
export function JoinTeamForm() {
  const router = useRouter();
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    const trimmed = code.trim();
    if (trimmed.length < 6) {
      setError("Paste the full invite code your team owner shared.");
      return;
    }
    setBusy(true);
    try {
      const res = await fetch("/api/proxy/teams/join", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ code: trimmed }),
      });
      if (!res.ok) {
        setError(await humaniseJoinError(res));
        return;
      }
      setCode("");
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
      aria-label="Join team with invite code"
      className="grid grid-cols-1 sm:grid-cols-[1fr_auto] gap-3 items-end"
    >
      <div>
        <label
          htmlFor="join-code"
          className="block text-xs font-medium text-slate"
        >
          Invite code
        </label>
        <input
          id="join-code"
          value={code}
          onChange={(e) => setCode(e.target.value)}
          maxLength={64}
          placeholder="paste invite code"
          className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
        />
      </div>
      <button
        type="submit"
        disabled={busy}
        className="rounded-xl bg-forge-navy px-4 py-2 text-white font-heading disabled:opacity-60"
      >
        {busy ? "Joining…" : "Join team"}
      </button>
      {error && (
        <p role="alert" className="sm:col-span-2 text-sm text-red-600">
          {error}
        </p>
      )}
    </form>
  );
}

async function humaniseJoinError(res: Response): Promise<string> {
  if (res.status === 401) return "Please sign in again.";
  if (res.status === 404) return "That invite code isn't recognised.";
  if (res.status === 409) {
    try {
      const body = (await res.json()) as { error?: unknown };
      if (body.error === "invite_expired") return "That invite code has expired. Ask the owner for a fresh one.";
      if (body.error === "invite_revoked") return "That invite code was revoked.";
      if (body.error === "invite_consumed") return "That invite code has already been used.";
    } catch {
      /* ignore */
    }
    return "That invite code can't be used.";
  }
  return "Could not join. Please try again.";
}
