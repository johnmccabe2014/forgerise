"use client";

import { useEffect, useState } from "react";

interface ActivityUnreadBadgeProps {
  teamId: string;
  /** Initial unread count from the server-side fetch. */
  initialUnread: number;
}

/**
 * Visible "N new" pill next to the Recent activity heading.
 *
 * Once the team page is mounted we POST /activity/seen so the next visit
 * starts at zero. The optimistic local state means the pill disappears
 * immediately even if the network call is in flight.
 */
export function ActivityUnreadBadge({
  teamId,
  initialUnread,
}: ActivityUnreadBadgeProps) {
  const [unread, setUnread] = useState(initialUnread);

  useEffect(() => {
    if (initialUnread <= 0) return;
    let cancelled = false;
    void fetch(`/api/proxy/teams/${teamId}/activity/seen`, {
      method: "POST",
    })
      .then(() => {
        if (!cancelled) setUnread(0);
      })
      .catch(() => {
        // Best-effort UX state; swallow.
      });
    return () => {
      cancelled = true;
    };
  }, [teamId, initialUnread]);

  if (unread <= 0) return null;
  const label = unread >= 99 ? "99+ new" : `${unread} new`;
  return (
    <span
      data-testid="activity-unread-badge"
      className="inline-flex items-center rounded-card bg-rise-copper px-2 py-0.5 text-xs font-semibold text-white"
      aria-label={`${unread} unread activity item${unread === 1 ? "" : "s"}`}
    >
      {label}
    </span>
  );
}
