# The screens since UX7, designed: what was found, the rules it shows are missing, each newer surface, and the parts they share (UIDESIGN1)

**Standing: proposed (D185), 2026-10-20.** Written from main at `02632075f` before any build. The owner reviews it, with
another maker's read, before a row it files starts. It builds nothing; §9's rows do.

> UIDESIGN1, decided as **D185** once the owner accepts it. `Dnn` is `docs/decisions/Dnn.md`. The owner's report of
> 2026-10-20 asked for the features built since UX7 to have a UI/UX design of the same standing as the fabric's
> (`docs/2026-10-16-fabric-events-design.md`), with the processes drawn as flowcharts (FLOWCHART1, D186, designs those).
> Two reports join it: Settings' content does not use the pane (UIFIT1a-c), and a sign-in panel says nothing while it
> waits (CLAUDESIGNIN1b).

It amends the platform language's §4 and §5 (D41), the frame model's §3b (D118), the components method's stories
(D55) and UX7's §4.5 sign-in step (D152). §8 lists each.

- §0 is what was looked at. §1 is what was found: the audit, a row per screen, then what cuts across.
- §2 is the rules the audit shows are missing, each an amendment with its reason and its check.
- §3 designs each newer surface: its place, its states, its words, its keys, its themes and its narrow form.
- §4 is the component catalogue, §5 the traps, §6 what is not built here.
- §7–§10 are what was rejected, what this amends, the rows in their order, and what this document's gate does not cover.

## 0. What was looked at

- **The code**, at `02632075f`: every component added under `src/Daoris.Web/src` since 2026-10-05 (UX7's day), found by
  git's history of added files, read at the lines cited below; its stories by name; where it is mounted, from its caller.
  A few files in that list were moves of older ones (`App.tsx`, `ui.tsx`) and are read only where a newer part sits in them.
- **The frame's own numbers** (`work/layout.ts`): the side bar opens at 45% of the window and the main area keeps a 400 px
  floor; Settings' list is 176 px to start (the frame model §3a). At the install's 1546 px window, Settings' main area is
  about **1,290 px with the side bar closed** and about **630 px with it open**; at a 680 px window, with the list and the
  side bar at their strips, about **540 px**. The wireframes below are drawn at these three widths, named **wide**, **half**
  and **narrow**.
- **The decisions behind them**: D176 (brainstorms), D177 (meetings), D180 (heavy runs), D182 (pushed state), D183 (Ask
  Daoris's modes), D184 (sessions' access, asking mode), D173 with the fabric design's parts D and E (where work stands),
  and D171's This Daoris design.
- **The contracts**: the platform language whole; the working surface §4; UX6 and UX7; the frame model; the Ask Daoris
  modes design; the verification lanes design.
- **Not looked at: the window.** UIFIT1a takes the shots that name which cause makes Settings look half used. §1.2 names
  the three candidate causes, and §2's first rule answers each, so the design holds whichever the shots show.

## 1. What was found

### 1.1 Every screen since UX7, a row each

*Keeps* and *breaks* name the platform language's rule (`platform-ux` §n) or the frame model's. A file named without a
story has none of its own; one drawn inside another's stories says whose.

**Settings and setup**

| Screen | Mounted | Shows | Keeps | Breaks | Worst problem |
|---|---|---|---|---|---|
| This Daoris (本机), `WiringDomain` and eleven cards (`Wiring*.tsx`) | Settings → This Daoris (D171) | How this install is wired: folders, service, loop, files, variables, versions, terminal, knowledge, logs | Rows (§4 *a setting is a row*), paths at separators, a fact not read said in its own words (`WiringParts.tsx:13-22`), skeleton first load | Values at the pane's far edge (`ui.tsx:1464`, `:1476`); cards with no space between (`WiringParts.tsx:46`), where other domains' take `mt-3.5`, 14 px, off the 4 px scale (§3 *Spacing*) | At the wide pane the words sit left and a short value (a size, a pill) a thousand pixels away; the stories draw each card in a 46rem box with a gap the page does not have (`Wiring.stories.tsx:24`) |
| Driver, its dials and this machine's sessions' access (`DriverDomain.tsx:86`, `:242`) | Settings → Driver | The home's door row, notify, strikes, cool-off; *Sessions' access* for this machine; the update | Rows; a number sized to two digits (`:165-190`) | The same far-edge values; two cards flush; the access card opens with a paragraph above its rows (`:245`), explanation set as content (UX7 §1.5) | A pause's change the driver refuses becomes a toast (`:257`: no `answered` for a pause) |
| AI features → Ask Daoris's access (`AiJobs.tsx`, `HelpModePicker.tsx:132-178`, `ConfiguredAccess.tsx`) | Settings → AI features (D183 calls it *Daoris's own AI*) | The agent, the mode chip, full access's confirmation, restricted mode's grants (a reviewed mock) | Rows; the chip's warning hue for full; grants unavailable said with why | `FullAccessConfirm` is a second confirm beside `InlineConfirm` (§4 *every such ask is one component*): no waiting status, no refusal inside it | Two confirmations for one rule; D183's name for the place is not the screen's |
| Sessions' access on Setup (`settings/Access.tsx`, in `RepositorySetup.tsx:420` and the workspace's) | Repositories → a repository or a workspace → Setup | Full access, then each pause with its value and where it was set | `Inheritable` (*Set for this …*, *Clear*), terminal twins, full access asked once through `InlineConfirm` (`Access.tsx:219-230`) | A pause's refusal is a toast, not said at its row (§4) | No story at any level: its three levels, its full state and its refusals are seen only on the window |
| Heavy gates on Setup (`HeavyGates.tsx`) | Repositories → a repository → Setup (HEAVYRUN1b2b-c) | This machine's list of heavy gates, edited whole; whether the repository's own file wins | The whole list edited in place; its refusal under *Save* (`:224`); four `ProjectPage` stories | The editor's four columns are fixed (`:158`) and never stack (§4 *a narrow row stacks*) | The person sets heavy gates and never sees one run or wait: the slot is said nowhere (§1.2 item 2) |
| Setup's fields: commits, helpers, opinions, reviews (`Commits.tsx:106`, `Helpers.tsx:111`, `Opinions.tsx:170`, `Reviews.tsx:213`); a remote's wiring (`WorkspaceSetup.tsx:465`); rules (`AgentRules.tsx:311`); favourites (`BrowserDomain.tsx:179`) | Setup tabs; an agent's page; Settings → Browser | Each a small form under its row | Fields sized by kind inside the form | Forms capped at 40–48rem (§4 *a form is sized to what it holds*, read as a cap); `md:` columns, the window's width (frame model §3b) | At the wide pane a form stops at half of it; with the side bar open its three columns stay three at 630 px, since the window, not the pane, chose them |

**Agents**

| Screen | Mounted | Shows | Keeps | Breaks | Worst problem |
|---|---|---|---|---|---|
| The sign-in panel (`agents/SignIn.tsx:86-193`) | Under an account's row; *Add an account…*'s step 2 (UX7 §4.5); *What needs you*'s sign-in row (UX6d) | A live dot, three numbered steps, the link, the device code, the code box, *Cancel*, the output folded | Steps in order, one live control (§4 *a step happens where it was started*), the system browser for the link | No phase, no time, no bound. At `02632075f` a refused start also closed the panel into a toast (`harnessRuns.tsx:194-199`) and the code was found by one version's words (`:202-207`); CODEXSIGNIN2 and CODEXSIGNIN3, merged the same day, say the refusal in the panel with *Try again* and *Close* and read the code by its shape | A sign-in at five seconds and at five minutes look the same; an organism holding its hook, so no story shows any state |
| An account's usage curve (`UsageCurve.tsx` in `AccountUse.tsx:405`) | An agent's page → Usage (D165) | Each account's share against its curve | Drawn in `AccountUse`'s stories | Not read closely here | None found at this reading |

**Ask Daoris, in the side bar**

| Screen | Mounted | Shows | Keeps | Breaks | Worst problem |
|---|---|---|---|---|---|
| History (`help/AskHistory.tsx`) | The panel's history | Conversations a page at a time, a search | The load-more foot it gave the language (§4) | — | None: it is the rule's source |
| The access chip and *What it has* (`HelpModePicker.tsx:47-126`, `WhatItHas.tsx`) | Beside the panel's composer; the panel's *What it has* view | The mode now and next; what each start was handed, what it lacks with its door | Never silent (D183 point 3), the warning hue for full, the receipt read and never derived | *What it has*'s rows switch on the window's width (`WhatItHas.tsx:207`, `max-sm:`) inside a side bar that is narrow at any window (frame model §3b) | No story, so its narrow form has never been seen |
| Configured access (`ConfiguredAccess.tsx`) | AI features, under Access while restricted | The five grants, their states, their operations | Eleven stories, both languages, narrow, dark | — | A mock: its grants are HELPMODE1c2-c3's |

**Sessions**

