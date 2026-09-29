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

- **The app strip** (36px) is the title bar: the mark, the menus by domain (*Daoris · Workspace ·
  Agents · View*, D75), the command center naming the scope, and the window's controls. **The
  activity bar** (48px) is the one navigation, a place per icon, and a badge counts what its place
  holds: Overview the whole of *What needs you*, Sessions its own sessions waiting on the person
  (UX5 U20). **The status bar** states the machine's standing facts: the driver, the active sessions,
  the workspace, the remote, the index and the recall tier (D24, stated on every screen). Global state
  lives in exactly one place; views never restate it.
- **The content column is the window's whole width** (UX5 U59, the owner: under a 72rem cap a
  maximized window left every view a third empty). What has a measure keeps it inside the column:
  prose at 65ch, a form at what it holds, a drawing at one unit a pixel.
- **Every view opens with a page header**: title, one-line description, and the view's primary action
  on the right. The header is the only h-level element per view; sections inside use small muted
  section titles.
- **Narrow, nothing hides behind a hamburger.** The activity bar keeps each place 36px and scrolls
  rather than crushing them (U22), and the command center gives way to the menus rather than covering
  them (U15).

## 3. Tokens

- **Type is seven named tokens** (D56): `text-meta` 11px, `text-small` 12, `text-body` 13,
  `text-title` 15, `text-view` 18, `text-wordmark` 20, `text-value` 26. `tokens.test.ts` fails on a raw
  size: the literal form had drifted to fifteen values across 203 sites with nothing reporting it.
  Tile values wear proportional figures; columns of numbers wear `tabular-nums`. **An ideograph is
  never slanted**: `font-synthesis-style: none` keeps Chinese upright, since its system face has no
  italic, and keeps the Latin face's true italic.
