# The platform's design language

**Status: approved direction from the owner, 2026-09-19 — "design the UI/UX properly, because this is
used by a human." Written before the build; D41 records the decision.** The platform design
(`2026-09-19-platform-design.md`) settled *what* the views are; this settles *how the product feels*:
the shell, the tokens, the components, and the interaction rules. One person uses this daily beside a
terminal, in light or dark, to manage a family of repositories being built largely by agents. It is a
professional operations console, and it should feel like one — calm, dense where density earns its
keep, and honest everywhere.

## 1. Character

**Warm paper, thin ink, one bronze accent.** The existing aesthetic is kept deliberately — it is
distinctive and restful — and formalized: the data and the words are the only loud things on the page.
Decoration budget goes to *hierarchy*, not ornament. The serif appears exactly once, in the wordmark;
everything else is the system sans. 道衍 sits beside the wordmark as the one brand gesture.

## 2. The shell

> **Amended by D56 (2026-09-21).** The labelled 15rem sidebar described here is **retired**: the
> application wears a 36px **app strip** (wordmark, Manage ⇄ Work, workspace scope) and a 48px
> **activity bar** carrying these five items as icons, identical in both frames, with the tier pill
> and the index count moved to the **status bar**. The active-item treatment, the badge, and "global
> state lives in exactly one place" all survive; only their homes moved.
> `docs/2026-09-21-desktop-frame-design.md` §3 is the current shell.

A **fixed left sidebar** and a content column — the shape of a console, not a document.

- **Sidebar (15rem)**: wordmark (`Daoris` · 道衍), then navigation — five items, each an icon, a label,
  and (Quests only) the outstanding-count badge; the active item carries a 2px accent rail and the
  accent-soft field. The foot of the sidebar holds the global state: the recall-tier pill (D24 — stated
  on every screen), the index count line, and the refresh action. Global state lives in exactly one
  place; views never restate it.
- **Content column**: max 72rem, generous padding. Every view opens with a **page header** — title,
  one-line description, and the view's primary action on the right (Quests: "New quest"). The header is
  the only h-level element per view; sections inside use small muted section titles.
- **Narrow (< 56rem)**: the sidebar becomes a top bar — wordmark left, icon navigation in a row, the
  refresh action collapsed to its icon. Nothing is hidden behind a hamburger; five items fit.

## 3. Tokens

> **Amended by D56 (2026-09-21).** The **type scale and the spacing are denser** — 13px body against
> 15.2px — and the scale is now **seven named tokens** (`text-meta` … `text-value`) rather than rem
> literals, because the literal form had drifted to fifteen values across 203 sites with nothing
> reporting it. `tokens.test.ts` now fails on any raw size. **The palette below is untouched**: the
> warm paper, the thin ink, the one bronze accent and the six-check validated status set stand
> exactly as written. `docs/2026-09-21-desktop-frame-design.md` §4 has the new scale.

- **Type scale** (rem): 0.72 mono-meta · 0.8 small · 0.875 secondary · 0.95 body · 1.05 card title ·
  1.25 view title · 1.5 wordmark · 2.0 tile value. Tile values wear proportional figures; columns of
  numbers wear `tabular-nums`.
- **Spacing** on a 4px scale: 4 · 8 · 12 · 16 · 24 · 32. **Radii**: 6 controls · 8 cards · 10 overlays.
- **Surfaces**: `--page`, `--raised` (cards, controls), `--overlay` (drawers, toasts — one step above
  raised), `--line` and `--line-strong`. **Ink**: `--ink`, `--ink-soft`, `--ink-faint`.
- **Accent** (`--accent`, `--accent-soft`) is the *interactive identity* — buttons, active nav, links,
  the single-series bars. It is never a status.