| Screen | Mounted | Shows | Keeps | Breaks | Worst problem |
|---|---|---|---|---|---|
| New conversation (`work/NewConversation.tsx`) | Sessions' main area, Ctrl+N (D162) | The words, then the repository, tree, agent and account chips, *Start* | A refusal whole under *Start* (`:131`), defaults that send nothing, IME's Enter; 33 stories | — | None: the exemplar for a start composer |
| The wait strip at the foot (`WaitStrip.tsx`) | Above the session's box (D168) | Each open wait with the head's press | One owner per answer, a refusal where pressed, the head's molecules reused | — | None found |
| A call held for the person (`PermissionAsks.tsx`) | The session's head and foot, under asking (D184) | The call, its kind, how long it waited and the bound, a press per choice | *How long* and the bound (`:161-163`), a status from the first (`:178-180`), the refusal in place (`:181`) | The bound is the page's copy of the driver's (`:55`; PERMUNTIL1) | The exemplar for §2's second rule, but for that copy |
| Rules a session proposed (`ProposedRules.tsx`) | Head and foot | A rule and its *Allow…* | Asks once | — | Drawn only inside `WaitStrip`'s stories |
| Helpers and hand-over (`HelperAsks.tsx`, `AskHelper.tsx`, `HandOverAsk.tsx`, `AgentChip.tsx`) | The session's page head and foot (D163) | An agent's ask for a helper, its yes and no; handing the task to another agent | Stories in `HelperAsks` and `SessionPageHead` | — | None found |
| Parts and the channel (`PartsStrip.tsx`, `asks/ChannelSection.tsx`) | Under the session's head; an ask's page (D167) | A session's parts; the ask's channel and its unread | A refusal by its code, in the composer (`ChannelSection.tsx:53`) | — | Adds to the record head (below) |
| Review and second opinion (`ReviewGate.tsx`, `OpinionGate.tsx`) | The session's head (D154, D155) | The review environment; the opinion's findings and disputes | Twenty-odd stories each, narrow and dark | — | None found |
| Landing on the head (`Lands.tsx`, `LandingSync.tsx`, `HeldDiscard.tsx`, `EntryAccept.tsx`) | Head and foot | *Accept…*, the rebase, a held discard | Asks once | — | Drawn only inside `WaitStrip`'s and `SessionHead`'s stories |
| The running call (WAITSAY1: `ConversationView.tsx:274`, `SessionRow.tsx:166`) | The conversation and the rail | *running {call} for {time}* | A wait said with its time | — | A call that waits on something else (the heavy-run slot) reads as running |
| Where this work stands (`WorkMilestone.tsx`) | The attended session's record (`AttendedSession.tsx:289`); its line under a tidied branch (`WorkspaceTidied.tsx:121`) | The end and its proof, the result, the clean-up, the recovery (the fabric's §D0) | Nothing judged again; the driver's clauses as recorded | Four facts set as a definition list of small type, where they are a sequence (§2's fourth rule) | No story; the work state is read in four places as lines (`App.tsx:304`, `AttentionBand.tsx:125`, `WorkspaceView.tsx:262`, `WorkFrame.tsx:1243`) with no designed screen |
| The attended session's record head (`AttendedSession.tsx:271-291`) | Sessions' main area | Since UX7: the parts strip, *Accept*, relations, the channel, the milestone, before *How it came to be* | Each block folded or one line | UX7 §6.2's budget, the conversation at most 200 px down, measured before five blocks joined | Nothing has measured it since |
| The workflow's run (`WorkflowRunView.tsx`, `WorkflowLine.tsx`) | A session view in the side bar (`WorkFrame.tsx:1147`); a quest's and an ask's page (`QuestPage.tsx:468`, `AskPage.tsx:287`) | Where a piece of work stands in its workflow | Derived, never inferred; hues by whose wait | — | The only drawn process, a vertical rail (FLOWCHART1) |

**Repositories and quests**

| Screen | Mounted | Shows | Keeps | Breaks | Worst problem |
|---|---|---|---|---|---|
| Branches, a branch's page, the commit graph (`BranchList.tsx`, `BranchPage.tsx`, `CommitGraph.tsx`) | A repository's Branches tab (UX6h) | Branches by kind, a branch's commits | The built-in Git design §2 | — | `CommitGraph` has no story of its own |
| Clean-up, recoveries, kept history (`WorkspaceBranches.tsx`, `WorkspaceTidied.tsx`, `WorkspaceRecoveries.tsx`, `KeptHistory.tsx`) | A workspace's Branches tab; Details | What the look cleaned, what is kept aside, what may be cleared | Asks once; lists what goes and what stays | — | `WorkspaceRecoveries` has no story |
| Set up for agents (`SetUpForAgents.tsx`) | A repository not adopted (`ProjectPage.tsx:237`, LAYOUT8) | Its plan, then *Set it up* | Stories in `ProjectPage` | — | Its run is a quest's session; nothing in place says its phases |
| The Workflow tab (`WorkflowTab.tsx`, `WorkflowChart.tsx`) | A repository's and a workspace's page (`ProjectPage.tsx:272`, `WorkspaceView.tsx:350`) | The current workflow as a rail of steps | Read-only, a list for the keyboard, nothing fixed-width | — | A rail where a flow is meant (FLOWCHART1) |
| A teammate's ask, an ask's review choice, conflicts (`TeammateAskPage.tsx`, `AskReview.tsx`, `Conflicts.tsx`) | Quests' main area (`AsksPart.tsx:358`, `AskPage.tsx:442`) | A teammate's ask read only; the review environment an ask chooses; conflicts | Read-only where it is not the person's | — | None has a story of its own |
| The workspace switcher (`app/WorkspaceSwitcher.tsx`) | The status bar's scope item | Which circle the console looks at | Absent with one workspace; stories | — | None found |

**Built with no screen**

| What | Where it lives today | Worst problem |
|---|---|---|
| Brainstorms (D176) | `daoris-driver brainstorm` and the CLI's twin; BRAINSTORM1d names "the page" in one clause | A packet the person must read before it goes has no page to be read on |
| Meetings (D177) | `daoris-driver meeting`; "the page follows BRAINSTORM1d's" | No place in the frame, no state on any screen |
| The heavy-run slot (D180) | `daoris-driver run-heavy`; the waiter's own output | Who holds a repository's slot and who waits is said to no person |
| A session's access (D184 point 1) | The record's `access` (`data/api.ts:434-438`), a line of uncoded English in its transcript | A session under full access wears nothing on its head, where Ask Daoris's full access wears a warning chip |
| Pushed state (D182) | The query cache, fed by pushes | No screen by design; the risk is an empty state drawn before a slice is judged |
| Verification lanes (proposed, FABRIC-VERIFY1) | The lanes design §7: *accepted; verification running* | Not built; named here so §2's second rule covers it when it is |

### 1.2 What cuts across

1. **The wide pane.** Three candidate causes for UIFIT1a, each real in the code:
   - `SettingRow`'s grid (`ui.tsx:1464`) gives the label's column `1fr` and the control `auto`, `justify-end` (`:1476`): at
     1,290 px a switch sits a thousand pixels from its words.
   - Forms are capped at 40–48rem (the six files above), half of the wide pane, and three of them choose columns by the
     window (`md:`).
   - Cards have no rule for the space between them: This Daoris's (`WiringParts.tsx:46`) and Driver's (`DriverDomain.tsx:86`,
     `:242`) touch, other domains' take `mt-3.5` each, and the domain's wrapper only zeroes the first (`SettingsView.tsx:166`).

   The stories hide all three: This Daoris's cards are drawn in a 46rem box whose decorator adds the gap
   (`Wiring.stories.tsx:24`), and `SettingRow`'s own story in a 48rem one (`ui.stories.tsx:109`). No story shows a wide pane.
2. **A wait that says nothing.** The sign-in panel, a heavy run's slot, and (unbuilt) a brainstorm's passes and a meeting's
   rounds: each is a process the person started and waits on, and none says where it is or for how long. Two built
   screens already do it right: the held call (`PermissionAsks.tsx:161-163`) and the running call (WAITSAY1).
3. **A refusal as a toast.** A refused sign-in start closed its panel and toasted (`harnessRuns.tsx:194-199`, until
   CODEXSIGNIN2 said it in the panel); a pause's change refused on Setup or Driver toasts (`DriverDomain.tsx:257`); full
   access's own confirmation has no place to say one. The language says it in the form, whole, for a start and for a confirm
   (§4); the newer screens did not carry it to every press, and each fix so far was one screen's.
4. **A process said as words.** The work state is read in four places and drawn as lines in each; the milestone's four facts
   are a sequence set as a list; the workflow rail is the only drawn process (FLOWCHART1).
5. **A surface with no door.** Brainstorms and meetings have no place, the heavy slot no line, a session's access no fact on
   its head.
6. **Stories at one width, and organisms with none.** The sign-in panel, *What it has*, the milestone, sessions' access,
   recoveries, a teammate's ask, an ask's review choice and the commit graph have no story of their own.
7. **The window's width inside a region.** `md:` in three forms and `max-sm:` in the side bar's *What it has*, where the
   frame model §3b says a region lays out by its own width.
8. **The record head grew.** UX7 measured the conversation at most 200 px down; five blocks joined above it since.

## 2. The rules the audit shows are missing

Each is an amendment, to the platform language unless named otherwise, with the reason that keeps it and the check that
holds it. Once the owner accepts this, each is written into the language, with its check, by the first feature of §9 that
applies it: R1, R6 and R7 by *Settings' rows and cards use the pane* and *Forms take the pane*, R2 by the sign-in's, R3 by
the access feature's, R4 by the milestone's, R5 by the first meeting feature.

### R1. A setting's value sits beside its words, a form takes the pane, and cards are spaced by one rule

