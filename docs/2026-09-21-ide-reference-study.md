# IDE reference study — the desktop is a code-gen-driven IDE

**Date:** 2026-09-21 · **Status:** evidence, feeding D55 · **Prompted by:** the owner, this session —
*"you should reference more existing application for designing the ui/ux"* and *"the desktop is
becoming more a dev ide (but code gen driven)"*.

## 0. Why a second study

`docs/2026-09-20-working-surface-research.md` studied **session managers** — Conductor, Vibe Kanban,
amux, Nimbalyst — and answered *how do you keep parallel agent runs legible*. It did not study
**IDEs**, because the surface was then framed as a view inside a management console: a sixth nav item
beside Overview and Quests.

That framing has moved. If the desktop is where the work happens, the reference class is not other
dashboards — it is **the applications developers already live in all day**, and the small number that
have re-cut those applications around generated code. This study adds that class, says what to take
from each, and forces one fork the old framing let us avoid.

Everything here is a **structural** reading: geometry, regions, what binds to what. Pixels remain
D41's and are not up for adoption (the same line `docs/2026-09-21-working-surface-components.md` §3a
drew around dsh).

## 1. The five frames that exist

| Frame | Exemplars | Organising object |
|---|---|---|
| **The workbench** | VS Code | the **file**: activity bar (domains) · side bar (navigator, swaps with the domain) · editor groups (tiling tabs) · panel (transient output) · status bar (ambient state) |
| **Tool windows** | IntelliJ, Rider, Visual Studio | the **project, plus a tree of running things**: dockable windows on all four edges, each with a stripe button |
| **Navigator · editor · inspector** | Xcode, and every design tool | the **selection**: left tree, centre canvas, right inspector bound to what is selected, bottom debug area |
| **An agent panel bolted to an editor** | Copilot in VS Code, Zed's Agent Panel, early Cursor | still the **file**; the agent is a guest in a frame built for something else |
| **The run list** | Devin, Codex cloud, Jules, GitHub Actions, Buildkite | the **run**: a list; select one and get its plan, logs and diff |

**Cursor 3** (April 2026) is the interesting case because it refuses to choose. Its **Agents Window**
is a *peer* to the IDE — the changelog's words are that you "switch back to the IDE anytime, or have
both open simultaneously" — and the interface is described as **centered on agents instead of files**.
That is the same sentence as the owner's, arrived at by the product with the most users in this
category, and it is why §4 below is a fork rather than a detail.

## 2. What each application already solved

Verdict column: **take** · **adapt** · **reject with a reason**.

### Cursor 3 — the Agents Window

**Mechanism.** A sidebar listing every active session — local or cloud, **across all repositories** —
each row carrying the task that started it, the repository it targets, where it runs, its current
step, elapsed time, and a live log. Sessions open as tabs that can sit side-by-side or in a grid.

**Verdict: take the row's content model wholesale.** It independently validates design §3's
`repository · kind · what it is for · state · age`, and it carries **two fields Daoris has in the
record and does not show**:

- **where it runs.** Daoris is multi-machine by construction (D47), and a session's row saying nothing
  about which machine holds it is a gap the remote made and nobody has filled.
- **elapsed**, not only "moved 4m ago". A session working for three hours and one working for three
  minutes read identically today.

### Cursor — worktrees from the Agents Window

**Mechanism.** `/worktree`, and "move an agent into a worktree" as an action *on the session*: Cursor
creates a separate checkout and the agent continues inside it, isolated from the main one.

**Verdict: take the control, the model is already built.** D51 made the tree the unit of exclusion and
SURF3 built session trees — but the only way to ask for one is the Machine view's **per-repository**
toggle. "Give *this* session its own tree" is a per-session act in every reference, and it is the one
that matches how the need actually arrives: you want a second session in a repository that currently
has one.

### Cursor — `/best-of-n`

**Mechanism.** The same task run across several models, each in its own worktree, outcomes compared.

