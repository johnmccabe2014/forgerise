"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

export interface CoachRow {
  userId: string;
  displayName: string;
  email: string;
  role: "owner" | "coach";
  joinedAt: string;
}

export interface CoachesPanelProps {
  teamId: string;
  myRole: "owner" | "coach";
  myUserId: string;
  coaches: CoachRow[];
}

/**
 * Renders the coaching staff for a team. Only the team Owner can remove
 * other coaches; the API also refuses to remove the last Owner.
 */
export function CoachesPanel({ teamId, myRole, myUserId, coaches }: CoachesPanelProps) {
  const router = useRouter();
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function remove(userId: string) {
    if (busyId) return;
    if (!window.confirm("Remove this coach from the team?")) return;
    setError(null);
    setBusyId(userId);
    try {
      const res = await fetch(`/api/proxy/teams/${teamId}/coaches/${userId}`, {
        method: "DELETE",
      });
      if (!res.ok) {
        if (res.status === 409) {
          setError("Can't remove the last owner of a team.");
        } else if (res.status === 403) {
          setError("Only the team owner can remove coaches.");
        } else {
          setError("Could not remove coach. Please try again.");
        }
        return;
      }
      router.refresh();
    } catch {
      setError("Network error. Please try again.");
    } finally {
      setBusyId(null);
    }
  }

  async function transferOwnership(userId: string, displayName: string) {
    if (busyId) return;
    if (
      !window.confirm(
        `Make ${displayName} the team owner? You will be demoted to a coach and ` +
          `they'll be able to manage invites, remove coaches, and delete the team.`,
      )
    )
      return;
    setError(null);
    setBusyId(userId);
    try {
      const res = await fetch(
        `/api/proxy/teams/${teamId}/coaches/${userId}/transfer-ownership`,
        { method: "POST" },
      );
      if (!res.ok) {
        if (res.status === 403) {
          setError("Only the team owner can transfer ownership.");
        } else if (res.status === 404) {
          setError("That coach is no longer on the team.");
        } else {
          setError("Could not transfer ownership. Please try again.");
        }
        return;
      }
      router.refresh();
    } catch {
      setError("Network error. Please try again.");
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="space-y-2">
      {coaches.length === 0 ? (
        <p className="text-sm text-slate">No coaches yet.</p>
      ) : (
        <ul className="space-y-2">
          {coaches.map((c) => {
            const isSelf = c.userId === myUserId;
            return (
              <li
                key={c.userId}
                className="flex items-center justify-between rounded-card bg-white p-3 shadow-soft"
              >
                <div>
                  <p className="font-medium text-deep-charcoal">
                    {c.displayName}
                    {isSelf && (
                      <span className="ml-2 text-xs text-slate">(you)</span>
                    )}
                  </p>
                  <p className="text-xs text-slate">
                    {c.email} ·{" "}
                    <span
                      className={
                        c.role === "owner"
                          ? "text-rise-copper font-medium"
                          : "text-slate"
                      }
                    >
                      {c.role === "owner" ? "Owner" : "Coach"}
                    </span>
                  </p>
                </div>
                {myRole === "owner" && !isSelf && (
                  <div className="flex items-center gap-3">
                    {c.role === "coach" && (
                      <button
                        type="button"
                        onClick={() => transferOwnership(c.userId, c.displayName)}
                        disabled={busyId === c.userId}
                        className="text-sm text-forge-navy hover:underline disabled:opacity-60"
                        aria-label={`Make ${c.displayName} the team owner`}
                      >
                        {busyId === c.userId ? "Transferring…" : "Make owner"}
                      </button>
                    )}
                    <button
                      type="button"
                      onClick={() => remove(c.userId)}
                      disabled={busyId === c.userId}
                      className="text-sm text-red-600 hover:underline disabled:opacity-60"
                      aria-label={`Remove ${c.displayName}`}
                    >
                      {busyId === c.userId ? "Removing…" : "Remove"}
                    </button>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}
      {error && (
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      )}
    </div>
  );
}