*Amends §4, Forms and settings: "a setting is a row", "a form is sized to what it holds".*

- **A card's rows share one label track.** The track is the widest of the card's labels, at least 12rem and at most 22rem;
  the value column is the rest. Every row's value starts at the track's edge, so a short value (a switch, a number, a pill,
  a size) sits beside its words and the values of a card form one column the eye runs down. A long value (a path, a list, a
  sentence) takes the value column's width. The row's own acts (*Open*, *Edit*, *Clear*) follow its value, never the pane's
  far edge. The hint (the terminal twin) and the ⓘ stay under and beside the label; a row's notice sits in the value
  column, under the value it is about.
- **Below 32rem of the card's own width** the track goes: the value goes under its words at the card's full width, as WSR4's
  26rem row did. The floor moves from the row to the card because the track is the card's.
- **What the right of a wide card holds is what its values need.** Nothing is stretched to fill it: a card of short values
  leaves its right empty, as a long English line at a wide pane is the language's stated cost (§4, D141).
- **A form is sized to its content up to the pane.** Each field by what it holds: a number 4.5rem, a name 16rem, a branch
  pattern 20rem, an address, a command or a folder the rest of the form's width. The form lays out by its own container,
  never the window's, and nothing caps it below the pane: the 40rem and 48rem caps go. A form of rows (heavy gates, rules,
  favourites) keeps its columns while its container holds them and stacks each row below its floor.
- **Cards are 16 px apart, set once by the domain's container.** A card sets no outer margin of its own. 16 px is the scale's
  step; the `mt-3.5` cards wear today is 14 px, which is not on it.
- **Why:** at the wide pane a value at the far edge is read by crossing a gulf, and a capped form leaves half the pane to a
  form that wants it; neither is visible in a story drawn at 46rem. **Check:** `SettingList`'s test reads every row's value
  starting at the track's edge in a wide container and under its words in a narrow one; `tokens.test.ts` fails on an outer
  margin on a `Card` and on a `max-w-[…rem]` on a form; the stories of R6.

### R2. A step that waits says its phase and how long

*Amends §4, after "a step in a task happens where it was started".*

- **Anything the person started and waits on for longer than a moment is a list of its phases, where it was started**: a
  sign-in, an install or an update, a heavy run, a brainstorm's passes, a meeting's rounds, a set-up for agents, a
  verification lane once built. The phases done, each with the time it took; the phase now, with how long it has run; the
  phases to come. One line names the whole's time.
- **A bound is said when one is known, never invented**: *the code expires in 13 min*, *refused by itself in 12 min* (the
  held call's). Where the usual time has been measured (CLAUDESIGNIN1 measures a sign-in's), a phase past it says it is
  *taking longer than usual* and what the usual is; where nothing was measured, it says only its time.
- **The phases are the driver's**, written where the process runs (its relay, its record) and read by the page; never
  guessed from a tool's printed words, which stay the fallback for a link or a code a tool prints.
- **Hues**: the phase now wears the live mark while Daoris or an agent works it (§3: a live mark means something runs); a
  phase that waits on the person is open's hue, theirs alone; a phase that waits on another session or on Daoris is neutral
  and names what it waits on; a failed phase wears the danger ink with its sentence. Done is done's ink. Never hue alone.
- **Time steps by its own clock**, seconds in the first minute and minutes after, and stops when its phase ends; nothing at
  rest moves (§3, FREEZE1).
- **Why:** a process that looks the same at five seconds and at five minutes cannot be told from a hung one, and the person
  stops it or waits for ever. **Check:** `PhaseList`'s stories per state; each holder's test reads its phases from the
  driver's words in both languages; a fake clock steps the time.

### R3. A refusal is said where its action was started

*Amends §4's toast and `InlineConfirm` rules into one rule for every press.*

- **The place a press was made says its refusal, whole, in the driver's sentence**: a setting's change under its row's
  editor, a mode switch under its chip, a sign-in in its panel at the phase that failed, a list under its *Save* (built). The
  editor or panel stays open with what the person typed, and the press is offered again.
- **A toast is for what has no place to stand in**: the place has gone (the person left it), or the act was started by a door
  that drew nothing (a menu item, a key, the palette). A cancel the person pressed is never said back.
- **Why:** a toast in the corner reads as about whatever is there, and it closes the form that would let the person try
  again. **Check:** each holder's test refuses a press and reads the sentence beside it, with no toast.

### R4. A process is drawn as a flow where its shape matters, and as phases where its present matters

*Amends §4 and §5; the flowchart is FLOWCHART1's (D186).*

- **One run, in its place, now**: R2's phase list. **A process's life** (a quest, a session, a unit of work, a workflow's
  run, landing and clean-up): FLOWCHART1's chart on its record, drawn from the record. This design draws no flowchart.
- **A state that is one of a sequence is shown in its sequence** where room allows: the milestone's four facts are four
  phases (§3.7); a run's step is its place on the chart.

### R5. Every surface is reached from the frame

*Amends §5's opening: how a view is reached.*

- **No new place on the activity bar**: a tenth is a decision with its own counts (UX6 §8). A newer surface lives in the
  place that holds its thing.
- **Each surface has**: its place and its list; a door from *What needs you* when it waits on the person; its verb in one
  menu and the palette (D152's one table); a door in Ask Daoris or an exemption with its reason (`HelpCoverageTests`); and
  its terminal twin at its foot. §3.9 tables each.
- **Why:** a surface reached only by a terminal is a surface the person who reads this application never finds.
  **Check:** the command table's test and `HelpCoverageTests` name each surface's verb and door.

### R6. A story shows the widths the frame gives, without help

*Amends the components method (`docs/2026-09-21-working-surface-components.md`, D55) and the working surface §4's
catalogue.*

- **A main-area organism's stories are drawn at wide, half and narrow**, from one helper (`storyWidths`: 80rem, 39rem and
  34rem of main area), in English and 中文, light and dark. A side-bar view's at the side bar's floor (300 px) and at 45%
  of the install's window.
- **No decorator adds what the page does not have**: a gap, a cap, a padding. A domain's cards are drawn in the domain's own
  container.
- **An organism that holds a hook splits a molecule for its states**, so every state has a story: the sign-in panel becomes
  `SignIn` over `SignInSteps` (§3.2).
- **Why:** every R1 defect passed its stories. **Check:** a story test that the helper's widths are used by every story
  of a component under `settings/`, `projects/` and `work/` mounted in a main area.

### R7. A region lays out by its own width

*Amends the frame model §3b ("it lays out by its own width, never the viewport's") from a rule into a check.*

- **No viewport variant inside a region** (`sm:`, `md:`, `lg:`, `max-sm:` and their kin): a container query on the region or
  the component. The strip and the status bar keep theirs, since both are the window's (the status items at
  `work/frame.tsx:419-451`).
- **Why:** the side bar and the list narrow a region without the window changing; a form at 630 px of pane kept its three
  columns because the window was 1546. **Check:** `tokens.test.ts` fails on a viewport variant outside the strip's files.

## 3. Each newer surface

Each section says the surface's place in the frame, its states (empty, loading, waiting, refused, done and its own), its
chrome in English and 中文 (proposed keys; authored content and the driver's sentences stay as written), its keyboard path,
both themes and its narrow form. A word new to the glossary is a proposal for D116's check.

### 3.1 Settings at the wide pane

**Place:** every Settings domain and every Setup tab; This Daoris, Driver and AI features first, since they are where it was
reported. **What changes:** the layout of rows, forms and cards (R1). No word changes.

```
 wide (1,290 px of main area), Settings → Driver, today
┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Daoris home                                                                <home path>   [Open This Daoris]  │
│ daoris-driver …                                                                                              │
│ Notify me when a session parks ⓘ                                                                       [■]   │
│ daoris driver notify …                                                                                       │
│ Failures before a quest parks ⓘ                                                                       [ 3]   │
│ Cool-off when no time is named ⓘ                                                                [ 60] min    │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────┐  no gap
│ Sessions' access                                                                                             │
│ This machine's level, under every workspace's and repository's …                                            │
│ Full access ⓘ                                                                                          [ ]   │
```

```
 wide, Settings → Driver, designed: one label track a card, values beside their words, cards 16 px apart
┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Daoris home                          <home path>  [Open This Daoris]                                         │
│ daoris-driver …                                                                                              │
│ Notify me when a session parks ⓘ     [■]                                                                     │
│ daoris driver notify …                                                                                       │
│ Failures before a quest parks ⓘ      [ 3]                                                                    │
│ Cool-off when no time is named ⓘ     [ 60] min                                                               │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
                                                                                                        16 px
┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Sessions' access ⓘ                                                                                           │
│ Full access ⓘ                        [ ]  Off: each pause below as set.                                      │
│ Permission prompts ⓘ                 auto, from this machine        [Set for this machine]                   │
│ Intake routing ⓘ                     you accept the route           [Set for this machine]                   │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

```
 wide, Settings → This Daoris, the Folders card, designed
┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Folders                                                                                    [Measure again]   │
│ The Daoris folder                    <path>                                                                  │
│ daoris-driver wiring                 installed · your account's DAORIS_HOME names it                         │
│ Program                              412 MB · counted 14:02                                                  │
│ <path>                                                                                                       │
│ Your settings and records            38 MB · counted 14:02                                                   │
│ <path>                               an entry at its old place: sessions/  2 MB  a conversation may resume   │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

```
 narrow (540 px), the same card: the track goes, each value under its words
┌──────────────────────────────────────────────┐
│ Folders                      [Measure again] │
│ The Daoris folder                            │
│ daoris-driver wiring                         │
│ <path, breaking at separators>               │
│ installed · your account's DAORIS_HOME …     │
│ ──────────────────────────────────────────── │
│ Program                                      │
│ <path>                                       │
│ 412 MB · counted 14:02                       │
└──────────────────────────────────────────────┘
```

```
 wide, Setup → Heavy gates, editing: the form takes the pane; narrow, each gate stacks
 wide                                                                       narrow
 Name        Command                                Folder                  1  Name    [web          ]
 [web     ]  [npm --prefix web test -- --run     ]  [web       ] ˄ ˅ ×        Command [npm --prefix…]
 [driver  ]  [dotnet test Driver.Tests --no-build]  [          ] ˄ ˅ ×        Folder  [web          ]
 [+ Add a gate]  [Save]  Never mind                                               ˄ ˅ ×
```

| State | What the domain shows |
|---|---|
| First load | Skeleton rows in each card's place, at the track's width |
| Ready | Every card, values at the track's edge |
| A fact not read | That row's value in its own words (`Unread`); no other row moves |
| A change on its way | The row's control disabled, its status beside it (§4's *while it waits it says so*) |
| A change refused | The driver's sentence under that row's editor, the editor open (R3) |
| Wide, half, narrow | Track beside; track beside (630 px holds it); stacked below 32rem of card |

