/**
 * Tiny pill that flags a check-in as player-submitted (vs coach-recorded).
 * Provenance metadata only — safe under master prompt §9 (no raw welfare).
 */
export function SelfSubmittedPill() {
  return (
    <span
      title="Player submitted this check-in via /me"
      className="inline-flex shrink-0 items-center rounded-full border border-rise-copper/40 bg-rise-copper/10 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-rise-copper"
    >
      self
    </span>
  );
}
