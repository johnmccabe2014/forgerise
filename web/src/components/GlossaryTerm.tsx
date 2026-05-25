/**
 * Inline jargon tooltip. We render the term as a dotted-underlined chip so the
 * coach can see at-a-glance that there's an explanation behind it. The
 * tooltip is a `<details>` + popover-positioned card so it works on both
 * desktop hover/focus and mobile tap without needing JS for show/hide.
 */
import type { ReactNode } from "react";

export interface GlossaryTermProps {
  term: string;
  plain: string;
  /** Optional override text — defaults to the term itself. */
  children?: ReactNode;
}

export function GlossaryTerm({ term, plain, children }: GlossaryTermProps) {
  return (
    <span
      className="relative inline-block group"
      data-testid={`glossary-${term.toLowerCase().replace(/\s+/g, "-")}`}
    >
      <button
        type="button"
        aria-describedby={`glossary-tip-${term.replace(/\s+/g, "-")}`}
        className="cursor-help underline decoration-dotted decoration-rise-copper/70 underline-offset-2 text-inherit"
      >
        {children ?? term}
      </button>
      <span
        role="tooltip"
        id={`glossary-tip-${term.replace(/\s+/g, "-")}`}
        className="pointer-events-none absolute left-1/2 top-full z-20 mt-1 w-60 -translate-x-1/2 rounded-card border border-slate/20 bg-white p-2 text-xs font-normal normal-case tracking-normal text-deep-charcoal shadow-soft opacity-0 transition-opacity duration-150 group-hover:opacity-100 group-focus-within:opacity-100"
      >
        <span className="block font-medium text-forge-navy">{term}</span>
        <span className="block text-slate">{plain}</span>
      </span>
    </span>
  );
}

export interface GlossaryChipListProps {
  /** Plain-English entries surfaced by the API for a block/drill. */
  glossary: ReadonlyArray<{ term: string; plain: string }> | undefined | null;
}

/**
 * Renders a row of glossary chips below a block or drill card. Empty/missing
 * input produces nothing (no empty container) so old plans render unchanged.
 */
export function GlossaryChipList({ glossary }: GlossaryChipListProps) {
  if (!glossary || glossary.length === 0) return null;
  return (
    <div
      data-testid="glossary-chiplist"
      className="mt-2 flex flex-wrap gap-1"
    >
      {glossary.map((g) => (
        <span
          key={g.term}
          className="inline-flex items-center rounded-pill border border-rise-copper/30 bg-rise-copper/5 px-2 py-0.5 text-[11px] uppercase tracking-wider text-rise-copper"
        >
          <GlossaryTerm term={g.term} plain={g.plain} />
        </span>
      ))}
    </div>
  );
}