**Keys:** unchanged. The reading order is the row's (label, hint, ⓘ, value, acts), so Tab meets each value right after its
words, as the eye does now. **Themes:** unchanged; the rule between rows stays `--line`. **What changes in UIFIT1b and
UIFIT1c:** §9.

### 3.2 Signing in

**Place:** under the account's row on an agent's page; *Add an account…*'s second step (UX7 §4.5); under a sign-in row of
*What needs you* (UX6d). Never a drawer or a toast. **The phases** are the driver's (CLAUDESIGNIN1b's split): written into the
same relay as the tool's output and read by the page.

| Phase | Says, while it is the phase now | Ends when |
|---|---|---|
| Starting {tool} | — | the tool's process is up |
| Waiting on your browser | the link with *Copy link*, opening in the system's browser; or, for a device code, the code with *Copy code* and when it expires | the tool reports the browser's return, or asks for the code |
| (Codex's device code, or a pasted code) Entering the code | the box, focused, and *Continue* | the code is sent |
| Checking who signed in | — | `auth status` answers |
| Joining the lists | — (for *Add an account…*, step 3 follows: its name and lists) | the roster reads the account |

```
 wide, Claude Code's page, account-2's row, signing in (1 min 12 s)
┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ account-2               unknown · never read         work                       [Read]                    ⋯  │
│ ┌ Signing in to account-2 ─────────────────────────────────────────── 1 min 12 s so far ─── [× Cancel] ──┐ │
│ │ ✓ Starting Claude Code                                                                       2 s       │ │
│ │ ● Waiting on your browser                                                           1 min 10 s so far  │ │
│ │   The tool opened your browser: finish signing in there.                                               │ │
│ │   ↗ https://claude.ai/oauth/authorize?…   [Copy link]   Opens in your system's browser                 │ │
│ │ ○ Checking who signed in                                                                               │ │
│ │ ○ Joining the lists                                                                                    │ │
│ │ ▸ The tool's output                                                                                    │ │
│ └────────────────────────────────────────────────────────────────────────────────────────────────────────┘ │
```

```
 narrow, Codex's device code, then a refused start
┌──────────────────────────────────────────────┐   ┌──────────────────────────────────────────────┐
│ Signing in to work-2          [× Cancel]     │   │ Signing in to work-2              [Close]    │
│ 48 s so far                                  │   │ ✕ Starting Codex                             │
│ ✓ Starting Codex                     1 s     │   │   <the driver's sentence, whole, wrapping    │
│ ● Waiting on your browser                    │   │   anywhere a long id needs>                  │
│   Open the page and enter this code:         │   │ [Try again]                                  │
│   ABCD-12EF   [Copy code]                    │   │ ▸ The tool's output                          │
│   Expires in 14 min                          │   └──────────────────────────────────────────────┘
│   ↗ https://auth.openai.com/…  [Copy link]   │
│ ○ Checking who signed in                     │
│ ○ Joining the lists                          │
└──────────────────────────────────────────────┘
```

| State | What the panel shows |
|---|---|
| Starting | The first phase live, the rest to come |
| Waiting, no link yet | *Waiting for the tool to open the page…*, the time |
| Waiting, link shown | The link, *Copy link*, the time |
| Waiting, device code | The code, *Copy code*, *Expires in …* from the tool's own bound |
| Code asked | The box, focused, *Continue*; then *Sent* |
| Longer than usual | Under the phase: *Taking longer than usual: usually under {usual}*, only where CLAUDESIGNIN1 measured it |
| Refused at the start | The first phase failed, the driver's sentence whole, *Try again* and *Close*; no toast (CODEXSIGNIN2 built it as a panel of its own; here it is the list's first phase) |
| Failed at a phase | That phase failed with its sentence; the earlier ones done; *Try again*, *Close* |
| Stopped at a bound | The driver's sentence (*the tool did not finish in …*), *Try again* |
| Cancelled | The panel closes; nothing is said (the person pressed it) |
| Done | The panel closes to one line under the row, *Signed in as {who} in {time}* with ×; the row's pill says *signed in*. For a new account, step 3 opens |

| Key | English | 中文 |
|---|---|---|
| `signin.phase.start` | Starting {{tool}} | 正在启动 {{tool}} |
| `signin.phase.browser` | Waiting on your browser | 等你在浏览器中完成 |
| `signin.phase.code` | Entering the code | 输入代码 |
| `signin.phase.who` | Checking who signed in | 正在确认登录的是谁 |
| `signin.phase.lists` | Joining the lists | 正在加入列表 |
| `signin.device.enter` | Open the page and enter this code: | 打开页面并输入这个代码： |
| `signin.device.expires` | Expires in {{time}} | {{time}}后过期 |
| `signin.done` | Signed in as {{who}} in {{time}} | 已登录为 {{who}}，用时 {{time}} |
| `phase.elapsed` | {{time}} so far | 已用 {{time}} |
| `phase.longer` | Taking longer than usual: usually under {{usual}} | 比平常久：通常不到 {{usual}} |

Today's `signin.title`, `signin.waiting`, `signin.opened`, `signin.copy`, `signin.copyCode`, `signin.cancel`,
`signin.output`, and CODEXSIGNIN2's `signin.retry` (*Try again*, 重试) and `signin.close` stand. `signin.codeLater` and the
static step 3 (`signin.ready`) go: the phases say both.

**Keys:** the panel opens with the focus on its first live control (*Copy link* once a link is shown, the code box once it
is asked); *Cancel* is reached by Tab and is never Escape, since it stops a process. A refusal moves the focus to *Try
again*. Done gives the focus to the row's act, or to step 3. Each phase is a list item named by its words and its state in
words (*done*, *now*, *waits on you*, *failed*, *to come*), so a reader hears where it is; the time is a status that speaks
when its phase changes, not each second. **Themes:** the marks are §3's: done's ink, the live mark, open's hue for the
person's phase, danger ink for a failed one. **Narrow:** the head's time moves under the title; the link truncates in the
middle with the whole on *Copy link*.

**What changes in the existing rows:** CLAUDESIGNIN1b is one feature built end to end by one builder (D187), not split at
the driver and page seam as its row says: the driver writes the phases, the page draws them through `PhaseList`, and `SignIn`
splits over `SignInSteps`, a molecule with a story per state above; its "UX7 §4.5 note first" is this section. CODEXSIGNIN2's
refused panel, merged, becomes the list's *Refused at the start* state, keeping its words and its two presses.

### 3.3 Sessions' access and the call held for the person

**Place:** a repository's and a workspace's Setup, section *Sessions' access* (built); Settings → Driver for this machine
(built); the session's own head (new); the held call on the head and the foot (built, kept).

- **Setup and Driver:** the same rows (`AccessRows`) at the three levels, laid out by R1. Driver's card loses its lead
  paragraph to its title's ⓘ, as Setup's fold carries its summary line. A pause's change refused is said under that row's
  editor (R3: `Inheritable` gains a refusal line, as `HeavyGates` has).
- **A session's head says its access** (D184 point 1, kept on its record and shown nowhere): where it started under full
  access, its facts line wears the plain warning chip Ask Daoris's full access wears, *Full access*; its *Details* lists each
  pause as it started (*Permission prompts: asking · Landing: your Accept*). A session started under today's defaults says
  nothing more, so an untouched start reads as it did.
- **The held call** keeps its shape (`PermissionAsks`), the exemplar of R2: what is asked, how long it has waited, the bound,
  a press per choice, a status, a refusal in place. PERMUNTIL1 takes its bound from the driver; PERMANCHOR1 leads it to its
  call in the conversation.

```
 half (630 px), a session's pinned head, started under full access
┌────────────────────────────────────────────────────────────────┐
│ ● Working  Fix the FTS index's ranking                 Stop… ⋯ │
│ engine · account-1 · ⚠ Full access · 12 min                    │
└────────────────────────────────────────────────────────────────┘
```

