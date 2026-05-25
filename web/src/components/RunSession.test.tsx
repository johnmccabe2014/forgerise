import { describe, it, expect } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import { RunSession, buildSteps, reduce, type RunBlock, type RunDrill } from "@/components/RunSession";

const blocks: RunBlock[] = [
  {
    block: "warmup",
    title: "RAMP warm-up",
    durationMinutes: 1,
    intent: "raise activate mobilise potentiate",
    intensity: "Standard",
    glossary: [{ term: "RAMP", plain: "Raise, Activate, Mobilise, Potentiate" }],
  },
  {
    block: "technical",
    title: "Passing square",
    durationMinutes: 2,
    intent: "hands under fatigue",
    intensity: "Standard",
  },
];

const drills: RunDrill[] = [
  {
    drillId: "passing-square",
    title: "Passing square",
    description: "Square of 4 players, ball travels both ways.",
    durationMinutes: 1,
    rationale: "Decision focus",
    tags: ["catch_pass"],
    longDescription: "Set a 10x10 grid, two balls, alternate directions.",
    coachingCues: ["Hands early", "Eyes up"],
    equipment: ["4 cones", "2 balls"],
    whatItMeans: "Catch and pass the ball cleanly in tight spaces.",
    diagramKey: "passing-square",
    glossary: [{ term: "SSG", plain: "Small-Sided Game" }],
  },
];

describe("RunSession reducer", () => {
  const steps = buildSteps(blocks, drills);
  const r = reduce(steps);

  it("buildSteps produces blocks then drills in order", () => {
    expect(steps.map((s) => (s.kind === "block" ? s.block.block : s.drill.drillId)))
      .toEqual(["warmup", "technical", "passing-square"]);
  });

  it("START seeds first step duration", () => {
    const s = r({ status: "idle", index: 0, secondsLeft: 0 }, { type: "START" });
    expect(s).toEqual({ status: "running", index: 0, secondsLeft: 60 });
  });

  it("TICK decrements and auto-advances on zero", () => {
    let s = r({ status: "running", index: 0, secondsLeft: 2 }, { type: "TICK" });
    expect(s.secondsLeft).toBe(1);
    s = r(s, { type: "TICK" });
    // Hit zero → advance to next step (technical, 2 min = 120s).
    expect(s.index).toBe(1);
    expect(s.secondsLeft).toBe(120);
    expect(s.status).toBe("running");
  });

  it("PAUSE then RESUME preserves remaining seconds", () => {
    const paused = r({ status: "running", index: 0, secondsLeft: 30 }, { type: "PAUSE" });
    expect(paused.status).toBe("paused");
    expect(paused.secondsLeft).toBe(30);
    const resumed = r(paused, { type: "RESUME" });
    expect(resumed.status).toBe("running");
    expect(resumed.secondsLeft).toBe(30);
  });

  it("PAUSE is a no-op when not running", () => {
    const s = r({ status: "idle", index: 0, secondsLeft: 0 }, { type: "PAUSE" });
    expect(s.status).toBe("idle");
  });

  it("SKIP advances to next step", () => {
    const s = r({ status: "running", index: 0, secondsLeft: 45 }, { type: "SKIP" });
    expect(s.index).toBe(1);
    expect(s.secondsLeft).toBe(120);
  });

  it("PREV from index 0 resets timer of current step", () => {
    const s = r({ status: "running", index: 0, secondsLeft: 5 }, { type: "PREV" });
    expect(s.index).toBe(0);
    expect(s.secondsLeft).toBe(60);
  });

  it("ADD_MINUTE bumps secondsLeft by 60", () => {
    const s = r({ status: "running", index: 0, secondsLeft: 10 }, { type: "ADD_MINUTE" });
    expect(s.secondsLeft).toBe(70);
  });

  it("TICK on last step transitions to complete", () => {
    const last = steps.length - 1;
    const s = r({ status: "running", index: last, secondsLeft: 1 }, { type: "TICK" });
    expect(s.status).toBe("complete");
  });

  it("START with no steps goes straight to complete", () => {
    const empty = buildSteps([], []);
    const s = reduce(empty)({ status: "idle", index: 0, secondsLeft: 0 }, { type: "START" });
    expect(s.status).toBe("complete");
  });
});

describe("RunSession component", () => {
  it("starts with start button, then shows clock + first step card", () => {
    render(
      <RunSession
        teamId="t1"
        sessionId="s1"
        focus="Catch-pass under fatigue"
        blocks={blocks}
        drills={drills}
      />,
    );
    expect(screen.getByText(/ready to start/i)).toBeInTheDocument();
    fireEvent.click(screen.getByTestId("run-start"));
    expect(screen.getByTestId("run-clock").textContent).toBe("01:00");
    expect(screen.getByTestId("run-step-block")).toHaveTextContent("RAMP warm-up");
    // Glossary chips should be rendered for blocks that have them.
    expect(screen.getByTestId("glossary-chiplist")).toHaveTextContent(/RAMP/);
  });

  it("uses injected ticker to advance the clock", () => {
    // Manual ticker: returns an unsubscribe, exposes its callback.
    let tick: (() => void) | null = null;
    const ticker = (cb: () => void) => {
      tick = cb;
      return () => {
        tick = null;
      };
    };
    render(
      <RunSession
        teamId="t1"
        sessionId="s1"
        focus="X"
        blocks={blocks}
        drills={drills}
        ticker={ticker}
      />,
    );
    fireEvent.click(screen.getByTestId("run-start"));
    expect(tick).not.toBeNull();
    act(() => tick!());
    expect(screen.getByTestId("run-clock").textContent).toBe("00:59");
  });

  it("skip advances and complete shows wrap-up", () => {
    render(
      <RunSession
        teamId="t1"
        sessionId="s1"
        focus="X"
        blocks={[blocks[0]]}
        drills={[]}
      />,
    );
    fireEvent.click(screen.getByTestId("run-start"));
    fireEvent.click(screen.getByTestId("run-skip"));
    expect(screen.getByText(/session complete — nice work/i)).toBeInTheDocument();
  });
});
