"use client";

import { useEffect, useMemo, useReducer } from "react";
import Link from "next/link";
import { DrillDiagram } from "@/components/drills/DrillDiagram";
import { GlossaryChipList, GlossaryTerm } from "@/components/GlossaryTerm";

export interface RunBlock {
  block: string;
  title: string;
  durationMinutes: number;
  intent: string;
  intensity: string;
  glossary?: { term: string; plain: string }[];
}

export interface RunDrill {
  drillId: string;
  title: string;
  description: string;
  durationMinutes: number;
  rationale: string;
  tags: string[];
  longDescription?: string | null;
  coachingCues?: string[] | null;
  equipment?: string[] | null;
  whatItMeans?: string | null;
  diagramKey?: string | null;
  glossary?: { term: string; plain: string }[] | null;
}

export type RunStep =
  | { kind: "block"; block: RunBlock; index: number }
  | { kind: "drill"; drill: RunDrill; index: number };

export interface RunSessionProps {
  teamId: string;
  sessionId: string;
  focus: string;
  blocks: RunBlock[];
  drills: RunDrill[];
  /**
   * Optional injected ticker for tests. Returns an unsubscribe.
   * Defaults to `window.setInterval(cb, 1000)`.
   */
  ticker?: (cb: () => void) => () => void;
}

interface State {
  status: "idle" | "running" | "paused" | "complete";
  index: number;
  secondsLeft: number;
}

type Action =
  | { type: "START" }
  | { type: "PAUSE" }
  | { type: "RESUME" }
  | { type: "TICK" }
  | { type: "NEXT" }
  | { type: "PREV" }
  | { type: "SKIP" }
  | { type: "ADD_MINUTE" }
  | { type: "COMPLETE" };

export function buildSteps(blocks: RunBlock[], drills: RunDrill[]): RunStep[] {
  // Run order: walk the plan blocks top-to-bottom, then dump any remaining
  // recommended drills as their own steps at the end (so coaches can choose
  // to slot them in or simply use them as a reference). Stable + deterministic.
  const steps: RunStep[] = [];
  blocks.forEach((b, i) => steps.push({ kind: "block", block: b, index: i }));
  drills.forEach((d, i) => steps.push({ kind: "drill", drill: d, index: i }));
  return steps;
}

function stepDurationSeconds(step: RunStep | undefined): number {
  if (!step) return 0;
  if (step.kind === "block") return Math.max(0, step.block.durationMinutes) * 60;
  return Math.max(0, step.drill.durationMinutes) * 60;
}

export function reduce(steps: RunStep[]): (s: State, a: Action) => State {
  // Closure captures `steps` so callers (and tests) can drive a known sequence.
  return (state, action) => {
    switch (action.type) {
      case "START":
        if (steps.length === 0) return { ...state, status: "complete" };
        return {
          status: "running",
          index: 0,
          secondsLeft: stepDurationSeconds(steps[0]),
        };
      case "PAUSE":
        if (state.status !== "running") return state;
        return { ...state, status: "paused" };
      case "RESUME":
        if (state.status !== "paused") return state;
        return { ...state, status: "running" };
      case "TICK": {
        if (state.status !== "running") return state;
        const next = state.secondsLeft - 1;
        if (next > 0) return { ...state, secondsLeft: next };
        // Auto-advance when the current step's timer hits zero.
        return advance(state, steps);
      }
      case "NEXT":
      case "SKIP":
        return advance(state, steps);
      case "PREV": {
        if (state.index === 0) {
          return {
            ...state,
            secondsLeft: stepDurationSeconds(steps[0]),
          };
        }
        const prev = state.index - 1;
        return {
          ...state,
          status: state.status === "complete" ? "running" : state.status,
          index: prev,
          secondsLeft: stepDurationSeconds(steps[prev]),
        };
      }
      case "ADD_MINUTE":
        return { ...state, secondsLeft: state.secondsLeft + 60 };
      case "COMPLETE":
        return { ...state, status: "complete" };
      default:
        return state;
    }
  };
}

