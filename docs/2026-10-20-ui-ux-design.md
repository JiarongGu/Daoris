# The screens since UX7, designed: what was found, the rules it shows are missing, each newer surface, and the parts they share (UIDESIGN1)

**Standing: proposed (D185), 2026-10-20.** Written from main at `02632075f` before any build, then revised the same day
after another maker's read, each finding checked against main at `872520fa5` (DESIGNREAD1, D185's note says what changed and
what did not). The owner reviews it before a row it files starts. It builds nothing; §9's rows do.

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
  about **1,290 px with the side bar closed** and about **630 px with it open**; the main area's own floor is **400 px**
  (`CENTRE_FLOOR`, `work/layout.ts:47`), which the side bar's opening reaches at any window. The wireframes below are drawn
  at these three widths, named **wide**, **half** and **narrow** (the floor). A 680 px window, with the list and the side bar
  at their strips, leaves about 540 px, between half and narrow: what holds at the floor holds there.
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
| Where this work stands (`WorkMilestone.tsx`) | The attended session's record (`AttendedSession.tsx:289`); its line under a tidied branch (`WorkspaceTidied.tsx:121`) | The end and its proof, the result, the clean-up, the recovery (the fabric's §D0) | Nothing judged again; the driver's clauses as recorded; each fact by its own value | Its rows drawn whole under the record head, with no disclosure (`WorkMilestone.tsx:66-116`, mounted at `AttendedSession.tsx:289`) | No story; the work state is read in four places as lines (`App.tsx:304`, `AttentionBand.tsx:125`, `WorkspaceView.tsx:262`, `WorkFrame.tsx:1243`) with no designed screen |
| The attended session's record head (`AttendedSession.tsx:271-291`) | Sessions' main area | Since UX7: the parts strip, *Accept*, relations, the channel, the milestone, before *How it came to be* | The chain and the trace folded to a line, the channel folded | UX7 §6.2's budget, the conversation at most 200 px down, measured before five blocks joined; the milestone is not folded, its four facts and any held attempt drawn whole | Nothing has measured it since |
| The workflow's run (`WorkflowRunView.tsx`, `WorkflowLine.tsx`) | A session view in the side bar (`WorkFrame.tsx:1147`); a quest's and an ask's page (`QuestPage.tsx:468`, `AskPage.tsx:287`) | Where a piece of work stands in its workflow | Derived, never inferred; hues by whose wait | — | The only drawn process, a vertical rail (FLOWCHART1) |

**Repositories and quests**

| Screen | Mounted | Shows | Keeps | Breaks | Worst problem |
|---|---|---|---|---|---|
| Branches, a branch's page, the commit graph (`BranchList.tsx`, `BranchPage.tsx`, `CommitGraph.tsx`) | A repository's Branches tab (UX6h) | Branches by kind, a branch's commits | The built-in Git design §2 | — | `CommitGraph` has no story of its own |
| Clean-up, recoveries, kept history (`WorkspaceBranches.tsx`, `WorkspaceTidied.tsx`, `WorkspaceRecoveries.tsx`, `KeptHistory.tsx`) | A workspace's Branches tab; Details | What the look cleaned, what is kept aside, what may be cleared | Asks once; lists what goes and what stays | — | `WorkspaceRecoveries` has no story |
| Set up for agents (`SetUpForAgents.tsx`) | A repository not adopted (`ProjectPage.tsx:237`, LAYOUT8) | Its plan, then *Set it up* | Stories in `ProjectPage` | — | Its run is a quest's session; nothing in place says its phases |
| The Workflow tab (`WorkflowTab.tsx`, `WorkflowChart.tsx`) | A repository's and a workspace's page (`ProjectPage.tsx:272`, `WorkspaceView.tsx:350`) | The current workflow as a rail of steps | Read-only, a list for the keyboard, nothing fixed-width | — | A rail where a flow is meant (FLOWCHART1) |
| A teammate's ask, an ask's review choice, conflicts (`TeammateAskPage.tsx`, `AskReview.tsx`, `Conflicts.tsx`) | Quests' main area (`AsksPart.tsx:358`, `AskPage.tsx:442`) | A teammate's ask read only; the review environment an ask chooses; conflicts | Read-only where it is not the person's | The review choice's refusal is a toast (`AsksPart.tsx:338-341`) | None has a story of its own |
| An ask's kind of work (`asks/AskWorkflowKind.tsx`) | An ask's page, its *kind* section (`AskPage.tsx:446-452`) | The kind the person chose and each proposal its intake kept, with *Apply* (WORKFLOW1i-c) | A proposal never applies itself (D74); *Apply* only while the ask is live | *Apply*'s refusal is a toast (`AsksPart.tsx:342-345`), never said at the proposal (§4) | Drawn only inside `AskPage`'s stories (`KindProposed`, en and zh), at one width |
| An ask's lines of work (`asks/AskLines.tsx`) | An ask's page, its *sessions* section (`AskPage.tsx:517-522`) | One line per repository's line of work: its parts counted, its leading part's word, a door into Sessions (SESSONE1c) | A door only where Sessions is; a word it lacks said as recorded | — | No story: no `AskPage` story hands it lines |
| A quest's closure states (`quests/QuestStatus.tsx`) | Wherever a quest's status is drawn off its page: the chain, the map's detail, an ask's work, a session's relations, Overview; `QuestList.tsx:158` reads the same | *submitted* for a closure graph's quest whose work is ready, and *source reopened* beside it (FABRIC-CLOSE1d2) | The pill keeps its status's tone, since a submission is not a status (§E5) | — | No story of its own: *submitted* changes what a list reads as done, and no story shows it or *source reopened* |
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
   CODEXSIGNIN2 said it in the panel); a pause's change refused on Setup or Driver toasts (`DriverDomain.tsx:257`); an ask's
   *Apply* and its review choice toast (`AsksPart.tsx:338-345`); full access's own confirmation has no place to say one. The language says it in the form, whole, for a start and for a confirm
   (§4); the newer screens did not carry it to every press, and each fix so far was one screen's.
4. **A process said as words.** The work state is read in four places and drawn as lines in each; the milestone's four facts
   are drawn whole under the record head, with no disclosure; the workflow rail is the only drawn process (FLOWCHART1).
5. **A surface with no door.** Brainstorms and meetings have no place, the heavy slot no line, a session's access no fact on
   its head.
6. **Stories at one width, and organisms with none.** The sign-in panel, *What it has*, the milestone, sessions' access,
   recoveries, a teammate's ask, an ask's review choice, an ask's lines of work, a quest's closure states and the commit graph
   have no story of their own; an ask's kind of work is drawn only inside its page's, at one width. Where a story sets a
   width, it is a local decorator (`mainAt`, `WorkspacePage.stories.tsx:310`, `WorkflowTab.stories.tsx:15`), and only a
   handful draw the main area's 400 px floor (an agent's page, a workspace's branches and workflow).
7. **The window's width inside a region.** `md:` in three forms and `max-sm:` in the side bar's *What it has*, where the
   frame model §3b says a region lays out by its own width.
8. **The record head grew.** UX7 measured the conversation at most 200 px down; five blocks joined above it since, and the
   milestone among them is never folded.

## 2. The rules the audit shows are missing

Each is an amendment, to the platform language unless named otherwise, with the reason that keeps it and the check that
holds it. Once the owner accepts this, each is written into the language, with its check, by the first feature of §9 that
applies it: R1, R6 and R7 by *Settings' rows and cards use the pane* and *Forms take the pane*, R2 by the sign-in's, R3 by
the access feature's, R4 by the milestone's, R5 by the first meeting feature.

### R1. A setting's value sits beside its words, a form takes the pane, and cards are spaced by one rule

*Amends §4, Forms and settings: "a setting is a row", "a form is sized to what it holds".*

- **A card's rows share one label track.** The track is the widest of the card's labels, between a floor and a cap; the
  value column is the rest. The bounds are set by UIFIT1 from its shots of the English and Chinese labels at the window's
  widths: 12rem and 22rem are this design's starting proposal, not its decision, since no window was looked at. Every row's value starts at the track's edge, so a short value (a switch, a number, a pill,
  a size) sits beside its words and the values of a card form one column the eye runs down. A long value (a path, a list, a
  sentence) takes the value column's width. The row's own acts (*Open*, *Edit*, *Clear*) follow its value, never the pane's
  far edge, so a control's own `justify-end` wrapper goes with the row's (`SetupParts.tsx:122` nests one). The hint (the
  terminal twin) and the ⓘ stay under and beside the label; a row's notice sits in the value column, under the value it is
  about, where today it spans both (`ui.tsx:1477`). `SettingRow` stays the one row: the card's list hands it the track.
