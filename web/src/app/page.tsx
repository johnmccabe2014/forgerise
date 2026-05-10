// ForgeRise landing — rugby coach ops intelligence.
// Mobile-first, calm, and explicitly built around the language the
// coach already uses on the touchline (forwards, backs, sessions, welfare).

import Link from "next/link";
import Image from "next/image";
import { ReadinessBadge } from "@/components/ReadinessBadge";

export const metadata = {
  title: "ForgeRise — Ops Intelligence for Rugby Coaches",
};

export default function Home() {
  return (
    <main className="min-h-screen bg-mist-grey text-deep-charcoal">
      <header className="bg-forge-navy text-white">
        <div className="mx-auto max-w-5xl px-6 py-4 flex items-center justify-between">
          <span className="inline-flex items-center gap-2">
            <Image
              src="/brand/crest-icon.png"
              alt=""
              width={32}
              height={32}
              priority
              className="h-8 w-8 object-contain"
            />
            <span className="font-heading tracking-wide">ForgeRise</span>
          </span>
          <nav aria-label="primary" className="flex items-center gap-4 text-sm">
            <Link href="/login" className="hover:text-soft-ember transition">
              Sign in
            </Link>
            <Link
              href="/register"
              className="rounded-lg bg-rise-copper px-3 py-1.5 font-heading hover:opacity-90 transition"
            >
              Get started
            </Link>
          </nav>
        </div>
      </header>

      <section
        aria-labelledby="hero-heading"
        className="mx-auto max-w-5xl px-6 pt-16 pb-12 text-center"
      >
        <p className="text-xs uppercase tracking-[0.2em] text-rise-copper font-heading">
          Built for rugby coaches
        </p>
        <h1
          id="hero-heading"
          className="mt-3 font-heading text-4xl sm:text-5xl text-forge-navy text-balance"
        >
          Pick a stronger XV in a fraction of the time.
        </h1>
        <p className="mt-5 max-w-2xl mx-auto text-slate text-balance">
          ForgeRise turns the bits and pieces a coach already tracks —
          attendance, welfare, training focus — into one calm view of the
          squad. Forwards on top, backs below. Selection on Saturday
          morning, not Sunday night.
        </p>

        <div className="mt-8 flex flex-col sm:flex-row gap-3 w-full max-w-md mx-auto">
          <Link
            href="/register"
            className="flex-1 inline-flex items-center justify-center rounded-lg bg-forge-navy px-5 py-3 text-white font-heading hover:opacity-90 transition"
          >
            Create your squad
          </Link>
          <Link
            href="/login"
            className="flex-1 inline-flex items-center justify-center rounded-lg border border-forge-navy px-5 py-3 text-forge-navy font-heading hover:bg-forge-navy hover:text-white transition"
          >
            Sign in
          </Link>
        </div>
      </section>

      <section
        aria-labelledby="features-heading"
        className="mx-auto max-w-5xl px-6 py-12"
      >
        <h2 id="features-heading" className="sr-only">
          What ForgeRise does for your rugby club
        </h2>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <FeatureCard
            tag="Squad"
            title="Forwards & Backs at a glance"
            body="Players are organised by their rugby position — pack on top, backline below — so attendance, contact load, and selection are obvious."
          />
          <FeatureCard
            tag="Sessions"
            title="Backs, Forwards, or Whole-Squad"
            body="Tag each training as backs-specific, forwards-specific, skills, S&C, or full team. ForgeRise tracks who actually showed up to the work that matters for them."
          />
          <FeatureCard
            tag="Welfare"
            title="Quiet signals, loud enough to act on"
            body="Player-reported wellness rolls up into a four-state readiness traffic light so you know who to load and who to protect."
          />
        </div>
      </section>

      <section
        aria-labelledby="readiness-heading"
        className="mx-auto max-w-5xl px-6 py-12"
      >
        <div className="rounded-card bg-white p-6 shadow-soft">
          <p className="text-xs uppercase tracking-widest text-rise-copper font-heading">
            Readiness
          </p>
          <h2
            id="readiness-heading"
            className="mt-2 font-heading text-2xl text-forge-navy"
          >
            One traffic light per player
          </h2>
          <p className="mt-2 text-slate max-w-2xl">
            We never show raw soreness or sleep numbers to coaches —
            ForgeRise rolls everything up into four coach-safe states so you
            can scan the squad in seconds.
          </p>
          <div className="mt-5 grid grid-cols-2 sm:grid-cols-4 gap-3">
            <ReadinessBadge category="ready" />
            <ReadinessBadge category="monitor" />
            <ReadinessBadge category="modify" />
            <ReadinessBadge category="recovery" />
          </div>
        </div>
      </section>

      <section
        aria-labelledby="cta-heading"
        className="mx-auto max-w-5xl px-6 pt-8 pb-20 text-center"
      >
        <h2
          id="cta-heading"
          className="font-heading text-2xl text-forge-navy"
        >
          Ready to spend Saturday coaching, not collating?
        </h2>
        <Link
          href="/register"
          className="mt-5 inline-flex items-center justify-center rounded-lg bg-rise-copper px-6 py-3 text-white font-heading hover:opacity-90 transition"
        >
          Create your squad
        </Link>
      </section>

      <footer className="bg-forge-navy text-white/80 text-xs">
        <div className="mx-auto max-w-5xl px-6 py-6 flex flex-col sm:flex-row justify-between gap-2">
          <p>© {new Date().getFullYear()} ForgeRise · Rugby coach ops intelligence</p>
          <p>Built mobile-first for the side of the pitch.</p>
        </div>
      </footer>
    </main>
  );
}

function FeatureCard({
  tag,
  title,
  body,
}: {
  tag: string;
  title: string;
  body: string;
}) {
  return (
    <article className="rounded-card bg-white p-5 shadow-soft h-full">
      <p className="text-xs uppercase tracking-widest text-rise-copper font-heading">
        {tag}
      </p>
      <h3 className="mt-2 font-heading text-lg text-forge-navy">{title}</h3>
      <p className="mt-2 text-sm text-slate">{body}</p>
    </article>
  );
}