- **Status is its own validated palette**, one token per quest state, soft field derived by color-mix:

  | State | Light (on `#faf9f6`) | Dark (on `#16161a`) |
  |---|---|---|
  | Open — waiting | `#b07818` | `#bd8226` |
  | Taken — in progress | `#2f6db3` | `#5c97dd` |
  | Done | `#2c9a62` | `#3da476` |
  | Declined | `#9e2f24` | `#c74534` |

  **Computed, not eyeballed** (the visualization skill's validator, all six checks): light passes with
  worst adjacent deutan ΔE 13.3 and normal-vision 21.0; dark passes with worst deutan 9.2 and
  normal-vision 17.5. The red/green pair is separated by lightness as well as hue, which is what moved
  deutan from a failing 3.9 to passing. Status never appears color-alone: every pill carries its text
  label.
- **Motion**: 140ms ease-out on overlays and hovers; `prefers-reduced-motion` disables it. No shimmer
  anywhere — loading placeholders are static two-tone.

## 4. Components

> **Amended 2026-09-22, from a polish pass driven by screenshots** (owner: *"keep polish the ui/ux
> — you can use screenshot tool to confirm"*). Four rules the original language did not state, each
> found by looking at the real window rather than by reasoning about it:
>
> - **Prose has a measure of its own.** §2's 72rem caps the CONTENT COLUMN, which is right for cards,
>   tiles and rows — and a paragraph inheriting it ran to about **190 characters a line** at the D56
>   scale, roughly triple what the eye tracks. Explanatory text goes in `Prose` (65ch, font-relative).
>   This is for the console EXPLAINING itself; a quest's body, a knowledge entry and a session's note
>   are **content** and are shown as they are.
> - **A field is outlined with `--line-strong`; a container with `--line`.** Inputs were `--line` on
>   `--raised` inside a `--raised` card, which in the dark theme is very nearly invisible — a form
>   whose fields you cannot find. Light mode hid the problem completely.
> - **A form is sized to what it holds**, never to the column it sits in: three equal thirds of a
>   72rem card gave a 570px box to the word "default".
> - **Status leads.** §5 already specifies `pill · title · route · how long` for Overview's rows, and
>   Quests had pushed the pill to the far edge — about a thousand pixels from the title it described.
>   The secondary marks stay right; identity goes first.
> - **A native form control carries the OS accent** (2026-09-23): `type="search"`'s built-in clear
>   button is painted by WebView2 in Windows' accent colour — a blue ✕ in a palette with no blue.
>   The platform draws its own affordances; a native one left showing is a defect in whichever
>   colour the person's system happens to be. **The same goes for a scrollbar** (2026-09-23): with
>   no `color-scheme` it is painted light whatever the tokens say, which showed as a pale OS bar
>   down a dark drawer the first time the composer grew long enough to scroll. `:root` declares
>   `color-scheme: light dark`, and the bar wears `--line-strong`.
> - **A setting is a row** (2026-09-23, owner: *"you have this really long list of setup (this
>   machine), which probably can be improved ui/ux"*): label, one-line hint, the control at the
>   right, and the paragraph that motivated it on an info glyph — `SettingRow`. Measured before: five
>   cards each opening with a four-line paragraph, the first checkbox 580px below the title and the
>   next dial a screen further down, and the right 60% of every card empty because prose is 65ch
>   and the column is 72rem. A settings page laid out as an essay is read once and scrolled past
>   every time after; a page of rows is scanned. A rarely-used form (wiring a deployment, adding an
>   account) is one press away, not open on every visit.
> - **A tooltip follows the editor's rules** (2026-09-23, owner: *"the tooltip not disappearing
>   properly, the tooltip position — for those we can follow vscode"*). Below its control, aligned
>   to its leading edge; beside a vertical rail; flipped only when there is no room. Gone on any
>   scroll, any key, any click, when the window loses focus, and the moment the pointer is off its
>   trigger — Radix alone closes on the trigger's own pointerleave, so a tip outlived a scrolled
>   list, a window blurred by the browser a login opened, and a button that disabled itself on the
>   click (a disabled control fires no pointerleave). `Tip` holds all of it; `ui.test.tsx` holds the
>   rules.
> - **A step in a task happens where it was started** (2026-09-23, owner: *"the ui for login claude,
>   during and after include the entire login workflow itself need better ui/ux"*). Signing an
>   account in streamed under the tool's DOOR, a card below the button pressed, and could not finish
>   at all (FIX-LOG). `SignIn` sits on the account's row: numbered steps in the order the person
>   meets them, one live control at a time (the link with a copy button, then the code box once the
>   tool asks), a Cancel that stops the process rather than the window, the raw output one
>   disclosure away, and the result as the row's own pill plus a sentence naming the account. The
>   door keeps its console for the actions that are the door's — install, update, pin.
> - **A file dropped anywhere but its target must not become the page** (2026-09-23, INT2). A
>   browser, and the desktop's webview, answers an unhandled file drop by *navigating to the file*,
>   which in the desktop replaces the whole application with a picture. So while the quest composer
>   is open, a stray drop is absorbed at the window, and the WHOLE composer is the drop target,
>   because aiming a file at a box inside a drawer is a chore. A pasted screenshot is a file, not
>   text: it is attached and kept out of the field it was pasted into.
> - **One navigation, and a count is a circle** (2026-09-23, owner → D66). The frame switch was a
>   second navigation stacked on the activity bar, so Sessions became a view on the bar and Settings
>   took the gear at its foot. The bar's counts are `CountBadge`: one fixed height, the same minimum
>   width, no line-height of their own. Measured 14 × 17.2px before and 16 × 16 after, on the window.
> - **The theme is the viewer's to choose** (D66): *System · Light · Dark* on Settings, applied as
>   `data-theme` before the first paint, pushed to the window's native chrome, and held by two
>   forced blocks in `tokens.css` that a test keeps equal to their system twins.
> - **An account reads as who is signed in, and a destructive edit asks once** (D66 §3). A row's
>   name is the tool's answer (`account-2` is only on disk). *Sign in to another account* is one
>   press with no name first. Remove wears the bin because it deletes, and its first press opens a
>   sentence saying what the second will do, beside *Remove it* and *never mind*. Only a door's own
>   work (install, update, pin) streams under *Ways in*: seen on the window, a removal's one line
>   sat there as a live console.
> - **A page never prints a machine path it was answered.** A local host answers this machine where
>   a kept file lies (a quest's attachment, D65 §2), and the drawer reads that path's *presence* as
>   "can be opened" and opens the file through the host's own route. A file the record names and
>   this machine does not hold says *kept on the machine that published it*, rather than looking
>   like a broken link.

> **Amended again 2026-09-22, the app strip** (owner: *"the application topbar you can take more
> example from application like vscode"*, and *"the backdrop should not cover the topbar? because we
> do have the hole for the 3 buttons"*). Both found by photographing the real window:
>
> - **The strip's middle is the command center.** It held ~1,400px of nothing at any real width —
>   wordmark and mode switch left, a lone 14px search glyph right against the caption buttons — while
>   the palette SURF10 named as repaying the icon rail's lost discoverability was that glyph. It is
>   now a centred, bounded, obviously-pressable pill that **says where you are** and prints its own
>   shortcut. Taken from the shape VS Code settled on, for the reason VS Code settled on it: a title
>   bar that carries no information is paying rent for a wordmark.
> - 🔴 **A scrim starts BELOW the strip, never over it.** The strip reserves three 44px slots the
>   **window** paints natively (SURF7), and a page-level backdrop cannot dim what the page does not
>   draw — so a full-bleed scrim greyed the whole title bar and left the caption buttons as a bright
>   block punched through it. Held by `tokens.test.ts` as a rule about every overlay, because the
>   next overlay would have inherited `inset-0` without anyone thinking about it. It is also what VS
>   Code does: the title bar stays live while quick-open is up. **And so does a panel** (2026-09-23):
>   the drawer stayed full-height after the scrim learned this, and its close button sat under the
>   window's — the same rule, now held for panels too.

- **Buttons**: `primary` (solid accent, paper text — the one loud control per view), default (raised +
  line), `ghost` (borderless, for in-card affordances), `danger` reserved for decline confirmation.
- **Pills** carry quest state: status text on its soft field with its hue — label always present.
- **Chips** carry declarations (owns/accepts/packs): quiet line-bordered tokens; `accent` variant for
  what a project *accepts*, because that is the actionable half.
- **Stat tiles**: label · value · context note, per the visualization discipline; a tile may wear the
  warn rail when its note is a warning (a quest sitting a week).
- **Drawer** (right, 32rem, overlay surface, scrim, ESC/scrim/× to close) is the single detail-and-form
  surface: reading a knowledge entry, composing a quest, and a quest's detail with its actions. One
  pattern instead of three; the list stays a list.
- **Toast** (bottom-right, overlay surface, auto-dismiss with close) carries every action outcome and
  error — the service's sentence verbatim, because the refusal text is the contract. Nothing shifts the
  layout to speak.
- **Empty states** are designed, not blank: a quiet glyph, one headline line, one body line, and the
  action that changes the fact ("Nothing is sitting" → "Ask for something").
- **Loading**: first load shows static skeleton rows; a refetch holds the previous render at reduced
  opacity — content never jumps.

> **Amended 2026-09-22 again, from a real VS Code window** (owner: *"there is a vscode opening in my
> app you can check its top/left/bottom bar design"*). Captured with the same instrument the polish
> passes use and read for structure only. Three differences from what had been built, and one gap:
>
> - **A menu bar carries no chevrons.** Its menus are plain words at small gaps — File, Edit,
>   Selection, View … — with no disclosure arrows at all. A menu bar is a convention strong enough
>   not to need marking, and a chevron per menu is one piece of furniture per menu. Dropped.
> - **Groups in the activity bar are separated by POSITION, not by rules**: a top group and a bottom
>   group pinned to the foot, no lines between. Daoris keeps one rule under the frames, because a
>   frame and a view inside one are different *kinds* of thing rather than two groups of the same
>   kind — but the rule is now the lighter of the two weights.
> - **The status bar is icon-and-number, not prose**: a remote glyph, then `⊗ 0  ⚠ 0`, then a bell.
>   Daoris's is wordier (`driver ● ready · 1 session(s) · workspace · every circle`), which is
>   defensible for a console whose facts need naming — but it is the bar most in need of a measured
>   pass, and this one did not do it.
> - 🔴 **The gap: layout toggles.** The right of its title bar holds four icons that show and hide the
>   panel, the sidebar and the secondary bar, immediately left of the window controls. Work has an
>   output panel and a right dock and neither can be reached from the strip — the state lives inside
>   `WorkFrame`, so this needs hoisting before it needs designing. Filed as **SURF11**.

> **Amended 2026-09-24, the sync item (SYNC6b)**, looked at on the real window against a throwaway
> remote, in both themes. The remote item became a **control** when the circle is wired, and it is
> **icon-and-number**: a cloud, then ↑ for work waiting to go up, ↓ for quests left behind, and ⚠
> for quests in conflict. A level circle shows the one word `synced`. Pressing it opens a menu above
> the bar (`AppMenu`'s rows, `modal={false}`) that says when the circle last synced, then the quests
> in conflict, then *Sync now*. Two things only the window showed:
> - A wall needs a **word on the bar**. The cloud-off glyph alone said nothing, so the bar now reads
>   `unreachable` in the warn tone.
> - A lead-in must not repeat the sentence it introduces. The host's wall already says what went
>   wrong, so the detail's lead-in says only *when*.

> **Amended 2026-09-24, the asks (INT4c).** Held by vitest and a Playwright check, and **not yet
> looked at on the real window** — the pass the other amendments here came from is still owed.
>
> - **An action goes with what it makes, not on the status bar.** The bar states facts, and its one
>   control (SYNC6b) acts on the bar's own fact. An ask makes quests, so its door heads Quests.
> - **Asking leads.** *Ask* is the view's one primary control, and *new quest* steps down to default:
>   the regular task enters at the circle (D65), and a quest to a repository the person already
>   knows is the second door.
> - **One carry, two composers.** The links, drop, paste and chooser are one molecule
>   (`compose/carry`). INT2's rules hold for both because there is one copy of them: a stray drop is
>   absorbed, the whole composer is the target, and a pasted screenshot is a file.
> - **A door opens something, or it is not a door.** Just after a publish, the answer names the
>   quest before the list holds it. So the record names it by id, and makes it a door only once the
>   page can open it. A button that closes one drawer and opens nothing is a dead click.

> **Amended 2026-09-24, asks in *What needs you* (INT4d).** Held by vitest and a Playwright check,
> and looked at on the real window in both themes and 中文, which found two defects.
>
> - **The band is live, or it lies.** An ask made by the other door (a terminal, a teammate's
>   sync) moves nothing else a tick reports, so the shell forwards a tick when the asks change too,
>   and the page refetches them on every tick. Before this, the band missed the ask until a reload,
>   and its parked intake read as a bare session.
> - **A record never says no intake ran above the intake that did.** A parked intake published
>   nothing, so the tier stays the declarations'. The record then says so in words of its own.
> - **A row is a door only where its destination exists.** A parked session opens in Sessions,
>   which only the desktop has. A quest nobody can take opens in its own drawer, and an ask opens in
>   its record, in a browser as on the desktop. A row with nowhere to go is text rather than a
>   button. Before this, a browser's parked row was a button that did nothing.
> - **An ask reads by what it waits for**: *proposed, not yet published*, or *its intake asked
>   you*. Its place is named as a circle, because a bare circle name among repository names reads
>   as one more repository.
> - **A record says who answered.** The tier is in words, and an intake session is a line of the
>   record: its state, then its tool. The tool is a door into Sessions wherever Sessions exists.

> **Amended 2026-09-24, Daoris's own AI (AGT6).** Held by vitest and a Playwright check, and looked
> at on the real window in both themes, with the intake off. The on state was not looked at, because
> choosing an agent on a machine that has asks waiting spawns a real session.
>
> - **Off is a value, not an absence.** The bridge leaves a null out, and the page tells a shell
>   older than the intake by the field's absence. So a null "off" hid the control from exactly the
>   state a person would switch it on from. Off is `""` on the wire.
> - **A section goes where its audience is.** *Daoris's own AI* sits between *Appearance* and *This
>   machine*: which tier answers search is the service's answer, given to every browser, so the
>   card is for everyone. Its intake row is `driver.json` and appears only with a shell. The card
>   is not moved under the machine's heading for the row it holds there.
> - **A tier is the service's sentence, and how to change it is ours.** The pill and the note are
>   `/api/status`'s, verbatim. The hint beside them is the page's own words: which variables, and
>   that the service reads them when it starts. A setting read from the environment gets a
>   sentence, never a control: a control would be a second source for a choice the deployment makes.
> - **A choice offers what can happen.** The intake's select lists the ways in this machine has
>   installed, because a named agent that is not installed only produces holds. What is in effect
>   is always shown, even when the list would not offer it.
> - **A status item that can be explained is a door.** The tier on the bar was text while nothing
>   explained it. It now leads to this card, by the bar's own rule that an item goes where its fact
>   is set.
> - **Two jobs in one list are named.** *What a start runs on* marks each row's job only once a
>   circle has two. Two rows called "a start in aurora" naming different agents would read as one
>   fact stated twice, and wrongly.

> **Amended 2026-09-24, a parked intake in Sessions (INT4g).** Held by vitest, and looked at on the
> real window in both themes, where *answer ask #id* opened the ask's record in Quests (Sessions is
> desktop-only, so Playwright does not reach it).
>
> - **A move offered where the answer is not is a dead end.** A parked intake was offered a parked
>   session's three moves, and each one ended the record without answering its ask, which fell back
>   to a proposal. *Finish* even wrote `completed`, which reads later as a publish. The intake's
>   answer is on the ask, so its one answering move is a door: *answer ask #id* opens the ask's
>   record in Quests, where publish and close already live.
> - **A move that stays says what it does not do.** *Stop it* stays, because ending the intake
>   without answering is a real choice, and it says the ask then stays a proposal.
> - **No box where nothing listens.** An intake is one turn, and a parked one has no process, so it
>   gets no composer. The "answer it in the box below" line goes with it.
> - **A record is named for what it serves.** An intake reads *intake for ask #id*, its rail kind is
>   *intake*, and its head says *ask* and *room* rather than *repository* and *tree*. A parked
>   intake holds no room, so its rail group claims nothing busy.

> **Amended 2026-09-24, registered is addressable (INT3, D70).** Held by vitest, and looked at on the
> real window: the group lists an unadopted repository under its new title. It offers that repository
> no *drive on this machine* control, which is INT3c.
>
> - **Who can be asked is the host's answer.** Every receiver list reads `addressable` from
>   `/api/registry` rather than re-deriving it from `adopted`. The lists are the quest composer's
>   *from*, *to* and *then ask*, the filter, and the ask's composer and record. A browser is never
>   told a root, so it could not derive the answer.
> - **The Projects view's unadopted group says who can answer**, in the door names a person sees
>   elsewhere: a protocol agent answers, and a direct one holds it. Its title is *Registered, not
>   adopted*, because *not addressable* stopped being true.
> - **What the planner drives, the screen can opt in** (INT3c, held by vitest and **not yet looked at
>   on the real window**). An unadopted repository with a root here gets the adopters' driving row
>   (drive, hold, own tree per session), one molecule for both. The planner treats the two alike
>   once past the door, and `daoris driver` already could (D50). One with no root gets nothing, since
>   there is nowhere to start it. Adoption's own acts stay off the row: *manage* writes a declaration
>   into the repository.
> - **A choice that outlives the door is offered, with the door said beside it.** On a machine whose
>   adapter rides the direct door, a quest there sits. The row says so under the control, in the
>   planner's terms, instead of hiding it. A standing opt-in is kept when the machine changes adapter,
>   and a control that appeared only on a protocol machine would leave the screen unable to set what
>   the terminal can. The door is read from the roster (the adapter and its `wire`). When the roster
>   cannot say, the row says nothing rather than guess.
> - **The group's paragraph claims only what is proven.** A protocol-door session there is handed
>   its connector. Whether a real agent can use it is not yet proven, because it asks before each use
>   and every request is refused (D70's last paragraph, INT3b). So the paragraph says that, rather
>   than "can answer it".

> **Amended 2026-09-24, a running intake in Sessions (INT4h).** Held by vitest, and looked at on the
> real window in both themes (Sessions is desktop-only, so Playwright does not reach it).
>
> - **No box where nothing listens, running or parked.** A running intake is one turn too. On the
>   pipe door a typed line went nowhere, and the page read the driver's `false` as *it ended*. On the
>   protocol door the line landed in the middle of the JSON-RPC stream. So it gets no composer, and
>   the head says why in the one line the box would have taken.
> - **A move that lived on a removed control moves with it.** The composer carried the only stop a
>   running intake had. The head carries it now, with the same line saying the ask then stays a
>   proposal. *Finish* does not move, because an intake ends itself.
> - **A door that looks is not a door that answers.** A running intake has asked nothing yet, so its
>   door to the ask is a default button (*open ask #id*), and only a parked one's is primary
>   (*answer ask #id*).

> **Amended 2026-09-24, what agents may do (PERM1, D72).** Held by vitest, and looked at on the
> installed window the same day (POLISH2, below), which found four defects on the card.
>
> - **A default has a switch and no remove; a person's rule has a remove.** A default is Daoris's,
>   switched off by id and removed by nothing, so the two kinds of row look different.
> - **Nothing on the card ranks one scope over another.** Precedence is the harness's (`deny` beats
>   `ask` beats `allow` across every scope), so each scope is listed as what it adds, and the one
>   sentence that says so sits on the info glyph.
> - **A list's word is a pill in the status palette**: allow in done's green, ask in open's amber,
>   deny in declined's red. They are read as outcomes, and the text label always rides with the
>   colour.
> - **The machine's scope is not called "This machine".** That is the section the card sits under,
>   so its scope reads *every session on this machine*.

> **Amended 2026-09-24, a folder waiting on the person's trust (D73).** Held by vitest, and looked at
> on the window in both themes at DEPLOY1's close (the archive has what it saw). *Trust this folder*
> was not pressed, because it writes the account's own file.
>
> - **The grant is offered where the hold is read.** The quest's drawer carries *trust this folder…*
>   under its sitting line, and *What needs you* carries a `trust` row. That row is the only place an
>   intake's held room shows, because an intake has no drawer.
> - **The question is the agent's own, asked in the open.** It names the folder, what trusting means
>   in the agent's terms, what the driver is holding, and the one file written. It grants on the
>   press and on nothing else. Inline in the drawer that is already open; in a drawer of its own from
>   the band.
> - **Never wider than the hold.** The screen offers a pair the driver produced and the shell
>   refuses any other, so a screen can never trust a folder the driver is not holding. The terminal is
>   where a person names any folder.
> - **One row per folder.** Two quests held on one untrusted tree are one grant, since the oldest
>   thing it holds.

> **Amended 2026-09-24, the installed application on an empty machine (POLISH2).** The owner's
> install, republished and read surface by surface in 中文 and English, in both themes. Every
> registration had been retired, so it showed what a new installation shows, which the fixture never
> can: the example family always holds a repository.
>
> - **An empty machine is a state, and it says what to do.** Projects was its header over a blank
>   page. The ask and quest composers opened forms whose circle, *from* and *to* offered nobody, so a
>   sentence written in full could never be sent. Projects now says nothing is registered and offers
>   *add repository*. Each composer says nobody can be asked yet, sends the reader to Projects, and
>   offers only *close*. The map, scoped to every circle, no longer says "this circle".
> - **A sentence's backticks are code** (`Inline`). Fourteen catalogue strings and many of the
>   service's and driver's sentences mark a command that way, and every one reached the window with
>   its backticks. The words are unchanged, so a service's sentence is still verbatim. A setting's
>   hint, a tip, a toast and an empty state take it on their own. Anywhere else a caller asks, since
>   content is shown as it is.
> - **A liveness mark never borrows an outcome's hue.** *Ended* is completed, failed, declined and
>   stopped at once, and it wore done's green, so a failed session sat in the rail beside a success
>   mark. It is neutral now, and the pill beside it names the outcome.
> - **An ideograph is never slanted.** The Chinese system face has no italic, so an italic hint was
>   slanted by synthesis. `font-synthesis-style: none` on the body keeps Chinese upright and the
>   Latin face's true italic.
> - **A card's heading is not its first row's label.** *What agents may do* and *Plugins* each said
>   their title twice. The row names the file or folder the card keeps, as Wiring's names its map.
> - **The rule between rows belongs to what has siblings.** Each default's `SettingRow` was alone in
>   its list item, so its `first:` and `last:` both held and took the rule and the padding away. Four
>   defaults ran together as one block. The item carries them now.
> - **One word per thing, in each language.** In 中文 "agent" had become 代理 in the later cards
>   and 智能体 everywhere else. "The driver" was sometimes 驱动器, which reads as a disk drive. And
>   the brand was 道衍 in running text where the rest says Daoris. The wordmark keeps 道衍.
> - **A default explains itself in the reader's language.** The English catalogue passes the driver's
>   sentence through as `{{why}}`, its only English copy, and 中文 translates it. A default the page
>   has not heard of keeps the driver's words.
> - **A panel that keeps its height keeps it as a sentence.** An ended session's console, after the
>   app restarted, was an empty bordered well, which reads as a field to type in. SURF8 had already
>   given the console a quiet sentence for this, and the output panel never passed one.
>
> **And what it left, closed as POLISH3 the same day:**
>
> - **The frame is three bars, and an overlay sits between them.** The scrim left the strip and the
>   activity bar alone and ran over the status bar, so behind a drawer the bar was bright for 48px
>   and grey after. Every scrim and panel now ends at `bottom-6`, held by `tokens.test.ts`.
> - **A name that repeats says which it is.** Two accounts signed in as one person read as one fact
>   stated twice, so the tool's own home says *this machine's own* beside the name, only then.
> - **中文 sets a number apart from Chinese**, in an age as in a span: `1 天前` beside `9 分钟`. A
>   Chinese word inside a Chinese sentence stays tight.
> - **A slow first answer is skeleton rows**, as §4 always said, with its words on the line the
>   count will take.

> **Amended 2026-09-24, the surfaces an empty install cannot show (POLISH4).** Read in the scratch
> shell over the example family, which holds what the owner's install did not: a parked session, a
> running and a parked intake, five asks, an unadopted repository. Both languages, both themes.
>
> - **A sentence about what the system can do is re-read when the decision under it moves.** Five
>   had outlived theirs: the index "scans the family's folder" (WSP2), only an adopter is
>   addressable (D70), the connector in an unadopted repository is unproven (PERM1 measured it),
>   finished work joins the band "once review exists" (review exists; looking is not recorded), and
>   the intake room's "nothing there can see a quest". A catalogue claim has no test pointing at the
>   code it describes, so it goes stale silently.
> - **A reason a person reads names no decision number.** The rules card said *(D37)*, and a person
>   using the application has no decisions record to look it up in. The sentence says what the
>   number meant.
> - **An ask says what it waits for wherever it is shown.** The band said *its intake asked you*
>   and the card below it said only *proposed*. The card now says *an intake is reading it*, or
>   wears the band's own warn mark.
> - **A place is named by its kind.** An ask's `default` beside `game → engine` read as one more
>   repository, as INT4d found for the band. The card and the record's receiver field say
>   *workspace default*.
> - **A label never repeats its value's first word**: *answered by* over *by declarations…*. And a
>   record's title is its first line, so its body is what follows, never the title again.
> - **Labels are a column.** A project card's *owns* and *accepts* flowed on one line with their
>   chips, so a wrapped chip fell back under the label. The labels sit in a column of their own and
>   the chips wrap in theirs. A command in a sentence is code, and the sentence is not monospace.
> - **A key names every mark, a number included; and parked is not working.** The number inside a
>   map node was in no legend, and a repository whose one session waited on the person was ringed
>   *working now*. It reads *waiting on you*, in the warn tone the band gives that fact.
> - **An excerpt shows the prose, never the file's machinery.** Every canon-shaped entry opens with
>   frontmatter, and a search snippet read `--- name: … applies_when: …`. The frontmatter still
>   matches; the window is taken after it.
> - **An empty answer is an empty state, and a note has a measure.** Convergence said *Nothing
>   converges above 0.75.* as a bare line beside a slider labelled ≥, and its tier note ran about
>   180 characters a line. 🔴 `max-w-prose` on a flex item caps its `basis-full`, so the measure goes
>   on a child of the full-width item, or the note stops breaking to its own line.
> - **One word per thing, again**: 中文 said 任务 for a quest in nineteen strings the map, the chain
>   strip, the monitor and the strikes setting grew after the glossary set 委托. A test holds it now.

> **Amended 2026-09-25, the conversation (D76, CONV2).** Held by vitest and stories, and looked at on
> the scratch window with a session driven over the protocol door, in English and 中文, light and dark.
>
> - **The centre is the record, then the conversation**, one scroll: the head read once, and the
>   agent's words what the region follows. It follows the tail until the person scrolls up, then
>   offers *back to bottom*; a history longer than a page offers *load earlier*.
> - **A finished turn folds its work.** Tool calls, thoughts and earlier messages collapse into one
>   row that counts them (*3 tool calls · 1 message · thought*); the last message stays open, because
>   the conclusion is what a reader came back for. A running turn is all open and says *working…*.
> - **The composed target is folded to two lines** and named as the driver's, never as the person's.
> - **A tool call is a row that says what it did**: its kind's glyph, its title, the file it
>   touched, an edit's `+n −m`, and its status. Closed by default, open when it failed. Open, an edit
>   shows its line diff in done's green and declined's red.
> - **Code is the paper's inks**: a keyword in the accent, a string and a number in the two cool
>   status hues, a comment faint, all from the tokens (`work/code.css`), with the language named and a
>   copy button. The agent's HTML is text, never markup, and a link opens outside the window.
> - **A plan's done step is a check, never a strike-through**: struck text reads as cancelled. Seen
>   on the window.
> - **An empty record says why**: the door carries only text, or the session ran before
>   conversations were kept. *Nothing said yet* under a parked pipe chat was untrue; a structured door
>   records the target the moment it starts. Seen on the window.
> - **The console is the raw view**, and its own empty sentence points at the conversation above
>   rather than saying *nothing said* beneath what was said. Seen in the detached window.

## 5. The views, restated in this language

- **Overview** — tiles row; then a two-column band on wide screens: *Outstanding, oldest first*
  (row = pill · title · route · how long — and, on the desktop, *sitting — why*, the driver's own
  sentence from its last look, truncated with the whole in its tip) beside *Repositories by index
  size* (single-hue bars, adopted dot, values in ink). Every row is a door: outstanding rows open the
  quest drawer; repository rows go to Projects. Above the tiles, *What needs you* (working surface
  §4) lists parked sessions, then asks waiting on a person (INT4d), then quests nobody can take, and
  it is absent when nothing waits.
- **Quests** — the primary page action is *Ask* (INT4c): its composer and each ask's record are
  drawers, and the asks sit above the quests as a group of their own, a card per ask. *New quest*
  opens the **compose drawer**; the list stays scannable (pill, title, route,
  age, first line of the ask) grouped Open / In progress / Closed; **clicking a card opens the detail
  drawer**: the whole ask, the meta, the note, and the actions — take, done, decline-with-reason —
  where there is room to act deliberately. Quick actions leave the cards; a card is for reading.
  What a quest carries (D65 §2) is **counted on the card** (a link glyph and a paperclip, each with
  its number, before the other marks) and **listed in the drawer**: links as links, and files by
  name and size, with a picture shown as one. The composer takes links one per line, and files by
  drop, paste or *choose files…*, each removable, and it says why a file was left off (the count,
  or the total) while the person is still choosing. **A chain** (D65 §4) is behind a press, *add a
  next step…*, because most asks are one quest: one step, whom and what, and a started step holds
  the publish back until it is whole. A step's card says which quest it follows. The drawer shows the
  whole chain as one strip (MAP1a): the ask, the quests before and after, the steps still to come as
  the asker wrote them (`{parent}` included), and every session under its quest. The quest being
  read is marked and named, and the others are underlined doors.
- **Projects** — adopted as cards (two columns wide) with chips and the local/canonical split;
  non-adopters as the marked list with the join steps proposed as text.
- **Convergence / Search** — unchanged in behaviour; they gain the page header and open entries in the
  drawer instead of a centered modal.
- **Map** (MAP2) — the circle's repositories on a ring, the quests between them as directed arrows
  with a count on each, and a shared finding as a dashed line; a detail panel beside the map says what
  the chosen node or line holds, and a legend says every mark in words. It adds no actions. What the
  window taught it (names outward, wide hit strokes, arrowheads in the map's units) is in
  `docs/2026-09-23-map-design.md` §1. A node's detail opens **its code map** (MAP3a), the same
  page one level in: modules as boxes in layers, what uses a module above it, an arrow that skips a
  layer bowed out past the column, and *Back to the workspace* where the page action goes.
- **Settings** (D66, as amended by **D75**) — one page with its **domains in a list at its left**,
  one shown at a time and reachable by name: *Appearance*, *Daoris's own AI*, *Workspace*,
  *Driver*, *Agents & accounts*, *Permissions*, *Plugins*. Every way in opens the domain its fact is
  set in, and a browser is offered only the first two. A card alone in its domain carries no title,
  because the list already names it. What follows is what the domains hold. *Appearance*: the theme
  and the language, each a segmented choice. Then, also for everyone, **Daoris's own AI** (AGT6): search and convergence with
  the service's tier and note verbatim and the variables that choose the model, and on the desktop
  the intake's agent with the account an intake in each circle runs as. Then, on the desktop only, **This machine**: the home's path under its heading,
  then cards that are *sections of one settings page*: Driver (the notification switch, the strikes dial), Wiring (the map's path,
  the wired rows, *Wire a workspace* behind a press), Agent tools (one row per tool, its accounts
  leading, its ways in beneath — a declared door wearing the plugin it came from), usage, *What a
  start runs on* (MAP1b: one row per workspace and job, each part with the setting that chose it, a held
  start's sentence at a reading measure), and
  Plugins (a row per folder: what it declares and speaks on, running or off, the driver's own
  sentence under a refused one, the switch and Remove). Every setting is a `SettingRow`; its
  terminal twin is the hint and its reason is the glyph.

## 6. Accessibility

Focus rings on everything interactive (accent, 2px, offset); drawers are `role="dialog"` with
`aria-modal`, ESC to close, focus moved in on open; icons are decorative (`aria-hidden`) beside real
labels; status is text plus hue, never hue alone; hit targets ≥ 28px; both themes are first-class —
the dark status palette is its own validated set, not a filter.