- **Below 32rem of the card's own width** the track goes: the value goes under its words at the card's full width, as WSR4's
  26rem row did. The floor moves from the row to the card because the track is the card's.
- **What the right of a wide card holds is what its values need.** Nothing is stretched to fill it: a card of short values
  leaves its right empty, as a long English line at a wide pane is the language's stated cost (§4, D141).
- **A form is sized to its content up to the pane.** Each field by what it holds: a number 4.5rem, a name 16rem, a branch
  pattern 20rem, an address, a command or a folder the rest of the form's width. The form lays out by its own container,
  never the window's, and nothing caps it below the pane: the 40rem and 48rem caps go. A form of rows (heavy gates, rules,
  favourites) keeps its columns while its container holds them and stacks each row below its floor, by a container query of
  its own: removing a cap is not the rule, and a fixed column (the heavy gates' `8rem` ones, `HeavyGates.tsx:158`, which
  never stack) yields to its container.
- **Cards are 16 px apart, set once by the domain's container.** A card sets no outer margin of its own. 16 px is the scale's
  step; the `mt-3.5` cards wear today is 14 px, which is not on it.
- **Why:** at the wide pane a value at the far edge is read by crossing a gulf, and a capped form leaves half the pane to a
  form that wants it; neither is visible in a story drawn at 46rem. **Check:** the geometry in a rendered browser, since the
  unit runner's jsdom lays nothing out (`vite.config.ts:29`, with `css: false`; `ResizeObserver` stubbed, `test/setup.tsx:34`)
  and a class proves no alignment: a Playwright check over the page at the wide and the narrow width reads each row's value
  box starting at the track's edge, and under its words at the narrow one. `SettingList`'s unit test holds its structure
  only; `tokens.test.ts` fails on an outer margin on a `Card` and on a `max-w-[…rem]` on a form; the stories of R6; the
  window.

### R2. A step that waits says its phase and how long

*Amends §4, after "a step in a task happens where it was started".*

