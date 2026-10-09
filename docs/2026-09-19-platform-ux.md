# The platform's design language

**Status: approved direction from the owner, 2026-09-19 — "design the UI/UX properly, because this is
used by a human." Written before the build; D41 records the decision.** The platform design
(`2026-09-19-platform-design.md`) settled *what* the views are; this settles *how the product feels*:
the shell, the tokens, the components, and the interaction rules. One person uses this daily beside a
terminal, in light or dark, to manage a family of repositories being built largely by agents. It is a
professional operations console, and it should feel like one — calm, dense where density earns its
keep, and honest everywhere.

**The body is current** (UX5 U6, 2026-09-26). Between 2026-09-21 and 2026-09-26 this document
gathered a dated amendment per pass; each is folded into the section it amended, with its reason
where the reason is what keeps the rule from being undone. What found each one, and what was looked
at, is in `docs/2026-09-26-ux5-screen-audit.md`, the task archive and the history.

## 1. Character

**Warm paper, thin ink, one bronze accent.** The existing aesthetic is kept deliberately — it is
distinctive and restful — and formalized: the data and the words are the only loud things on the page.
Decoration budget goes to *hierarchy*, not ornament. The serif appears exactly once, in the wordmark;
everything else is the system sans. 道衍 sits beside the wordmark as the one brand gesture.

## 2. The shell

The frame is specified in `docs/2026-09-21-desktop-frame-design.md` §3 (D56, as amended by D66 and
D75), where its own rules live; this is what a view may assume of it.

- **The app strip** (36px) is the title bar: the mark, the menus of verbs and places (*Workspace · Edit ·
  View · Go · Run · Terminal · Help*, D152), the command center naming the scope, and the window's controls. **The
  activity bar** (48px) is the one navigation, a place per icon and Settings alone at its foot (UX6j),
  and a badge counts what its place holds: Overview the whole of *What needs you*, Sessions its own sessions waiting on the person
  (UX5 U20), its list's *Waiting on you*: a parked quest counts there too (D126 §2.5), and Agents the signed-out accounts a
  list or a default holds (UX6e, D150 §2.1). **The status bar** states the machine's standing facts: the driver, the active sessions,
  the workspace, the remote, the index and the recall tier (D24, stated on every screen). Global state
  lives in exactly one place; views never restate it.
- **The content column is the window's whole width** (UX5 U59, the owner: under a 72rem cap a
  maximized window left every view a third empty), **and a page's every block is the column's**
  (D141): prose wraps at its edge too. What keeps a size of its own is no page's block: a form at
  what it holds, an overlay, an empty state's notice, a drawing at one unit a pixel.
- **Every view opens with a page header**: title, one-line description, and the view's primary action
  on the right. The header is the only h-level element per view; sections inside use small muted
  section titles.
- **Narrow, nothing hides behind a hamburger.** The activity bar keeps each place 36px and scrolls
  rather than crushing them (U22), and the command center gives way to the menus rather than covering
  them (U15). The one exception is the menu bar below the width its seven names need (39rem of window,
  UX7a2): they fold into one ☰ whose rows are the menus, and the keys reach it as they reach the bar.
- **One frame for every view** (D118, FRAME1a; built view by view as FRAME1b–i). Each view has a list
  pane and a main area of its own, beside the frame's side bar and panel. Overview and Map have no list.
  Each view's list collapses, resizes, answers the same keys and is remembered as Sessions' rail is, and
  the main area lays out by its own width. `docs/2026-10-01-frame-model-design.md` is the contract.

## 3. Tokens

- **Type is seven named tokens** (D56): `text-meta` 11px, `text-small` 12, `text-body` 13,
  `text-title` 15, `text-view` 18, `text-wordmark` 20, `text-value` 26. `tokens.test.ts` fails on a raw
  size: the literal form had drifted to fifteen values across 203 sites with nothing reporting it. It
  fails too on a `text-` class `tokens.css` declares no step, colour or layout for (FRAME1h): the
  monitor's title wore `text-h3`, which renders at the body's size, and the old scan read it as nothing.
  Tile values wear proportional figures; columns of numbers wear `tabular-nums`. **An ideograph is
  never slanted**: `font-synthesis-style: none` keeps Chinese upright, since its system face has no
  italic, and keeps the Latin face's true italic.
- **Spacing** on a 4px scale: 4 · 8 · 12 · 16 · 24 · 32. **Radii**: 4 controls · 6 cards · 8 overlays,
  one step flatter than the first language's 6 · 8 · 10 (the owner, 2026-09-30: *"I think we should
  move into more flatten design"*). **A bar is flat**: thin, square-ended, on a track that shows the
  whole scale, since a thick bar with a rounded end made a short one a blob rather than a share.
- **Surfaces**: `--page`, `--raised` (cards, controls), `--overlay` (drawers, toasts — one step above
  raised), `--line` and `--line-strong`, and `--sunken` one step below the page for the box a move asks once in and
  the command center, while a field stays raised (LOOK6: thirteen files asked for it and none drew; `tokens.test.ts`
  now holds every `bg-`, `border-` and `text-` colour a token, and every ink readable on every surface). **Ink**: `--ink`, `--ink-soft`, `--ink-faint`, and each status hue's ink for its words, `--ink-open`, `--ink-taken`, `--ink-done` and `--ink-danger`. **A field is
  outlined with `--line-strong`, a container with `--line`**: a `--line` field on a `--raised` card is
  very nearly invisible in dark, a form whose fields you cannot find. **A mark that is content wears an
  ink**, never a container's line: the code map's arrows at `--line-strong` were 1.6:1, and stepped
  back, gone in dark (U48).
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
  label. A list's word reads as an outcome and wears its hue: *allow* done's, *ask* open's, *deny*
  declined's (PERM1).
- **Waiting on the person is open's hue, everywhere it is shown** (UX5 U1): a session's pill and dot,
  *What needs you*'s card and rows, the map's ring and word. It had three hues, one of them declined's
  red, and a session waiting on its person read as one that had failed. **Red is only ever an
  outcome**: declined, failed, a failed tool call.