| State | Shows |
|---|---|
| Today's defaults | Nothing on the head; *Details* says *as this machine's defaults* |
| Full access | The chip on the facts line; *Details* lists every pause open |
| Some pauses changed | No chip; *Details* lists each pause and where it was set |
| A record from before | *Details* says *not recorded* |
| Setup, a change refused | The sentence under the row; the editor open |

| Key | English | 中文 |
|---|---|---|
| `work.access.full` | Full access | 完全访问 |
| `work.access.details` | Access | 访问权限 |
| `work.access.asStarted` | {{pause}}: {{value}} | {{pause}}：{{value}} |
| `work.access.defaults` | As this machine's defaults | 按本机的默认 |
| `work.access.notRecorded` | Not recorded | 未记录 |

**Keys:** the chip is text, not a control; *Details* is the head's fold. **Themes:** the chip's warning line and icon, never
red (full access is a choice, not an outcome, §3). **Narrow:** the facts line wraps after the chip, never cutting it.

### 3.4 Ask Daoris's access

**Place:** the panel's composer bar (the chip), its *What it has* view, and Settings → AI features. The screen's name for
the place stands, *AI features* (AI 功能); D183 point 2's *Daoris's own AI* is the card's description there, not a place.

- **One confirmation.** Full access is asked through `InlineConfirm` (warning tone), carrying the servers it would add as its
  body and when it takes effect; it says it waits, and a refusal is said inside it (§4). `FullAccessConfirm` goes.
- **The chip keeps its chevron.** A value picked from a short menu is drawn as `SelectField` is, its chevron included; §4's
  *an arrow is a direction, never a menu* is about a region's menu, which is ⋯ (an amendment of wording, §8). A switch the
  driver refuses is said under the chip, in the composer bar (R3).
- **What it has lays out by the side bar's width** (R7): its label column beside its value while the view holds 24rem, stacked
  under it below. It gets stories: the newest receipt, earlier ones folded, *not recorded*, reading, failed, lacks with their
  doors, full access; at 300 px and at 45%; both languages and themes.
- **AI features** draws Access as one row whose value is the chip, and Configured access as rows below it (R1).

```
 the side bar at 300 px: Ask Daoris, full access asked once, then a switch refused
┌──────────────────────────────┐   ┌──────────────────────────────┐
│ Ask Daoris · work            │   │ …                            │
│ …                            │   │ [⚠ Full access ˅] [What it…] │
│ Allow Ask Daoris to run      │   │ ✕ <the driver's sentence,    │
│ commands, edit files and use │   │   whole>                     │
│ every installed plugin       │   │ [Type to Ask Daoris…       ] │
│ server without Apply?        │   └──────────────────────────────┘
│ It adds: browser, git-tools  │
│ From the next start.         │
│ [⚠ Enable full access]       │
│ Never mind   Waiting…        │
│ [Restricted ˅] [What it has] │
└──────────────────────────────┘
```

| State | Shows |
|---|---|
| Restricted | The chip, quiet; *What it has* lists what it lacks with doors |
| Full access asked | The confirm in the panel, the focus in it; Escape and *Never mind* give the focus back to the chip |
| Waiting | *Waiting…* beside the presses, the presses kept |
| Refused | The sentence inside the confirm, or under the chip for a switch back |
| Full | The warning chip; the next start reads it, *full access next start* until then |
| *What it has*: reading, failed, not recorded | A status line; the bridge's sentence with *Try again*; *not recorded* |

No word is new: `help.mode.*`, `help.wired.*` and `help.grants.*` stand, and the confirm's sentence is today's
`help.mode.confirm.*`.

**Keys:** the chip opens its menu on Enter or Space (Radix); picking full moves the focus into the confirm (today's
`handing`); the confirm's presses come first, then *Never mind* (§4). **Themes:** the warning line and icon on the chip and
the confirm, both themes' `--warn`. **Narrow:** the side bar's floor is 300 px at every window width, which is the narrow
form.

### 3.5 Heavy runs

**Place:** a repository's Setup (the list, built); the session that runs or waits (new); the repository's Details (new).
What runs the gates and holds the slot is the driver's (`daoris-driver run-heavy`, HEAVYRUN1a); the page reads it.

- **Setup:** the list as built, its editor laid out by R1's form rule (the narrow wireframe in §3.1). The moves keep ˄ and ˅,
  since each moves one way (§4).
- **The session running its heavy gates** says, in place of *running daoris-driver run-heavy for 6 min*, the gates as
  phases: on the rail's line *heavy gates · 2 of 3 · test:web · 6 min*, and on the head's facts line the same, opening to the
  phase list in *Details*. A gate that failed is said with its exit and its sentence as recorded; the session's turn goes on
  reading it, as the agent does.
- **The session waiting for the slot** says *waiting for the heavy run · behind {session} · 3 min*, neutral (it waits on
  another session, not on the person), its *{session}* a door to that session. It is never a row of *What needs you*.
- **The repository's Details** gains one row, *Heavy run*: who holds the slot and at which gate, for how long, and how many
  wait, each a door; *No heavy run now* otherwise; and its terminal twin.

```
 wide, Repositories → engine → Details, the slot held
┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Heavy run ⓘ                          Fix the FTS index's ranking · 2 of 3 · test:web · 6 min                 │
│ daoris-driver …                      1 waits: Add the ask's channel · 3 min                                  │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘

 narrow, Sessions' rail, the two rows
┌──────────────────────────────────────┐
│ ● Fix the FTS index's ranking        │
│   engine · heavy gates · 2 of 3 · 6m │
│ ● Add the ask's channel              │
│   engine · waiting for the heavy run │
│   · behind Fix the FTS… · 3m         │
└──────────────────────────────────────┘
```

| State | Shows |
|---|---|
| None set | Setup: *No heavy gates set here*, *Add heavy gates*; the repository's file's rule said |
| The repository's file wins | Setup says so, with no editor (built, HEAVYRUN1b2c) |
| Running, gate i of n | The session's line and head; Details' row |
| Waiting behind another | The waiter's line, neutral, the holder a door; the holder told (HEAVYRUN1b3) |
| A gate failed | Its exit and sentence on the head's phase list; the line says *heavy gates failed at {gate}* |
| A list refused | Under *Save* (built) |

| Key | English | 中文 |
|---|---|---|
| `work.heavy.running` | heavy gates · {{n}} of {{total}} · {{gate}} · {{time}} | 重型检查 · 第 {{n}} 项，共 {{total}} 项 · {{gate}} · {{time}} |
| `work.heavy.waiting` | waiting for the heavy run · behind {{session}} · {{time}} | 等待重型检查 · 排在 {{session}} 之后 · {{time}} |
| `work.heavy.failed` | heavy gates failed at {{gate}} | 重型检查在 {{gate}} 失败 |
| `projects.heavy.slot` | Heavy run | 重型检查运行 |
| `projects.heavy.none` | No heavy run now | 当前没有重型检查在运行 |
| `projects.heavy.waits` | {{count}} waits: {{sessions}} | {{count}} 个在等：{{sessions}} |

**Keys:** the holder and the waiters are doors (Enter opens the session); the rail's keys are unchanged. **Themes:** live
mark for the running gate; neutral for the wait. **Narrow:** the line wraps after its separators; the gate's name is cut
before the session's, with the whole in its tip.

### 3.6 Meetings and brainstorms

**Place: Sessions.** A meeting is the person and named agents of different makers in one conversation Daoris runs (D177),
and a brainstorm is one protocol on it (D177 point 6). It is not a session (one line of work in one repository, D167), but it
is a conversation whose turns run on this machine's accounts, with a console, waits on the person and an end: the rail is
where the person already looks for those. So the rail lists meetings as a kind of their own among its rows, by the same
state groups (D126): one waiting for the person's *Send* under *Waiting on you*, one whose round runs under *Working*, one
over under *Ended*. Its row is its question's first line, a meeting glyph, and its line *meeting · Claude Code, Codex ·
engine* (or *brainstorm · …*). The ⋯'s *by repository* puts a meeting under its repository, or under *No repository*.

**Starting one** follows New conversation (D162), not a drawer: Workspace › *New meeting…*, the rail's `＋` (*New
conversation* first, then *New meeting…*), the palette, and Ask Daoris's door. Sessions' main area shows the composer: the
question in the person's words, *Talk · Brainstorm* (a segmented choice), the agents (a chip per maker, two at first, the
account by the walk, D125, never chosen here), the repository (a chip; *no repository* asks the question alone), the
attachments by the composer's carry, and for a brainstorm its criteria in words (optional). Its press is *Assemble the
context*: it spends nothing, makes the packet and opens the meeting's page on it.

**The meeting's page** (the main area): UX7's head, then the phase list, then the packet, then the transcript or the
document.

- **Head:** its state's pill and its question's first line; its facts line *Talk* or *Brainstorm*, the agents, the
  repository, *round 1 of 2*; its loud act by state (*Send to 2 agents* while the packet waits; *Stop…* while a round or a
  pass runs; *Send round 2* when the first ended); ⋯ with *Copy ID* and *Clear from this machine…* (asks once, D153).