function advance(state: State, steps: RunStep[]): State {
  const nextIndex = state.index + 1;
  if (nextIndex >= steps.length) {
    return { ...state, status: "complete", secondsLeft: 0 };
  }
  return {
    status: "running",
    index: nextIndex,
    secondsLeft: stepDurationSeconds(steps[nextIndex]),
  };
}

function format(secondsLeft: number): string {
  const mm = Math.floor(secondsLeft / 60).toString().padStart(2, "0");
  const ss = Math.floor(secondsLeft % 60).toString().padStart(2, "0");
  return `${mm}:${ss}`;
}

export function RunSession({
  teamId,
  sessionId,
  focus,
  blocks,
  drills,
  ticker,
}: RunSessionProps) {
  const steps = useMemo(() => buildSteps(blocks, drills), [blocks, drills]);
  const reducer = useMemo(() => reduce(steps), [steps]);
  const [state, dispatch] = useReducer(reducer, {
    status: "idle" as const,
    index: 0,
    secondsLeft: stepDurationSeconds(steps[0]),
  });

  // dispatch from useReducer is stable across renders, so it's safe to
  // reference directly inside the effect without a ref.
  useEffect(() => {
    if (state.status !== "running") return;
    if (ticker) return ticker(() => dispatch({ type: "TICK" }));
    const id = window.setInterval(() => dispatch({ type: "TICK" }), 1000);
    return () => window.clearInterval(id);
  }, [state.status, ticker]);

  const current = steps[state.index];
  const upNext = steps[state.index + 1];

  return (
    <section
      aria-labelledby="run-heading"
      className="mx-auto max-w-2xl px-4 py-6 space-y-4"
      data-testid="run-session"
    >
      <header className="flex items-baseline justify-between gap-3">
        <div>
          <p className="text-xs uppercase tracking-widest text-rise-copper">
            Running session
          </p>
          <h1 id="run-heading" className="font-heading text-2xl text-forge-navy">
            {focus}
          </h1>
        </div>
        <Link
          href={`/teams/${teamId}/sessions/${sessionId}`}
          className="text-sm text-slate underline shrink-0"
        >
          Exit
        </Link>
      </header>

      <div
        className="rounded-card bg-forge-navy text-white p-6 text-center shadow-soft"
        data-testid="run-timer"
      >
        <p className="text-xs uppercase tracking-widest text-white/70">
          {state.status === "complete"
            ? "Session complete"
            : state.status === "idle"
              ? "Ready to start"
              : `Step ${state.index + 1} of ${steps.length}`}
        </p>
        <p
          className="font-heading text-6xl tabular-nums my-2"
          aria-live="polite"
          data-testid="run-clock"
        >
          {format(state.secondsLeft)}
        </p>
        <div className="flex flex-wrap justify-center gap-2 mt-3">
          {state.status === "idle" && (
            <button
              type="button"
              onClick={() => dispatch({ type: "START" })}
              className="rounded-pill bg-rise-copper px-6 py-2 font-medium"
              data-testid="run-start"
            >
              ▶ Start session
            </button>
          )}
          {state.status === "running" && (
            <button
              type="button"
              onClick={() => dispatch({ type: "PAUSE" })}
              className="rounded-pill bg-white/15 px-4 py-2"
              data-testid="run-pause"
            >
              ❚❚ Pause
            </button>
          )}
          {state.status === "paused" && (
            <button
              type="button"
              onClick={() => dispatch({ type: "RESUME" })}
              className="rounded-pill bg-rise-copper px-4 py-2"
              data-testid="run-resume"
            >
              ▶ Resume
            </button>
          )}
          {(state.status === "running" || state.status === "paused") && (
            <>
              <button
                type="button"
                onClick={() => dispatch({ type: "PREV" })}
                className="rounded-pill bg-white/15 px-3 py-2"
                data-testid="run-prev"
              >
                ‹ Prev
              </button>
              <button
                type="button"
                onClick={() => dispatch({ type: "ADD_MINUTE" })}
                className="rounded-pill bg-white/15 px-3 py-2"
                data-testid="run-add"
              >
                +1 min
              </button>
              <button
                type="button"
                onClick={() => dispatch({ type: "SKIP" })}
                className="rounded-pill bg-white/15 px-3 py-2"
                data-testid="run-skip"
              >
                Skip ›
              </button>
            </>
          )}
        </div>
      </div>

      {current && state.status !== "complete" && (
        <CurrentStepCard step={current} />
      )}

      {upNext && state.status !== "complete" && (
        <aside
          className="rounded-card border border-slate/10 bg-white/70 p-3 text-sm text-slate"
          data-testid="run-upnext"
        >
          <p className="text-[11px] uppercase tracking-widest text-rise-copper">
            Up next
          </p>
          <p className="font-medium text-deep-charcoal">
            {upNext.kind === "block" ? upNext.block.title : upNext.drill.title}
          </p>
        </aside>
      )}

      {state.status === "complete" && (
        <div className="rounded-card bg-white p-6 shadow-soft text-center space-y-3">
          <p className="font-heading text-xl text-forge-navy">
            Session complete — nice work.
          </p>
          <p className="text-sm text-slate">
            Head back to the session to log a review.
          </p>
          <Link
            href={`/teams/${teamId}/sessions/${sessionId}`}
            className="inline-block rounded-pill bg-rise-copper text-white px-6 py-2 font-medium"
          >
            Back to session
          </Link>
        </div>
      )}
    </section>
  );
}

