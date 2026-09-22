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
>   Code does: the title bar stays live while quick-open is up.

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

## 5. The views, restated in this language

- **Overview** — tiles row; then a two-column band on wide screens: *Outstanding, oldest first*
  (row = pill · title · route · how long) beside *Repositories by index size* (single-hue bars, adopted
  dot, values in ink). Every row is a door: outstanding rows open the quest drawer; repository rows go
  to Projects.
- **Quests** — page action opens the **compose drawer**; the list stays scannable (pill, title, route,
  age, first line of the ask) grouped Open / In progress / Closed; **clicking a card opens the detail
  drawer**: the whole ask, the meta, the note, and the actions — take, done, decline-with-reason —
  where there is room to act deliberately. Quick actions leave the cards; a card is for reading.
- **Projects** — adopted as cards (two columns wide) with chips and the local/canonical split;
  non-adopters as the marked list with the join steps proposed as text.
- **Convergence / Search** — unchanged in behaviour; they gain the page header and open entries in the
  drawer instead of a centered modal.

## 6. Accessibility

Focus rings on everything interactive (accent, 2px, offset); drawers are `role="dialog"` with
`aria-modal`, ESC to close, focus moved in on open; icons are decorative (`aria-hidden`) beside real
labels; status is text plus hue, never hue alone; hit targets ≥ 28px; both themes are first-class —
the dark status palette is its own validated set, not a filter.