- **What it covers: a press whose answer is a start, its end arriving later.** Such a process says its phase where it was
  started: a sign-in (its action answers `started`, then its end as news), a heavy run, a brainstorm's passes, a meeting's
  rounds, each built by a row of §9. A press answered within its own request is §4's *while it waits it says so*, not a
  phase list. Today's other such processes keep their present words until a row takes them (§6): an agent's install or pin,
  the install's update (its standing already says *draining* with what it waits on, *waiting* and *applying*, with no time,
  `settings/Update.tsx:48-64`), a set-up for agents (a quest's session, whose state the rail says), a verification lane once
  built.
- **One line first, the phases behind it.** The line says the phase now with its mark, how long it has run, and the whole's
  time; a failed phase says its sentence on that line. A disclosure (*n phases*) opens the list: the phases done, each with
  the time it took, and the ones to come by name. Where the process is its own surface (the sign-in panel, a meeting's page)
  the list may stand open; in a head, a row or a record (a session's head, the rail, the record head) it is the line alone,
  since UX7's budget for the record head, the conversation at most 200 px down (`docs/2026-10-05-ux7-design.md:677`), has
  no room for a list per wait.
- **A bound is said when one is known, never invented**: *the code expires in 13 min*, *refused by itself in 12 min* (the
  held call's). Where the usual time has been measured (CLAUDESIGNIN1 measures a sign-in's), a phase past it says it is
  *taking longer than usual* and what the usual is; where nothing was measured, it says only its time.
- **The phases are the driver's**, written with each one's start where the process runs (its relay, its record) and read by
  the page. A phase is a step the driver performs or observes itself: what happens inside a tool it cannot see (a browser's
  return) is no phase of its own, and ends when the tool does. Never guessed from a tool's printed words, which stay the
  fallback for a link or a code a tool prints, read by its shape. An older host that writes no phases draws as today, with
  the time since the press.
- **Hues**: the phase now wears the live mark while Daoris or an agent works it (§3: a live mark means something runs); a
  phase that waits on the person is open's hue, theirs alone; a phase that waits on another session or on Daoris is neutral
  and names what it waits on; a failed phase wears the danger ink with its sentence. Done is done's ink. Never hue alone.
- **Time steps by its own clock**, seconds in the first minute and minutes after, and stops when its phase ends; nothing at
  rest moves (§3, FREEZE1).
- **Why:** a process that looks the same at five seconds and at five minutes cannot be told from a hung one, and the person
  stops it or waits for ever; and a list per wait would push a session's conversation off its head. **Check:**
  `PhaseList`'s stories per state, as the line and opened; each holder's test reads its phases from the driver's words in
  both languages; a fake clock steps the time.

### R3. A refusal is said where its action was started

*Amends §4's toast and `InlineConfirm` rules into one rule for every press.*

- **The place a press was made says its refusal, whole, in the driver's sentence**: a setting's change under its row's
  editor, a mode switch under its chip, a sign-in in its panel at the phase that failed, an ask's *Apply* under its proposal
  and its review choice under its press, a list under its *Save* (built). The editor or panel stays open with what the person
  typed, and the press is offered again.
- **A toast is for what has no place to stand in**: the place has gone (the person left it), or the act was started by a door
  that drew nothing (a menu item, a key, the palette). A cancel the person pressed is never said back.
- **Why:** a toast in the corner reads as about whatever is there, and it closes the form that would let the person try
  again. **Check:** each holder's test refuses a press and reads the sentence beside it, with no toast.

### R4. A process is drawn as a flow where its shape matters, as phases where its present matters, and independent facts as facts

*Amends §4 and §5; the flowchart is FLOWCHART1's (D186).*

- **One run, in its place, now**: R2's line and its phases. **A process's life** (a quest, a session, a unit of work, a
  workflow's run, landing and clean-up): FLOWCHART1's chart on its record, drawn from the record. This design draws no
  flowchart. A run's step is its place on the chart.
- **A record's standing that is several independent facts is a fact list**, each fact marked by its own value, never drawn
  as a progression: no fact is done because another is, and none is to come because another is not reached. The milestone
  is the case (§3.7): its result, clean-up and recovery are separate collections (`milestone.ts:85-97`), and a clean-up
  completed with nothing kept for recovery is ordinary (`WorkMilestone.test.tsx:30-38`), which a sequence would draw as a
  recovery done.

### R5. Every surface is reached from the frame

*Amends §5's opening: how a view is reached.*

- **No new place on the activity bar**: a tenth is a decision with its own counts (UX6 §8). A newer surface lives in the
  place that holds its thing.
- **Each surface has**: its place and its list; a door from *What needs you* when it waits on the person; a door in Ask
  Daoris or an exemption with its reason (`HelpCoverageTests`); and its terminal twin at its foot.
- **A verb is reached by what it acts on.** A verb that starts something from no chosen item (*New meeting…*) is in one menu
  and the palette (D152's one table). A verb on an item (*Sign in* on an account's row) is that item's act and its ⋯ entry,
  and no palette command of its own. A surface that only says a state (a heavy run's slot, a session's access, where work
  stands) has no verb: its door is its thing's place. §3.9 tables each, a dash in its menu column being one of the last two.
- **Why:** a surface reached only by a terminal is a surface the person who reads this application never finds; a command
  for every surface would fill the palette with verbs that need an item it cannot choose. **Check:** the command table's
  test names each starting verb, and `HelpCoverageTests` each door.

### R6. A story shows the widths the frame gives, without help

*Amends the components method (`docs/2026-09-21-working-surface-components.md`, D55) and the working surface §4's
catalogue.*

- **A main-area organism's stories are drawn at wide, half and narrow**, from one helper (`storyWidths`: 80rem, 39rem and
  25rem of main area, narrow being the main area's 400 px floor, `CENTRE_FLOOR`, `work/layout.ts:47`), in English and
  中文, light and dark. The helper replaces the local `mainAt` and `at` decorators stories carry today. A side-bar view's at
  the side bar's floor (300 px) and at 45% of the install's window.
- **No decorator adds what the page does not have**: a gap, a cap, a padding. A domain's cards are drawn in the domain's own
  container.
- **An organism that holds a hook splits a molecule for its states**, so every state has a story: the sign-in panel becomes
  `SignIn` over `SignInSteps` (§3.2).
- **Why:** every R1 defect passed its stories. **Check:** a story test that the helper's widths are used by every story
  of a component under `settings/`, `projects/` and `work/` mounted in a main area.

### R7. A region lays out by its own width

*Amends the frame model §3b ("it lays out by its own width, never the viewport's") from a rule into a check.*

- **No viewport variant inside a region** (`sm:`, `md:`, `lg:`, `max-sm:`, `min-[…]:` and their kin): a container query on
  the region or the component.
- **What is placed against the window keeps its viewport variants**, by its responsibility, not its file: the strip and the
  status bar (the status items at `work/frame.tsx:419-451`), the toast viewport that stands beside an open drawer
  (`design/ui.tsx:1619-1630`, platform-ux's UXTOAST1 rule), a drawer and its scrim. The toasts share `ui.tsx` with atoms a
  region draws, so a file-level exemption would pass both.
- **Why:** the side bar and the list narrow a region without the window changing; a form at 630 px of pane kept its three
  columns because the window was 1546. **Check:** `tokens.test.ts` fails on a viewport variant anywhere but the window's
  parts, which its allow-list names by component, each with its reason.

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
 narrow (the 400 px floor), the same card: the track goes, each value under its words
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
*What needs you* (UX6d). Never a drawer or a toast. **The phases** are the driver's own steps, each written with its start
into the same relay as the tool's output and read by the page. **The record comes first:** today a console line carries
only its sequence and its text (`bridge/console.ts:23`), and the action answers `started`, then its end as news
(`bridge/agents.ts:136-164`), so the row builds the phase events before the panel draws them. The driver sees the tool's
process start and exit, then runs its own read of who signed in and its own write of the lists (`AgentActions.cs:325-350`):
those are the phases. What the tool does with the browser in between is one phase that ends when the tool exits. An older
host sends no phases, and the panel draws as today, with the time since the press.

| Phase | Says, while it is the phase now | Ends when |
|---|---|---|
| Starting {tool} | — | the driver has the tool's process up |
| Waiting on your browser | *Waiting for the tool to open the page…*; then the link with *Copy link*, opening in the system's browser, or for a device code the code with *Copy code* and when it expires; where the tool asks for a code, the box, focused, with *Continue*. The link, the code and the ask are read from the tool's output by their shape (CODEXSIGNIN3), the fallback R2 keeps: none is a phase | the tool's process exits |
| Checking who signed in | — | the driver's own read of who signed in answers |
| Joining the lists | — (for *Add an account…*, step 3 follows: its name and lists) | the driver has written the lists |

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

**What changes in the existing rows:** CLAUDESIGNIN1b (the backlog holds it inside SIGNIN1) is one feature built end to end
by one builder (D187), not split at the driver and page seam as its row said: the driver writes the phases, the page draws
them through `PhaseList`, and `SignIn` splits over `SignInSteps`, a molecule with a story per state above; its "UX7 §4.5 note
first" is this section. CODEXSIGNIN2's refused panel, merged, becomes the list's *Refused at the start* state, keeping its
words and its two presses. The panel is the sign-in's own surface, so its list stands open (R2); the row under *What needs
you* says the line alone.

### 3.3 Sessions' access and the call held for the person

**Place:** a repository's and a workspace's Setup, section *Sessions' access* (built); Settings → Driver for this machine
(built); the session's own head (new); the held call on the head and the foot (built, kept).

- **Setup and Driver:** the same rows (`AccessRows`) at the three levels, laid out by R1. Driver's card loses its lead
  paragraph to its title's ⓘ, as Setup's fold carries its summary line. A pause's change refused is said under that row's
  editor (R3: `Inheritable` gains a refusal line, as `HeavyGates` has).
- **A session's head says its access** (D184 point 1, kept on its record and shown nowhere): where it started under full
  access, its facts line wears the plain warning chip Ask Daoris's full access wears, *Full access*; its *Details* lists each
  pause as it started (*Permission prompts: asking · Landing: your Accept*). A session whose pauses were not all open wears
  no chip, so an untouched start reads as it did.
- **Only what the record keeps.** The record holds each pause and its value as the driver resolved them at the start
  (`data/api.ts:433-438`), and nothing of where each was set, nor whether it was this machine's default. So *full access* is
  `isFull` over the recorded pauses (`settings/Access.tsx:75-77`: a pause the page does not know is never read as open), and
  *Details* says each pause's value as recorded, never where it was set: today's settings are never read into an old record.
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
| Not every pause open | Nothing on the head; *Details* lists each pause and its value as recorded |
| Full access | The chip on the facts line; *Details* lists every pause open |
| A pause the page does not know | Listed by its name and value as recorded; never counted as open |
| A record from before | *Details* says *not recorded* |
| Setup, a change refused | The sentence under the row; the editor open |

| Key | English | 中文 |
|---|---|---|
| `work.access.full` | Full access | 完全访问 |
| `work.access.details` | Access | 访问权限 |
| `work.access.asStarted` | {{pause}}: {{value}} | {{pause}}：{{value}} |
| `work.access.notRecorded` | Not recorded | 未记录 |

**Keys:** the chip is text, not a control; *Details* is the head's fold. **Themes:** the chip's warning line and icon, never
red (full access is a choice, not an outcome, §3). **Narrow:** the facts line wraps after the chip, never cutting it.

### 3.4 Ask Daoris's access

**Place:** the panel's composer bar (the chip), its *What it has* view, and Settings → AI features. The screen's name for
the place stands, *AI features* (AI 功能); D183 point 2's *Daoris's own AI* is the card's description there, not a place.

- **One confirmation.** Full access is asked through `InlineConfirm` in a warning tone, which it gains beside its danger and
  primary ones (`InlineConfirm.tsx:183-184`), carrying the servers it would add as its body and when it takes effect; it says
  it waits, stays open until the driver answers, and a refusal is said inside it (§4). Today's confirm closes before the
  answer (`useModeChoice`'s `enable`, `HelpModePicker.tsx:33-36`). `FullAccessConfirm` goes.
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

**The record comes first.** The holder's record keeps its process, its gates, since when and the session its environment
named, where one did (`HeavySlot.cs:8-20`); it keeps neither the gate it runs now nor who waits. A gate's start goes to the
run's output (`HeavyRun.cs:246-256`), and a waiter only looks again. So HEAVYSLOT1 builds the record before the screens: the
holder rewrites its record at each gate with that gate and its start, and each waiter keeps a record of its own beside the
slot, gone as it takes the slot or ends. Neither file alone is a hold: the handle is the lock, and a holder that died
leaves its record behind (`HeavySlot.cs:46-52`, `:181-194`), so a record whose process (its id and its start) is gone holds
nothing and waits for nothing. A holder whose environment named no session (a run from a terminal) is said as *a run from a
terminal*, with no door.

- **Setup:** the list as built, its editor laid out by R1's form rule (the narrow wireframe in §3.1). The moves keep ˄ and ˅,
  since each moves one way (§4).
- **The session running its heavy gates** says, in place of *running daoris-driver run-heavy for 6 min*, the gates as
  phases: on the rail's line *heavy gates · 2 of 3 · test:web · 6 min*, and on the head's facts line the same, R2's line,
  opening to the phase list in *Details*. A gate that failed is said with its exit and its sentence as recorded; the
  session's turn goes on reading it, as the agent does.
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
| Held by a run from a terminal | Details' row says so, with no door; a waiting session's line says *behind a run from a terminal* |
| A record left by a holder that died | Nothing: the slot is free, and the next run ends what it named (HEAVYRUN1a) |
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
| `projects.heavy.terminal` | a run from a terminal | 从终端发起的一次运行 |

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
account by the walk, D125, never chosen here), and the repository (a chip; *no repository* asks the question alone). A
meeting's request holds its question, its repository and its agents, and nothing else (`MeetingFiles.cs:7-9`), so the
composer offers no attachments and no criteria until a row gives the request and the packet a place for them (§6). Its
press is *Assemble the context*: it spends nothing, makes the packet and opens the meeting's page on it.

**The meeting's page** (the main area): UX7's head, then the phase list, then the packet, then the transcript or the
document.

- **Head:** its state's pill and its question's first line; its facts line *Talk* or *Brainstorm*, the agents, the
  repository, *round 1*; its loud act by state (*Send to 2 agents* while the packet waits; *Stop…* while a round or a pass
  runs); ⋯ with *Copy ID* and *Clear from this machine…* (asks once, D153).
- **Round 2 is an act, never a phase to come.** A meeting holds a second round only by the person's press, and no more
  (`MeetingPacket.cs:40-41`), so once the first round ends the head offers *Send round 2* as its next act, its packet shown
  first; the phases say nothing of a round not sent.
- **Phases** (R2): for Talk, *Context assembled → Shown to you → Round 1: Claude Code, then Codex*, each agent's turn a phase,
  in the order the turns run, one at a time (`DriverMeetings.cs:14-17`); for a brainstorm, *Context assembled → Shown to
  you → Proposals: Claude Code, Codex → Synthesis → Document*, with *Challenge* before the document once BRAINSTORM1e runs
  one. A pass that stalled, failed or met a limit says so at its phase, by code, in words.
- **Each phase's time comes from a record.** A pass record keeps its role, who ran it, its state, its codes and its turns,
  and no time (`BrainstormDocument.cs:6-13`), so MEETUX1a adds a start and an end to each pass record (fields added beside
  the old; a record from before says no time). A round's start is its state's own time (`MeetingFiles.cs:11-17`), and *Sent
  14:02* the send's (`:140`).
- **What the agents will read** (the packet, D176 point 2): the tier that found its sources (*meaning*, *words*, *files*),
  the question, then each source (its kind, its path and lines, the revision it was read at, why it was taken, *kept 4,000
  of 12,000 characters*); *Left out*, folded, with why. A packet is frozen once per round and named by its digest, and the
  send names that digest (`MeetingPacket.cs:18-29`): no verb revises one, so the page offers no *Leave out* and no *Add a
  file…* (§6). Once sent it is read-only: *Sent 14:02 to Claude Code and Codex*. A refreshed packet for round 2 says what it
  adds beyond the first (D177 point 2).
- **The transcript** (Talk): a section per round; each message its maker's mark and agent's name (the account on this machine
  only), its turn's end once pass records keep one (a message has no time of its own, `MeetingPacket.cs:12-16`), and its
  words as written. A turn that did not answer is said in its round's line. **Turns answer the packet, never each other in
  the round**: no turn reads another's message of its round, which reaches the others in the next round's packet
  (`DriverMeetings.cs:19-21`), so a round is drawn as each turn's answer to the packet, in turn order, never as a reply
  thread.
- **The document** (Brainstorm, D176 points 3–6): its coverage first (*2 makers answered: Claude Code, Codex*, or *one maker
  answered: this is no cross-maker brainstorm*); then the ranked groups, each its rank, title, merged idea and why the
  synthesis ranked it (*the synthesis's claim*), its flags as chips (*hard to reverse*, *risky*), how many makers named it,
  its members folded with each proposal's fields. Once BRAINSTORM1e records them, the challenge's findings stand beside it
  as claims (*stands*, *weakened*, *unsupported*, *unknown*); a required challenge that did not finish holds its group out of
  the ranking, *not challenged*, with *Challenge again* (a new spend, said as one) and *Rank it unchallenged*. Today's
  document holds no challenge (`BrainstormDocument.cs:43-50`) and the verb offers ask, show, send, stop, clear and list
  (`BrainstormCommand.cs:13-16`): the findings and both presses are BRAINSTORM1e's to give the driver first. Then what was
  left out with why, and what the passes needed that the packet lacked. Its foot: *Copy as Markdown* and *Attach to a new
  ask…*.
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
│  │                              │           kept 4,000 of 9,812 characters                                     │
│  │                              │           knowledge .claude/knowledge/model-decoupling.md · …                │
│  │                              │           ▸ Left out (2)                                                     │
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
│ Talk · Claude Code, Codex · round 1          │
│ [Stop…] ⋯                                    │
│ ✓ Context assembled   ✓ Sent 14:02           │
│ ● Round 1: Claude Code  ✓ 3 min              │
│            Codex        ● 1 min 20 s so far  │
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
| Round 1 ended | The next round's packet frozen and shown, with what it adds; *Send round 2* offered as the next act, which the person may leave unpressed | *What needs you*, as *Waiting for your send* |
| Round 2 ended | The transcript whole; no further round is offered (two at most) | — |
| An agent's ask for a brainstorm | *An agent in {repository} asks for this brainstorm*, the packet, *Send* or *Refuse* (BRAINSTORM1d's `ask`) | *What needs you* |
| Stopped | What answered kept; *Stopped by you* | — |
| Interrupted | *Interrupted when Daoris stopped; nothing runs again by itself*; what answered kept. No verb resumes the missing passes, so none is offered: the same question may be asked again as a new one (a new spend, said) | — |
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
| `meeting.packet.left` | Left out | 已排除 |
| `meeting.packet.kept` | kept {{kept}} of {{total}} characters | 保留 {{total}} 个字符中的 {{kept}} 个 |
| `meeting.send` | Send to {{count}} agents | 发给 {{count}} 个智能体 |
| `meeting.round` | Round {{n}} | 第 {{n}} 轮 |
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
phases (read, not pressed), *Left out*'s fold, then the rounds or the groups; each round and group is a
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

- **The milestone is four facts, never a sequence** (R4): *End*, *Result*, *Clean-up*, *Recovery*, each marked by its own
  value with `Dot`'s tones (§4) and said as today in the driver's clauses as recorded. The end is *reached* with its proof
  and time, or *not reached* with the driver's why, or *handed*, *dropped* or *unknown* by its own word, an unknown end never
  read as reached. The result is what was kept, or *nothing kept*. The clean-up is *pending*, with what waits and why and a
  blocked entry in the danger ink, or *completed*, with what went. The recovery is kept until a date, a bound (R2) and the
  earliest the person must act by, or *nothing kept*, which wears no done mark: no fact is marked because another is. A held
  plugin step's attempt stays its own row under them (`WorkMilestone.tsx:98-105`).
- **Folded to its line by default, opened in place.** `MilestoneLine`'s *ended · clean-up pending · recovery until 3 Nov*
  heads it with a disclosure, and gains *blocked* when a clean-up entry is blocked and *held* when an attempt is held, so
  nothing that waits on the person hides behind the fold. Today the facts draw whole under the record head
  (`AttendedSession.tsx:289`), the one block there with no fold.
- **The record head keeps its budget.** UX7's 200 px is measured again with every block present (the parts strip, *Accept*,
  the relations, the channel and the milestone's line), in a rendered browser, since jsdom measures nothing; what passes it
  folds to one line, opening in place.

```
 half, the attended session's record, the milestone opened from its line
┌────────────────────────────────────────────────────────────────┐
│ ended · clean-up pending · recovery until 3 Nov              ˄ │
│ End        ● reached: merged into main at 3f9a12c · 14:02      │
│ Result     ● kept: the branch's commits on main                │
│ Clean-up   ● pending: the tree waits for its session's last    │
│              turn                                              │
│ Recovery   ● kept until 3 Nov: the moved-aside folder          │
└────────────────────────────────────────────────────────────────┘
 each ● is that fact's own tone and word: done's ink for reached and kept, the waiting tone for pending, the danger
 ink for a blocked entry, none for nothing kept
```

| State | Shows |
|---|---|
| End not reached | *not reached*, with the driver's why; the other facts as they stand |
| End unknown, handed or dropped | The end's own word, as today; never *reached* |
| Clean-up pending | Its waits and why, each by its own value |
| Clean-up blocked | The blocked entry in the danger ink; the line says *blocked*; *What needs you* holds its row |
| Clean-up completed | What went; the recovery says what it keeps until when, or *nothing kept*, unmarked |
| An attempt held | Its row under the facts, as today; the line says *held* |
| No unit (a chat, a record from before) | Nothing drawn |

No word is new but the line's *blocked* and *held* (`work.milestone.line.blocked`, *blocked* / 受阻;
`work.milestone.line.held`, *held for you* / 等你处理); `work.milestone.*` stand. **Keys:** the fold is a disclosure
button; the facts are a description list. **Themes and narrow:** `Dot`'s tones (§4); the label column stacks under 24rem of
its own width.

### 3.8 Pushed state

No surface: D182 moves where the page's data comes from, not what it shows. Three rules keep that true.

- **A slice not yet judged is loading, never empty**: skeleton rows, never an empty state's sentence. *Nothing needs you*
  drawn before the attention slice is judged is a false statement.
- **A view that combines inputs is judged when every one is.** *What needs you* reads over a dozen inputs, each empty by
  default while unanswered (`AttentionBand.tsx:159-162`), so *Nothing needs you* waits for all of them, never for one.
- **A resync holds the last render** at the language's reduced opacity until the snapshot lands (§4 *Loading*); nothing jumps.
  A resync is a state of its own, which the slice keeps while it asks (`bridge/slices.ts:187-204`) and the page reads; a
  query's fetching is not it.
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

A dash in *Menu and palette* is R5's: a verb on an item, which is that item's act and its ⋯ entry, or a surface that only
says a state and has no verb.

### 3.10 One meaning for a number: what waits on you (BADGEUX1)

**Standing: built 2026-10-22 under D185 (BADGEUX1), revised after Codex's read, every blocking finding taken (§3.10.8); the
steward's to accept under D191.** The owner's direction of 2026-10-22, given while using Daoris: a number on a session, a quest
or a list place counts only what needs the person's step, the same things the window's taskbar button and the title bar's bell
count; work that is running is not counted, and is shown in another, quieter style. The whole window then has one meaning for a
number: *this waits on you*. Read for it: D196 and the notification design §2 and §3.3;
this design's §2 and §4; the platform language §2 and §3; D76's SESSBADGE1 note and D130's CAPCOUNT1 note; NEEDSSCOPE1's and
GOAHEADSTALE1's entries; `person-waits.md` and `translation-parity.md`; the glossary. Every statement about today was read from
the code at `662151ce1`.

#### 3.10.1 Every number and mark today

| Where | What it counts today | Read at |
|---|---|---|
| The title bar's bell | each press of either source once, and every row of *What needs you* but a press's own (`needsListed`), in the window's workspace, open's hue; the presses' count in the warning hue while one waits unshown; a dot in the accent while anything is unread | `App.tsx:491-499`, `NotificationsBell.tsx:31-41`, `attention.ts:106-108`, `bridge/attention.ts:91-96` |
| The taskbar button and the title (`SET_BADGE`) | the bell's count, per main window (NOTIFY2c) | `App.tsx:498-499` |
| Activity bar: Overview | nothing since NOTIFY2b | — |
| Activity bar: Sessions | **the sessions the driver runs** (the cap's count, CAPCOUNT1: queued, starting, working and parked, never a person's conversation) in the accent; a dot in open's hue while a session is parked asking or a quest is parked on its failures (U20's set) | `App.tsx:506-512`, `:1264-1278`; `NeedsAPerson.cs:448-480`; `frame.tsx:287-289` |
| Activity bar: Quests | **every outstanding quest** of the scope (open or taken), in the accent | `App.tsx:238-239`, `:485`, `:1268` |
| Activity bar: Agents | the signed-out accounts a list or a default holds, judged on the page from the roster, open's hue | `AgentsView.tsx:141-146`, `App.tsx:402-403` |
| Activity bar: Repositories, Map, Knowledge, Plugins, Settings | nothing | — |
| *Notifications*' group heads | the presses (warning), *What needs you*'s rows (open's ink), **Messages' unread and Done and failed's unread, bare figures in open's ink** | `NotificationsView.tsx:141-169`, `:201-203` |
| Overview's summary lines | the presses, each group's rows, **Messages' asks and every notice, in open's ink** | `NeedsSummary.tsx:82-98`, `:111` |
| Sessions' list (the rail): rows | a dot and its word per row (the live mark pulses three times as a turn begins and settles, `tokens.css:233-237`); facts in words; no figure alone | `SessionRow.tsx:236` |
| Sessions' list closed to its strip | each session's initial and its dot, its state its accessible name and tip | `SessionRow.tsx:33-38`, `ui.tsx:449-497` |
| Sessions' list: headings | the rows each group heads, in its words (*Working (3)*); an ask's heading *· 2 unread* | `SessionList.tsx:219-224`, `:244` |
| The console's tabs | each stream's dot, its state in the tab's accessible name | `frame.tsx:863-880` |
| The strip's browser chip | *browser driven by 2 sessions*, a count with its noun | `BrowserDoor.tsx:90-101` |
| Quests' list: rows | pills in words; **an ask's *2 unread* pill in open's hue**, a teammate's ask's too; a quest's carried links and files, an icon and a faint figure each | `AskRow.tsx:100`, `TeammateAskRow.tsx:30`, `QuestList.tsx:189` |
| Quests' list: headings | the rows each group heads (*Asks (2)*) | `QuestList.tsx:92`, `:107` |
| An ask's channel; an item of *Messages* | **its *N unread* in open's hue** | `ChannelSection.tsx:171`, `Messages.tsx:116-118` |
| The status bar | *N sessions*: the cap's count, the same as Sessions' badge, its tip saying *working, or waiting on you*; *setup: n of 5* until the setup is done; the index's entries, a faint figure with its tip | `frame.tsx:410-419`, `:449-472`, `work.status.json` |
| The Workspace menu's scope rows | each workspace's repositories, a faint figure | `commands.ts:545` |

There is no *Asks* place: an ask is a row of Quests' list. So two of the activity bar's three numbers counted work that was
not waiting on anyone (Sessions' and Quests'), in a badge drawn like the bell's; and six places said *unread* with a figure in the
hue the platform language keeps for the person's waits (§3: *open's hue is the person's alone*).

#### 3.10.2 The rule

- **A badge counts only what waits on the person.** A badge is a figure drawn as a mark of its own, standing for its count
  without a noun: on a place of the activity bar, on the bell, on the taskbar. It counts the rows `needsListed` returns, in the
  window's workspace as NEEDSSCOPE1 scopes them, and on the bell and the taskbar alone the presses as well, since a press is the
  bell's own (§2.1 of the notification design). It wears open's hue, the bell's unshown presses the warning hue as now. Nothing
  else is ever a badge. A count said with its noun (*3 unread*, *2 sessions*, *Working (3)*) is words, and the rule asks of it only
  that it never wear open's hue unless it counts waits; so the window's one meaning is the badge's.
- **Each wait is counted at one place: the place its row's door opens** (`App.tsx:976-1038`), where its step is taken. So a
  place's number is the bell's for the same rows, and the places' numbers, the presses and the rows only the bell's view settles
  sum to the bell's count of what needs the person: the figure the bell shows while no press waits unshown (while one does, it
  shows the presses' count in the warning hue, §3.3 of the notification design), and the taskbar's always.

  | Place | Its waits, by kind |
  |---|---|
  | Sessions | `parked`, `review`, `opinion`, `helper`, `hand-over`, `permission`, `answer`; a `work` row whose branch was cut for a session the page holds, which opens that session (FABRIC-UNKNOWN1c) |
  | Quests | `proposal`, `intake`, `unanswerable`, `parked-quest`, `go-ahead`, `departure`, `set-up`, `trust` |
  | Repositories | every other `work` row (the workspace's Branches, where the clean-up's press is) |
  | Agents | `rule`, `account-wait`, `signed-out` |
  | Settings | `space` |
  | none, the bell's view alone | `confirmation` (its press is the bell's); a held attempt (`work` with `held`), whose presses are on its row alone and whose door opens nothing; a kind this build does not know |

  The kinds' table is `ATTENTION_PLACE` beside `ATTENTION_GROUP` (`work/attention/attention.ts`), typed over every kind, so a new
  kind fails the typecheck until it is placed; `placeOf` reads a `work` row's own door, judging the cut session by the same
  `workCutFor` the band's door reads, over the sessions the page holds. Overview and *Notifications*' tab wear none: Overview
  holds no wait of its own, its lines being doors into the view, and the view's count is the bell.
- **Running work is a mark, never a number.** An 8 px dot at the place's lower corner, in taken's hue (`--st-taken`, the status
  palette's *in progress*, which a working session's pill and a taken quest's already wear), ringed in the page's colour so it
  stands off the icon, still. It says what its source holds, and no more:
  - **Sessions: the driver's work under way.** The sessions the driver runs in the window's workspace, the cap's count
    (`NeedsAPerson.Working`: queued, starting, working and parked, never a person's own conversation, CAPCOUNT1), less those
    parked on the person (`.WaitingInSessions`' `session:` keys), both judged in .NET and keyed alike, so the page subtracts keys
    and judges nothing. So a park already answered and waiting for the driver's next look is under way, and a session holding a
    tool call for the person is both under way and a wait, its number beside the mark. A person's own conversation, whose turn may
    be running, lights nothing here: its row in the list says it. Its words are *under way*, never *working*, which is a turn's.
  - **Quests: a quest taken.** Its status, whoever took it, a taken quest waiting on another repository's answer among them
    (`QuestList.tsx:192-194`): its words are *taken*.
  - nowhere else: no other place holds work that runs.
- **Both at once**: the number at the upper corner, as the bell's, and the mark at the lower; they differ in shape (a figure in
  a ring, a filled dot), in place and in hue, and the place's words say both, the waits first.

| Token | Light | Dark | Against (non-text, 3:1 needed) |
|---|---|---|---|
| `--st-taken`, the running mark | `#2f6db3` | `#5c97dd` | the page 5.0:1 and 6.0:1; the current place's `--accent-soft` 4.4:1 and 5.1:1; a hovered place's `--raised` 5.3:1 and 5.5:1 |
| `--st-open` with `--ink-open`, the number (as today) | `#b07818`, `#8b5c02` | `#bd8226`, `#c68b31` | the status palette's validation (§3): open and taken are adjacent hues separated for deutan vision |

Computed from the tokens by WCAG's relative luminance, not measured on a screen.

#### 3.10.3 Why that mark, and not the others

- **Not green.** Green is done's hue, an outcome, and the platform language keeps a liveness mark off an outcome's hue (§3,
  POLISH2: *ended* wore done's green beside a failure). A place lit green would read *done*.
- **Not the accent, the rows' live mark.** At badge size the accent and open's amber read alike (UX5 U24, why SESSBADGE1 put its
  wait in a dot), and the mark sits beside an open number; the accent is also the interactive identity, never a status (§3).
  Taken's blue is the palette's other side. The rail's rows keep their accent dot: it is one turn's liveness beside its word,
  where a place's mark is a standing fact about the driver's work.
- **Still.** It stands for hours while the driver works, and a mark at rest is still (FREEZE1, `tokens.css:233-237`); the place
  is not where a turn is watched.
- **A dot, not a stroke** (Codex's 6.2). Once Sessions' waiting dot leaves, a dot on a place means one thing; the accent's dots
  are on the rows and the bell, never on a place; and the direction named a dot.
- **Not a quieter number.** Two figures on one 36 px place (D76's note) and a figure that means something other than a wait are
  what the direction removes.

#### 3.10.4 What each place says, in both languages

The place's accessible name stays its name (*Sessions*, 会话). Its description, which its tip repeats after the name and its key,
says what its marks show, the waits first, joined by ` · `:

| Mark | English | 中文 | Key |
|---|---|---|---|
| The number | *2 waiting on you* | 2 个等你处理 | `nav.waiting`, the taskbar's own words, so the place and the button say one sentence |
| Sessions' mark | *3 under way* | 3 个进行中 | `nav.underWay` (new): the driver's work under way, never *working* (工作中), which is a turn's, and which a queued or answered session is not |
| Quests' mark | *1 taken* | 1 个已接下 | `nav.taken` (new), the glossary's *take* (接下), never 进行中, which it avoids for a taken quest |

`nav.active` (*N active*), the count SESSBADGE1 said, is retired from both catalogues. A place with neither says nothing beyond
its name. 进行中 names the driver's sessions under way and a taken quest is 已接下, so the two places never share a word.

#### 3.10.5 What changes elsewhere

- **Sessions' badge (SESSBADGE1).** Its count of the sessions the driver runs leaves the badge: the running mark shows that work
  is under way and its words say how many, and the status bar keeps the cap's count. Its waiting dot leaves too, since what waits
  is now the number. The `sessions` partition stays, read only to keep parks out of the running mark (`useAttention`'s `running`);
  a quest parked on its failures is counted on Quests, where its *Try again* is, and no longer in Sessions (U20 read it there).
- **Quests' badge.** Outstanding quests are no longer counted: a taken quest lights the mark, an open one sitting is neither a wait
  nor running work. The quests are still read for the mark.
- **Agents' badge** is the rows' judgement (`signed-out`, `account-wait`, `rule`), not the page's own count of signed-out accounts
  (`useAgentsWaiting` goes), so it is the bell's judgement and scoped as the bell's is.
- **The status bar keeps *N sessions*:** the cap's count (D76's note), how much of the cap is held, a standing fact with its noun
  beside it, never a badge, its tip unchanged. It is Sessions' *under way* plus the driver's sessions parked on the person.
- **Unread is said in words, in a neutral ink.** News waits on nobody (D196 point 6; the notification design §2.4), so wherever a
  figure counts it, the figure carries its word (*3 unread*) in the neutral ink, never open's: *Notifications*' Messages and Done
  and failed heads, an ask's row, a teammate's ask's row, an ask's channel and an item of Messages. Overview's lines for Messages
  and Done and failed count their items and notices, not what is unread, which their detail says: their figures keep what they
  count and leave open's ink for the neutral one. The bell keeps its accent dot, never a count.
- **Kept as they are, each read against the rule:** a list heading's tally of its rows, written in its words (*Working (3)*); a
  row's dot and its word, in the list and closed to its strip; the console's tabs' dots; a quest's carried links and files (an
  icon and a faint figure); the strip's *browser driven by 2 sessions*; the status bar's *setup: n of 5* and the index's entries;
  the Workspace menu's repository count (a faint figure on a menu row). Each is a count with its noun or a state with its word, none
  is a badge on a place, and none wears open's hue for anything but a wait (the setup's mark waits on the person).
- **A browser** has no rows, so no place carries a number there; Quests' mark still lights from the service's quests.

#### 3.10.6 What holds it

- `attention.test.ts`: every kind's place; every listed row counted at one place, the places summing to `needsListed`; a kind this
  build does not know at none; a unit of work by its own door (cut for a session, held, neither), the band's `cutFor` the same.
- `app/sessionsBadge.test.tsx`, over the window and a shell that pushes the attention slice: at each of the five places, its
  number is the bell's for the same rows and no other place counts them; every wait at one place, the places summing to the bell;
  in a window on one workspace and on every one; running work alone, the mark and no figure, at Sessions and at Quests; both at
  once; a parked session never running, the status bar's count still holding it; the words in English and 中文.
- `chrome.test.tsx`: the bar draws every number in open's hue, the mark in taken's, both and neither, and says each in words.
- `NotificationsView`, `NeedsSummary`, `ChannelSection` and `QuestList` tests: unread said with its word, never in open's ink; a
  wait's figure in it.
- Stories: `Chrome/AppStrip`'s bar with waits and running work, running alone, waits alone with a count past 99 and the gear's,
  each in light and dark; and in a browser, no number.
- Not covered: the installed window, and the contrast seen rather than computed. The parent's look: the bar while sessions run
  and nothing waits (a blue dot, no figure), a park arriving (the figure beside the dot), the bell's number equal to the places'
  sum, *Notifications*' and Overview's unread in the neutral ink, in both themes.

#### 3.10.7 Rejected

- **Green for running** (an outcome's hue); **the accent's pulsing dot** (reads as open's amber at badge size, and moves forever).
- **A grey or smaller number for running**: a figure that means anything but a wait is what the direction removes.
- **Parked quests counted in Sessions** (U20): a wait is counted where its step is taken, and *Try again* is on the quest's page.
- **The running mark judged on the page from the sessions' list**: a second rule SESSBADGE1 rejected for the count; the page only
  subtracts two partitions .NET judged. **A new .NET judgement of turns in flight** (Codex's 3, its second way): the mark says the
  driver's work under way, which its source holds exactly, and a person's own turn is said on its row; no count needs a new
  source, so the modules are left as they are.
- **A fixed place per kind for a unit of work** (round 1 of this section): its door is its row's, so its place is too.
- **The status bar counting only what runs**: its count is the cap's, the number the cap spends (CAPCOUNT1).
- **A badge on Overview or on *Notifications*' tab**: neither holds a wait of its own; the bell is that count.

#### 3.10.8 The read (D191)

Codex read the first version of this section on 2026-10-22, before its build, read-only in this repository, told not to read
`local/`; its prompt and answer are kept in the machine's scratch. Six findings, three marked blocking; each was checked against
the code at `662151ce1` before it was taken. Fable did not read: the section changes no record, protocol, concurrency or decided
rule others rest on, only what the page draws from what .NET already judges.

| # | Finding | Verdict | What changed |
|---|---|---|---|
| 1 | The inventory missed the strip's browser chip, the list's strip marks, the console's tabs and the status bar's other figures; the live dot pulses three times, not while a turn runs (blocks a claim of completeness) | holds | §3.10.1 gains the four rows and the pulse's truth; §3.10.5 classes each as kept |
| 2 | A unit of work's door is its row's (held, cut for a session, or Branches), so a kind's place is not its door's; the bell's shown figure is the presses' alone while one waits unshown (blocking) | holds | `placeOf` reads a `work` row's own door through the band's own `workCutFor`; a held attempt is the bell's view's alone; the sum is stated against the bell's count of what needs the person and the taskbar's |
| 3 | Taken's hue is right; but `working` less parks holds queued sessions, answered parks and held calls, and no person's turn, so it is no turn's liveness; a taken quest may wait on another repository (blocking for the predicates) | holds in part | the hue stands; the marks are named by what their sources hold: *the driver's work under way* and *a quest taken*; a held call's session is said to be both; no new .NET judgement (§3.10.7) |
| 4 | 工作中 and 已接下 fit the glossary, the name and description shape is right, but *working* overstates its source; perhaps *2 waits* | holds in part | *under way* (进行中) for Sessions; *waiting on you* kept, being the taskbar's and the bell's sentence |
| 5 | Unread in a neutral ink with its word is right; Overview's Messages and Done figures count items and notices, not unread | holds | those two figures go neutral and keep what they count; their details still say the unread |
| 6 | The promise is broader than the result: keep *one meaning for a badge*; a stroke would tell progress by shape | holds in part | §3.10.2 binds the badge and says what a count with its noun keeps; the dot stands (§3.10.3) |

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
| `Dot` and `DotMark`, extended | atoms, `ui.tsx:386-430` | A phase's or a fact's mark, by the liveness mark that already names its state in words: `live` (now), `parked` (waits on you), `failed`, `ended` and `idle` stand, and `done` and `waits` (on another, neutral) join them. No second mark and no second palette | Each tone, light and dark |
| `Elapsed` | atom, `ui.tsx` | A span stepping by its own clock and stopping when told, over what exists: `useNow` (`work/useNow.ts:7`, which stops on `null`) and `span` and `elapsed` (`locales/format.ts:111-139`). No second timer and no second formatter; never a live region by itself | Under a minute, minutes, hours; stopped |
| `PhaseList` | molecule, `design/` | R2's line and its list: the phase now with its mark and time, the whole's time, a failed phase's sentence on the line; behind *n phases*, the phases with their times, a bound, *longer than usual*, a detail slot under the phase now; open by default only on a process's own surface | The line and the list opened; starting, waiting on the person, waiting on another, longer than usual, bound, failed at the first and at a middle phase, done; en and zh; light and dark; 300 px and narrow |
| `SettingList` | molecule, `ui.tsx` | A layout wrapper, never a second row: it gives a card's `SettingRow`s one label track and the rule between them, and below 32rem of its own width stacks them. `SettingRow` (`ui.tsx:1453-1481`) stays the one row, with its label, hint, ⓘ and narrow form, reading the track, its notice moved into the value column | Short values, a path, a notice, a long label, a row with acts; at wide, half and narrow; en and zh |
| `FormGrid` | molecule, `ui.tsx` | A form laid by its own width: fields by kind (`number`, `name`, `pattern`, `long`), each yielding to its container, a form of rows stacking each row below its floor by a container query | One field, three fields, a list of rows; wide, half, narrow |
| `SignInSteps` | molecule, `agents/` | The sign-in's phases and their details (link, code, box), from props; `SignIn` keeps the hooks | Every state in §3.2's table, en and zh, both themes, narrow |
| `AccessFact` | molecule, `work/` | A session's access from its record's pause map: the chip on its head where `isFull` holds over it, each pause and its value as recorded in *Details* | No pause open, some open, every pause open, a pause the page does not know, not recorded |
| `HeavySlotLine` | molecule, `work/` | The running or waiting line, and the Details row | Running, waiting, failed gate, held by a run from a terminal, none; narrow |
| `MeetingComposer` | molecule, `work/` | §3.6's composer: question, protocol, agents and repository, through New conversation's `AgentChip` and `RepositoryPicker` (`NewConversation.tsx:10-11`) rather than copies; its carry (`compose/carry`, `:221-225`) joins once a meeting's request holds files | Empty, typed, refused, a browser (absent), narrow, zh |
| `PacketView` | molecule, `work/` | The packet, read-only: tier, question, sources, left out, sent | Unsent, sent, refreshed for round 2, files-only tier, zh |
| `TranscriptRound` | molecule, `work/` | A round's turns in order, each its maker's mark and its answer to the packet, a turn that did not answer | Two answers, one silent, a long message, zh |
| `ProposalGroup` | molecule, `work/` | A ranked group: rank, title, merged idea, why (a claim), flags, makers' count, members folded; the challenge beside it once BRAINSTORM1e records one | Ranked, flagged, unranked by maker; after BRAINSTORM1e weakened and not challenged with its presses; zh; dark |
| `MeetingPage` | organism, `work/` | The page: head, phases, packet, transcript or document | Its states through the molecules' stories, and a story per page state at wide and narrow |

`FullAccessConfirm` goes (into `InlineConfirm`, which gains its warning tone). `WorkMilestone` keeps its own fact list, marked
by `Dot`'s tones and folded to `MilestoneLine` (§3.7): it is no `PhaseList`, since its facts are no sequence.

## 5. The traps, each a test or a story

| Trap | Test or story |
|---|---|
| A row's value drifts to the far edge again | A rendered-browser check (Playwright) at wide and narrow: every value's box starts at the track's edge, or under its words; jsdom lays nothing out, so no unit test can hold it |
| A story hides the page's spacing | A story test: no decorator under `settings/` adds a gap, a cap or a padding; the domain's container draws its cards |
| A card margin returns | `tokens.test.ts` fails on an outer margin on `Card` |
| The window chooses a region's columns | `tokens.test.ts` fails on a viewport variant outside the window's parts, which its allow-list names by component |
| A window overlay flagged as a region | The same test passes the toast viewport beside a drawer, the drawer, the strip and the status bar, each by its reason |
| A wait looks hung | `PhaseList`'s stories at 5 s and 5 min differ in words; each holder's test reads its phase from the driver's words |
| A phase invented from a tool's words | The sign-in's driver test: its phases come from the driver's own steps (start, exit, the who-read, the lists); a link and a code are read by their shape |
| A list per wait pushes the head down | `PhaseList`'s line is the default in a head or a row; its stories draw the line and the list opened |
| A bound is invented | The sign-in's test: no *expires* line without the tool's bound; no *longer than usual* without a measured usual |
| A clock keeps the window drawing | `Elapsed` stops when its phase ends (a fake clock reads no render after) |
| A refusal leaves its place | Each holder's test (sign-in, a pause on Setup and Driver, the mode chip, an ask's *Apply* and review choice) refuses and reads the sentence in place, with no toast |
| A sign-in's link or code read by one version's words | CODEXSIGNIN3's fixtures per recorded version |
| Full access hides on a session, or an old record is read against today | `AccessFact`'s test over the record's pause map: every pause at its open value wears the chip; a pause closed, or one the page does not know, wears none; nothing is resolved against today's settings |
| A milestone fact marked by another's | `WorkMilestone`'s test: a clean-up completed with nothing kept for recovery says *nothing kept*, unmarked; an unknown or dropped end is never *reached*; a held attempt keeps its row |
| A stale heavy holder read as a live hold | HEAVYSLOT1's driver test: a record whose process is gone holds nothing and waits for nothing |
| A meeting's packet sent from a row | *What needs you*'s test: the row is a door, it carries no *Send* |
| A meeting drawn as a session | The rail's test: a meeting is attended by its own identity, and a session's acts are never offered on it |
| Turns read as replies within a round | `TranscriptRound`'s story: each turn answers the packet; the others' messages arrive in the next round's |
| A brainstorm's rank read as fact | `ProposalGroup`'s story labels the rank the synthesis's claim and the challenge's findings claims |
| A flag drawn as an outcome | `tokens.test.ts`'s ink rule: a flag never wears the danger ink |
| An empty state before a slice is judged | A pushed-slice test: an unjudged attention slice draws skeleton rows, not *Nothing needs you*; so does a judged one while another input of the band is not |
| The record head pushes the conversation down | A rendered-browser measurement at narrow with every block present: the conversation's top under UX7's budget |
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
- **R2's later holders**: an agent's install or pin, the install's update (its standing says its state in words with no
  time), a set-up for agents. Each keeps its present words until a row gives its process phases with their times.
- **A meeting's attachments, constraints and criteria, and a packet revised before *Send*** (*Leave out*, *Add a file…*):
  the request and the packet hold none, and a revision must keep the send naming the digest the person read (D177 point 2).
  A later row gives the driver the verb and the fields first.
- **Resuming a brainstorm's missing passes**: no verb does it; the same question is asked again as a new one.
- **A message's own time in a meeting's transcript**: a message keeps none; its turn's end, once pass records keep it,
  stands for it.
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
- **The milestone as a progress sequence** (*End reached → Result kept → Clean-up → Recovery kept*): its facts are
  independent, and a sequence draws a recovery that was never kept as done (R4, §3.7).
- **A full phase list on every wait**: in a head or a row it spends the 200 px the conversation is owed; the line comes
  first and the list opens (R2).
- **Round 2 drawn as a phase to come**: a second round is the person's choice, and a phase to come says it will happen.
- **Every surface's verb in the palette**: a verb on an item needs the item, which the palette cannot choose (R5).

## 8. What this amends

- **The platform language §4** (D41): *Forms and settings* takes R1 and R3; after *a step happens where it was started*, R2;
  *an arrow is a direction, never a menu* is worded for a region's menu, a value picker keeping its chevron; *Doors* takes R5;
  the ask-once confirmation gains a warning tone, for a choice that loosens and destroys nothing (full access).
- **The platform language §5**: Sessions lists meetings (§3.6); Repositories' Details the heavy run (§3.5); a session's head
  its access (§3.3); Settings' rows R1.
- **The frame model §3b** (D118): R7 as a check.
- **The components method** (D55) and the working surface §4: R6.
- **UX7 §4.5** (D152): the sign-in step's phases (§3.2).
- **D183 point 2**: the place is *Settings → AI features*.
- **D176 and D177**: their page is §3.6, in Sessions; BRAINSTORM1d keeps its caller.
- **§3.10 (BADGEUX1, 2026-10-22)**: the platform language §2 (the activity bar's numbers and running marks, the status bar's count);
  D76's SESSBADGE1 note (Sessions' badge); D150 §2.1 (Agents' badge is the rows').

D41's character, tokens and accessibility, D118's regions, D150's rule that a setting lives on its thing, D152's menus and
heads, and D173's milestone's facts stand.

## 9. The rows, in the order to build them

**The rows follow D187**: each is one feature a person can see working, named by its outcome and built end to end by one
builder across every lane it needs (driver, modules, service, the page), never a host half and a page half. A feature too
large for one builder's context is split only into parts that each work on their own and are each seen working (the
meeting's four, D187 point 5). A part shared by several features (`PhaseList`, `SettingList`) is built by the first feature
that shows it. Each proof includes its stories at R6's widths (the 400 px floor among them), its tests red first (its own
`Process` classes among them, D187 point 4), a rendered-browser check where it claims a geometry (jsdom lays nothing out),
the web's `i18n:check` and `names:check -- --strict` where words change, and the parent's look on the window at 1546 and
680 px in both themes and languages. A row whose screen needs a record nothing keeps builds the record first, in the same
feature. UIFIT1a (the parent's shots) comes first, unchanged. **Each row names its trap**: the mistake its builder is most
likely to make, which its proof must catch.

1. **UIFIT1b, changed — Settings' rows and cards use the pane** (the backlog joins it with UIFIT1d as UIFIT1). Absorbs
   UIFIT1c. R1's label track (`SettingList`, around `SettingRow`) with its bounds set from UIFIT1a's shots in both
   languages, and its 16 px card spacing in every Settings domain and Setup tab, R6's story widths for them, R1's and R6's
   notes in the platform language with their checks. *Trap:* changing the outer row and leaving the nested `justify-end`
   wrappers (`SetupParts.tsx:122`) and the full-span notice (`ui.tsx:1477`) as they are, so values still drift or notices
   sit under the label.
2. **UIFIT1d — Forms take the pane.** R1's form rule (`FormGrid`): the 40–48rem caps and the `md:` and `max-sm:` columns go,
   the heavy gates editor and *What it has* lay out by their own width, and R7's check holds it with its allow-list of the
   window's parts. *Trap:* deleting the caps and calling it responsive: the heavy gates' fixed `8rem` columns
   (`HeavyGates.tsx:158`) must yield to their container and stack by a rule of their own.
3. **CLAUDESIGNIN1b, changed — The sign-in panel says its phase and how long** (the backlog holds it inside SIGNIN1). The
   driver writes its own steps as phase events with their times, the panel draws them (§3.2), one builder end to end; it
   extends `Dot`, builds `Elapsed` over `useNow` and `span`, `PhaseList` and `SignInSteps`, and writes R2. *Trap:* moving the
   panel's word-matching (`SignIn.tsx:61-70`) into .NET and calling it the phases: a process's start and exit cannot tell
   the browser's states apart (`HarnessActions.cs:487-517`), so the browser's wait is one phase that ends with the tool.
4. **ACCESSASK1 — Access changes ask once and say a refusal where they were pressed.** Full access through the one
   `InlineConfirm` in its new warning tone, open until the driver answers; a pause's refusal under its row, a mode switch's
   under its chip, Driver's lead paragraph to its ⓘ; R3, whose census of toasting presses also brings an ask's *Apply* and
   review choice (`AsksPart.tsx:338-345`) to say theirs in place. *Trap:* swapping the component and keeping
   `useModeChoice`'s `enable`, which closes the confirm before the answer (`HelpModePicker.tsx:33-36`).
5. **SESSACCESS1 — A session says the access it runs under.** The head's chip by `isFull` over the record's pauses, and
   *Details*' pauses as recorded (§3.3). *Trap:* reading `access` as a word `full`, or resolving an old record against
   today's settings: it is the pause map the driver resolved at the start (`data/api.ts:433-438`), and the tests' fixture is
   that map.
6. **MILESTONEUX1, changed — Where a piece of work stands reads at a glance and opens in place.** The milestone's four facts
   marked by `Dot`'s tones, folded to `MilestoneLine` with *blocked* and *held* on it, the record head's budget measured again
   in a rendered browser and kept (§3.7); R4. *Trap:* marking an absent recovery done, losing *unknown* and *dropped*, or
   dropping the held attempts (`WorkMilestone.tsx:19-31`, `:98-105`) to fit four stages.
7. **HEAVYSLOT1 — A heavy run is seen running and waiting.** The driver records the gate running and each waiter, and answers
   the slot; the session's line and head and the repository's Details say it, a run from a terminal included (§3.5).
   *Trap:* parsing the run's output, or reading a holder's record left behind as a live hold: the handle is the lock
   (`HeavySlot.cs:46-52`, `:181-194`).
8. **MEETUX1a — A meeting is started from the window, its context read and sent.** The rail's kind, the composer (question,
   protocol, agents, repository), the page's head, phases and read-only packet, *Send*, *Stop*, a start and an end on each
   pass record, the bridge to the driver's meetings (§3.6); R5. After BRAINSTORM1c and AGENTROOM1b-c. *Trap:* drawing a
   meeting as a session to reuse the rail's selection and acts: the rail selects a bare session id and offers a session's
   acts (`SessionRail.tsx:46-58`); an attended item needs a typed identity, a session or a meeting.
9. **MEETUX1b — A meeting's rounds are followed on its page.** The transcript by round and turn, round 2's packet and *Send
   round 2* as an optional next act, the side bar's views. *Trap:* implying replies within a round: each turn answers the
   frozen packet, and another's message reaches it only in the next round (`DriverMeetings.cs:19-21`).
10. **MEETUX1c — A brainstorm's document is read on its page.** Coverage, ranked groups, flags, the challenge's claims and its
    presses, left out and needs, *Copy as Markdown*, *Attach to a new ask…*. After MEETUX1a and BRAINSTORM1e, which records
    the challenge and gives the driver *Challenge again* and *Rank it unchallenged*. *Trap:* drawing the challenge's claims
    or its presses before BRAINSTORM1e supplies them.
11. **MEETUX1d — A meeting that waits on the person is in What needs you, and Ask Daoris opens one.** Its rows, an agent's ask,
    the go door and its `HelpCoverageTests` row. After MEETUX1a and BRAINSTORM1d's caller, through which an agent's ask
    comes. *Trap:* building the door and leaving out the agent's request's life (held for the person's yes under `ask`,
    refused under `off`), which BRAINSTORM1d's caller owns.
12. **PUSHUX1 — Nothing reads as empty before it is known.** An unjudged pushed slice draws skeleton rows, a combined view
    waits for every input, a resync is read as its own state (§3.8). *Trap:* gating one slice: *What needs you* combines a
    dozen inputs, each empty by default today (`AttentionBand.tsx:159-162`).
13. **STORYGAP1 — Every screen since UX7 is in the catalogue at the frame's widths.** Stories for those with none that no row
    above draws (§1.2 item 6): `WorkspaceRecoveries`, `TeammateAskPage`, `AskReview`, `Conflicts`, `CommitGraph`, `AskLines`,
    `QuestStatus` (*submitted*, *source reopened*) and `AskWorkflowKind` at every width. *Trap:* taking §1.1's list as
    complete instead of the screens added since UX7, and drawing narrow at 34rem only: the floor is 400 px.

The backlog holds each in its own shape (contract by this document's section, proof); the steward files them with
UIDESIGN1's archive entry once the owner accepts the design.

## 10. What this document's gate does not cover

- **The window was not looked at.** Every wide-pane claim is read from the code and the frame's numbers; UIFIT1a's shots may
  show one cause outweighing the others, and R1 answers all three.
- **No count was taken.** UX6's measures (concepts, controls, words, screens) were not run on any story; MEETUX1a's and
  UIFIT1b's proofs take them before and after.
- **The driver's half is named by meaning, not by wire shape.** The read checked each wireframe against the records at
  `872520fa5`: what no record keeps is either built first by its row (the sign-in's phase events, the heavy slot's gate and
  waiters, a pass's start and end) or taken off the wireframe (where a session's access was set, a meeting's attachments
  and its packet's revision, a message's own time, a brainstorm's resumed passes). The fields' shapes are each row's.
- **The words are proposals** for D116's check. `verify` checks this document's place in the router and D185's shape; it
  checks none of the words, and no claim about what the code does beyond the lines cited.
