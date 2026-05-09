"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";

/**
 * Lets a player paste a player invite code shared by their coach to claim
 * their roster profile. On success refreshes the parent so the linked
 * player appears in the list.
 */
export function ClaimPlayerForm() {
  const router = useRouter();
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const trimmed = code.trim();
    if (trimmed.length < 6) {
      setError("Paste the full invite code your coach shared.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await fetch("/api/proxy/player-invites/redeem", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ code: trimmed }),
      });
      if (!res.ok) {
        setError(await humanise(res));
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
      aria-label="Claim player profile with invite code"
      className="grid grid-cols-1 sm:grid-cols-[1fr_auto] gap-3 items-end"
    >
      <div>
        <label
          htmlFor="claim-code"
          className="block text-xs font-medium text-slate"
        >
          Player invite code
        </label>
        <input
          id="claim-code"
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
        {busy ? "Claiming…" : "Claim profile"}
      </button>
      {error && (
        <p role="alert" className="sm:col-span-2 text-sm text-readiness-recovery">
          {error}
        </p>
      )}
    </form>
  );
}

async function humanise(res: Response): Promise<string> {
  if (res.status === 401) return "Please sign in again.";
  if (res.status === 404) return "That invite code isn't recognised.";
  if (res.status === 409) {
    try {
      const body = (await res.json()) as { error?: unknown };
      if (body.error === "invite_expired")
        return "That invite code has expired. Ask your coach for a fresh one.";
      if (body.error === "invite_revoked") return "That invite code was revoked.";
      if (body.error === "invite_consumed")
        return "That invite code has already been used.";
    } catch {
      /* ignore */
    }
    return "That invite code can't be redeemed.";
  }
  return "Could not claim that profile. Please try again.";
}