- **Phases** (R2): for Talk, *Context assembled → Shown to you → Round 1: Claude Code, Codex → Round 2 (by your press)*, each
  agent's turn a phase with its time; for a brainstorm, *Context assembled → Shown to you → Proposals: Claude Code, Codex →
  Synthesis → Challenge → Document*. A pass that stalled, failed or met a limit says so at its phase, by code, in words.
- **What the agents will read** (the packet, D176 point 2): the tier that found its sources (*meaning*, *words*, *files*),
  the question, the constraints, the attachments, then each source (its kind, its path and lines, the revision it was read
  at, why it was taken, *kept 4,000 of 12,000 characters*), each with *Leave out*; *Add a file…*; *Left out*, folded, with why.
  A change makes a new packet, said as such. Once sent it is read-only: *Sent 14:02 to Claude Code and Codex*. A refreshed
  packet for round 2 says what it adds beyond the first (D177 point 2).
- **The transcript** (Talk): a section per round; each message its maker's mark and agent's name (the account on this machine
  only), when, and its words as written. A turn that did not answer is said in its round's line.
- **The document** (Brainstorm, D176 points 3–6): its coverage first (*2 makers answered: Claude Code, Codex*, or *one maker
  answered: this is no cross-maker brainstorm*); then the ranked groups, each its rank, title, merged idea and why the
  synthesis ranked it (*the synthesis's claim*), its flags as chips (*hard to reverse*, *risky*), how many makers named it,
  its members folded with each proposal's fields, and the challenge's findings beside it as claims (*stands*, *weakened*,
  *unsupported*, *unknown*); a required challenge that did not finish holds its group out of the ranking, *not challenged*,
  with *Challenge again* (a new spend, said as one) and *Rank it unchallenged*. Then what was left out with why, and what the
  passes needed that the packet lacked, with *Add to a new packet*. Its foot: *Copy as Markdown* and *Attach to a new ask…*.
- **The side bar** for an attended meeting: the console a tab per turn running (CONSOLE2); the timeline its rounds; the review
  says *a meeting changes no files* (no box where nothing listens).

```
 wide, Sessions, a brainstorm waiting for the person's Send
┌──┬──────────────────────────────┬──────────────────────────────────────────────────────────────────────────────┐
│S │ Sessions               ＋ ⋯ ⟨│ ◔ Waiting on you  Should the index move to SQLite FTS5?                      │
│  │ ▾ Waiting on you          2  │ Brainstorm · Claude Code, Codex · engine        [Send to 2 agents]  Stop… ⋯  │
│  │  ◔ Should the index move…    │ ✓ Context assembled · words · 14 sources                                     │
│  │    brainstorm · engine       │ ◔ Shown to you · 4 min so far                                                │
│  │  ◔ Fix the landing rule…     │ ○ Proposals: Claude Code, Codex   ○ Synthesis   ○ Challenge   ○ Document     │
│  │ ▾ Working                 1  │ ──────────────────────────────────────────────────────────────────────────── │
│  │  ● Fix the FTS index's …     │ What the agents will read                                                    │
│  │ ▾ Ended                  12  │ Question  Should the index move to SQLite FTS5? …                            │
│  │                              │ Sources   decision  docs/decisions/D24.md:1-40 · at 02632075f · "tier"       │
│  │                              │           kept 4,000 of 9,812 characters                       [Leave out]   │
│  │                              │           knowledge .claude/knowledge/model-decoupling.md · …  [Leave out]   │
│  │                              │           [Add a file…]   ▸ Left out (2)                                     │
└──┴──────────────────────────────┴──────────────────────────────────────────────────────────────────────────────┘
```

```
 half (630 px), the brainstorm's document
┌────────────────────────────────────────────────────────────────┐
│ ✓ Done  Should the index move to SQLite FTS5?               ⋯  │
│ Brainstorm · Claude Code, Codex · engine · 18 min              │
│ 2 makers answered: Claude Code, Codex                          │
│ 1  Keep the lexical tier and add FTS5 beside it                │
│    Ranked first by the synthesis: answers the question as      │
│    asked, reversible, low risk. (the synthesis's claim)        │
│    Named by 2 makers   ▸ Its 2 proposals                       │
│ 2  Replace the index with FTS5        [hard to reverse] [risky]│
│    Challenge: weakened — the migration drops … (its claim)     │
│ —  Rewrite the history of the index's table  [hard to reverse] │
│    Not challenged: the pass did not finish                     │
│    [Challenge again]  [Rank it unchallenged]                   │
│ ▸ Left out (1)   ▸ What the passes needed (2)                  │
│ [Copy as Markdown]  [Attach to a new ask…]                     │
└────────────────────────────────────────────────────────────────┘
```

```
 narrow, Talk, round 1 running
┌──────────────────────────────────────────────┐
│ ● Working  Which of the two retry shapes …   │
│ Talk · Claude Code, Codex · round 1 of 2     │
│ [Stop…] ⋯                                    │
│ ✓ Context assembled   ✓ Sent 14:02           │
│ ● Round 1: Claude Code  ✓ 3 min              │
│            Codex        ● 1 min 20 s so far  │
│ ○ Round 2 (by your press)                    │
│ Round 1                                      │
│ CC Claude Code · 14:05                       │
│    <its words, as written>                   │
└──────────────────────────────────────────────┘
```

| State | Shows | Where else |
|---|---|---|
| Composing | The composer; *Assemble the context* waits for words | — |
| Assembling | The page with its phase *Assembling the context…*, skeleton sources | — |
| Waiting for your send | The packet, *Send to N agents* loud, open's hue | *What needs you*: a row whose door is the page (reading first, never *Send* from the row) |
| Waiting for the driver | *Nothing drives here now: it starts when Daoris drives this home* (the verb's own wait, D176 point 1) | — |
| Running | The phase now, its time; *Stop…* | The rail under *Working* |
| Round ended | The next round's packet frozen and shown, with what it adds; *Send round 2* | *What needs you*, as *Waiting for your send* |
| An agent's ask for a brainstorm | *An agent in {repository} asks for this brainstorm*, the packet, *Send* or *Refuse* (BRAINSTORM1d's `ask`) | *What needs you* |
| Stopped | What answered kept; *Stopped by you* | — |
| Interrupted | *Interrupted when Daoris stopped; nothing runs again by itself*, *Run the missing passes* (a new spend, said) | — |
| Refused | *Not in this machine's policy* or *No agent could answer*, with the codes in words | — |
| Done, partial | Coverage first, then the document | — |
| Done, unranked | *Unranked: the synthesis did not finish*, with why, the proposals by maker | — |
| Cleared | The item's *gone* state (the frame model §3b) | — |

| Key | English | 中文 |
|---|---|---|
| `meeting.kind` | meeting | 会议 |
| `brainstorm.kind` | brainstorm | 头脑风暴 |
| `meeting.new` | New meeting… | 新会议… |
| `meeting.protocol.talk` / `.brainstorm` | Talk / Brainstorm | 交谈 / 头脑风暴 |
| `meeting.assemble` | Assemble the context | 整理上下文 |
| `meeting.packet.title` | What the agents will read | 智能体将读到的内容 |
| `meeting.packet.leaveOut` | Leave out | 不纳入 |
| `meeting.packet.left` | Left out | 已排除 |
| `meeting.packet.kept` | kept {{kept}} of {{total}} characters | 保留 {{total}} 个字符中的 {{kept}} 个 |
| `meeting.send` | Send to {{count}} agents | 发给 {{count}} 个智能体 |
| `meeting.round` | Round {{n}} of {{total}} | 第 {{n}} 轮，共 {{total}} 轮 |
| `meeting.sendRound` | Send round {{n}} | 发送第 {{n}} 轮 |
| `meeting.waitDriver` | Nothing drives here now: it starts when Daoris drives this home | 现在没有驱动在这里运行：Daoris 驱动这个主目录时它才开始 |
| `meeting.interrupted` | Interrupted when Daoris stopped; nothing runs again by itself | Daoris 停止时中断；不会自行重新运行 |
| `brainstorm.coverage` | {{count}} makers answered: {{makers}} | {{count}} 家厂商作答：{{makers}} |
| `brainstorm.oneMaker` | One maker answered: this is no cross-maker brainstorm | 只有一家厂商作答：这不是跨厂商的头脑风暴 |
| `brainstorm.flag.hard` / `.risky` | hard to reverse / risky | 难以撤销 / 有风险 |
| `brainstorm.claim` | the synthesis's claim | 综合者的说法 |
| `brainstorm.notChallenged` | Not challenged | 未经质疑 |
| `brainstorm.challengeAgain` | Challenge again | 再次质疑 |
| `brainstorm.rankAnyway` | Rank it unchallenged | 不经质疑排名 |
| `brainstorm.unranked` | Unranked: the synthesis did not finish | 未排名：综合没有完成 |
| `brainstorm.named` | Named by {{count}} makers | {{count}} 家厂商提出 |
| `brainstorm.copy` / `.attach` | Copy as Markdown / Attach to a new ask… | 复制为 Markdown / 附到新需求… |

*Meeting* (会议) and *brainstorm* (头脑风暴) join the glossary with these words; *maker* (厂商) is proposed beside them, since
the document counts makers and the glossary names none.

**Keys:** the rail's ↑ ↓ and Enter open a meeting as they open a session. On the page, Tab runs the head's loud act, the
phases (read, not pressed), each source's *Leave out*, *Add a file…*, then the rounds or the groups; each round and group is a
section with a heading, reached by a reader's heading keys; F6 moves between regions. *Send* has no key: it spends (UX7
§7's rule for an act a slip must not reach). **Themes:** waiting for the person in open's hue; a running pass's live mark;
flags in the warning line, never red (a flag is a claim from the fields, not an outcome); a maker's mark by its two letters
(*CC*, *Cx*, UX7 §4.6), never a hue alone. **Narrow:** the head's acts take their own line below 560 px (as a session's);
a source's path over its why; a group's chips wrap under its title.

