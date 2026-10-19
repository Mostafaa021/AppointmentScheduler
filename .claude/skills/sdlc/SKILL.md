---
name: sdlc
description: Run a feature or change through the full SDLC ceremony with the role agents (product-owner, architect, developers, qa, security, code review, devops, tech-writer, scrum-master). Use when the user says "/sdlc <feature>", asks to build a new feature "properly", or for the interview live change with "--fast".
---
# /sdlc — role-agent ceremony

Arguments: `<feature description>` and optional `--fast` (small change: no architect/security/devops phases).
Process definition: `docs/sdlc.md`. Agents: `.claude/agents/`.

## Steps (main session dispatches; agents hand off through repo files)
0## Steps (main session dispatches; agents hand off through repo files)
0. **Plan** — the change gets a plan file `.claude/plans/NNN-<slug>.md` (index in `.claude/plans/README.md`); the architect's
   design is linked from it, and each gate updates its Progress line.
1. **Open** — run `scrum-master` (mode `open`) with the request. It returns the backlog row and the product-owner brief.
2. **Refine** — run `product-owner` with that brief → spec with AC IDs, DoR checklist.
   If the agent reports open questions: ask the user, then re-run. Do not guess.
3. **Design** (skip with `--fast`) — `scrum-master gate` → `architect`.
4. **Human approval** — show the user the spec path + Design summary (AC list, layers, data/API changes) and ask to approve.
   Stop until they answer. With `--fast`, show the AC list instead.
5. **Build** — `scrum-master gate` → `backend-developer` and/or `frontend-developer`. Run both in parallel only when the spec's
   API contract is final and they touch disjoint files.
6. **Test** — `scrum-master gate` → `qa-engineer`.
7. **Review** — `scrum-master gate` → `code-reviewer` (and `security-engineer` in parallel unless `--fast`).
   Any must-fix → back to the developer agent with the review file path → re-run the reviewer. Max 3 loops, then ask the user.
8. **Release** (skip with `--fast` unless config/infra changed) — `devops-engineer`.
9. **Document** — `tech-writer` (DoD).
10. **Close** — `scrum-master close`; then run `/log-prompt` for any notable prompt of this item; summarize to the user:
    spec path, tests added (counts), review verdicts, files changed.

## Rules
- Before each dispatch, the scrum-master gate must say ADVANCE; BLOCKED → route to the agent it names.
- Pass agents file paths, not pasted content. Each brief states the gate the agent must reach.
- Never commit without the user's go-ahead.
