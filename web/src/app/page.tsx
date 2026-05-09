// ForgeRise landing — minimal brand sample, mobile-first.
// Master prompt §5/§6: calm, modern, mobile-first, supportive.

import { ReadinessBadge } from "@/components/ReadinessBadge";

export default function Home() {
  return (
    <main className="min-h-screen flex flex-col items-center justify-center px-6 py-16 text-center">
      <p className="text-sm uppercase tracking-widest text-rise-copper font-heading">
        ForgeRise
      </p>
      <h1 className="mt-3 font-heading text-4xl sm:text-5xl text-forge-navy text-balance">
        Ops Intelligence for Coaches
      </h1>
      <p className="mt-5 max-w-xl text-slate text-balance">
        Save time, support player welfare, and make better training decisions —
        from minimal manual input.
      </p>

      <section
        aria-label="Readiness categories preview"
        className="mt-12 grid grid-cols-2 sm:grid-cols-4 gap-3 max-w-xl w-full"
      >
        <ReadinessBadge category="ready" />
        <ReadinessBadge category="monitor" />
        <ReadinessBadge category="modify" />
        <ReadinessBadge category="recovery" />
      </section>

      <p className="mt-12 text-xs text-slate">
        Phase 1 scaffold · {new Date().getFullYear()}
      </p>
    </main>
  );
}