function CurrentStepCard({ step }: { step: RunStep }) {
  if (step.kind === "block") {
    const b = step.block;
    return (
      <article className="rounded-card bg-white p-4 shadow-soft space-y-2" data-testid="run-step-block">
        <p className="text-[11px] uppercase tracking-widest text-rise-copper">
          Plan block · {b.intensity}
        </p>
        <h2 className="font-heading text-xl text-forge-navy">{b.title}</h2>
        <p className="text-sm text-deep-charcoal">{b.intent}</p>
        <GlossaryChipList glossary={b.glossary} />
      </article>
    );
  }
  const d = step.drill;
  return (
    <article className="rounded-card bg-white p-4 shadow-soft space-y-3" data-testid="run-step-drill">
      <p className="text-[11px] uppercase tracking-widest text-rise-copper">
        Drill · {d.durationMinutes} min
      </p>
      <h2 className="font-heading text-xl text-forge-navy">{d.title}</h2>
      <p className="text-sm text-deep-charcoal">
        {d.longDescription ?? d.description}
      </p>
      {d.whatItMeans && (
        <p className="rounded-card bg-mist-grey p-2 text-xs text-deep-charcoal">
          <span className="font-medium text-forge-navy">In plain English: </span>
          {d.whatItMeans}
        </p>
      )}
      <DrillDiagram diagramKey={d.diagramKey} label={`Layout for ${d.title}`} />
      {d.coachingCues && d.coachingCues.length > 0 && (
        <div>
          <p className="text-[11px] uppercase tracking-widest text-slate">
            Call out
          </p>
          <ul className="mt-1 list-disc list-inside text-sm text-deep-charcoal space-y-0.5">
            {d.coachingCues.map((c) => (
              <li key={c}>{c}</li>
            ))}
          </ul>
        </div>
      )}
      {d.equipment && d.equipment.length > 0 && (
        <p className="text-xs text-slate">
          <span className="font-medium text-deep-charcoal">Kit: </span>
          {d.equipment.map((e, i) => (
            <span key={e}>
              {i > 0 && ", "}
              <GlossaryTerm term={e} plain={e}>{e}</GlossaryTerm>
            </span>
          ))}
        </p>
      )}
      <GlossaryChipList glossary={d.glossary ?? undefined} />
    </article>
  );
}