- **A status hue drawn as words wears its ink** (UXFIX5, UXFIX5c, the 2026-10-07 second opinion). Red is
  `--ink-danger`: a danger button's label, a declined pill's word, a failed mark's word, a refusal, a removed count, a
  diff's deletion. Open's, taken's and done's words are `--ink-open`, `--ink-taken` and `--ink-done`: a pill's word, a
  waiting mark's word, a waiting count, the map's *waiting on you*, a diff's letters and added count, code's strings and
  numbers. A status hue is computed as a fill, and as text it fell under the 4.5:1 text floor: declined's dark red read
  3.95:1 on the sunken box a move asks in, 3.18:1 on an overlay and 2.94:1 on its own soft field there; open's and
  done's words read 2.8 to 3.8:1 in light, taken's 4.1:1 on its field over the sunken box, and in dark the three read
  4.1 to 4.4:1 on their fields over an overlay. An ink keeps its fill's OKLCH hue, within a degree, and moves in
  lightness until it holds 4.5:1, with a tenth to spare, on every surface and on its own soft field over each, in both
  themes. Where the fill already reads, the two are one value: declined's, in light. Measured at worst, each on its own
  field, over the sunken box in light and over an overlay in dark:

  | Ink | Light | At worst | Dark | At worst |
  |---|---|---|---|---|
  | `--ink-open` | `#8b5c02` | 4.62:1 | `#c68b31` | 4.61:1 |
  | `--ink-taken` | `#2765ab` | 4.63:1 | `#619ce2` | 4.64:1 |
  | `--ink-done` | `#017545` | 4.61:1 | `#43aa7b` | 4.63:1 |
  | `--ink-danger` | `#9e2f24`, the fill | 5.53:1 | `#ee6955` | 4.61:1 |

  A fill, a border, a mark (a dot, a ring, an icon, a plan's check) and the terminal's colours keep the `--st-` hue.
  `tokens.test.ts` computes every ink and fails on a status word in its fill's colour, whether a `text-` class, a
  stylesheet's `color` or an SVG word's `fill`. A mark that takes its colour as a word does is named there with its
  reason.
- **Open's hue is the person's alone** (D126 §2.3, SESSUX1c): among sessions, *waiting on you* and *parked* (a parked
  quest's last session) wear it, and nothing else does. `queued` wore it too and left the word to tell the two apart;
  it is neutral, keeping its idle mark. *Awaiting reply* is neutral: nothing runs, and nothing waits on the person.
- **A liveness mark never borrows an outcome's hue, and a live mark means a turn is running.**
  *Ended* is neutral, since it covers completed, failed, declined and stopped at once, and the pill
  beside it names the outcome (POLISH2: it wore done's green beside a failure). A live chat between
  turns is **idle**, a quiet mark and a neutral word, as the reference console draws it; the driver
  says whether a turn is in flight, and before it answers the record's word stands (U17). A fact
  about a tree (*busy*) is words, never the live mark (U60).
- **A path breaks at its separators**: `PathText`, never `break-all`, which broke a tree inside a name
  (`family\g` / `ame`). Anything else that may be wider than its line (an account name, a tool's raw
  output) wraps `anywhere`, which breaks inside a word only when that one word will not fit (U8). **A grid that holds
  a cut line or someone's words bounds its column** (`grid-cols-[minmax(0,1fr)]`, ASKHIST1b): an implicit column is
  `auto`, which grows to its widest item's min-content, and a `truncate` line's is the whole line, so a pasted URL in
  Ask Daoris's history widened every row past the dock, cut nothing, and scrolled the panel sideways. **A code block wraps
  where its own pane is narrow** (ASKHIST1c): below 48rem of its own width, a container query on the block and never the
  window's, its long lines wrap until its *Wrap* press says otherwise, and *Copy* takes the text as written; a table keeps
  its own scrolling box. **Nothing floats over a conversation's words**: the way back to its tail is a strip of its own
  between the words and the box, where its host draws one.
- **A command breaks between its words**: `CodeText`, every code span, one box per word, since a line
  may break after any hyphen and a setting's terminal twin read `--no-` / `keep` in 中文 (LOOK5). A word
  breaks inside only when it alone is wider than its line. **The marks after a span stay on its last word's line**
  (UXFIX4b): Chromium breaks after any inline box, even before a comma, so a branch name filling its line at the 400 px
  floor pushed `, whose work…` onto the next. A one-word span and its closing marks (`,` `.` `)`, `，` `。` `）`) are
  one box, as text inside which no line breaks before such a mark, in either language. A command's last box is inside
  its code and the marks are not code, so it sits alone in a group that does not wrap, which leaves the break after it
  to the same rules, and it breaks inside at its line less an em a mark, so its marks are never pushed past the edge.
- **Motion**: 140ms ease-out on overlays and hovers; `prefers-reduced-motion` disables it. No shimmer
  anywhere — loading placeholders are static two-tone. **Nothing at rest moves for ever** (FREEZE1): the live mark
  pulses three times as it turns live and settles, since an endless pulse kept the window drawing at the display's rate.

## 4. Components

The primitives live in `ui.tsx`, and a molecule imports no hook (the components plan). The frame's own
controls are in the frame design's §3.

- **A page wraps at one edge, its pane's** (D141, LAYOUT11; it replaces *prose has a measure of its own*). Every block
  of a page takes the column's width: a header's line, a quest's body, a note, a refusal, `Prose`, a box a move asks
  in. A 65ch measure per paragraph gave every page two edges (at 1600 px a quest's title ran the pane while its body
  stopped at 456 px) and held about 32 glyphs of 中文. The column (`ViewMain`) is the one place a line's length is
  set, and it sets none; a cap, if one is ever wanted, goes there once, in `em`, never `ch`. A long English line at a
  wide pane is the stated cost, which the side bar and the list's edge narrow. `tokens.test.ts` and
  `pageMeasure.test.tsx` hold it. Content is shown as it is: a page header's line (a plugin's description, a
  repository's summary) wraps whole, never cut to one line with its words in a tip (NAME2).
- **Status leads.** A row reads `pill · title · route · how long`: identity first, the secondary marks
  at the right. A pill pushed to the far edge sat a thousand pixels from the title it described.
- **A row stacks by its list's width, and its name is never cut** (UXFIX4). A workspace's branch rows are one part,
  `BranchRow`, on a list that is its own container: three columns where the list holds 30rem, and narrower the sentence
  under the name, since at the main area's 400 px floor three columns left the sentence what the name did not take. The
  name wraps after its separators, never cut to a line with the rest in a tip that neither a keyboard nor a reader
  reaches, and the row is named by the text it shows. A session head's branch is shown so too (UXFIX4b), on its
  line's own wrapping row.
- **Buttons**: `primary` (solid accent, paper text — the one loud control per view), default (raised +
  line), `ghost` (borderless, for in-card affordances), and `danger` for a move that ends or removes
  something: decline, close an ask, retire, remove an account, stop, discard. It wears the hue of the
  outcome it causes, and never marks a state (U18): its line and its hover field in declined's hue, its label in the
  danger ink (UXFIX5). **The move comes first, then *never mind*,** in
  every drawer's footer and every confirming pair (U38). **A destructive edit asks once**: its first
  press opens a sentence saying what the second will do, beside the move and *never mind*. Every such ask is one
  component, `InlineConfirm` (UXFIX2): in the page, never a modal; it opens whole in view, its nearest edge first, and
  only then the sentence takes the focus, without scrolling again (UXFIX2d: a focus alone showed one line of an ask near
  the page's foot); the sentence describes the move;
  Escape and *never mind* give the focus back to what opened it; the press keeps it open and waiting, and a refusal is
  said inside it, whole, rather than in a toast; it closes only on success or a cancel. **While it waits it says so**
  (UXFIX2c): a status beside the presses, there from the first so a reader hears it speak, inside nothing `aria-busy`,
  since a reader may hold a busy element's words back until the wait is over; the move keeps its name. **Closed either way, the focus goes to the press drawn again**, and where none is (a delete took its
  record, or nothing more is offered) to the nearest section or list that held it, never the page's body. A refusal
  shrinks with its column and breaks anywhere, since a long id or a raw error otherwise widens it past the main area's
  400 px floor.
- **Pills** carry state: status text on its soft field with its hue — label always present. Each word is its hue's
  ink on that hue's field, and declined's is the danger ink (UXFIX5, UXFIX5c).
- **Chips** carry declarations (owns/accepts/packs): quiet line-bordered tokens; `accent` variant for
  what a project *accepts*, because that is the actionable half. A phrase stays in the body face
  (U37).
- **Stat tiles**: label · value · context note, per the visualization discipline; a tile may wear the
  warn rail when its note is a warning (a quest sitting a week).
- **Drawer** (right, 32rem, overlay surface, scrim, ESC/scrim/× to close) is the single detail-and-form
  surface: reading a knowledge entry, composing a quest, a quest's detail with its actions. One pattern
  instead of three; the list stays a list. *Changed by D118*: the drawer keeps forms. A record moves to
  the main area of the view whose list holds it, because since DOCK1a a drawer lies over the side bar
  and the panel: a quest and an ask since FRAME1d, and a knowledge entry since FRAME1f, which retired the
  reader and with it the `wide` drawer (52rem) a line of source needed (U43): the main area holds one.
- 🔴 **An overlay sits between the strip and the status bar**, every one of them: a scrim, a drawer, a
  panel. It starts below the strip, because the strip holds the caption buttons the window paints and
  a page backdrop cannot dim what the page does not draw (a full-bleed scrim greyed the title bar and
  left the buttons as a bright block punched through it, and a full-height drawer put its close button
  under the window's). It ends above the status bar, which otherwise ran bright for 48px and grey
  after. The title bar stays live while an overlay is up, as VS Code's does during quick-open.
  `tokens.test.ts` holds the rule for every overlay, so the next one cannot inherit `inset-0`.
- **Toast** (bottom-right, overlay surface, auto-dismiss with close) carries an action's outcome and
  an error that has no form to stand in — the service's sentence verbatim, because the refusal text is
  the contract. Nothing shifts the layout to speak. **A request that reaches nobody is said in
  Daoris's words**, never the browser's *Failed to fetch* nor a socket's, and the page's own requests
  and the driver's tick say the same sentence for it (U29, U30). **A sentence already on screen is said
  once**: its newest copy replaces it, and a failure the tick repeats is said once until a tick runs
  again, since a tick report is a log and a toast an interruption (D62).
- **A tooltip follows the editor's rules** (the owner: *"for those we can follow vscode"*): below its
  control, aligned to its leading edge; beside a vertical rail; flipped only when there is no room.
  Gone on any scroll, key or click, when the window loses focus, and the moment the pointer is off its
  trigger — Radix alone closes on the trigger's own pointerleave, so a tip outlived a scrolled list, a
  window blurred by a login's browser, and a button that disabled itself on the click. `Tip` holds all
  of it; `ui.test.tsx` holds the rules.
- **Empty states** are designed, not blank: a quiet glyph, one headline line, one body line, and the
  action that changes the fact ("Nothing is sitting" → "Ask for something"). **An empty machine is a
  state that says what to do** (POLISH2): a view or a composer with nobody to offer says so and names
  the door, rather than a form whose sentence can never be sent. **An empty answer is an empty
  state** in the answering tier's own words, with the action that changes it.
- **Loading**: a first load shows static skeleton rows, with its words on the line the count will
  take; a refetch holds the previous render at reduced opacity — content never jumps.
- **A panel that keeps its height keeps it as a sentence**: an ended session's console was an empty
  bordered well, which reads as a field to type in.

**Forms and settings.**

- **A form is sized to what it holds**, never to the column it sits in: three equal thirds of a card
  gave a 570px box to the word "default". A completion list is sized to its rows (U64).
- **The platform draws its own controls**: no native select, checkbox or radio outside `ui.tsx`
  (`tokens.test.ts`, U5); no native search clear, which WebView2 paints in the system's accent; and a
  scrollbar that follows the theme, through `color-scheme: light dark` and `--ink-faint`. **The
  scrollbar is a current OS's** (the owner, 2026-09-30: *"match this to regular app like"*): no
  arrows, no track, a fully rounded knob four pixels wide at rest that widens to six and darkens
  under the pointer, in a ten-pixel lane that takes a grab across its whole width.
- **A setting is a row** (`SettingRow`, the owner: *"this really long list of setup … can be
  improved"*): the label, a one-line hint that is its terminal twin, written as code (U55), the
  control at the right, and the paragraph that motivated it on an info glyph. **The label keeps its
  room**: its column has a floor, 16rem or half the row, and the control gives way to it, a path
  breaking at its separators (U58: a path crushed its label to one character a line). **A narrow row
  stacks** (WSR4, 2026-09-30): the row is its own container, and below 26rem its control goes under
  the label at the row's full width. With Settings' nav and the side bar open a row was 309px, the
  floor left the control 130px, and a fixed-width branch pattern spilled left over the row's words; a
  field in a control gives way to its column (`min-w-0 max-w-full`, and the form around it `min-w-0`). A settings page
  laid out as an essay is read once and scrolled past every time after; a page of rows is scanned. A
  rarely used form (wiring a deployment, adding an account) is one press away. The rule between rows
  belongs to what has siblings: a row alone in its list item lost both.
- **A step in a task happens where it was started** (the owner, on signing in): `SignIn` sits on the
  account's row — numbered steps in the order the person meets them, one live control at a time, a
  Cancel that stops the process, the raw output one disclosure away, and the result as the row's own
  pill and a sentence. A refused start is said in the start form, whole (U68). A door's own work
  (install, update, pin) keeps its console.
- **A file dropped anywhere but its target never becomes the page** (INT2): a webview answers a stray
  drop by navigating to the file, which replaces the application. While a composer is open a stray
  drop is absorbed, the whole composer is the target, and a pasted screenshot is a file, kept out of
  the field it was pasted into. One carry serves every composer (`compose/carry`), so the rule has one
  copy.
- **The theme is the viewer's to choose** (D66): *System · Light · Dark*, applied as `data-theme`
  before the first paint, pushed to the window's native chrome, and held by two forced blocks in
  `tokens.css` that a test keeps equal to their system twins.

**Doors, names and what the page may say.**

- **A door opens something, or it is not a door.** A row is a door only where its destination exists:
  a parked session opens in Sessions, which only a shell has, so a browser's row is text. A record
  names a quest by id until the page can open it. A button that closes one drawer and opens nothing is
  a dead click. A menu item named for a part of a domain opens at that part (U72).
- **No box where nothing listens.** A session that takes no messages (an intake, an ended session with
  nothing typed) gets no composer, and the line the box would have taken says why.
- **A move offered where the answer is not is a dead end**, and a move that stays says what it does
  not do. A move that lived on a removed control moves with it.
- **A right-click is a door to the acts already there** (D138, `docs/2026-10-03-context-menu-design.md`): the page's own
  menu at the pointer, MENU1's, offering a surface's acts from the owner it has (a row what it does, its ⋯'s where it has
  one; a page what is done to its record), then copying its name, after selected words', a link's and a code span's
  own. A text field keeps the engine's menu, the strip's own space the window's system menu, and everywhere else with
  nothing to offer the engine's is suppressed: its acts are a web page's.
- **An arrow is a direction, never a menu** (the owner, 2026-09-30: *"instead of using the up down
  arrow all over the entire design we can use more '...' or options"*). A region's own menu is "⋯",
  as VS Code's *Views and More Actions* is. A setting with a value, such as a size, is a menu behind
  that value. A chevron stays only where it opens and closes something (a tool card, a file in the
  review) or moves one way (a find's previous and next). Scrollbars have no arrows at their ends,
  since they are drawn in `tokens.css`: Chromium on Windows keeps its ▲ and ▼ on any bar it draws
  itself, even a thin one.
- **An account reads as who is signed in, with one name on every card** (D66 §3, U53): who signed in,
  a key's handle, the directory, and the tool's own home as *this machine's own* when nobody is known,
  from the roster's one namer. A name that repeats says which it is. A way in is named by its tool and
  door, *Claude Code — protocol (claude-code-acp)*, by one helper (U67).
- **A page never prints a machine path it was answered.** A local host answers where a kept file lies,
  and the page reads that path's *presence* as "can be opened" and opens it through the host's route.
  A file this machine does not hold says *kept on the machine that published it*.
- **A sentence's backticks are code** (`Inline`): the words are unchanged, so a service's sentence is
  still verbatim, and a command reads as one. A sentence with a command in it is not monospace.
- **A tab is its whole name or its icon, never a name cut** (TABS1): a 430px side bar cut 时间线 to
  时… and saved six pixels. When a region's row cannot hold every tab's name, the selected tab keeps its
  name and the rest are their icons, each named by its tip and its label; only then does the row
  scroll. A file's name is the exception, since it is long and its tip carries the path.
- **A card's heading is not its first row's label**, and a card alone in its settings domain carries
  no title, because the list names it (U57). **Labels are a column**: a label beside wrapping chips
  sits in its own column. **A label never repeats its value's first word**, and a record's title is its
  first line, so its body is what follows.

**Language.**

- **Dates, counts and Daoris's own sentences are in the reader's language.** A date is written the
  way the reader's language writes it, with the machine's habits when the machine speaks that language
  (`format.ts`, and nothing else formats: a bare `en` wrote a British machine's dates the US's way). A
  sentence the driver writes is chrome, so 中文 translates it by its typed half (a verdict, a code),
  never by matching the English. A session's note is drawn by `Note` from its parts (D142): Daoris's
  lines worded from `note.json`, someone's words as written; a part the page cannot word, and a record
  from before parts, show their English marked *shown as recorded* / 按原文显示, which retires *the
  driver's words stand* for a note (U27, U28 stand elsewhere). A default's reason passes the driver's
  sentence through in English and is translated in 中文. A count is said in its number, never with
  *(s)* (U54).
- **One word per thing, in each language**: the glossary (`src/Daoris.Web/src/locales/glossary.json`,
  D116) names each concept once in each, and `names:check --strict` in the web's build holds every
  label to it (NAME1b): 智能体 for an agent, 驱动 for the driver (never 驱动器, a disk drive), 委托 for a
  quest, 需求 for an ask, 仓库 for a repository, 工作区 for a workspace, and *Daoris* in running text
  (the wordmark keeps 道衍). A name is designed in each language for its element, not translated
  (`docs/2026-10-01-naming-design.md`). A view is named by its name: no sentence sends a person to
  *Work* (U19).
- **中文 sets a number apart from Chinese**, in an age as in a span (`1 天前`, `9 分钟`); a Chinese
  word inside a Chinese sentence stays tight. Two sentences join the catalogue's way: English puts a
  space after a full stop and Chinese does not.
- **A reason a person reads names no decision number**, since a person using the application has no
  decisions record; the sentence says what the number meant. **A sentence about what the system can
  do is re-read when the decision under it moves**: a catalogue claim has no test pointing at the code
  it describes, so it goes stale silently (POLISH4 found five).

## 5. The views, restated in this language

- **Overview** — tiles row; then a two-column band on wide screens: *Outstanding, oldest first*
  (row = pill · title · route · how long — and, on the desktop, *sitting — why*, the driver's own
  sentence from its last look, truncated with the whole in its tip) beside *Repositories by index
  size* (single-hue bars, adopted dot, values in ink). Every row is a door: outstanding rows open the
  quest's page on Quests (FRAME1d); repository rows go to Repositories. **The page leads with *What needs you***
  (UX6c, D150 §6): *Holding work* (parked sessions, quests parked on their failed sessions, go-aheads, folders waiting on
  trust, and since UX6d a start that *waits for an account*, an ask's intake among them, and a *signed out* account a list
  holds that no waiting start names), *Waiting for your word* (asks to publish or whose intake asked, departures, widenings, quests nobody can take)
  and *Ready for you* (work to review, five rows then *N more in Sessions*), the longest waiting first in each group and
  an empty group not shown; with nothing waiting it is one line, *Nothing needs you* and the sessions working. A row reads
  `kind · what · where · how long` and its why, acts where one press is safe (*Publish to …*, *Try again*, *Accept the
  departure*), asks once under itself before a go-ahead's answer, a trust or a widening, and keeps a reading-first answer
  (a park, an intake, a review) as its door alone; every other row's door is *Open* at its end. **An account's row says each
  account as last read, with when** (UX6d): *Sign in to …* for each read signed out, its steps under the row, *Read …* for one
  never read, and *Let … run …*, which asks once since it widens what Daoris may spend; its door is the agent's page, named
  by the agent. No row starts a process to find out. Overview's badge counts every row. **The band is live, or it lies**: an
  ask made by another door moves nothing else a tick reports, so the shell forwards a tick when the
  asks change and the page refetches them. A wait is a span (*waiting 4d 3h*, U40).
- **Quests** (on the frame since FRAME1d, D118 §2) — a list pane and a main area. The list's `＋` offers
  *Ask* first (INT4c), then *New quest*: the regular task enters at the workspace (D65), and an action
  goes with what it makes, never on the status bar. Its ⋯ holds *Receiver* and *Include closed*,
  ticked as they stand and remembered for the list, and a receiver filter is said at the list's head.
  The asks sit above the quests as a group of their own, a row per ask, what has waited
  longest first and a closed ask last (U32). **An ask says what it waits for wherever it is shown**
  (*proposed, not yet published*, *its intake asked you*, *an intake is reading it*), and its place
  is named by its kind, *workspace default*, never as one more repository name. Its record says who
  answered, the tier in words and an intake as a line (its state, then its tool); a record never says
  no intake ran above the intake that did. **On a shell its quests section is its work** (PAUSE1h, D132 §7.1): each quest with
  its state and why it sits, its sessions as doors into Sessions, and the questions they asked beneath it, which is what a
  pause or an abandon reaches. The ask composer promises only what this machine will do:
  an intake agent publishes on its own, so *nothing is published until you name a receiver* is said
  only where none is set (U34). The two composers are **drawers**, since they are forms (D118 §3d);
  the list stays scannable (pill and its marks, title, route, age) grouped Open / Taken / Closed;
  **choosing a row opens its page in the main area**, beside Ask Daoris rather than under a scrim:
  **its head leads with its state** (UX7c, D152 §7): the pill, then its name (the short title, else the title, two lines at
  most, whole on *Show all*), then one line of facts, whom it asks, who asked, when, and the one live fact; the loud act is
  the quest's next step, *Take* while it is open, *Mark done* once it is taken (U31), then *Decline…*, which asks its reason
  under the header, and ⋯ with the rest and its id. A closed quest's ⋯ adds *Clear from this machine…* and *Clear failed
  sessions…*, and a done ask's header a ⋯ with its own, each only where its plan says something may go, listing what goes
  and what stays under the header before its second press (HIST1e, D153); a workspace's Details ends in *Kept on this
  machine*, the same clear for all its finished work. Its id, lanes and full times fold into *Details*; then the ask, its first
  line left out where it is the title the head said, and the note. The
  page stays on the quest as each act leaves it, and with nothing chosen the main area says how to
  choose and offers the `＋`'s two kinds. A row is for reading. What a quest carries (D65 §2) is **counted on its
  row** (a link glyph and a paperclip, each with its number) and **listed on its page**: links as
  links, files by name and size, a picture shown as one. The composer takes links one per line and
  files by drop, paste or *Choose files…*, and says why a file was left off while the person is still
  choosing. **What the person required is quoted on the page** (DRIFT1d2, D133 §3–§4): each requirement in their words,
  set off by a rule and kept as said, with its check, then how the done answered it, *met* in done's hue or *departed* with
  the reason and the words it relied on. A departure that holds the quest for their yes is what it waits on, so it sits
  above the body as a conflict does, in open's hue, says what the yes lets go on and carries *Accept the departure*, one
  press; its header and its row say *awaits your yes*, and the list puts it first, since the default list leaves closed
  quests out. Ask Daoris's accept card draws each departure the same way. **A chain** (D65 §4) is behind *Add a next step…*; the page shows it as one strip
  (MAP1a): the ask, the quests before and after, the steps still to come as written, and every
  session under its quest, the one being read marked and the others underlined doors. **How it came to be** (TRACE1b,
  D143) closes the page, as it closes a session's record above its conversation: folded to its name, read only on the
  press, then its story on the folded line (*ask #a1 → quest #q1 → 2 sessions*). Open, it is a step per link, each naming
  the store it was read from; someone's words quoted as written, the driver's own notes marked *shown as recorded*, and a
  link nothing keeps marked *missing* where it would have been. A browser has none, since it has no driver.
- **A folder waiting on trust** (D73) — the grant is offered where the hold is read: the quest's
  page carries *Trust this folder…* under its sitting line, and *What needs you* a `trust` row with the same press, asking
  under the row (UX6c), the only place an intake's held room shows. The question is the agent's own, asked in the open: the
  folder, what trusting means in the agent's terms, what the driver is holding, and the one file
  written. It is never wider than the hold, since the shell refuses any pair the driver did not
  produce, and there is one row per folder.
- **Repositories** (named *Projects* until NAME1b, the owner's call; on the frame since FRAME1e, D118 §2) —
  a list pane and a main area. The list holds the adopted, then *Registered, not adopted*, each group
  counted; a row is the repository's name with its standing on this machine at its right, in the
  session list's words (*held*, *drives here*, *not on this machine*), and its one line: an adopter's
  summary, *no domain declared yet*, or what the index reads of one not adopted. Its `＋` is *Add
  repository* and its ⋯ holds *Import a folder…*, both a shell's, so a browser's list makes nothing.
  **Choosing a row opens its page in the main area**: its name, its standing (*not adopted* too), its
  summary as the header's line, and *Open code map* and *Manage* in its header, the map one level in
  (MAP3a) and *Manage* an adopter's on a shell. Beneath, on a shell, two tabs remembered for the view (UX6f,
  D150 §4.2). *Details*: the index with the local/canonical split, the commit it was fed from, its
  workspace, its line read-only with a door to Setup, and its unlanded branches; its declaration as chips;
  and for one not adopted the *Adoption steps*, led by one line with its reasoning on the info glyph (U36)
  and the join steps proposed as text. *Setup*: every setting it holds on this machine in four sections
  (*Driving*, *Line and landing*, *Sessions*, *Reach*), each folded to a line naming its values, a default
  marked as such, and open where it holds a value of its own with its *Clear*; a value from above offers
  *Set for this repository*, and each row's hint is its terminal twin. A browser's page is Details alone. The chosen repository is remembered, and adding, importing and
  managing stay drawers (D118 §3d). **Who can be asked is the host's answer**: every receiver
  list reads `addressable` from the registry, never re-derived from `adopted` (D70). A choice that
  outlives the door is offered with the door said beside it: on a machine whose adapter rides the
  direct door, a quest there sits, and the page says so under the control. The steps' paragraph claims
  only what is proven. *Manage* edits the declaration in the face it is read in (U37).
- **Knowledge: Search and Convergence** (on the frame since FRAME1f, D118 §2; one place since UX6i, D150 §2.2) — a list
  pane and a main area, the pane named *Knowledge* and headed by a two-way choice, *Search · Convergence*, remembered for
  the place and held above the rows as they scroll. Each mode keeps its list, its page and its memory as it had them as a
  place: its chosen item and its filter, and what was typed in Search while Convergence is in front. The pane's closing
  and width are the place's, and its strip is the two modes by their glyphs. Every door into either opens the place in
  that mode: Edit's *Search knowledge* (Ctrl+Shift+F) and a right-click's *Search Daoris for it* on Search, the palette's
  *Go to: Convergence*, an empty search's *Look in Convergence* and the map's shared finding on Convergence; the bar's place
  and Go's (Ctrl+6) open it as it was left.
  **Search's list is the box, *each repository's own only* and the hits**: ↓ goes from the box into the
  hits, Escape clears it, and a hit is its title, its repository and kind, and its excerpt. **Convergence's
  list is the similarity, its tier's note, then the findings**: a row is its entries' titles, its kind of
  likeness and how alike, and the repositories that reached it, a likeness in different words in the
  accent. *Local only* and the similarity are remembered, the similarity once it holds still, and so is
  the item chosen; what was typed is not. Each list's head stays in reach as its rows scroll. An excerpt
  is the entry's prose, without its frontmatter or its Markdown's markers (U39), and a hit's highlight
  adds no space beside its word (U61). A first answer is skeleton rows with its words on the count's
  line; a newer one holds the last rows at reduced opacity (audit SR11, the look to confirm). Each empty
  answer is an empty state in its tier's own words with the action that changes it: Search's leads to
  Convergence, and Convergence's lowers the similarity until its floor, where it says it is the floor
  (U41, U42). **An entry is read as it is written**, unrendered (U43), in Search's main area under its
  title, kind, provenance and place; **a finding** is read in Convergence's, the service's sentence first
  and then each of its entries whole, one entry's trouble said in its own place. Neither page adds an
  act: doctrine changes where its repository keeps it (D31). A note takes its region's width, as every
  block does (D141).
- **Map** (MAP2; MAP4a past twelve repositories: columns by who asks whom, which pan and zoom, a
  search, and a sizing menu behind the percentage; MAP4b: what the asks became in long dashes, a
  chain's hops dotted, and a *Connections* menu that switches each kind) — the circle's repositories on a ring, the quests between them as directed arrows
  with a count on each, and a shared finding as a dashed line; a detail panel beside the map says what
  the chosen node or line holds, and a legend says every mark in words, the number in a node
  included. It adds no actions: a quest in a detail opens its page on Quests (U46; FRAME1d). What the window taught it
  (names outward, wide hit strokes, arrowheads in the map's units, a choice that toggles, the circles
  kept together when it spans several) is in `docs/2026-09-23-map-design.md` §1. **A drawing is drawn
  at its own size**, one unit a pixel and framed on what it draws, so its text is the type scale's at
  every width: a narrow card gets a smaller ring, never smaller names, and a wide one a larger ring as
  far as the window's height leaves it (U44, U59). A node's detail opens **its code map** (MAP3a), the
  same page one level in: modules as boxes in layers, what uses a module above it, an arrow that skips
  a layer bowed out past the column, and *Back to the workspace* where the page action goes.
- **Sessions** (D55, D76) — a rail, the attended session, the right side bar on demand (U7), and the
  panel beneath. Its four views (the timeline, the review, Ask Daoris, the console) move between the
  side bar and the panel from each region's tab list (DOCK1b); in every label the right region is
  *the side bar* and the bottom one *the panel*, as VS Code names them, never *the console*, which is
  a view. **The side bar and the panel are on every view** (DOCK1a): one frame, as VS Code's
  workbench is, with the view in its centre; the rail is Sessions' alone, and off Sessions the session
  views say which session they speak for.
  - **The rail** groups **by state** (D126, SESSUX1c): *Waiting on you*, *To review*, *Working*, *Resumes later*
    and *Ended*, each heading counted, each session where the driver's one reader places it; a row's line names its
    repository, and *Ended* shows twelve and then *Show N more*. Its ⋯ offers **by repository** instead, remembered:
    a group per repository stating its facts (*drives here*, *held*, *busy* in words), what needs the person first
    (*parked* with it), and the ended beneath. Closed, its strip holds what waits on the person, then what runs.
    It searches by name at once and by what was said when typing settles, archived sessions included and marked. A
    row's menu holds the session's acts where its row is (SESSUX1d, D126 §3.1), each where it applies: *Answer…*,
    *Stop…*, *Try again*, *Review*, *Open folder*, *Open a terminal here*, its own window, *Archive* on an ended row or
    *Unarchive* on an archived one (SESSUX1e), *Delete…* on a conversation that served no quest (SESSUX1f), and its id. The ⋯ shows archived sessions as *Archived*, last, and
    *Archive what ended…* lists what it would take and what stays before its second press archives.
  - **The attended session has a page header, pinned** (SESSUX1d, D126 §3.2; UX7c, D152 §7): its word on its pill, then
    its title on one line, whole in its tip, then a facts line its state chooses (its repository, account, how long, and
    *attempt 4: the 3 before it failed* where its quest's earlier sessions failed); then its loud act where its state has
    one (*Try again*, *Review*), *Stop…* while it is live, and ⋯ with the rest and its id. The record under it says the
    whole title once only where the header said a short one, and folds its reference into *Details*; the chain opens
    folded on every session. The conversation scrolls under it, and the long run's way through pins beneath it.
    Below 560 px of main area its acts take their own line. **A session's stop has one owner, this header**: *Stop…*
    asks once under it, saying what follows by what the session is, then *Stop session* or *Never mind*. *Delete…*, in
    its ⋯ only where the driver says the delete would be taken, asks the same way: what goes, and that nothing brings it
    back. The parked
    card keeps *Finish* and *Decline…*, a chat's composer *Finish* and *Stop turn*, and a quest's page its door into
    Sessions; Ask Daoris's panel keeps its own, since the side bar holds that conversation on every view.
  - **The centre is the record, then the conversation**, one scroll, following the centre's width
    (U16): the head read once, then the agent's words, which the region follows until the person
    scrolls up and is then offered *Back to bottom*. **A long run reads from what it was asked**
    (SESS1): the ask comes first, and *Load earlier* sits in the gap after it. **Every word the agent
    said stays in view, and the work between two of them folds** into one row that counts it and its
    failures in the failed tone (*5 tool calls · 1 failed*); a run of one is shown as itself, and the
    run in hand stays open while the turn goes on, which says *working…*. A driven session is one
    turn, so folding a turn whole hid its account of the run. A turn the session ended inside says so,
    and its open calls read *stopped*. **A long run has a way through**, kept at the top of the
    conversation: *First failure*, *Last words*, and *find in this session*; a jump opens the fold it
    lands in and outlines the block. The head says the branch its own tree left and whether its work
    landed, and a failed or superseded attempt's branch whose tree is gone offers *Discard branch…* there, asking once,
    as each such row of a workspace's Branches does (LAND3b); *how this work ran* marks *this session*. The driver's composed
    target is folded to two lines and named as the driver's. A tool call is a row that says what it
    did (its kind's glyph, title, file, an edit's `+n −m`, its status), closed by default and open when
    it failed, its text without the fence an adapter wrapped it in (U63). **Code is the paper's
    inks**: a keyword in the accent, a string and a number in the two cool status hues, a comment
    faint (`work/code.css`), with its language and a copy button; the agent's HTML is text, and a link
    opens outside the window. A plan's done step is a check, never a strike-through. An empty record
    says why (a text-only door, or a session from before conversations were kept). **The console is
    the raw view**, and its empty sentence points at the conversation above. **It has a tab for each
    thing that is running** (CONSOLE2): the session first, then each subagent and background task it
    started, in the order they opened. A tab is its name, its kind's glyph and a mark, and it is named
    by kind and state in words (*background · running*). One tab is no tabs, so a session that runs
    nothing beside itself keeps the header it had. Picking a tab opens a hidden panel, and a stream the
    session no longer lists falls back to the session's own console.
  - **The composer's turn** (CONV4b): while a turn runs *Send* reads *Queue*, and what waits behind it
    sits above the box under *waiting for this turn to end*; *Stop turn* stands beside *Finish*,
    neutral, only while the driver says a turn is in flight and only on a door that can see a turn
    end. A stop hands back what was waiting into the box, and a stopped turn says *the turn was
    stopped here*, its cut calls drawn as *stopped*. A draft is kept per session, across a switch and
    a reload. Files ride the composer (CONV4c) as chips above the box, and the sent message names them.
    `@` completes from what git says the tree holds (CONV4d).
  - **An intake is named for what it serves** (INT4g, INT4h): *Intake for ask #id*, *Ask* and *Room*
    in its head. It takes no messages, so it gets no composer; its answer is on the ask, so its one
    answering move is a door (*Answer ask #id*), and its stop is the page header's, whose ask says the ask then stays
    a proposal (SESSUX1d). A running intake's door looks (*Open ask #id*) and only a parked one's answers.
  - **A tree is reviewed as one only when it is the session's own** (U66): a session in the
    repository's checkout is offered no merge and no discard, since both could only refuse; sending the
    work back is about the work and stays.
  - **Starting a session** names each way in by its tool and door and says what this machine's default
    is (U67), opens on a repository nothing works in, marks a busy one and says a working tree of its
    own lets it start beside it, and says a refused start in the form (U68).
  - **The monitor is the present tense**, its rail as its tiles, at the main rail's width (U69, U70).
    **Its rail is a list pane** (FRAME1h, D118 §4): resized by its edge, closed to its strip, and a strip
    by itself where the tiles would fall below their floor, never hidden; the strip keeps each running
    session's mark and lays the rail over the tiles. Ctrl+B toggles it, and what it keeps is the
    monitor's, apart from the main window's rail. Its title is the view step.
  - **A detached session's console is the output panel** (FRAME1h, D118 §4): grown, hidden, and Ctrl+J,
    kept for every detached window apart from the main window's panel. It offers no views menu, since
    the window has no side bar to move one to, and no stop, since nothing in it acts.
- **Agents** (UX6e, D150 §5) — the activity bar's place after Knowledge, shell-only, on the frame. Its list is each agent once,
  whatever doors reach it: its name and maker, its accounts in a phrase (*3 accounts · 2 signed out*, *not installed*), the
  waiting mark on one with a signed-out account a list or a default holds. Every agent the build knows is listed, the ones
  not installed under a *Not installed* head of their own, each carrying *Install* beside its row and in its right-click
  (AGENTS2, UX7b). An agent's page heads in **one line** (D152 §4): its product, maker, the version alone and whether
  installed, *Add an account…* and a ⋯; one not installed heads with *Install*. Then **its accounts, a row each, in columns
  that line up** while the main area holds them (*Account*, *State*, *Runs for*, *Now*), stacking to three lines below 40rem:
  the account's name leading (the person's, ACCT2), who signed in beside it where the two differ; its state as last known
  (*signed in* neutral, *signed out* in open's hue where it holds work, *cooling* with its reset, *unknown*) and when; the
  workspaces it runs for, *no workspace* said; what runs on it now; and **one act by state**, *Sign in* to that account,
  *Read* that account, *Try now*, *Use in a workspace…*, the rest in its ⋯. *Your own sign-in* is the last row, its
  explanation on its ⓘ. *Add an account…* offers a signed-out or unknown account's own *Sign in* first, then the tool's
  sign-in, then the new account's name and the lists it joins. **Opening the page asks no account**: *Read again* at the
  list's head asks that agent's accounts, one at a time, and a row's *Read* or its ⋯'s *Read again* asks that one account
  (ROSTER1). Then its sections, each folded to a line naming its values
  and opened by its chevron: *How accounts are used*, *Workspaces*, *Ways in* (a door is a property, never a second list),
  *What it may do* (only where Daoris hands the agent the rules file; a proposal waiting opens it), *Model and effort*
  (only where Daoris knows the tool's settings) and *Usage*; its terminal twins at its foot.
- **Plugins** (D119; a catalogue since D140) — the activity bar's place after Agents, shell-only, on the frame. Its
  list is a catalogue: *Installed*, those waiting on the person first, then on, then off; then *Daoris's own plugins*
  not installed, each with *Install* on its row; then, with packages, *Available*. A row is the plugin's icon, its
  name, version and state's word, its description cut to the row, and a meta line: where it came from, what it adds,
  *update available*. **A plugin wears its own icon or a monogram**: the icon is its manifest's, handed to the page as
  its bytes and drawn as an image, never markup; the monogram is its name's first character on one of six identity
  hues (`--ident-*`) picked by its id, computed to 4.5:1 on its field and ΔE 20 from every state, so it is identity and
  never a status. Its strip marks each installed plugin by its icon: the waiting mark, faint for off, nothing for on.
  A plugin's page heads with its icon, its switch, *Try*, *Update…* and *Remove…*, which asks once, and *update
  available* beside its state. A refused one's sentence leads, then Points, Agents, Tests (its last trial here), Data
  folder and Source, and its terminal twins at its foot; every block of the detail takes the pane's width, as on every
  page (D141). *Running* is the neutral pill, never done's green. The `＋` asks Ask Daoris for a plugin first,
  then *Make a plugin…*, the kit in a drawer; the ⋯ holds *Try a folder…*.
- **Settings** (D66, as amended by **D75**) — one page with its **domains in a list at its left**,
  one shown at a time and reachable by name. D150 moves Workspace and Permissions to the workspace
  and repository pages, Agents to its own place, and Plugins to its place (UX6j). **Seven domains
  remain**, the machine's and the person's: Get started, Appearance, AI features, Driver, Tools,
  Browser and Machine log; `settings/domains.ts` and the translated catalogues own their displayed
  names. A domain remembered from before it left opens where what it held stayed (Plugins at Driver),
  else on Appearance. Every way in opens the domain its
  fact is set in, at the part it names, and a browser is offered only the first three, its list saying
  beneath them that a machine's own settings are on the desktop. **Since FRAME1g (D118) the list is the
  frame's list pane**, 176–320 px and 176 to start, closed to its strip, a strip by room and laid over
  the domain from it, never stacked above it; the domain is the main area, its header naming it, and a
  machine domain's first load is skeleton rows in its cards' place. What the domains hold:
  - **Get started** (D97; *Setup* from NAME1b until UX6j gave that name to a repository's and a
    workspace's tab): the six setup steps in order, each a row with its state pill (done's hue,
    open's for *to do*, neutral for optional), what done means, its commands with a copy each, and the
    doors to the screens that do it at the right; a done step's doors are quiet, a step to do's are
    buttons. *Set up with Ask Daoris* is the head's one primary control, present only where Ask Daoris
    has an agent, and *Don't open at start* sits at the foot. A browser lists the one step it can know
    and says the rest is the desktop's, with neither control. The status bar's *setup: n of 5* leads
    here until the required steps are done.
  - *Appearance*: the theme and the language, each a segmented choice. The language is also View's
    *Language ▸* and the palette's, since it left the activity bar's foot (UX6j); each remembers it.
  - **AI features** (AGT6), for everyone, because which tier answers search is the service's
    answer and given to every browser: search and convergence with the service's tier and note
    verbatim, and beside them the page's own words for what changes it (the variables, read when the
    service starts: a setting from the environment gets a sentence, never a control). On the desktop,
    the intake's agent, off as a value (`""` on the wire, since the bridge leaves a null out), offering
    only the ways in this machine has installed while always showing what is in effect, with the
    account an intake in each circle runs as.
  - **Tools** (TOOLS7, D121 §4.1), on the desktop only: a card per program Daoris runs beside its agents, what it needs
    it for, how it is run as one choice (*System · Managed · Custom*), the file it runs and the version it answers.
    **Nothing applies on choosing**: a segment shows a way's controls and a press applies it, so a switch of git is said
    before it applies, the keys the two gits read a checkout differently by, beside *Switch Git* and *Never mind*. A
    download is followed on its card (its console and *Stop download*), *Delete* asks once, and *Resource locations*
    lists the person's, then the list built in, which cannot be removed.
  - On the desktop only, the machine's domains: *Driver* (the home's path, the plugins folder under it with *Open
    Plugins*, the notification switch, the strikes dial, the cool-off, and the install's update: what is staged, the
    drain, the last swap and the banner's three words, UPDATE1b). Nothing about a plugin is set in Settings: its acts are
    the Plugins place's (UX6j, D119 §5).
  - Workspace wiring, what a start runs on and scoped rules now belong to Repositories' workspace
    and repository pages (D150 §3.1). Daoris's defaults, machine-wide session rules and proposals belong
    to the agent's page. Precedence remains the harness's; no card ranks one scope over another.

## 6. Accessibility

Focus rings on everything interactive (accent, 2px, offset); drawers are `role="dialog"` with
`aria-modal`, ESC to close, focus moved in on open; the right-click menu is a `menu` named by what it is for, opened
on the menu key and Shift+F10 with its first act focused, ESC closing it and the focus going back where it was (D138);
icons are decorative (`aria-hidden`) beside real
labels; status is text plus hue, never hue alone; hit targets ≥ 28px; both themes are first-class —
the dark status palette is its own validated set, not a filter.

**A press an input method is composing with is the input method's** (IME1): its Enter accepts a candidate and its
Escape drops the composition, so no handler sends, picks, steps, clears or closes on one, and no dialog closes on that
Escape. Every handler that reads Enter or Escape asks `isComposing` (`lib/composing.ts`) first, and
`composingKeys.test.ts` holds it.