**Verdict: reject, and record why.** Daoris's adapters are one-per-repository by **domain ownership**
(D23): the agent that owns a codebase is the one that knows it. Racing models inside a repository is a
different product with a different premise. Worth noting that it is *cheap* now that the tree is the
unit of exclusion — so this is a "not this" rather than a "cannot".

### Zed — the Agent Panel over ACP, and the review multibuffer

**Mechanism.** Zed's agent panel speaks **Agent Client Protocol** — the same wire ACP1 just built —
and its review surface is an `AgentDiffPane`: a **multibuffer** aggregating every changed file into
one scrollable view with **keep/reject per hunk**, and the same hunk controls available inline in the
individual files. Tool calls that edit code stream into that diff view as they happen.

**Verdict: two separate takes.**

1. **The strongest available validation of D53.** The reference UI in this category is an ACP client.
   Daoris speaking ACP is not a bet on a protocol — it is the same door the nearest neighbour uses,
   which is exactly the "protocol, not product" argument D53 made from the dsh evidence.
2. **Adapt the multibuffer for SURF6.** One scrollable review of everything that changed, hunk
   controls in place, beats a file tree plus a diff pane — it is the shape that makes a forty-file
   review finishable. But **Daoris's review is read-only**: the session already committed, and D37 puts
   the person at the *outcome*. So the geometry transfers and the verbs do not — not keep/reject, but
   **accept the work** or **send it back as a quest**, which is the one move Daoris has that an editor
   does not.

### VS Code — the workbench

**Mechanism.** Activity bar for domains; side bar that swaps with the chosen domain; a **panel** for
transient output (terminal, problems, output) that grows, shrinks and hides; a **status bar** for
ambient state.

**Verdict: take three of the four.**

- **The activity bar.** Daoris's five nav items are already domains; nothing changes but their home.
- **The panel.** A session's console is transient output. It is currently a fixed-height well inside a
  drawer, which is the one arrangement that cannot be made bigger when you need it bigger.
- **The status bar.** `driver: running · 2 sessions · workspace: default · remote: wired` is ambient
  state that must be true without being looked at, and Daoris has **nowhere to put it** today.

