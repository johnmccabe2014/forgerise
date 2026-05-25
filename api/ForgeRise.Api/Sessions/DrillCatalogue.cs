namespace ForgeRise.Api.Sessions;

/// <summary>
/// One drill in the static catalogue. <see cref="Tags"/> drives matching by
/// intensity, contact load, and focus area; the remaining fields are the
/// coach-facing content surfaced in the "Run session" mode for newer coaches
/// who would otherwise hit jargon-only titles like "RAMP warm-up".
///
/// The catalogue is intentionally in-process and hand-curated for v1 —
/// master prompt §4 leaves room for a data-driven catalogue later.
/// </summary>
public sealed record Drill(
    string Id,
    string Title,
    string Description,
    int DurationMinutes,
    IReadOnlyList<string> Tags,
    string LongDescription,
    IReadOnlyList<string> CoachingCues,
    IReadOnlyList<string> Equipment,
    string WhatItMeans,
    string DiagramKey);

public static class DrillCatalogue
{
    public static readonly IReadOnlyList<Drill> All = new[]
    {
        new Drill("mobility-flow", "Mobility flow + breathing",
            "10-min guided mobility circuit; nasal breathing only.",
            10, new[] { "low_contact", "recovery", "warmup" },
            LongDescription:
                "Slow, controlled mobility circuit working through the major joints: ankles, hips, " +
                "thoracic spine, shoulders, neck. Players move at conversation pace, breathing in and " +
                "out through the nose only. The goal is to wake the nervous system without spiking " +
                "heart rate — useful as a warm-up after a hard week or as a stand-alone recovery block.",
            CoachingCues: new[]
            {
                "Nose breathing only — if anyone is mouth-breathing, slow down.",
                "Full range of motion, no bouncing.",
                "Eyes up, posture tall on every rep.",
            },
            Equipment: new[] { "Open grass area" },
            WhatItMeans:
                "A guided sequence of mobility movements (joint circles, lunges with rotation, " +
                "deep squats, scapular work) paired with calm nasal breathing.",
            DiagramKey: "open-grid"),

        new Drill("walk-throughs", "Walk-through phase play",
            "Defensive line + ruck shape at walking pace; talk through cues.",
            12, new[] { "low_contact", "decision", "team_shape" },
            LongDescription:
                "Players walk through the team's defensive line shape and ruck commitments at " +
                "walking pace. Coach pauses the action to ask 'who's our pillar?', 'who's our " +
                "guard?', 'where's the inside shoulder?'. No tackling, no real contact.",
            CoachingCues: new[]
            {
                "Talk loud — every cue should be spoken, not assumed.",
                "Hands on hips at the breakdown, not in the contact zone.",
                "If a player is unsure, stop and replay the phase.",
            },
            Equipment: new[] { "Tackle bags (passive)", "Cones to mark the line" },
            WhatItMeans:
                "A walking-pace rehearsal of a game phase — the rugby equivalent of a stage " +
                "read-through. Everyone learns their role before doing it at speed.",
            DiagramKey: "phase-shape"),

        new Drill("hand-skill-square", "Hand-skill square",
            "4-corner passing square; both hands, 60–70% pace.",
            12, new[] { "low_contact", "skill", "back_play" },
            LongDescription:
                "Four cones in a 10m × 10m square. One player per corner, ball starts at one cone. " +
                "Players run their line, catch, pass, follow the ball. Reverse direction every 60s " +
                "so both hands work. Keep it at 60–70% pace — quality over speed.",
            CoachingCues: new[]
            {
                "Hands up early — give the passer a target.",
                "Swing the hips toward the receiver before you pass.",
                "Catch with soft hands, pass with intent.",
            },
            Equipment: new[] { "4 cones per square", "1 ball per square" },
            WhatItMeans:
                "Classic warm-up passing drill on a square grid. Builds clean catch-pass habits " +
                "in both hands with continuous movement.",
            DiagramKey: "passing-square"),

        new Drill("ruck-clean-tech", "Ruck clean technique",
            "Bagged hits → live cleanout, body height + foot speed cues.",
            15, new[] { "skill", "forward_play", "contact" },
            LongDescription:
                "Pairs work on ruck clean-out technique: first against a held tackle bag to grove " +
                "the shape (low body, leg drive, hands beyond the ball), then in 3-second live " +
                "cleanouts against a passive partner. Reset every 3 reps.",
            CoachingCues: new[]
            {
                "Low hips, eyes up — never dive in head-first.",
                "Feet keep moving on contact; don't stop on impact.",
                "Hands clamp past the ball-carrier, not on the ground.",
            },
            Equipment: new[] { "Tackle bags", "Mouthguards mandatory" },
            WhatItMeans:
                "Teaches the technique for clearing opposition players off the ball at a ruck — " +
                "the breakdown after a tackle. Body shape and foot speed are the keys.",
            DiagramKey: "ruck-clean"),

        new Drill("lineout-throws", "Lineout throwing + lifting",
            "Calls → throws → contested lifts; reset every 5.",
            15, new[] { "skill", "forward_play", "lineout", "low_contact" },
            LongDescription:
                "Forwards work through the team's lineout calls. Hooker throws to a pre-called " +
                "target, jumper is lifted by two pods, catches, brings it down. Rotate jumpers " +
                "every 5 throws so everyone gets reps in each pod position.",
            CoachingCues: new[]
            {
                "Call clearly — the call is for the team, not the opposition.",
                "Lifters grip below the knee, drive straight up.",
                "Jumper times the jump off the lifters' first dip.",
            },
            Equipment: new[] { "Lineout ball", "Cones to mark the lineout" },
            WhatItMeans:
                "The set-piece where a forward is lifted in the air to catch a throw-in from the " +
                "sideline. Practice covers the call, the throw, and the lift in sequence.",
            DiagramKey: "lineout"),

        new Drill("scrum-engage", "Scrum engagement sequence",
            "Crouch-bind-set sequence with scrum machine; live for last 3.",
            18, new[] { "skill", "forward_play", "scrum", "contact" },
            LongDescription:
                "Front row and second row work the 'crouch — bind — set' engagement sequence " +
                "against the scrum machine. First 5 are technique reps, focus on body angles. " +
                "Last 3 add the back row for a fully bound 8-person live engage.",
            CoachingCues: new[]
            {
                "Backs flat, hips above knees on engage — no diving.",
                "Bind tight before the call — loose binds get pinged.",
                "Drive starts from the feet, not the shoulders.",
            },
            Equipment: new[] { "Scrum machine", "Mouthguards mandatory", "Front-row certified coach on hand" },
            WhatItMeans:
                "The rehearsed sequence of commands ('crouch', 'bind', 'set') that brings the " +
                "two scrums together safely. Getting the body shape right at engage is a safety " +
                "issue as much as a performance one.",
            DiagramKey: "scrum"),

        new Drill("attack-shape", "Attack shape vs passive D",
            "Run patterns vs passive defenders; coach calls phase.",
            15, new[] { "decision", "back_play", "team_shape", "low_contact" },
            LongDescription:
                "Attacking unit (usually 9 + backs + 1-2 forwards) runs the team's first-phase " +
                "patterns against a passive defensive line. Defenders touch only — no tackle. " +
                "Coach calls the phase ('phase 2 left', 'edge play right') so the attack has to " +
                "react, not memorise.",
            CoachingCues: new[]
            {
                "10 stays flat — depth kills the line break.",
                "Front-foot ball; if the ruck slows, reset and replay.",
                "Communicate the call back so everyone hears it.",
            },
            Equipment: new[] { "Cones to mark channels", "Touch belts optional" },
            WhatItMeans:
                "A rehearsal of the team's attacking pattern — who runs where, who passes to " +
                "whom — against defenders who give the picture but don't tackle.",
            DiagramKey: "phase-shape"),

        new Drill("kick-chase", "Kick chase pressure",
            "Box kick + chase; reload; emphasise alignment.",
            12, new[] { "decision", "back_play", "fitness" },
            LongDescription:
                "Scrum-half box-kicks to a deep receiver. The chase line of 3-4 players sprints " +
                "to contest in the air. Receiver either catches or sets up a counter-ruck. Reset, " +
                "rotate, repeat for 12 minutes.",
            CoachingCues: new[]
            {
                "Chase line stays connected — no solo sprinters.",
                "Aerial contest: eyes on the ball, arms up early.",
                "If we don't win it back, we want a tackle inside 3 seconds.",
            },
            Equipment: new[] { "Plenty of balls", "Cones for chase channels" },
            WhatItMeans:
                "Practice for the moments when the 9 kicks the ball deep ('box kick') and the " +
                "chase line tries to win it back in the air or by tackling the receiver.",
            DiagramKey: "kick-chase"),

        new Drill("conditioned-game", "Conditioned small-sided game",
            "8 v 8 in 40m channel; constraints to bias your focus.",
            18, new[] { "game", "decision", "fitness", "contact" },
            LongDescription:
                "Small-sided game (typically 8 v 8) in a narrow 40m channel. Coach adds " +
                "constraints to bias the focus of the session — e.g. 'every try must come from " +
                "an offload' or 'three passes minimum before contact'. Full contact unless " +
                "constraint says otherwise.",
            CoachingCues: new[]
            {
                "Constraints are non-negotiable — reset and replay if broken.",
                "Talk through reset between scores — what just happened?",
                "Coaches are silent during play; debrief at the whistle.",
            },
            Equipment: new[] { "Cones for the channel", "Bibs in two colours", "Mouthguards mandatory" },
            WhatItMeans:
                "A short-sided game where a constraint (a rule the coach adds) nudges players " +
                "into making the kind of decisions you're trying to teach. Learning happens " +
                "through play, not lecturing.",
            DiagramKey: "ssg-channel"),

        new Drill("touch-decision", "Touch + decision SSG",
            "Touch rugby with a tag rule; rewards decision over collision.",
            16, new[] { "game", "decision", "low_contact", "fitness" },
            LongDescription:
                "Touch rugby variant: two-handed touch counts as a tackle, attacker must place " +
                "the ball, defenders must retreat 5m. Add a rule that rewards decision-making " +
                "(e.g. a try from a pre-touch offload counts double). Excellent for high-volume " +
                "decision reps with zero collision load.",
            CoachingCues: new[]
            {
                "Eyes up before the touch — pick your option early.",
                "Defenders communicate which channel they own.",
                "Reward good decisions out loud, even on a turnover.",
            },
            Equipment: new[] { "Cones for the pitch", "Bibs in two colours" },
            WhatItMeans:
                "A touch-rugby small-sided game ('SSG'). No tackling, but the rules nudge " +
                "players into making the same decisions they'd make in a full-contact game.",
            DiagramKey: "ssg-channel"),

        new Drill("cooldown-1to1", "Cool-down + 1:1 check-in",
            "Light jog, mobility, coach checks in with each player.",
            10, new[] { "low_contact", "recovery", "cooldown" },
            LongDescription:
                "Players jog gently for 3 minutes, then run through a short mobility flow while " +
                "the coach moves between individuals for a brief check-in: how did the session " +
                "feel, any niggles, anything they want noted for next time.",
            CoachingCues: new[]
            {
                "Ask, then listen — don't fix problems in this slot.",
                "Note anything physical for the welfare log later.",
                "Finish on something positive — a moment from the session that worked.",
            },
            Equipment: new[] { "Notebook or phone for notes" },
            WhatItMeans:
                "The wind-down at the end of a session combined with a quick one-on-one " +
                "conversation with each player. This is where you spot issues early.",
            DiagramKey: "open-grid"),
    };

    public static Drill? TryFind(string id) =>
        All.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
}