- **Spacing** on a 4px scale: 4 · 8 · 12 · 16 · 24 · 32. **Radii**: 6 controls · 8 cards · 10 overlays.
- **Surfaces**: `--page`, `--raised` (cards, controls), `--overlay` (drawers, toasts — one step above
  raised), `--line` and `--line-strong`. **Ink**: `--ink`, `--ink-soft`, `--ink-faint`. **A field is
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
- **A liveness mark never borrows an outcome's hue, and a live mark means a turn is running.**
  *Ended* is neutral, since it covers completed, failed, declined and stopped at once, and the pill
  beside it names the outcome (POLISH2: it wore done's green beside a failure). A live chat between
  turns is **idle**, a quiet mark and a neutral word, as the reference console draws it; the driver
  says whether a turn is in flight, and before it answers the record's word stands (U17). A fact
  about a tree (*busy*) is words, never the live mark (U60).
- **A path breaks at its separators**: `PathText`, never `break-all`, which broke a tree inside a name
  (`family\g` / `ame`). Anything else that may be wider than its line (an account name, a tool's raw
  output) wraps `anywhere`, which breaks inside a word only when that one word will not fit (U8).
- **Motion**: 140ms ease-out on overlays and hovers; `prefers-reduced-motion` disables it. No shimmer
  anywhere — loading placeholders are static two-tone.

## 4. Components

The primitives live in `ui.tsx`, and a molecule imports no hook (the components plan). The frame's own
controls are in the frame design's §3.

- **Prose has a measure of its own.** Explanatory text goes in `Prose` (65ch, font-relative): a
  paragraph inheriting the column's width ran to about 190 characters a line, roughly triple what the
  eye tracks. This is for the console EXPLAINING itself; a quest's body, a knowledge entry, a
  repository's summary and a session's note are **content**, and content is shown as it is.
- **Status leads.** A row reads `pill · title · route · how long`: identity first, the secondary marks
  at the right. A pill pushed to the far edge sat a thousand pixels from the title it described.
- **Buttons**: `primary` (solid accent, paper text — the one loud control per view), default (raised +
  line), `ghost` (borderless, for in-card affordances), and `danger` for a move that ends or removes
  something: decline, close an ask, retire, remove an account, stop, discard. It wears the hue of the
  outcome it causes, and never marks a state (U18). **The move comes first, then *never mind*,** in
  every drawer's footer and every confirming pair (U38). **A destructive edit asks once**: its first
  press opens a sentence saying what the second will do, beside the move and *never mind*.
- **Pills** carry state: status text on its soft field with its hue — label always present.
- **Chips** carry declarations (owns/accepts/packs): quiet line-bordered tokens; `accent` variant for
  what a project *accepts*, because that is the actionable half. A phrase stays in the body face
  (U37).
- **Stat tiles**: label · value · context note, per the visualization discipline; a tile may wear the
  warn rail when its note is a warning (a quest sitting a week).
- **Drawer** (right, 32rem, overlay surface, scrim, ESC/scrim/× to close) is the single detail-and-form
  surface: reading a knowledge entry, composing a quest, a quest's detail with its actions. One pattern
  instead of three; the list stays a list. A `wide` drawer (52rem) is for what holds a line of source
  (U43).
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
  scrollbar that follows the theme, through `color-scheme: light dark` and `--line-strong`.
- **A setting is a row** (`SettingRow`, the owner: *"this really long list of setup … can be
  improved"*): the label, a one-line hint that is its terminal twin, written as code (U55), the
  control at the right, and the paragraph that motivated it on an info glyph. **The label keeps its
  room**: its column has a floor, 16rem or half the row, and the control gives way to it, a path
  breaking at its separators (U58: a path crushed its label to one character a line). A settings page
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
- **A card's heading is not its first row's label**, and a card alone in its settings domain carries
  no title, because the list names it (U57). **Labels are a column**: a label beside wrapping chips
  sits in its own column. **A label never repeats its value's first word**, and a record's title is its
  first line, so its body is what follows.

**Language.**

- **Dates, counts and Daoris's own sentences are in the reader's language.** A date is written the
  way the reader's language writes it, with the machine's habits when the machine speaks that language
  (`format.ts`, and nothing else formats: a bare `en` wrote a British machine's dates the US's way). A
  sentence the driver writes is chrome, so 中文 translates it by its typed half (a verdict, a code),
  never by matching the English; where the words need what the page is not told, the driver's words
  stand (U27, U28). A default's reason passes the driver's sentence through in English and is
  translated in 中文. A count is said in its number, never with *(s)* (U54).
- **One word per thing, in each language**, held by tests: 智能体 for an agent, 驱动 for the driver
  (never 驱动器, a disk drive), 委托 for a quest, 工作区 for a workspace, and *Daoris* in running text
  (the wordmark keeps 道衍). A view is named by its name: no sentence sends a person to *Work* (U19).
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
  quest drawer; repository rows go to Projects. Above the tiles, *What needs you* (working surface
  §4) lists parked sessions, then asks waiting on a person (INT4d), then quests nobody can take, then
  folders waiting on trust, and it is absent when nothing waits. **The band is live, or it lies**: an
  ask made by another door moves nothing else a tick reports, so the shell forwards a tick when the
  asks change and the page refetches them. A wait is a span (*waiting 4d 3h*, U40).
- **Quests** — the primary page action is *Ask* (INT4c), and *new quest* is the second door: the
  regular task enters at the workspace (D65), and an action goes with what it makes, never on the
  status bar. The asks sit above the quests as a group of their own, a card per ask, what has waited
  longest first and a closed ask last (U32). **An ask says what it waits for wherever it is shown**
  (*proposed, not yet published*, *its intake asked you*, *an intake is reading it*), and its place
  is named by its kind, *workspace default*, never as one more repository name. Its record says who
  answered, the tier in words and an intake as a line (its state, then its tool); a record never says
  no intake ran above the intake that did. The ask composer promises only what this machine will do:
  an intake agent publishes on its own, so *nothing is published until you name a receiver* is said
  only where none is set (U34). *New quest* opens the **compose drawer**; the list stays scannable
  (pill, title, route, age, first line of the ask) grouped Open / In progress / Closed; **clicking a
  card opens the detail drawer**: the whole ask, the meta, the note, and the actions — take, done,
  decline-with-reason — and the loud one is the quest's next step: *take* while it is open, *done*
  once it is taken (U31). A card is for reading. What a quest carries (D65 §2) is **counted on the
  card** (a link glyph and a paperclip, each with its number) and **listed in the drawer**: links as
  links, files by name and size, a picture shown as one. The composer takes links one per line and
  files by drop, paste or *choose files…*, and says why a file was left off while the person is still
  choosing. **A chain** (D65 §4) is behind *add a next step…*; the drawer shows it as one strip
  (MAP1a): the ask, the quests before and after, the steps still to come as written, and every
  session under its quest, the one being read marked and the others underlined doors.
- **A folder waiting on trust** (D73) — the grant is offered where the hold is read: the quest's
  drawer carries *trust this folder…* under its sitting line, and *What needs you* a `trust` row, the
  only place an intake's held room shows. The question is the agent's own, asked in the open: the
  folder, what trusting means in the agent's terms, what the driver is holding, and the one file
  written. It is never wider than the hold, since the shell refuses any pair the driver did not
  produce, and there is one row per folder.
- **Projects** — adopted as cards (two columns from 1024px, one below) with chips and the local/canonical
  split; non-adopters as one group, *Registered, not adopted*, led by one line with its reasoning on
  the info glyph (U36), each with the driving row where it has a root here, and the join steps
  proposed as text at a reading measure. **Who can be asked is the host's answer**: every receiver
  list reads `addressable` from the registry, never re-derived from `adopted` (D70). A choice that
  outlives the door is offered with the door said beside it: on a machine whose adapter rides the
  direct door, a quest there sits, and the row says so under the control. The group's paragraph claims
  only what is proven. *Manage* edits the declaration in the face it is read in (U37).
- **Convergence / Search** — pages with the page header that open entries in the drawer. An excerpt is
  the entry's prose, without its frontmatter or its Markdown's markers (U39), and a hit's highlight
  adds no space beside its word (U61). Each empty answer is an empty state in its tier's own words
  with the action that changes it: Search's leads to Convergence, and Convergence's lowers the
  similarity until its floor, where it says it is the floor (U41, U42). **The reader shows the entry
  as it is written**, unrendered, in a drawer wide enough for a line of the source (U43). A note keeps
  a measure: 🔴 `max-w-prose` on a flex item caps its `basis-full`, so the measure goes on a child of
  the full-width item, or the note stops breaking to its own line.
- **Map** (MAP2; MAP4a past twelve repositories: columns by who asks whom, which pan and zoom, a
  search, and a sizing menu behind the percentage) — the circle's repositories on a ring, the quests between them as directed arrows
  with a count on each, and a shared finding as a dashed line; a detail panel beside the map says what
  the chosen node or line holds, and a legend says every mark in words, the number in a node
  included. It adds no actions: a quest in a detail opens its drawer (U46). What the window taught it
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
  - **The rail** groups by repository, what needs the person first; a group states its repository's
    facts (*drives here*, *held*, *busy* in words), and the ended sit beneath. It searches by name at
    once and by what was said when typing settles. A row's menu holds what has no other home: its own
    window, its review, its id.
  - **The centre is the record, then the conversation**, one scroll, following the centre's width
    (U16): the head read once, then the agent's words, which the region follows until the person
    scrolls up and is then offered *back to bottom*. **A long run reads from what it was asked**
    (SESS1): the ask comes first, and *load earlier* sits in the gap after it. **Every word the agent
    said stays in view, and the work between two of them folds** into one row that counts it and its
    failures in the failed tone (*5 tool calls · 1 failed*); a run of one is shown as itself, and the
    run in hand stays open while the turn goes on, which says *working…*. A driven session is one
    turn, so folding a turn whole hid its account of the run. A turn the session ended inside says so,
    and its open calls read *stopped*. **A long run has a way through**, kept at the top of the
    conversation: *first failure*, *last words*, and *find in this session*; a jump opens the fold it
    lands in and outlines the block. The head says the branch its own tree left and whether its work
    landed, and *how this work ran* marks *this session*. The driver's composed
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
  - **The composer's turn** (CONV4b): while a turn runs *send* reads *queue*, and what waits behind it
    sits above the box under *waiting for this turn to end*; *stop turn* stands beside the endings,
    neutral, only while the driver says a turn is in flight and only on a door that can see a turn
    end. A stop hands back what was waiting into the box, and a stopped turn says *the turn was
    stopped here*, its cut calls drawn as *stopped*. A draft is kept per session, across a switch and
    a reload. Files ride the composer (CONV4c) as chips above the box, and the sent message names them.
    `@` completes from what git says the tree holds (CONV4d).
  - **An intake is named for what it serves** (INT4g, INT4h): *intake for ask #id*, *ask* and *room*
    in its head. It takes no messages, so it gets no composer; its answer is on the ask, so its one
    answering move is a door (*answer ask #id*), and *stop it* stays, saying the ask then stays a
    proposal. A running intake's door looks (*open ask #id*) and only a parked one's answers.
  - **A tree is reviewed as one only when it is the session's own** (U66): a session in the
    repository's checkout is offered no merge and no discard, since both could only refuse; sending the
    work back is about the work and stays.
  - **Starting a session** names each way in by its tool and door and says what this machine's default
    is (U67), opens on a repository nothing works in, marks a busy one and says a working tree of its
    own lets it start beside it, and says a refused start in the form (U68).
  - **The monitor is the present tense**, its rail as its tiles, at the main rail's width (U69, U70).
- **Settings** (D66, as amended by **D75**) — one page with its **domains in a list at its left**,
  one shown at a time and reachable by name: *Appearance*, *Daoris's own AI*, *Workspace*, *Driver*,
  *Agents & accounts*, *Permissions*, *Plugins*. Every way in opens the domain its fact is set in, at
  the part it names, and a browser is offered only the first two. What the domains hold:
  - *Appearance*: the theme and the language, each a segmented choice.
  - **Daoris's own AI** (AGT6), for everyone, because which tier answers search is the service's
    answer and given to every browser: search and convergence with the service's tier and note
    verbatim, and beside them the page's own words for what changes it (the variables, read when the
    service starts: a setting from the environment gets a sentence, never a control). On the desktop,
    the intake's agent, off as a value (`""` on the wire, since the bridge leaves a null out), offering
    only the ways in this machine has installed while always showing what is in effect, with the
    account an intake in each circle runs as.
  - On the desktop only, the machine's domains: *Driver* (the home's path, the notification switch,
    the strikes dial); *Workspace* (the workspaces, the wiring with *Wire a workspace* behind a press,
    and *What a start runs on*: one row per workspace and job, each part with the setting that chose
    it, a job named once a circle has two, a held start's sentence at a reading measure);
    *Agents & accounts* (one card per tool, its accounts leading, its ways in beneath — a declared door
    wearing the plugin it came from — and what each account has carried); *Permissions* (the rules
    file, Daoris's defaults each with a switch and no remove, a person's rules with a remove, and
    nothing on the card ranking one scope over another, since precedence is the harness's; the
    machine's scope reads *every session on this machine*); *Plugins* (a row per folder: what it
    declares and speaks on, running or off, the driver's sentence under a refused one, the switch and
    Remove).

## 6. Accessibility

Focus rings on everything interactive (accent, 2px, offset); drawers are `role="dialog"` with
`aria-modal`, ESC to close, focus moved in on open; icons are decorative (`aria-hidden`) beside real
labels; status is text plus hue, never hue alone; hit targets ≥ 28px; both themes are first-class —
the dark status palette is its own validated set, not a filter.
