/**
 * Tiny inline SVG diagrams that demonstrate the geometry of a drill so a
 * brand-new coach can picture the setup at a glance. These are deliberately
 * abstract pitch-cone diagrams, not action illustrations — they answer
 * "where do the cones go and which direction does the ball travel?".
 *
 * The `diagramKey` strings are produced by the API (DrillCatalogue.DiagramKey)
 * and exhaustively switched here, with a generic fallback so an unknown key
 * never breaks the page.
 */

export type DiagramKey =
  | "open-grid"
  | "phase-shape"
  | "passing-square"
  | "ruck-clean"
  | "lineout"
  | "scrum"
  | "kick-chase"
  | "ssg-channel";

export interface DrillDiagramProps {
  diagramKey: string | null | undefined;
  /** Optional caption for screen readers. Falls back to the diagram key. */
  label?: string;
  className?: string;
}

const PITCH_FILL = "#F0EFEA"; // mist-grey-ish
const PITCH_STROKE = "#1F2A44"; // forge-navy
const ACCENT = "#C77B3E"; // rise-copper
const PLAYER = "#1F2A44";

export function DrillDiagram({ diagramKey, label, className }: DrillDiagramProps) {
  const title = label ?? `Drill layout: ${diagramKey ?? "generic"}`;
  return (
    <svg
      role="img"
      aria-label={title}
      viewBox="0 0 200 120"
      className={
        className ??
        "h-32 w-full max-w-md rounded-card border border-slate/10 bg-white"
      }
    >
      <title>{title}</title>
      {renderBody(diagramKey)}
    </svg>
  );
}

function pitch() {
  return (
    <rect
      x="4"
      y="4"
      width="192"
      height="112"
      rx="4"
      fill={PITCH_FILL}
      stroke={PITCH_STROKE}
      strokeWidth="1"
    />
  );
}

function cone(cx: number, cy: number) {
  return <circle cx={cx} cy={cy} r="3" fill={ACCENT} />;
}

function player(cx: number, cy: number, key?: string) {
  return <circle key={key} cx={cx} cy={cy} r="3.5" fill={PLAYER} />;
}

function arrow(x1: number, y1: number, x2: number, y2: number, key?: string) {
  return (
    <line
      key={key}
      x1={x1}
      y1={y1}
      x2={x2}
      y2={y2}
      stroke={ACCENT}
      strokeWidth="1.5"
      markerEnd="url(#arrowhead)"
    />
  );
}

function defs() {
  return (
    <defs>
      <marker
        id="arrowhead"
        viewBox="0 0 10 10"
        refX="8"
        refY="5"
        markerWidth="5"
        markerHeight="5"
        orient="auto-start-reverse"
      >
        <path d="M0,0 L10,5 L0,10 z" fill={ACCENT} />
      </marker>
    </defs>
  );
}

function renderBody(key: string | null | undefined): React.ReactElement {
  switch (key) {
    case "open-grid":
      return (
        <g>
          {defs()}
          {pitch()}
          {/* 30x20 grid of cones */}
          {cone(40, 30)}
          {cone(160, 30)}
          {cone(40, 90)}
          {cone(160, 90)}
          {player(70, 60)}
          {player(100, 60)}
          {player(130, 60)}
          {arrow(70, 60, 160, 60)}
        </g>
      );
    case "phase-shape":
      return (
        <g>
          {defs()}
          {pitch()}
          {/* Pods at 1, 2, 3 across the field */}
          {[60, 100, 140].map((x) => (
            <g key={x}>
              {player(x - 6, 70, `${x}-l`)}
              {player(x, 60, `${x}-m`)}
              {player(x + 6, 70, `${x}-r`)}
            </g>
          ))}
          {/* ball carrier moves rightward */}
          {arrow(54, 65, 94, 65)}
          {arrow(94, 65, 134, 65)}
        </g>
      );
    case "passing-square":
      return (
        <g>
          {defs()}
          {pitch()}
          {cone(60, 30)}
          {cone(140, 30)}
          {cone(60, 90)}
          {cone(140, 90)}
          {player(60, 30)}
          {player(140, 30)}
          {player(140, 90)}
          {player(60, 90)}
          {arrow(60, 30, 140, 30)}
          {arrow(140, 30, 140, 90)}
          {arrow(140, 90, 60, 90)}
          {arrow(60, 90, 60, 30)}
        </g>
      );
    case "ruck-clean":
      return (
        <g>
          {defs()}
          {pitch()}
          {/* tackled player + arriving cleaner */}
          <ellipse cx="100" cy="60" rx="14" ry="8" fill={ACCENT} opacity="0.25" />
          {player(95, 60)}
          {player(105, 60)}
          {player(70, 60)}
          {arrow(70, 60, 90, 60)}
          {/* defenders */}
          {player(125, 50)}
          {player(125, 70)}
        </g>
      );
    case "lineout":
      return (
        <g>
          {defs()}
          {pitch()}
          {/* touchline + lineout pods */}
          <line x1="20" y1="10" x2="20" y2="110" stroke={PITCH_STROKE} strokeDasharray="3,2" />
          {[40, 70, 100, 130, 160].map((x) => player(x, 50, `j-${x}`))}
          {[40, 70, 100, 130, 160].map((x) => player(x, 80, `o-${x}`))}
          {/* hooker throws */}
          {player(20, 65)}
          {arrow(20, 65, 95, 50)}
        </g>
      );
    case "scrum":
      return (
        <g>
          {defs()}
          {pitch()}
          {/* 8 vs 8 packed scrum */}
          <rect x="78" y="48" width="44" height="24" rx="4" fill={ACCENT} opacity="0.2" />
          {[85, 100, 115].map((x) => player(x, 54, `f-${x}`))}
          {[88, 100, 112].map((x) => player(x, 66, `s-${x}`))}
          {/* scrum-half feeds */}
          {player(70, 80)}
          {arrow(70, 80, 88, 70)}
        </g>
      );
    case "kick-chase":
      return (
        <g>
          {defs()}
          {pitch()}
          {/* kicker bottom-left, chasers in a line */}
          {player(30, 90)}
          {arrow(30, 90, 170, 30)}
          {[60, 80, 100, 120].map((x) => player(x, 95, `c-${x}`))}
          {[60, 80, 100, 120].map((x) => arrow(x, 92, x + 30, 60, `ca-${x}`))}
        </g>
      );
    case "ssg-channel":
      return (
        <g>
          {defs()}
          {pitch()}
          {/* narrow conditioned-game channel */}
          <rect
            x="50"
            y="20"
            width="100"
            height="80"
            fill={ACCENT}
            opacity="0.08"
            stroke={ACCENT}
            strokeDasharray="3,3"
          />
          {cone(50, 20)}
          {cone(150, 20)}
          {cone(50, 100)}
          {cone(150, 100)}
          {[70, 90, 110, 130].map((x) => player(x, 45, `a-${x}`))}
          {[70, 90, 110, 130].map((x) => player(x, 75, `b-${x}`))}
        </g>
      );
    default:
      // Generic fallback: pitch with three cones forming a triangle.
      return (
        <g>
          {defs()}
          {pitch()}
          {cone(60, 80)}
          {cone(100, 30)}
          {cone(140, 80)}
          {player(60, 80)}
          {player(140, 80)}
          {arrow(60, 80, 100, 30)}
          {arrow(100, 30, 140, 80)}
        </g>
      );
  }
}