Reject the **editor group** model: Daoris does not edit files, and tiling editors is chrome for a
capability it deliberately does not have (D31 — doctrine is unwritable from every surface, and the
repository's own code is the session's job, not the person's, in a code-gen-driven loop).

### VS Code — the Source Control view

**Mechanism.** A tree of staged and unstaged changes with counts, inline diff on click.

**Verdict: adapt the entry, reject the model.** Daoris never stages — the session commits. Take the
**tree with counts as the door into review**; leave staging where it belongs.

### JetBrains — the Services tool window

**Mechanism.** One tree of every running thing — servers, containers, test runs, database sessions —
grouped by type then by configuration, **each node carrying its own console and its own controls**.
Stopping a node is a control on the node.

**Verdict: the closest existing analogue to Daoris's rail, and the most battle-tested.** It predates
every agent product here by a decade. What it gets right that the agent products mostly do not: the
console is **attached to the selected node** rather than being a separate surface you navigate to, and
the grouping is a deliberate two-level discipline rather than a flat list that grows. Daoris groups by
repository, which §3 of the design already argues is the right axis — this is the evidence that a
*two-level* grouping is what survives contact with many running things.

### JetBrains / VS Code — the command palette

**Verdict: take, and note the cost it pays for.** It is the only affordance that scales past roughly
seven top-level domains, and Daoris is about to have Work, Review and five management domains. Adding
it later is much more expensive than the keybinding suggests, because every action has to become
addressable by name — which is a discipline, not a widget.

### Devin · Codex cloud · Jules — the run list

**Mechanism.** A list of runs; selecting one gives plan, shell, editor and browser as **tabs** within
the attended run.

**Verdict: take the tabs, reject the gate.** Daoris's attended session already has four parts — head,
stream, timeline, composer — and tabs are how those stop competing for one column on a laptop. But
**reject the plan-approval gate**: D37 and D52 put the person at the target and the outcome, not at a
mid-run checkpoint, and a surface that offers approval mid-run is the surface that quietly widens
autonomy, which is precisely what D52 forbids.

### GitHub Actions · Buildkite — annotations beside logs

**Mechanism.** A run list; inside a run, the raw log, with structured **annotations** rendered beside
it rather than inside it.

**Verdict: validation, nothing to take.** Design §3's "timeline beside the stream, never inside it"
is the same pattern, reached independently, in the domain that has had the longest to get it wrong.

### Warp — blocks

**Mechanism.** Each command and its output is one addressable, collapsible, linkable block.

**Verdict: take for the timeline.** An agent turn — prompt, tool calls, message — is a block, and ACP
hands Daoris exactly those boundaries in `session/update` where stdout parsing never could. This is
the concrete payoff D52 predicted and D53 bought: *the timeline is only buildable because the protocol
is.*

### GitHub pull-request review — "Viewed" per file

**Verdict: take for SURF6.** On a long diff, a per-file "I have looked at this" is the single thing
that makes a forty-file review finishable, and it is cheap: one boolean per path, per person, not
even persisted server-side.

## 3. The five patterns that recur — and are therefore not opinions

1. **The list of running things is a first-class, always-available region** — not a drawer, not a
   modal, not a card in a feed.
2. **Selecting one thing binds every other region to it.** Xcode's inspector, JetBrains' console,
   Devin's tabs, Cursor's agent tab: one selection, everything else follows.
3. **Output is transient and lives in a region you can grow, shrink and hide** — never a fixed-height
   well inside a card.
4. **Ambient state lives in a status bar**, because it has to be true without being looked at.
5. **Review is a surface of its own, aggregated across files, with a per-unit "done" mark.**

**Daoris's platform today has none of the five.** That is not a criticism of it — it was built as a
management console for quests and knowledge (D38), and it is a good one. It is the precise statement
of what the positioning change costs.

## 4. The fork this forces: one frame, or two?

The old framing let this go unasked, because a sixth nav item is not a frame. It has to be answered
now.

| | What it is | Cost |
|---|---|---|
| **A. Work as a sixth nav item** | the current plan (SURF4d) | **loses all five patterns of §3.** A rail plus a stream plus a timeline does not fit in a content column designed for cards, and the console stays a well inside a box |
| **B. Two modes, one app** ← **recommended** | a **mode switch** between *Manage* and *Work*, Cursor 3's answer: peers, either one open, both reachable instantly | one router change and one frame component. Work is simply **absent in a browser**, exactly as every shell-gated control already is |
| **C. Two frontends** | a desktop-only IDE shell that hosts the platform's views as tool windows | a second frontend, and it re-opens D38 — the platform stops being the only UI |

**B, for three reasons.** It keeps one bundle, one router and one component language, so D42's stack
and D38's "the platform is the only UI" both survive — as *one UI with two frames*, which is what
Cursor shipped. It costs the browser nothing: over a keyed remote the Work mode is not rendered, the
same rule as the stream, the diff and a tree path (D47 §4), and Playwright asserts the absence rather
than the presence. And it is the only option that lets the five recurring patterns exist at all
without a second application to maintain.

**What B does *not* mean.** Not a second design language, not a second build, not an editor. Daoris is
a **code-gen-driven** IDE: the person's job is to set targets, attend, and verify outcomes — so the
centre of the frame is a *session*, not a file, and there is no text editor in the plan at any point.

## 5. What this changes in the plan

Nothing built so far is wasted — SURF4a's atoms and the molecules of SURF4b are frame-independent,
which is exactly what the component plan's "a molecule imports no hook" rule bought.

- **SURF4d changes shape**: from "a sixth nav item and last-view memory" to "the **Work frame** and
  the mode switch". Same components, different container.
- **Three regions join the inventory**: the **status bar** (ambient), the **panel** (the stream,
  growable/hideable), and the **mode switch** itself.
- **`SessionRow` gains two fields** — *where it runs* and *elapsed* — both already in the record or
  derivable from it.
- **A per-session "own tree" control** joins SURF4b/4c, beside the Machine view's per-repository one.
- **SURF6 becomes a multibuffer**, not a file tree plus a pane, with *accept* / *send back as a quest*
  where an editor has keep/reject, and a per-file "viewed" mark.
- **A command palette** becomes a real item rather than a nicety, and should land before the domain
  count grows, not after.

## 5b. The window is part of the frame (owner, same session)

*"you probably can use shenora frameless window and setup custom top menu and also we might need
multi window for monitoring too"* — and the shell runtime already ships both, which was checked
against `Shenora.Windows` 0.16.0's own API documentation before it was planned.

| Capability | The framework's piece | What Daoris gets |
|---|---|---|
| **Frameless chrome** | `OptimizedForm` + `OptimizedFormOptions.FramelessChrome` — no title bar, native side/bottom resize borders kept, manual work-area maximize, DWM border themed, rounded while windowed | the top strip becomes **application chrome**: app menu, the Manage ⇄ Work mode switch, the workspace scope, the caption buttons — the arrangement every IDE in §2 uses |
| **Page-driven window commands** | `WindowCommandModule` (`SHENORA.WINDOW`) / `WindowCommands` in `@shenora/react`: minimize, toggle-maximize, close, is-maximized, start-drag, start-resize, set-theme, set-caption-buttons | the custom strip behaves like a real title bar — including **Windows 11 Snap Layouts**, which the page gets only by reporting its caption-button rectangles |
| **Multi-window** | `SecondaryWindows` — named windows, each on its **own STA thread and message pump**, one per name, geometry persisted per name | a **monitor window** on a second screen, and a **detached session** window; §3's pattern 1 becomes true even while the main window is in Manage |

**Three traps the documentation names, and why they argue for adopting rather than hand-rolling.**
`Form.WindowState` and `Form.RestoreBounds` **lie** about a frameless window — the maximize is manual
and keeps `WindowState.Normal` — and reading them makes *restore a permanent no-op*;
`IAppMaximizable` is the truth and the existing state stack already reads it.
`WindowCommandModule` must be mapped **late**, from where the window is created, because it needs a
live form. And `SET_THEME` is not optional for an app that follows the OS theme, or the DWM border
keeps the old one.

**One constraint, found by reading and recorded now.** `WindowCommandModule` targets one form and its
module name is reserved and singular, so **secondary windows keep their native frame**; frameless
chrome belongs to the main window. A monitor window is a utility, and OS chrome is what a utility
should wear.

## 6. What does not move

- **D41's language**, **D42's stack**, the **i18n parity gate**, the **three-part refusal**, and the
  **disclosure boundary** — the same five that §6 of the component plan already fixed.
- **D23**: adapters arrive deliberately, one owner per domain. No model racing.
- **D37 / D52**: the person is at the target and the outcome. No mid-run approval gate, however many
  references offer one — an approval surface is how autonomy widens by accident.
- **D31**: doctrine stays unwritable from every surface, and no reference here changes that.

## Sources

- [New Cursor Interface — Cursor changelog 3.0](https://cursor.com/changelog/3-0)
- [Worktrees — Cursor Docs](https://cursor.com/docs/configuration/worktrees)
- [Reviewing and Testing Code — Cursor Learn](https://cursor.com/docs/agent/review)
- [Agent Panel — Zed Docs](https://zed.dev/docs/ai/agent-panel)
- [Claude Code: Now in Beta in Zed — Zed's Blog](https://zed.dev/blog/claude-code-via-acp)
- [Agent Panel and UI — zed-industries/zed, DeepWiki](https://deepwiki.com/zed-industries/zed/8.1-agent-panel-and-ui)
- [Background Coding Agents Compared: Cursor vs Claude Code vs Devin vs Copilot vs Codex (2026) — amux](https://amux.io/guides/background-agents-compared/)
- [Cursor 3 Agents Window: Multi-Agent Coding in 2026](https://www.gilricardo.com/blog/cursor-3-agents-window-multi-agent-coding-2026)
- VS Code's workbench, JetBrains' Services tool window, Xcode's navigator/inspector, Warp's blocks and
  GitHub's pull-request review are read from working knowledge of those products rather than from a
  cited page; each is stable, long-standing behaviour rather than a recent release note.
