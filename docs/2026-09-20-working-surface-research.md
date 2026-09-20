# The working surface — research, before the design

> Written 2026-09-20, from the owner's direction: **the desktop is becoming a user-driven
> application, and it should hold code sessions the way a terminal agent CLI does — but across
> different agents, repositories and concurrent sessions.** Research first, deliberately: the platform's
> existing look was designed properly once (D41, `2026-09-19-platform-ux.md`) and this is a bigger
> change than that one. This document is the *input* to that design, not the design. It records what
> the field already knows, what Daoris already has that nobody else does, and the questions the design
> has to answer — above all one collision that is not a UI question at all.

## 1. What is actually being asked for

The platform today is an **operations console**: five views, a sidebar, drawers, and a design language
built for *managing* a family of repositories (`2026-09-19-platform-ux.md` — "one person uses this
daily beside a terminal"). The phrase *beside a terminal* is the thing that has changed. The ask is
for the terminal's job to move inside: a person should **work** here, holding several live coding
sessions across several repositories, not merely watch records of them.

Those are different products. An operations console optimises for *scanning state*; a working surface
optimises for *holding attention on one thing while several others run*. Almost every decision below
follows from that sentence.

## 2. What the field has settled on

Four concerns appear in every tool built for this, and they are a useful checklist because Daoris
currently has one and a half of them:

| Concern | What it means | Daoris today |
|---|---|---|
| **Visibility** | what every agent is doing *right now* | the console streams (SES1) — but only inside one drawer, one session at a time |
| **Isolation** | worktrees, branches or containers so agents do not collide | **serialised instead**: one session per repository (see §4) |
| **Review** | diffs, approvals, merge readiness | none — evidence is a commit-list string |
| **Organisation** | boards, lists or groupings that keep parallel work legible | quests, which is a *better* primitive than most (see §3) |

Other findings worth carrying into the design:

- **Mid-run visibility is not decoration.** Sessions with no mid-run progress panel produced **3× the
  user abandonment** of sessions with one — *at identical output quality*. This is the strongest
  empirical claim in the material and it argues for promoting the console from a well inside a drawer
  to a first-class surface.
- **The activity panel is separate from the conversation.** Tool calls, steps and progress belong in
  an audit layer beside the transcript, not inline in it, with **progressive disclosure** — "3 of 7
  steps" by default, drill-down on demand. Daoris's console is currently one undifferentiated stream.
- **Three approval shapes** recur: high-risk action gates (pause and present the action with its
  consequences), plan-and-execute previews (review the steps before autonomous work), and progressive
  delegation (autonomy widens as the person's own approvals show a pattern). D37 already fixes where
  Daoris's line is; what is missing is the *surface* — and note that `AwaitingPerson` is already a
  session state meaning "only the person can clear this", with **nowhere in the UI that says so**.
- **Session identity is the highest-leverage detail.** The named anti-pattern is tab overload:
  sessions that cannot be told apart at a glance. Naming and grouping beat any amount of chrome.
- **Do not ask the person to watch.** Continuous monitoring is reported as the main source of
  fatigue; the recommended shape is periodic status plus a **notification on completion or block**.
  Daoris raises `SESSION_ENDED` on its bus already and surfaces it as a toast only if the page is open.
- **Boards, sidebars and pane workspaces have replaced flat transcript lists**, and
  **worktree-per-session or container-per-session is becoming the standard isolation model.**

## 3. What Daoris already has that these tools do not

Worth stating plainly, because the design should lean on it rather than re-implementing a kanban
board:

- **Quests are a real work primitive across repositories.** Other tools invent a board to hold
  "tasks"; Daoris has an asked-for, addressed, answerable unit with a lifecycle and a refusal path,
  already shared between machines. The organisation layer is largely built.
- **Cross-repository is native, not bolted on.** The registry, workspaces and the driver are already
  many-repository. Most of the field is one-repo-per-window with a switcher on top.
- **The session record is observed and durable**, survives a restart, and syncs to other machines —
  so "what happened here" outlives the window, which a terminal multiplexer cannot claim.
- **The console already streams**, bounded and sequenced, with the transcript as the durable copy.
- **The harness is not hard-wired** (D24, SES3): several harnesses, several accounts, chosen per
  session. A surface that assumes one agent vendor would throw that away.

## 4. The collision that is not a UI question

**Worktree-per-session is the field's answer to isolation. Daoris's answer is the opposite: one active
session per repository, because "two agents in one working tree corrupt it regardless of who is
typing" (D49 §3, D46).** The lock is deliberate, it is enforced in one place (`SessionLedger`), and
the family rehearsal asserts it in both directions.

The direction "concurrent sessions across repositories" does not by itself break that lock — different
repositories, different trees. But "code sessions like what we are doing here", plural, *within* one
repository does, and a working surface will make people want exactly that.

So the design must answer, explicitly and with a decision recorded:

1. **Does the tree stay the unit of exclusion?** Keeping it is coherent and cheap, and the refusal
   already names what holds the tree. It also caps a person at one session per repository forever.
2. **Or does a session get its own worktree**, making the *worktree* the unit of exclusion rather than
   the repository? That is the field's answer, it composes with git rather than against it, and it
   changes: the registry (one root per repository today), the clean-tree spawn rule, evidence
   collection, `WorkingTree`, the provenance stamp (WSP4: which commit speaks), and the ledger's lock.
   It is a substantial change to the parts of Daoris that are most load-bearing.

**This is the first thing the design session should settle, and it is a D-numbered decision, not a
layout choice.** Everything about the surface — whether the primary axis is repository or session,
whether a repository row expands to several live sessions — follows from it.

## 5. Questions the design has to answer

- **What is the primary axis** — repository, session, or quest? The console's five views are
  repository- and quest-shaped; a working surface is usually session-shaped.
- **Does the operations console survive alongside it, or become a view inside it?** D38's one-UI rule
  says there is one interface; it does not say which shape wins.
- **Where does a session that needs the person appear**, and how loudly? `AwaitingPerson` exists and
  is invisible; a blocked session nobody notices is the failure this whole surface exists to prevent.
- **What does review look like** when evidence is currently a commit list? A diff surface is the
  single largest missing piece, and it is what makes "approve and merge" possible at all.
- **How much of the terminal's affordance is actually wanted** — is this a transcript with an input
  box (what SES2 built), or closer to a pane with a real PTY? The second is a much larger commitment
  and changes what "the harness carries the conversation" means in practice.
- **What still holds when the window is closed?** The driver already runs headless; the surface must
  not become the only way to see what happened.
- **Notifications**: OS-level, and on what — completion, block, refusal? The research is clear that
  polling by eye is what people stop doing.

## 6. What must not move

- **D38 — one UI.** The platform is the same bytes in a browser and in the shell. A working surface
  that only exists in the desktop is acceptable *only* where it is structurally desktop-only, which is
  the same argument the console and chat already make (D47 §4: output is transcript-class and never
  leaves the machine).
- **D31 — doctrine stays unwritable** from every surface.
- **D37 — the automated middle, the human boundary.** More UI must not become more autonomy:
  destructive, irreversible and outward-facing actions stay explicitly human however good the
  approval surface gets.
- **D24 — no model is named.** The harness carries the model; the surface reports what a record says.
- **The design language of `2026-09-19-platform-ux.md`** — tokens, the validated status palette, the
  accessibility rules. A second visual language inside one app is the thing D41 was written to prevent.

## Sources

- [Managing Multiple AI Agent Sessions Without Losing Your Mind — Agents UI](https://agents-ui.com/blog/managing-multiple-ai-agent-sessions/)
- [Agentic UX: Frontend Design Patterns for AI Agents in 2026 — Zylos Research](https://zylos.ai/research/2026-05-28-agentic-ux-frontend-design-patterns-ai-agents/)
- [Best Tools for Managing Parallel AI Coding Agents in 2026 — DEV Community](https://dev.to/stravukarl/best-tools-for-managing-parallel-ai-coding-agents-in-2026-14l8)
- [Conductor vs Vibe Kanban vs Nimbalyst: Agent Management Compared — Nimbalyst](https://nimbalyst.com/compare/nimbalyst-vs-conductor-vs-vibe-kanban/)
- [AI Agent Orchestration in 2026: Patterns, Tools, and the Complete Architecture Guide — amux](https://amux.io/guides/ai-agent-orchestration-2026/)
- [How to Run a Multi-Agent Coding Workspace — Augment Code](https://www.augmentcode.com/guides/how-to-run-a-multi-agent-coding-workspace)
