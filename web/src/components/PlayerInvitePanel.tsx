"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

export interface PlayerInviteRow {
  id: string;
  code: string;
  createdAt: string;
  expiresAt: string;
  consumedAt: string | null;
  revokedAt: string | null;
  requiresGuardianConsent: boolean;
  guardianConsentAcknowledged: boolean;
}

export interface PlayerInvitePanelProps {
  teamId: string;
  playerId: string;
  /** Used to detect under-16 players who require guardian consent. */
  playerBirthYear: number | null;
  invites: PlayerInviteRow[];
}

function status(i: PlayerInviteRow): { label: string; tone: string } {
  if (i.revokedAt) return { label: "Revoked", tone: "text-slate" };
  if (i.consumedAt) return { label: "Claimed", tone: "text-readiness-ready" };
  if (new Date(i.expiresAt) <= new Date())
    return { label: "Expired", tone: "text-slate" };
  return { label: "Active", tone: "text-rise-copper" };
}

function fmt(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    day: "2-digit",
    month: "short",
    year: "numeric",
  });
}

/**
 * Coach-side widget on the player profile to issue, share, and revoke
 * single-use invite codes the player can redeem at /me to claim their
 * roster spot.
 */
export function PlayerInvitePanel({
  teamId,
  playerId,
  playerBirthYear,
  invites,
}: PlayerInvitePanelProps) {
  const router = useRouter();
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [guardianAck, setGuardianAck] = useState(false);

  const isMinor =
    playerBirthYear !== null &&
    new Date().getFullYear() - playerBirthYear < 16;

  async function create() {
    if (isMinor && !guardianAck) {
      setError(
        "Confirm guardian consent before issuing an invite for an under-16 player.",
      );
      return;
    }
    setBusy("create");
    setError(null);
    try {
      const res = await fetch(
        `/api/proxy/teams/${teamId}/players/${playerId}/invites`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ guardianConsentAcknowledged: isMinor && guardianAck }),
        },
      );
      if (!res.ok) {
        setError("Could not create invite. Please try again.");
        return;
      }
      setGuardianAck(false);
      router.refresh();
    } catch {
      setError("Network error. Please try again.");
    } finally {
      setBusy(null);
    }
  }

  async function revoke(id: string) {
    setBusy(id);
    setError(null);
    try {
      const res = await fetch(
        `/api/proxy/teams/${teamId}/players/${playerId}/invites/${id}`,
        { method: "DELETE" },
      );
      if (!res.ok) {
        setError("Could not revoke invite. Please try again.");
        return;
      }
      router.refresh();
    } catch {
      setError("Network error. Please try again.");
    } finally {
      setBusy(null);
    }
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <p className="text-xs text-slate">
          Single-use codes that let the player link their account.
        </p>
        <button
          type="button"
          onClick={create}
          disabled={busy === "create" || (isMinor && !guardianAck)}
          className="rounded-xl bg-forge-navy px-3 py-1.5 text-sm text-white font-heading disabled:opacity-60"
        >
          {busy === "create" ? "Creating…" : "+ New invite"}
        </button>
      </div>

      {isMinor && (
        <label className="flex items-start gap-2 rounded-card border border-rise-copper/30 bg-rise-copper/5 p-3 text-xs text-deep-charcoal">
          <input
            type="checkbox"
            checked={guardianAck}
            onChange={(e) => setGuardianAck(e.target.checked)}
            className="mt-0.5"
            aria-label="I confirm I have guardian consent for this under-16 player"
          />
          <span>
            This player is under 16. I confirm I have explicit guardian
            consent before issuing a self-service invite.
          </span>
        </label>
      )}

      {error && (
        <p role="alert" className="text-sm text-readiness-recovery">
          {error}
        </p>
      )}

      {invites.length === 0 ? (
        <p className="text-sm text-slate">No invite codes issued yet.</p>
      ) : (
        <ul className="divide-y divide-slate/10">
          {invites.map((i) => {
            const s = status(i);
            const isActive = s.label === "Active";
            return (
              <li
                key={i.id}
                className="flex items-center justify-between gap-3 py-2"
              >
                <div className="min-w-0">
                  <p className="font-mono text-sm text-deep-charcoal truncate">
                    {i.code}
                  </p>
                  <p className="text-xs text-slate">
                    issued {fmt(i.createdAt)} · expires {fmt(i.expiresAt)}
                  </p>
                </div>
                <div className="flex shrink-0 items-center gap-3">
                  <span className={`text-xs font-medium ${s.tone}`}>
                    {s.label}
                  </span>
                  {isActive && (
                    <button
                      type="button"
                      onClick={() => revoke(i.id)}
                      disabled={busy === i.id}
                      className="text-xs text-slate underline disabled:opacity-60"
                    >
                      {busy === i.id ? "Revoking…" : "Revoke"}
                    </button>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
