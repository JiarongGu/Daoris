# The desktop's own frame — the design

**Status: the contract for SURF10, confirmed with the owner 2026-09-21 before any code.** The brief
is `docs/archive/2026-09-21-desktop-design-brief.md` ("I still dont see good design for the desktop app
itself"); this is the answer, in the shape SURF1 took — a reference pass on the **window**, a
decision recorded before the build, and a real-window loop. **D56** records what it settles.

It does not re-argue D55. The frame's *regions* were settled there and every one of them works. This
settles the **application they sit in**.

## 1. What was measured

Not eyeballed. `npm run desktop -- run`, then `shot` and `eval` against the real window, which is the
only instrument that can see any of this. Three captures (Manage, Work empty, Work attended) on a
**1267 × 765 CSS-px** window:

| Region | Measured | What it means |
|---|---|---|
| Manage's nav column | 240px wide, **240px of content in a 738px column** | ~440px empty in Work, and all six items belong to the other frame |
| `StartSession` | **287 × 200, permanent**, at the rail's top | 27% of the rail is a form that is not a session |
| The attended session | scroll region **client 365px / content 442px** | the organising object reads in a 365px box, and scrolls |
| Chrome | 45px OS title bar | above a page that draws its own everything |
| Body type | **15.2px / 23.56px line** | document density; the study's references sit near 13px / 1.4 |

**42% of the width is navigation and a form; the session gets about 28% of the window.** That is the
owner's sentence stated as an allocation, and it is the whole diagnosis: *the regions are right, and
the frame around them is still the management console's.*

Two defects the brief did not name, both visible in the attended capture:

- **The same three verbs appear twice, 400px apart.** `finish it · decline… · stop it` in the
  attention band and `send · finish · stop` in the composer, shown **simultaneously** on a parked
  session. Two owners for one set of moves is worse than either.
- **The mode survives a restart and the selection does not.** `daoris.mode` is remembered;
  `attending` starts `null`. Relaunching into Work lands on *Nothing attended* while a session is
  parked waiting on you — the one arrangement the attention work of SURF5a exists to prevent.

## 2. The reference pass — on the window, not the regions

The study (`docs/2026-09-21-ide-reference-study.md`) read the *frames*. This reads what the
applications do with the **window** itself. Five rules recur, and Daoris breaks all five.

1. **Chrome belongs to the application, not the OS.** VS Code, Zed, Cursor, JetBrains, Warp — each
   draws its own top strip and puts the global things in it. None ships a bare OS title bar over a
   page that then draws its own everything.
2. **The navigator swaps with the domain.** VS Code's side bar changes completely per activity-bar
   item; Xcode's navigator changes per tab. **Nobody shows the other frame's navigator**, which is
   exactly what Work does today.
3. **Density is decided once, at the root.** An IDE is one scale throughout. Two densities in one
   application is how a design language forks.
4. **"New" is one control** — a `+` in a list header, or a palette command. Never a permanent form
   holding prime vertical space.
5. **The application opens on what you were doing.** Not on a dashboard, when something is waiting.

## 3. The frame, region by region

> **Amended by D66 (2026-09-23, the owner's choice).** There is no Manage ⇄ Work any more. The
> activity bar is the one navigation — **Overview, Sessions, Quests, Projects, Convergence, Search**,
> with **Settings** at its foot — and *Sessions* is what the Work frame was, as one view among the
> others. §3a's mode switch and §3b's frame icons are gone; everything else here stands, including
> the strip as the title bar, the rail, the attended column's one owner for the verbs, the panel and
> the status bar.
>
> **Amended by D118 (FRAME1a, 2026-10-01; to be built as FRAME1b–i).** Every view gets the same
> regions. A list pane and a main area are the view's own, beside the frame's side bar and panel.
> Sessions' rail becomes one list pane among several. A list becomes a strip when the main area has no
> room beside it, rather than below a fixed 1024 px, and a strip the window drew opens its list over the
> main area. `docs/2026-10-01-frame-model-design.md` is the contract, and §3c's rail is its first list.

```
┌────────────────────────────────────────────────┐
│ ▦ Daoris 道衍  [Manage│Work①]  every circle ─ □ ✕│  app strip   36px
├──┬───────────────┬─────────────────────────────┤
│▣ │ sessions    ＋│ conversation       awaiting │  head        fixed
│▤①│  ● awaiting   │ engine · stub · 10m         │
│▥ │    conversat. │ ─────────────────────────── │
│◈ │  ● busy       │ This one is waiting on you  │  band        parked only
│⌕ │    build docs │ [finish] [decline] [stop]   │
│⚙ │               │                             │
│  │ game        ＋│ Timeline                    │  scrolls
│  │  ● idle       │  ▸ turn 3 · edited 2 files  │
│  │               │ ─────────────────────────── │
│⟳ │               │ [ …ask this repository… ]   │  composer    pinned
│文│               │ [send]                      │
├──┴───────────────┴─────────────────────────────┤
│ ▸ console                                      │  panel       growable
├────────────────────────────────────────────────┤
│ driver ● ready · 2 sessions · every circle · local only · lexical only │  40px→26px
└────────────────────────────────────────────────┘
 48px             15rem            fills
```

### a. The app strip — one global row, 36px

> **Amended by D75 (2026-09-24, the owner's choice).** The strip's menus are the setup domains,
> *Daoris · Workspace · Agents · View*, each item opening its own domain of one Settings page, and the
> workspace is named in every state, none included. `docs/2026-09-24-menus-design.md` is the contract.
>
> **Amended by BRW7 and BRW8 (2026-09-30).** The strip's right-hand group holds Daoris's browser's door
> before the region toggles, on a shell only, and beside it, while a session drives the browser, a chip
> naming it that opens it. The door opens another window and changes no place, so it is an act of the
> strip's and not a place on the activity bar; the in-app browser design's §3c and §3d have the reasoning.

Wordmark (`Daoris` 道衍 — the serif's one appearance, D41 §1), the **Manage ⇄ Work** mode switch, and
the **workspace scope**, right-aligned with three reserved slots at its right edge. It is present in
both frames because everything in it is true in both.

The mode switch leaves the sidebar, which is the move D55 §a already named and the brief called a
piece of the answer. The workspace scope leaves the sidebar foot for the same reason: it decides what
every number on every screen means, so it belongs to the application.

**The strip is three groups on one line**: the mark and the menus, the command center, then the
scope and the caption room. The two outer groups grow alike from nothing, so the command center is
centred on the strip while both fit beside it, and gives way to them when they do not, as VS Code's
does. It never covers a menu, and it narrows before anything else does: the shortcut goes first,
then the scope ends in an ellipsis. No padding sits on the strip or a group, since the sides share
the space by their content boxes and padding would put the middle off centre. The line never wraps,
because a Chinese label may break between any two characters (UX5 U15). The space the groups leave
empty drags the window as the strip does.

**The command center says where you are and prints its own shortcut.** The strip's middle held about
1,400px of nothing at any real width, and the palette SURF10 named as repaying the icon rail's lost
discoverability was a 14px glyph against the caption buttons. It is a bounded, obviously pressable
pill instead, taken from VS Code for VS Code's reason: a title bar that carries no information pays
rent for a wordmark. **The menus carry no chevrons**, because a menu bar is a convention strong enough
not to need marking, and a chevron per menu is furniture per menu.

**Since SURF7 this strip is the title bar** (§5): it drags the window, double-click maximizes it, a
sliver above it resizes from the top, and the three reserved slots are the rectangles the **window**
paints its caption buttons into. SURF10 built the strip as a region with the room already held open,
which is why nothing in it shifted when the window claimed those pixels.

### b. The activity bar — 48px, identical in both frames

Manage's six domains become icons with tooltips, plus the Quests badge. **The same bar in both
frames**, so a domain is one click away from anywhere; clicking one in Work switches to Manage on
that domain, which is what "peers" has to mean if it means anything.

The labelled 15rem sidebar is **gone from both frames**, not hidden in one. Its foot redistributes by
what each thing *is*: the workspace scope is global chrome and goes to the strip; the **tier pill and
the index count are ambient state** and go to the status bar (D24's "stated on every screen" is
better served there — the status bar is on every screen by construction); refresh and language are
actions and sit at the bar's foot as icons.

The cost is discoverability, paid by tooltips, the view's own page header, and SURF9's palette —
which the study already argued is cheaper before the domain count grows.

> Amended by D150 §2.1 (UX6j, 2026-10-07): the foot holds Settings alone. Refresh and the language
> left for View's *Refresh the index* and *Language ▸*, the palette, and Settings → Appearance.

**How the bar is laid out.** The views sit at the top and the foot holds the actions (refresh,
language), then Settings, where every workbench keeps its gear. The two groups are separated by
POSITION, with no rule between them, as VS Code separates its own. A view that does not exist on this
surface is **absent, never disabled**: a browser is given no Sessions, because a greyed row implies
the thing exists somewhere the person could get to. A place keeps its full 36px, and **a bar too short
to hold them all scrolls** rather than crushing them, with no scrollbar drawn in 48px (UX5 U22: at
the window's least height the places touched, the counts sat over their neighbours and Settings went
under the status bar).

**A count is a circle**, `CountBadge`: one fixed height and the same minimum width, since a count
drawn as a pill of its own line height measured 14 × 17px on the window. Zero wears nothing. **A
badge counts what its place holds** (UX5 U20, the owner's choice): Overview counts all of *What needs
you*, beside the band that lists it, and Sessions counts its own sessions waiting on the person. Both
are that status and wear open's hue; outstanding quests are a quantity and wear the accent.

### c. The rail — a header, and `＋` behind one control

`sessions` as a header row with a `＋` at its right. `＋` opens `StartSession` in **D41's drawer** —
the design language's single detail-and-form surface, already built, already `role="dialog"` with ESC
and focus handling. 200 permanent pixels become a 30px header row, and the rail becomes a list of
sessions, which is what it is for.

### d. The attended column — and one owner for the verbs

The head is **fixed** at the column's top and the body scrolls beneath it, so the session's identity
does not scroll away from the work. The composer is **pinned** to the column's bottom.

**The verbs get one owner at a time, decided by state:**

| Session state | Who owns the moves |
|---|---|
| `awaiting person` | the **attention band**: `finish it · decline… · stop it`. The composer keeps **`send` alone**, under the band's own sentence — *answering it in the box below lets it carry on* |
| live / busy | the **composer**: `send · finish · stop`. No band is rendered |

That is the rule, and it removes the doubling without removing a capability: every move stays
reachable in every state it is legal in, from exactly one place.

**The selection is remembered**, alongside the mode. A relaunch into Work reopens the session you
were attending — and if that record is gone, the rail's own *awaiting person* row is where the person
was going anyway.

### e. The panel and the status bar

Unchanged in behaviour — both landed in SURF4d and both are right. They re-measure at the new density
(the status bar's 40px becomes 26px), and the status bar gains the tier pill and the index count from
the sidebar foot.

**The status bar states facts, and it is icon-and-number.** Left, what the machine is doing: the
driver, the sessions active here (running, or parked waiting on the person, which is what the count
holds; UX5 U21), the workspace, and the remote. Right, what the index answers with: its count and the
recall tier, whose sentence is the service's own, verbatim, in the tip (D24). Each value keeps its
noun, dimmed, in front of it, because `● ready` alone says nothing; the tip leads with the noun too.
The remote and the index count give way first on a narrow window (below 640px and 768px), and the
driver, the sessions, the workspace and the tier stay.

**An item is a control only where it leads somewhere, and it leads to where its fact is set**: the
driver and the remote to their settings, the sessions to Sessions, the index count to Projects, the
tier to *Daoris's own AI*. Anything else is text that cannot be tabbed to, because a bar where half
of what looks alike responds teaches people to press the half that does not. Every item fills the
bar's height, so a hover is a box rather than a word that lit up.

**A wired workspace's remote item is the sync control** (SYNC6b), still icon-and-number: a cloud,
then ↑ for work waiting to go up, ↓ for quests left behind and ⚠ for quests in conflict, or the one
word *synced*. A wall needs a word on the bar: *unreachable*, in the warn tone, since the cloud-off
glyph alone said nothing. Pressing it opens a menu above the bar that says when the workspace last
synced, the quests in conflict, and *Sync now*; its lead-in says only *when*, because the host's
wall already says what went wrong.

## 4. Density — an amendment to D41 §3, not a drift

D41's spacing was drawn for *"a professional operations console"* read at arm's length, and the Work
frame inherited it without anyone deciding to. **One denser scale, application-wide** — both frames
and the browser, because the platform is one UI (D38) and two densities in one bundle is a fork.

| | D41 as written | Amended |
|---|---|---|
| Body | 0.95rem / 1.55 (15.2px / 23.6px) | **0.8125rem / 1.45** (13px / 18.9px) |
| Type scale | eight rem values, **as literals in every component** | **seven named steps as tokens** (below) |
| Spacing | 4 · 8 · 12 · 16 · 24 · 32 | **2 · 4 · 6 · 8 · 12 · 16 · 24 · 32** |
| Nav row | ~50px | **36px** (activity bar), rail header 32px |
| Radii | 6 · 8 · 10 | unchanged |

The scale becomes **tokens**, not new literals, because the literal form had already drifted and
nothing reported it: the components carried **fifteen** distinct `text-[…rem]` values where D41 named
eight, and four of them — 0.7, 0.78, 0.82, 0.85 — belonged to no scale at all. All 203 sites now name
a step, and `tokens.test.ts` fails on a raw `text-[…rem|px|em]` anywhere in the platform.

| Step | rem | px | replaces |
|---|---|---|---|
| `text-meta` | 0.6875 | 11 | 0.68 · 0.7 · 0.72 |
| `text-small` | 0.75 | 12 | 0.75 · 0.78 · 0.8 · 0.82 |
| `text-body` | 0.8125 | 13 | 0.85 · 0.875 · 0.9 · 0.95 |
| `text-title` | 0.9375 | 15 | 1.05 |
| `text-view` | 1.125 | 18 | 1.25 |
| `text-wordmark` | 1.25 | 20 | 1.5 |
| `text-value` | 1.625 | 26 | 2.0 |

**What the amendment does not touch:** the warm paper, the thin ink, the one bronze accent, and the
**validated status palette** — all four state colours in both themes, with their six-check
validation, are untouched, and status still never appears colour-alone. The accent stays the
interactive identity and never a status.

**D41 §6 survives intact and constrains the number.** Hit targets stay **≥ 28px**, which is why the
row lands at 30 rather than 26; focus rings, `aria-modal` drawers, decorative icons beside real
labels and both themes as first-class are all unchanged.

## 5. What this landing builds — and what SURF7 takes

**SURF10 (this):** D56 recorded first; the app strip as a region; the activity bar and the retired
sidebar; the density amendment in `tokens.css` and the components; the rail header with `＋` behind
the drawer; the attended column's fixed head, pinned composer and single verb owner; the remembered
selection. Verified by the vitest inner loop over the mocked bridge, the story per component, and the
real-window loop.

**SURF7 — landed 2026-09-22.** `MainForm` is an `OptimizedForm` with `FramelessChrome`, so there is
no OS title bar and this strip **is** the title bar: it drags the window, double-click maximizes,
a 4px sliver above it resizes from the top, and the reserved room is handed to the OS as real caption
buttons. `WindowCommandModule` is mapped **late**, from the form's own constructor; `SET_THEME`
repaints the native chrome; `AppPlacement` is read and `Form.WindowState` never is.

**One thing changed while building it, and D56 carries the amendment: the WINDOW paints the caption
buttons.** The page drawing them would cost it every mouse event in those rectangles, because
claiming the hit-test makes Windows call them non-client — no CSS `:hover`, no clicks, and a separate
channel needed to render hover state. `NativeCaptionButtons` cuts the rectangles out of the WebView2
and paints there instead, so the reservation this landing already built is the whole page-side job.

**The two-bar interim is closed**, and it bought 29px of height: the window went from 1267×765 to
1268×794, which is exactly the title bar that is gone — and the attended column, which SURF10 left
scrolling in a 341px box, now fits its content without scrolling at all.

### What it measured afterwards

Same instrument, same window (1267 × 765 CSS px), after the build:

| | Before | After |
|---|---|---|
| Navigation and forms | 240 + 287 = **527px wide (42%)** | 48 + 240 = **288px (23%)** |
| The attended session | 739 × 365 — **~28% of the window** | 979 × 709 — **~72%** |
| Body type | 15.2px / 23.6px | 13px / 18.9px |
| The rail's permanent form | 287 × 200, always | **gone** — a 32px header and a `＋` |
| Empty nav column in Work | ~440px | **none** — Work has no nav column |

**One thing the numbers say that the design did not.** The attended column's *scroll region* is
**341px** tall, against 365 before — it gained the whole width and no height, because the app strip
took 36px off the top and the output panel still holds 236 and the composer 126. Its **content** fell
from 442px to 371px on density alone, so it now very nearly fits where it used to scroll by a fifth.
The vertical budget is the honest remaining defect, and it is the one **SURF6 relieves rather than
this landing**: the right dock arrives with the diff and takes the timeline out of this column, which
is the arrangement the components plan specified all along. Recorded rather than fixed here, because
raising it now would mean changing the panel's default height — a behaviour this landing said it
would leave alone.

## 6. What does not move

- **D41's language** — paper, ink, accent, the validated status palette, never hue alone. Only §3's
  *scale* is amended, and §6's accessibility floor constrains the amendment.
- **D42's stack** — headless primitives, tokens as the theme, Storybook over the shipped components,
  the i18n parity gate, and **a molecule imports no hook**, which is what makes every state of this
  reachable by passing props.
- **No editor, at any point** (D55). No model named anywhere (D24). No mid-run approval gate
  (D37/D52).
- **Structure and geometry may come from a reference; pixels and identity may not** (components §3a).
- **The disclosure boundary** (D47 §4): over a keyed remote the Work frame is not rendered at all, and
  a browser sees the record and never a stream, a tree path or a diff. Playwright holds the negative,
  and the activity bar must not become a way to reach Work in a browser.

## 7. Rejected

**Keeping the labelled sidebar in Manage and collapsing it only in Work.** It keeps discoverability
where the domains are read most, and it costs two sidebars kept in step by hand — and the two frames
then look like two applications, which is the complaint one level up.

**A dense scale scoped to Work.** Smaller and safer, and it makes switching frames visibly change the
type size. The desktop *is* the platform; a frame boundary is not a reason for a second design
language.

**Fixing the allocation and leaving the type alone.** This was a real option — it addresses every
measured number in §1 without touching a decision. Rejected because the brief's density bullet would
close unanswered, and because the 365px session box is partly *caused* by the scale: at 13px the same
column holds a third more.

**A popover for `StartSession` instead of the drawer.** The reference arrangement, and new machinery
for a form D41 already has a surface for. The drawer is the cheaper answer and the doctrine-consistent
one; if it proves wrong for a three-field form in a frame whose right side is the attended session,
that is a later correction with evidence behind it.

**Folding SURF7 into this landing.** It would avoid the two-bar interim entirely. Rejected with the
cost stated: it doubles a session-sized landing, and the riskiest half — frameless chrome, Snap
Layouts, `IAppMaximizable` — has only `shot` to verify it, so it deserves its own run.
