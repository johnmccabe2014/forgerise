"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

export interface InviteRow {
  id: string;
  code: string;
  createdAt: string;
  expiresAt: string;
  consumedAt: string | null;
  revokedAt: string | null;
}

export interface InvitesPanelProps {
  teamId: string;
  invites: InviteRow[];
}

type Status = "active" | "expired" | "consumed" | "revoked";

function statusOf(i: InviteRow): Status {
  if (i.revokedAt) return "revoked";
  if (i.consumedAt) return "consumed";
  if (new Date(i.expiresAt).getTime() <= Date.now()) return "expired";
  return "active";
}

/**
 * Owner-only panel. Generates new invite codes and shows their lifecycle.
 * Active codes can be revoked; consumed/expired codes are kept for audit.
 */
export function InvitesPanel({ teamId, invites }: InvitesPanelProps) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copiedCode, setCopiedCode] = useState<string | null>(null);

  async function generate() {
    setError(null);
    setBusy(true);
    try {
      const res = await fetch(`/api/proxy/teams/${teamId}/invites`, {
        method: "POST",
        headers: { "content-type": "application/json" },
      });
      if (!res.ok) {
        setError("Could not generate an invite. Please try again.");
        return;
      }
      router.refresh();
    } catch {
      setError("Network error. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  async function revoke(id: string) {
    if (!window.confirm("Revoke this invite code? It can't be reused.")) return;
    setError(null);
    setBusyId(id);
    try {
      const res = await fetch(`/api/proxy/teams/${teamId}/invites/${id}`, {
        method: "DELETE",
      });
      if (!res.ok && res.status !== 204) {
        setError("Could not revoke. Please try again.");
        return;
      }
      router.refresh();
    } catch {
      setError("Network error. Please try again.");
    } finally {
      setBusyId(null);
    }
  }

  async function copy(code: string) {
    try {
      await navigator.clipboard.writeText(code);
      setCopiedCode(code);
      setTimeout(() => setCopiedCode((c) => (c === code ? null : c)), 1500);
    } catch {
      // clipboard may be blocked — caller can select+copy manually
    }
  }

  return (
    <div className="space-y-3">
      <button
        type="button"
        onClick={generate}
        disabled={busy}
        className="rounded-xl bg-rise-copper px-4 py-2 text-white font-heading disabled:opacity-60"
      >
        {busy ? "Generating…" : "Generate invite code"}
      </button>
      {error && (
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      )}
      {invites.length === 0 ? (
        <p className="text-sm text-slate">No invites issued yet.</p>
      ) : (
        <ul className="space-y-2" aria-label="Invite codes">
          {invites.map((i) => {
            const status = statusOf(i);
            return (
              <li
                key={i.id}
                className="flex items-center justify-between gap-3 rounded-card bg-white p-3 shadow-soft"
              >
                <div className="min-w-0">
                  <p className="font-mono text-sm text-deep-charcoal truncate">
                    {i.code}
                  </p>
                  <p className="text-xs text-slate">
                    {status === "active" && (
                      <>expires {new Date(i.expiresAt).toLocaleDateString()}</>
                    )}
                    {status === "expired" && <>expired</>}
                    {status === "consumed" && i.consumedAt && (
                      <>used {new Date(i.consumedAt).toLocaleDateString()}</>
                    )}
                    {status === "revoked" && <>revoked</>}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  {status === "active" && (
                    <>
                      <button
                        type="button"
                        onClick={() => copy(i.code)}
                        className="text-sm text-forge-navy hover:underline"
                      >
                        {copiedCode === i.code ? "Copied!" : "Copy"}
                      </button>
                      <button
                        type="button"
                        onClick={() => revoke(i.id)}
                        disabled={busyId === i.id}
                        className="text-sm text-red-600 hover:underline disabled:opacity-60"
                      >
                        {busyId === i.id ? "Revoking…" : "Revoke"}
                      </button>
                    </>
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