**What changes in the existing rows:** BRAINSTORM1d keeps the caller (the connector's `brainstorm_ask` and `brainstorm_read`,
another repository's asker, the CLI); its page half becomes MEETUX1a-d (§9). AGENTROOM1's page is the same rows, since a
brainstorm is one protocol on a meeting.

### 3.7 Where a piece of work stands

**Place:** where the work is read, never a new place: the attended session's record (built), *What needs you* (a held attempt
and a blocked clean-up, built as rows), the line under a tidied branch (built), a quest's page (FABRIC-END5), Ask Daoris's
answer. The unit's whole life, its nodes and wires, is FLOWCHART1's chart (FABRIC-GRAPHVIEW1's running view, the fabric
design §E8); this section is its present tense.

- **The milestone is a phase list** (R4): *End reached → Result kept → Clean-up → Recovery kept*. Each phase its sentence as
  today (the driver's clauses as recorded); the end's proof and time on the first; the clean-up *pending* is the phase now,
  with what waits and why, a blocked entry in the danger ink; *completed* is done; the recovery's *kept until* is a bound
  (R2), the earliest date the person must act by. A held plugin step's attempt sits under the phase it holds.
- **The record head keeps its budget.** UX7's 200 px is measured again with the parts strip, *Accept*, the relations, the
  channel and the milestone; what passes it folds to one line (the milestone to its `MilestoneLine`, *ended · clean-up
  pending · recovery until 3 Nov*), opening in place.

```
 half, the attended session's record, the milestone open
┌────────────────────────────────────────────────────────────────┐
│ Where this work stands                                       ˄ │
│ ✓ End reached     merged into main at 3f9a12c · 14:02          │
│ ✓ Result kept     the branch's commits on main                 │
│ ● Clean-up        pending: the tree waits for its session's    │
│                   last turn                                    │
│ ○ Recovery kept   the moved-aside folder until 3 Nov           │
└────────────────────────────────────────────────────────────────┘
```

| State | Shows |
|---|---|
| Not reached | The first phase now: *not reached*, with the driver's why |
| Reached, clean-up pending | Two done, the third now with what waits |
| Blocked | The clean-up's blocked entry in the danger ink; *What needs you* holds its row |
| Completed | Four done; folded to its line by default |
| Dropped or handed | The end's own word (*dropped*, *handed*), as today |
| No unit (a chat, a record from before) | Nothing drawn |

No word is new: `work.milestone.*` stand. **Keys:** the fold is a disclosure button; the phases are a list. **Themes and
narrow:** the phase marks of §4; the label column stacks under 24rem of its own width.

### 3.8 Pushed state

No surface: D182 moves where the page's data comes from, not what it shows. Three rules keep that true.

- **A slice not yet judged is loading, never empty**: skeleton rows, never an empty state's sentence. *Nothing needs you*
  drawn before the attention slice is judged is a false statement.
- **A resync holds the last render** at the language's reduced opacity until the snapshot lands (§4 *Loading*); nothing jumps.
- **A partition gone is its item's *gone* state** (the frame model §3b), never a blank main area.

### 3.9 How each is reached

"As today" means the door exists and this design changes nothing in it; a new door's row in `HelpCoverageTests` is named
by the row that adds its press.

| Surface | Place, list | *What needs you* | Menu and palette | Ask Daoris | Terminal twin |
|---|---|---|---|---|---|
| Settings' rows and forms | Settings, its domains; Setup tabs | — | as today | as today | each row's hint |
| Signing in | Agents, an agent's page, its account's row | the sign-in row, as today | — (a row's act) | as today | the agent page's twins, as today |
| Sessions' access | Repositories → Setup; Settings → Driver; a session's head | — | as today | `SET_ACCESS` exempt, the person's own (D184) | `daoris-driver access` |
| Ask Daoris's access | The panel; Settings → AI features | — | — | the panel itself | `daoris-driver help mode` |
| Heavy runs | Setup (list); a session (running, waiting); a repository's Details (slot) | — | — | a door to the repository's Details (HEAVYSLOT1's row) | `daoris-driver heavy`, `run-heavy` |
| Meetings and brainstorms | Sessions, the rail; its `＋` | waiting for your send; an agent's ask | Workspace › New meeting…; the palette | a door to the composer with its question (MEETUX1d's row) | `daoris-driver meeting`, `brainstorm` |
| Where work stands | A session's record; a quest's page; Branches | held attempts, blocked clean-ups, as today | — | its answer (FABRIC-MILESTONE1c) | `daoris-driver work state` |

## 4. The component catalogue

The method holds (D55): a story first, a molecule imports no hook from `./queries` or `./shell` (a clock like `useNow` is its
own), and a component's layer is what it imports. Stories follow R6.

### 4.1 What the newer screens share today

| Part | Layer | Used by the newer screens |
|---|---|---|
| `Card`, `SectionTitle`, `SettingRow`, `WhyGlyph` | atoms (`ui.tsx`) | every Settings domain, This Daoris, Setup |
| `Pill`, `Dot`, `Chip`, `PathText`, `CodeText`, `Inline` | atoms | everywhere |
| `CheckField`, `Segmented`, `SelectField`, `Menu`, `Tip` | atoms | access, heavy gates, the mode chip |
| `EmptyState`, `SkeletonRows`, `MonoWell` | atoms | first loads; the sign-in's output |
| `InlineConfirm`, `Refused` | molecules (`work/`) | full access on Setup, every ask-once |
| `PageHead`, `ViewMain`, `ListPane`, `DetailsFold` | molecules (`work/`) | session, quest, repository pages |
| `SetupSection`, `Inheritable` | molecules (`projects/SetupParts.tsx`) | Setup and access rows |
| `WaitStrip`, `PermissionAsks`, `HelperAsks`, `ReviewGate`, `OpinionGate` | molecules (`work/`) | a session's waits |
| `WiringCard`, `Fact`, `Unread`, `Fold`, `Door` | molecules (`settings/WiringParts.tsx`) | This Daoris |

### 4.2 What the design adds

| Part | Layer and home | What it is | Its stories |
|---|---|---|---|
| `PhaseMark` | atom, `ui.tsx` | A phase's mark by state: done, now (the live mark), waits on you, waits on another, failed, to come; named in words | Each state, light and dark |
| `Elapsed` | atom, `ui.tsx` | A span of time stepping by its own clock (seconds, then minutes), stopping when told; never a live region by itself | Under a minute, minutes, hours; stopped |
| `PhaseList` | molecule, `design/` | R2's list: phases with marks, time, a bound, *longer than usual*, a detail slot under the phase now, a refusal at a failed phase, the whole's time in its head | Starting, waiting on the person, waiting on another, longer than usual, bound, failed at the first and at a middle phase, done; en and zh; light and dark; 300 px and narrow |
| `SettingList` | molecule, `ui.tsx` | The wide setting row: a card's rows on one label track (12–22rem) with the rule between rows; below 32rem of its own width, stacked; `SettingRow` keeps its props | Short values, a path, a notice, a long label, a row with acts; at wide, half and narrow; en and zh |
| `FormGrid` | molecule, `ui.tsx` | A form laid by its own width: fields by kind (`number`, `name`, `pattern`, `long`), a form of rows stacking each row below its floor | One field, three fields, a list of rows; wide, half, narrow |
| `SignInSteps` | molecule, `agents/` | The sign-in's phases and their details (link, code, box), from props; `SignIn` keeps the hooks | Every state in §3.2's table, en and zh, both themes, narrow |
| `AccessFact` | molecule, `work/` | A session's access on its head (the chip) and in *Details* (each pause) | Defaults, full, some pauses, not recorded |
| `HeavySlotLine` | molecule, `work/` | The running or waiting line, and the Details row | Running, waiting, failed gate, none; narrow |
| `MeetingComposer` | molecule, `work/` | §3.6's composer: question, protocol, agents, repository, files, criteria | Empty, typed, a brainstorm with criteria, refused, a browser (absent), narrow, zh |
| `PacketView` | molecule, `work/` | The packet: tier, question, constraints, attachments, sources with *Leave out*, left out, sent | Unsent, a source left out, sent, refreshed for round 2, files-only tier, zh |
| `TranscriptRound` | molecule, `work/` | A round's messages with makers' marks, a turn that did not answer | Two answers, one silent, a long message, zh |
| `ProposalGroup` | molecule, `work/` | A ranked group: rank, title, merged idea, why (a claim), flags, makers' count, members folded, challenge beside | Ranked, flagged and weakened, not challenged with its presses, unranked by maker; zh; dark |
| `MeetingPage` | organism, `work/` | The page: head, phases, packet, transcript or document | Its states through the molecules' stories, and a story per page state at wide and narrow |

`FullAccessConfirm` goes (into `InlineConfirm`); `WorkMilestone` is redrawn on `PhaseList`, `MilestoneLine` kept.

## 5. The traps, each a test or a story

| Trap | Test or story |
|---|---|
| A row's value drifts to the far edge again | `SettingList`'s test: in an 80rem container every value starts at the track's edge |
| A story hides the page's spacing | A story test: no decorator under `settings/` adds a gap, a cap or a padding; the domain's container draws its cards |
| A card margin returns | `tokens.test.ts` fails on an outer margin on `Card` |
| The window chooses a region's columns | `tokens.test.ts` fails on a viewport variant outside the strip |
| A wait looks hung | `PhaseList`'s stories at 5 s and 5 min differ in words; each holder's test reads its phase from the driver's words |
| A bound is invented | The sign-in's test: no *expires* line without the tool's bound; no *longer than usual* without a measured usual |
| A clock keeps the window drawing | `Elapsed` stops when its phase ends (a fake clock reads no render after) |
| A refusal leaves its place | Each holder's test (sign-in, a pause on Setup and Driver, the mode chip) refuses and reads the sentence in place, with no toast |
| A sign-in's link or code read by one version's words | CODEXSIGNIN3's fixtures per recorded version |
| Full access hides on a session | `AccessFact`'s test: a record with `access: full` wears the chip; an untouched one says nothing |
| A meeting's packet sent from a row | *What needs you*'s test: the row is a door, it carries no *Send* |
| A brainstorm's rank read as fact | `ProposalGroup`'s story labels the rank the synthesis's claim and the challenge's findings claims |
| A flag drawn as an outcome | `tokens.test.ts`'s ink rule: a flag never wears the danger ink |
| An empty state before a slice is judged | A pushed-slice test: an unjudged attention slice draws skeleton rows, not *Nothing needs you* |
| The record head pushes the conversation down | A story at narrow with every block present measures the conversation's top under UX7's budget |
| One word for two things | `names:check -- --strict` with *meeting*, *brainstorm* and *maker*; the glossary's *wire* (a workspace to a remote) kept apart from the fabric's wires (FLOWCHART1's names) |

## 6. What is not built here

- **The flowcharts** (FLOWCHART1, D186): a quest's life, a session's states, the fabric's units and nodes, landing and
  clean-up, a workflow's run. This design refers to them and draws none.
- **The graph editor** (WORKFLOW1g, the fabric design §E8) and **the running view** (FABRIC-GRAPHVIEW1).
- **Verification lanes' screen** (FABRIC-VERIFY1's slices): R2 covers its state when built (*accepted; verification
  running*).
- **A meeting whose participants write**, a meeting on a shared host, modes per workspace (D177, D183): their decisions
  reject them.
- **Settings' Plugins section** (PLUGSET1) and the grants' build (HELPMODE1c2-c3).
- **The usual times** a phase compares itself with: CLAUDESIGNIN1 and SIGNINLOG1 measure them.
- **The window.** Each row's proof includes it; this document does not.

## 7. Considered and rejected

- **A tenth place for meetings.** UX6 §8 makes a place a decision with its own counts; a meeting is a conversation whose
  turns run here, which is what Sessions holds. Revisited if meetings outnumber sessions.
- **Meetings in the side bar beside Ask Daoris.** The side bar holds what is the same on every view (the frame model §3c);
  a packet to read and a ranked document need the main area.
- **Meetings in Quests.** A brainstorm's result is a document, never work (D176's rejection of an ask or a quest).
- **A drawer to start a meeting.** New conversation replaced the start drawer (D162); a meeting starts the same way.
- **Settings' values kept at the far edge, with leader dots** across the gulf: an ornament for a gap the layout made.
- **A capped, centred settings column.** The pane is the owner's to use (D141); a cap would answer the wide pane by not
  using it.
- **Two columns of cards at the wide pane.** Settings read top to bottom; two columns break the scan and the keyboard order.
- **The sign-in's phases read from the tool's printed words.** They change with each version (CODEXSIGNIN3's finding); the
  driver writes them.
- **A countdown on every wait.** Only a known bound is said; an invented one is a promise nobody keeps.
- **A heavy-run wait in *What needs you*.** It waits on another session, not on the person.
- **Removing the chips' chevrons** to keep §4's arrow rule to the letter: the platform's own select carries one, and a
  value picker without it reads as a label.

## 8. What this amends

- **The platform language §4** (D41): *Forms and settings* takes R1 and R3; after *a step happens where it was started*, R2;
  *an arrow is a direction, never a menu* is worded for a region's menu, a value picker keeping its chevron; *Doors* takes R5.
- **The platform language §5**: Sessions lists meetings (§3.6); Repositories' Details the heavy run (§3.5); a session's head
  its access (§3.3); Settings' rows R1.
- **The frame model §3b** (D118): R7 as a check.
- **The components method** (D55) and the working surface §4: R6.
- **UX7 §4.5** (D152): the sign-in step's phases (§3.2).
- **D183 point 2**: the place is *Settings → AI features*.
- **D176 and D177**: their page is §3.6, in Sessions; BRAINSTORM1d keeps its caller.

D41's character, tokens and accessibility, D118's regions, D150's rule that a setting lives on its thing, D152's menus and
heads, and D173's milestone's facts stand.

## 9. The rows, in the order to build them

**The rows follow D187**: each is one feature a person can see working, named by its outcome and built end to end by one
builder across every lane it needs (driver, modules, service, the page), never a host half and a page half. A feature too
large for one builder's context is split only into parts that each work on their own and are each seen working (the
meeting's four, D187 point 5). A part shared by several features (`PhaseList`, `SettingList`) is built by the first feature
that shows it. Each proof includes its stories at R6's widths, its tests red first (its own `Process` classes among them,
D187 point 4), the web's `i18n:check` and `names:check -- --strict` where words change, and the parent's look on the window
at 1546 and 680 px in both themes and languages. UIFIT1a (the parent's shots) comes first, unchanged.

1. **UIFIT1b, changed — Settings' rows and cards use the pane.** Absorbs UIFIT1c. R1's label track (`SettingList`) and its
   16 px card spacing in every Settings domain and Setup tab, R6's story widths for them, R1's and R6's notes in the platform
   language with their checks.
2. **UIFIT1d — Forms take the pane.** R1's form rule (`FormGrid`): the 40–48rem caps and the `md:` and `max-sm:` columns go,
   the heavy gates editor and *What it has* lay out by their own width, and R7's check holds it.
3. **CLAUDESIGNIN1b, changed — The sign-in panel says its phase and how long.** The driver writes the phases, the panel draws
   them (§3.2), one builder end to end; it builds `PhaseMark`, `Elapsed`, `PhaseList` and `SignInSteps`, and writes R2.
4. **ACCESSASK1 — Access changes ask once and say a refusal where they were pressed.** Full access through the one
   `InlineConfirm`, a pause's refusal under its row, a mode switch's under its chip, Driver's lead paragraph to its ⓘ; R3.
5. **SESSACCESS1 — A session says the access it runs under.** The head's chip and *Details*' pauses from the record (§3.3).
6. **MILESTONEUX1 — Where a piece of work stands reads as its phases.** The milestone on `PhaseList`, the record head's
   budget measured again and kept (§3.7); R4.
7. **HEAVYSLOT1 — A heavy run is seen running and waiting.** The driver answers the slot, the session's line and head and the
   repository's Details say it (§3.5).
8. **MEETUX1a — A meeting is started from the window, its context read and sent.** The rail's kind, the composer, the page's
   head, phases and packet, *Send*, *Stop*, the bridge to the driver's meetings (§3.6); R5. After BRAINSTORM1c and
   AGENTROOM1b-c.
9. **MEETUX1b — A meeting's rounds are followed on its page.** The transcript, round 2's packet and *Send round 2*, the side
   bar's views.
10. **MEETUX1c — A brainstorm's document is read on its page.** Coverage, ranked groups, flags, the challenge's claims and its
    presses, left out and needs, *Copy as Markdown*, *Attach to a new ask…*.
11. **MEETUX1d — A meeting that waits on the person is in What needs you, and Ask Daoris opens one.** Its rows, an agent's ask,
    the go door and its `HelpCoverageTests` row.
12. **PUSHUX1 — Nothing reads as empty before it is known.** An unjudged pushed slice draws skeleton rows (§3.8).
13. **STORYGAP1 — Every screen since UX7 is in the catalogue at the frame's widths.** Stories for those with none that no row
    above draws (§1.2 item 6).

The backlog holds each in its own shape (contract by this document's section, proof); the steward files them with
UIDESIGN1's archive entry once the owner accepts the design.

## 10. What this document's gate does not cover

- **The window was not looked at.** Every wide-pane claim is read from the code and the frame's numbers; UIFIT1a's shots may
  show one cause outweighing the others, and R1 answers all three.
- **No count was taken.** UX6's measures (concepts, controls, words, screens) were not run on any story; MEETUX1a's and
  UIFIT1b's proofs take them before and after.
- **The driver's half is assumed where a phase is read**: the sign-in's phases (CLAUDESIGNIN1b's driver half), the heavy
  slot (HEAVYSLOT1), a meeting's state (D176's and D177's records) each must answer what §3 draws; this document names
  the fields by meaning, not by wire shape.
- **The words are proposals** for D116's check. `verify` checks this document's place in the router and D185's shape; it
  checks none of the words, and no claim about what the code does beyond the lines cited.
