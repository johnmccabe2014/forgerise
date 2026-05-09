# .copilot — Agent Operating System

This folder is the single source of truth for how Copilot (or any agentic LLM) operates on this repo.

## Layout

```
.copilot/
├── README.md                  # this file
├── master.prompt.md           # THE master prompt — load this first, every session
├── config.yaml                # routing rules: which agent owns which paths/tasks
├── agents/                    # role-scoped operating instructions
│   ├── planner.md
│   ├── frontend.md
│   ├── backend.md
│   ├── tester.md
│   ├── security.md
│   └── principal-reviewer.md
├── skills/                    # reusable, composable capabilities any agent can invoke
│   ├── testing.md
│   ├── validation.md
│   ├── code-review.md
│   ├── security-scan.md
│   ├── refactor.md
│   └── observability.md
├── workflows/                 # multi-agent processes
│   ├── feedback-loop.md       # the iterate-improve cycle
│   ├── new-feature.md
│   └── bugfix.md
└── templates/                 # required output shapes (plans, PRs, test reports)
    ├── plan.md
    ├── review.md
    └── test-report.md
```

## Where the master prompt goes

Put it at **`.copilot/master.prompt.md`**. Reasons:

1. **Discoverable** — top of the folder, no nesting, every agent loads it first.
2. **Versioned** — lives in git so changes are reviewable.
3. **Tool-agnostic** — works with VS Code Copilot custom chat modes, Cursor rules, Claude projects, or a homegrown agent runner.

To wire it into VS Code Copilot specifically, also reference it from:
- `.github/copilot-instructions.md` — repo-wide instructions Copilot auto-loads. Make this file a one-liner: `See [.copilot/master.prompt.md](../.copilot/master.prompt.md) and [.copilot/config.yaml](../.copilot/config.yaml).`
- `.github/chatmodes/*.chatmode.md` — one per agent if you want them as selectable chat modes; each `include`s its `agents/<role>.md` plus `master.prompt.md`.

## Operating principle

Every task flows through the **feedback loop** in `workflows/feedback-loop.md`:

`Plan → Build → Test → Validate → Review → Iterate`

No code is "done" until it is **testable, tested, validated, and reviewed**. The Principal Reviewer is the only agent that can mark work complete.
