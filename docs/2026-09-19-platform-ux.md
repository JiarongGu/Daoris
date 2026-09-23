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

## 5. The views, restated in this language

- **Overview** — tiles row; then a two-column band on wide screens: *Outstanding, oldest first*
  (row = pill · title · route · how long — and, on the desktop, *sitting — why*, the driver's own
  sentence from its last look, truncated with the whole in its tip) beside *Repositories by index
  size* (single-hue bars, adopted dot, values in ink). Every row is a door: outstanding rows open the
  quest drawer; repository rows go to Projects.
- **Quests** — page action opens the **compose drawer**; the list stays scannable (pill, title, route,
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
  `docs/2026-09-23-map-design.md` §1.
- **Settings** (D66) — *Appearance* first, for everyone: the theme and the language, each a
  segmented choice. Then, on the desktop only, **This machine**: the home's path under its heading,
  then cards that are *sections of one settings page*: Driver (the notification switch, the strikes dial), Wiring (the map's path,
  the wired rows, *Wire a workspace* behind a press), Agent tools (one row per tool, its accounts
  leading, its ways in beneath — a declared door wearing the plugin it came from), usage, and
  Plugins (a row per folder: what it declares and speaks on, running or off, the driver's own
  sentence under a refused one, the switch and Remove). Every setting is a `SettingRow`; its
  terminal twin is the hint and its reason is the glyph.

## 6. Accessibility

Focus rings on everything interactive (accent, 2px, offset); drawers are `role="dialog"` with
`aria-modal`, ESC to close, focus moved in on open; icons are decorative (`aria-hidden`) beside real
labels; status is text plus hue, never hue alone; hit targets ≥ 28px; both themes are first-class —
the dark status palette is its own validated set, not a filter.
