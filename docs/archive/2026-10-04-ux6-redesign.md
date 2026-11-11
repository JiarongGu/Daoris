# Daoris gets simpler as it grows: a setting on its thing, agents as products, and one place for what needs you

**Standing:** contract under D150, amended by D152. D150's dated notes record implementation;
`TASKS.md` holds the remaining surface, naming and installed proofs. §0 is the original review.

> UX6 and PLUGTOOL1, decided as **D150** (2026-10-04), before implementation. `Dnn` is `docs/decisions/Dnn.md`. The owner,
> in five messages the same day: *"this is confusing right now since I have logged 3 accounts in before and also there
> should be just one account management for Claude Code"* · *"you kind of need to improve the UI/UX since this is getting
> more and more complex, and we do need to add features later for coworking between agents"* · *"there probably will be
> more types of agents later too so the UI/UX redesign is needed"* · *"I saw the Azure CLI is in Tools, which should be in
> a plugin"* · *"the current permissions in Settings are mostly about repositories, and we probably should have a
> repository setup in the repository screen, since repository management (with git) is also planned"*.

It amends the platform language (D41 §2, §5), the frame (D56 §3b, D66, D75), the frame model (D118 §2), the dock design
(DOCK1a's side bar off Sessions), the tools (D121 §1, §4.1), the plugin manifest (D64 §3, D101), the built-in git's place
(D147 §6–§7) and where D130 §3.2 shows a workspace's accounts. §11 lists each.

- §0 is what was measured. §1 is the rule. §2 is the information architecture, §3 the move map.
- §4 is Repositories with its setup and its git, §5 the Agents place, §6 What needs you, §7 a plugin's tools.
- §8 is the room left for what is coming. §9 is what is measured and how. §10–§13 are what was rejected, what this
  amends, the build, and what this document's gate does not cover.

## 0. What was measured

**The install, as the owner left it** (中文, dark, 1546 × 1013, 29 repositories in two workspaces, three Claude Code
accounts). Eight screenshots of it were taken for this design. Two of them, labelled Sessions and Repositories, both
show Settings → Permissions, so those two views were read from their stories instead.

| Seen on the install | What it means |
|---|---|
| Settings → Permissions repeats one three-way control and one *Allow writing into…* menu for each of 29 repositories, under each workspace's default row | 122 controls in one card, about three screens of scroll, for a setting each repository holds alone |
| The side bar on Overview shows an ended session from the day before, its whole done note in italics, running past the window's foot | The side bar follows the remembered attended session on every view (DOCK1a), however long ago it ended |
| Quests opens on yesterday's done quest while two asks wait to be published | The list's remembered choice (`daoris.list.quests.chosen`) outlives the work it chose |
| Convergence opens on *this finding is no longer in the list* | The same rule, met by a finding the index no longer holds |
| Overview's *What needs you* lists the two asks, each *proposed, not yet published*, with *Open the ask to publish* | The row is a door, and the act is two screens away. Both asks sat until someone pressed through |
| Nothing anywhere says that two of the three accounts are signed out, or that the asks' intake waits on the third's cool-off | A start waiting for an account is not a row (TOOL4m designed it, unbuilt), and a signed-out account is said nowhere (TOOL6g) |
| The activity bar: Overview, Sessions, Quests, Repositories, Map, Convergence, Search, Plugins, then refresh, language and Settings | Nine places and two actions. D147 adds a tenth, Git, and an agents screen would add an eleventh |
| A plugin's page lists *What it needs* as its README's prose: *az, the Azure CLI, signed in with `az login`, and its devops extension* | Nothing checks it (the tools design §1). The tool itself is set in Settings → Tools, a screen away |

**The source** (main at `1bc5d3a3`) adds the rest:

- **One repository's setup is on three screens: four lists and its own page.** Its line, its session language and how its work lands
  are rows in three lists in Settings → Workspace (`Lines.tsx`, `Languages.tsx`, `Landings.tsx`), each a row per
  repository and per workspace. Reading and writing across is a fourth list, in Settings → Permissions (`Across.tsx`).
  Driving, hold, a tree per session, the standing answer and, again, the session language are on its page
  (`ProjectPage.tsx`). **The session language is set in two places.**
- **The terminal already groups them by repository**: `daoris driver drive|hold|trees|line|landing|across|standing|language
  <repo>`, each with `--workspace <name>` for the default. The screen scattered what the terminal kept together.
- **An agent's accounts are listed per adapter.** `byTool` groups the doors into one card, but the accounts behind them
  are per harness directory. The owner reads Claude Code's accounts twice, once per door, in two states (TOOL6g's
  finding on the install).
- **Settings holds eleven domains**: Setup, Appearance, AI features, Workspace, Driver, Agents, Tools, Permissions,
  Plugins, Browser, Machine log (`settings/domains.ts`). Four are about things that are not the machine or the person:
  repositories and workspaces (Workspace, Permissions), agents (Agents) and plugins (Plugins, leaving under PLUGUI1c).
- **The dock holds five views**: the timeline, the review and Ask Daoris in the side bar, the console and the terminal in
  the panel (`work/placements.ts`). Their placement is right; what the side bar follows off Sessions is not.

The counts behind each screen are §9.3.

## 1. The rule: a setting lives on the thing it is about

1. **A setting has one home, on its thing**: a repository's on the repository, a workspace's on the workspace, an
   agent's on the agent, a plugin's on the plugin. Anywhere else it is shown, it is read-only with a door to its home.
2. **Settings keeps what is the machine's or the person's**: the Daoris home, the driver's dials, the update, the tools
   Daoris itself runs, Daoris's browser, the machine log, AI features, appearance, and the first-start guide.
3. **Every move keeps its terminal twin** (D50). A setting that moves keeps the command it has today; §3 names each one.
   A read's twin is the command that answers it, shown to copy, as D147 §3.3 does for git.
4. **What Daoris decides folds; what the person decides shows.** A section of settings closes to one line naming its
   values, each at Daoris's default marked as such. A value the person set opens its section and carries its *Clear*.
   This answers UX6's *what the person must decide set apart from what Daoris can decide*.
5. **A thing is listed once.** An agent's accounts are one list. A setting is not repeated per repository on a page
   about something else. A row that stands for another (a waiting start that names its signed-out accounts) takes the
   other's act, and the other is not listed again (the attention band's rule, kept).
6. **A remembered choice ends with what it chose.** A view reopens its chosen item only while that item still waits on
   something. One that closed, ended or went opens the view with nothing chosen, never on the *gone* state, which stays
   for an item that disappears while it is open.

## 2. The information architecture

### 2.1 The places

| Today | After | Why |
|---|---|---|
| Overview | **Overview**, *What needs you* its lead (§6) | The one place for what needs the person, ahead of everything else |
| Sessions | Sessions | Unchanged (D55, D126) |
| Quests | Quests | Unchanged; an ask's publish is also on *What needs you* |
| Repositories | **Repositories**: workspaces, each holding its repositories; a workspace's page and a repository's page, each with its setup and its git (§4) | A repository's setup and its git on the repository |
| Git (D147 §6, planned) | Inside Repositories | One place for a repository, not two |
| Map | Map | Unchanged (D67 §3): it spans workspaces, and its canvas needs the area |
| Convergence, Search | **Knowledge**, one place whose list switches between *Search* and *Convergence* | Two views of one index, neither with an act |
| Settings → Agents | **Agents**, a place (§5) | An agent is a product with accounts, and more kinds are coming |
| Plugins | Plugins, each page gaining its tools (§7) | A plugin's tools on the plugin |
| Settings, eleven domains | Settings, seven (§2.3) | The machine's and the person's |
| Refresh and language on the bar's foot | The Daoris menu and the palette; the language also in Appearance | Rarely pressed, and each already has two other doors |

**Nine places, the same number as today, holding three more things** (Git, the agents, every repository's and
workspace's setup), and no actions on the bar. A tenth place is a decision with its own measure (§8), never a default.

```
 1546 px                                                                                          
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│ ▦ Daoris  Workspace  Agents  View          [ work · Ctrl K ]                   ◧ ▤ ◨   ─ □ ✕     │ strip
├──┬───────────────────────────────────────────────────────────────────────────┬───────────────────┤
│O②│ the view's list pane and main area (D118)                                 │ side bar:         │
│S │                                                                           │ Ask Daoris, and a │
│Q │                                                                           │ running or waiting│
│R │                                                                           │ session's views   │
│M │                                                                           │                   │
│K │                                                                           │                   │
│A①│                                                                           │                   │
│P │                                                                           │                   │
│  ├───────────────────────────────────────────────────────────────────────────┤                   │
│⚙ │ panel: console · terminal                                                 │                   │
├──┴───────────────────────────────────────────────────────────────────────────┴───────────────────┤
│ driver ● ready · 0 sessions · work · local only                       522 entries · lexical only │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
  O Overview · S Sessions · Q Quests · R Repositories · M Map · K Knowledge · A Agents · P Plugins · ⚙ Settings
```

The badges count what their place holds (U20): Overview every row of *What needs you*, Sessions its sessions waiting on
the person, Agents the accounts a list or a default holds that read *signed out*. All three wear open's hue. Quests'
count of outstanding quests stays a quantity, in the accent.

### 2.2 Knowledge

One place, *Knowledge* (知识, already a glossary term). Its list's head is a two-way choice, *Search · Convergence*,
remembered. Each mode keeps today's list, main area, filters and memory (D118 §3f, FRAME1f): Search's box, *each
repository's own only* and its hits; Convergence's similarity, its tier's note and its findings. A door into either
(the palette's *Go to Search*, an empty search's *Try Convergence*, Ask Daoris's go) opens the place in that mode. The
status bar's tier still leads to AI features.

### 2.3 Settings, after

```
 Settings                     
 ┌──────────────┬─────────────────────────────────────────────┐
 │ Get started  │  Driver                                     │
 │ Appearance   │  Daoris home     <path>                     │
 │ AI features  │  Plugins folder  <path>      [Open Plugins] │
 │ Driver       │  Notify me when a session parks       [■]   │
 │ Tools        │  Failures before a quest parks        [3]   │
 │ Browser      │  Cool-off when no time is named     [60 m]  │
 │ Machine log  │  Update     nothing staged                  │
 └──────────────┴─────────────────────────────────────────────┘
```

Seven domains. *Setup* is renamed **Get started** (its name in D97, and the code's id `start`), because *Setup* becomes a
repository's and a workspace's tab (§4) and one word names one thing (D116). The names check decides both words.
A browser is offered Get started, Appearance and AI features, as today, less Workspace (§4.3 gives a browser the
workspace's page, read-only).

### 2.4 The strip's menus and the status bar

The four menus stay (D75), and their items open the new homes:

| Menu | Items, after |
|---|---|
| Daoris | Settings · Driver · Tools · Plugins · ─ · Refresh the index · Switch language · ─ · About |
| Workspace | every workspace, the current one marked · ─ · Add repository… · Import a folder… · ─ · *This workspace's setup*, which opens its page at Setup |
| Agents | each agent, which opens its page · *Sign in to another account…* · *What agents may do* (Claude Code's page at that section) · *Proposals* (Overview at its rule rows) · Usage · AI features |
| View | unchanged |

The status bar is unchanged. Its index count still leads to Repositories and its tier to AI features.

### 2.5 The dock: what the side bar follows off Sessions

- **Off Sessions, a session's views follow the attended session only while it runs or waits on the person.** Once it has
  ended, they say nothing is attended there, and the side bar opens on Ask Daoris. On Sessions nothing changes. This
  amends DOCK1a only for an ended session; the side bar stays the frame's.
- **A note longer than four lines folds to them in the timeline**, with *Show all*. A long note is someone's words or
  the driver's account of a run; the conversation and the record hold it whole.

## 3. The move map

Every Settings domain, every card in it, and every place, with its home after and its terminal twin. Nothing is
dropped. *Read-only* means the value shows there, with a door to its home.

### 3.1 Settings

| Today | After | Terminal twin (unchanged) |
|---|---|---|
| **Setup**: the six steps, *Set up with Ask Daoris*, *Don't open at start* | Settings → **Get started**, as it is | each step's own commands |
| **Appearance**: theme, language | Settings → Appearance, as it is. The bar's language toggle folds here | none: the viewer's own (D66) |
| **AI features**: search and convergence's tier | Settings → AI features | the service's variables |
| AI features: the intake's agent, Ask Daoris's agent | Settings → AI features. A workspace's page shows its intake's agent and account read-only | `daoris driver intake <agent>\|off`, `daoris driver helper <agent>\|off` |
| **Workspace**: the list of workspaces and their repositories | Repositories' list, a group per workspace (§4.1) | `daoris status --machine`, `daoris connect --workspace W` |
| Workspace: *Wiring*, the remotes map, *Wire a workspace*, *Unwire* | The workspace's page → Setup → *Remote and reach* | `daoris remote list\|add\|remove` |
| Workspace: *What a start runs on* | The workspace's page → Details, read-only (it is a reading) | `daoris agent list` and `daoris driver list` name each part |
| Workspace: *Lines*, a workspace's row | The workspace's page → Setup → *Defaults* | `daoris driver line --workspace W <branch>\|--clear` |
| Workspace: *Lines*, a repository's row | The repository's page → Setup → *Work* | `daoris driver line <repo> <branch>\|--clear` |
| Workspace: *Session language*, a workspace's row | The workspace's page → Setup → *Defaults* | `daoris driver language --workspace W en\|zh\|--clear` |
| Workspace: *Session language*, a repository's row, and its twin on the repository's page | The repository's page → Setup → *Sessions*, once | `daoris driver language <repo> en\|zh\|--clear` |
| Workspace: *How work lands*, a workspace's row | The workspace's page → Setup → *Defaults* | `daoris driver landing --workspace W …` |
| Workspace: *How work lands*, a repository's row (form, pattern, plugin, accept automatically, tidy) | The repository's page → Setup → *Work* | `daoris driver landing <repo> merge\|branch <pattern> [--plugin] [--auto-accept] [--tidy]\|--clear` |
| Workspace: *Session branches* (the clean-up) | Repositories: a workspace's or a repository's page → Branches → *Clean up…* (§4.4) | `daoris-driver trees clean` |
| Workspace: *Updates* (bring up to date) | The same pages → Branches → *Bring up to date…* | `daoris-driver trees sync` |
| **Driver**: the home, notify, strikes, cool-off, the update | Settings → Driver, as it is, with PLUGUI1c's *Plugins folder* row | `daoris driver notify\|strikes\|cooloff`, `daoris-driver update` |
| **Agents**: every card | The Agents place (§5) | `daoris agent …`, every verb |
| Agents: *How accounts are used*, under each workspace (D130 §3.2) | The agent's page → *Workspaces*. The workspace's page shows it read-only | `daoris agent profile order\|use … --workspace W` |
| **Tools**: Git, Node.js, PowerShell | Settings → Tools, as it is | `daoris tool …` |
| Tools: GitHub CLI, Azure CLI | The page of each plugin that declares it (§7); Tools lists one only while no installed plugin declares it and a way or a download is kept | `daoris tool use\|download\|update\|delete gh\|az` |
| Tools: *Resource locations* | Settings → Tools, as it is | `daoris tool locations`, `daoris tool look` |
| **Permissions**: *Reading and writing across*, a workspace's row | The workspace's page → Setup → *Defaults* | `daoris driver across --workspace W read on\|off\|--clear` |
| Permissions: *Reading and writing across*, a repository's row and its writes | The repository's page → Setup → *Reach* | `daoris driver across <repo> read …`, `… write-to <other>` |
| Permissions: Claude Code's rules for the machine, and Daoris's defaults | The agent's page → *What it may do* | `daoris agent rules`, `daoris agent rules default <id> on\|off` |
| Permissions: rules for a workspace, and for a repository | The workspace's page → Setup → *Remote and reach*; the repository's page → Setup → *Reach* | `daoris agent rules … --workspace W`, `… --repository R` |
| Permissions: *Proposals* | Overview → What needs you, its rule rows; the agent's page → *What it may do* | `daoris agent rules proposals\|accept\|decline <id>` |
| **Plugins** | The Plugins place (PLUGUI1c, D119 §5), as designed | `daoris plugin …`, `daoris-driver plugins new\|try` |
| **Browser** | Settings → Browser, as it is | `daoris browser …` |
| **Machine log** | Settings → Machine log, as it is | `daoris-driver logs` |

### 3.2 Places and the repository's page

| Today | After | Terminal twin |
|---|---|---|
| The repository's page: index, fed from, workspace, line, unlanded branches, declaration, *Open code map*, *Manage* | The repository's page → Details | `daoris status`, the registry's own commands |
| The repository's page: *Drive on this machine*, *Hold*, *A tree per session* | → Setup → *Driving* | `daoris driver drive\|undrive\|hold\|resume\|trees <repo>` |
| The repository's page: the standing answer | → Setup → *Sessions* | `daoris driver standing <repo> "…"\|--clear` |
| Convergence, Search | Knowledge, in its two modes | the service's own reads |
| The bar's refresh | The Daoris menu's *Refresh the index*, the palette | none, as today |
| The bar's language toggle | Appearance, the Daoris menu, the palette | none, as today |
| Git's list and pages (GIT1e, GIT1f) | Repositories' Branches and History tabs (§4.4) | `daoris-driver git …` (D147 §3.3) |

### 3.3 The dock

| View | After |
|---|---|
| Timeline, review | The side bar, as today. Off Sessions they follow a running or waiting session only (§2.5); a long note folds |
| Ask Daoris | The side bar, as today; it opens there when no session is followed |
| Console, terminal | The panel, as today |

## 4. Repositories: a repository's setup and its git on the repository

### 4.1 The list

The list is grouped by workspace, always, since a workspace is always named (D75 §4). A workspace's row heads its group
and opens the workspace's page. Under it, its repositories as today: the adopted, then *Registered, not adopted*, each
row its name, its standing on this machine (*drives here*, *held*, *not on this machine*) and its one line. A row that
holds Daoris's branches says how many (*3 branches of Daoris's*). Past twelve repositories the list gains a filter box,
as the map gained a search past twelve (MAP4a). `＋` is *Add repository…*, and the ⋯ holds *Import a folder…*, *Fetch
all*, *Use Daoris's own Git…* and *Every repository* (D147 §6's list menu).

### 4.2 A repository's page

The header names it and its standing, its summary as its line, and its acts: *Open code map* and *Manage* (a drawer,
D118 §3d). A session's *Show in Git* (D147 §2.7) opens this page at Branches, its branch chosen. Beneath, four tabs, the
chosen one remembered per view:

- **Details**: what the page holds today, read-only where a value is set on Setup.
- **Branches**: its lines and branches by kind (D147 §2.2 for one repository), with *Create a branch…*, *Fetch*, *Clean
  up…* and *Bring up to date…*.
- **History**: its line's graph (D147 §2.3); a commit opens beside it or in its place.
- **Setup**: every setting the repository holds, in four sections, each folding to its summary (§1 rule 4):

| Section | Rows | Folded, it reads |
|---|---|---|
| **Driving** | Drive on this machine · Hold · A tree per session | *driven here · a tree per session · not held* |
| **Work** | Its line · How work lands (merge or branch, the pattern, the plugin, accept automatically, clean up once landed) | *line `main` (the workspace's default) · lands on `feature/{quest}-{slug}`, pushed by GitHub pull request* |
| **Sessions** | The language its sessions write in · The standing answer | *English (Daoris's default) · a standing answer of 3 lines* |
| **Reach** | Read by agents outside it · Its sessions also write into · Claude Code's rules here | *read (the workspace's default) · writes into nothing else · 2 rules* |

Each row is a `SettingRow` with its terminal twin as its hint (D41 §4). A value from the workspace says so and offers
*Set for this repository*; one set here offers *Clear*, which returns it to the workspace's. A browser gets Details alone
(D47 §4): branches, history and setup are this machine's.

```
 1546 px, Repositories, a repository chosen, side bar open                                         
┌──┬──────────────────────────┬────────────────────────────────────────────────────────────┬────────────┐
│R │ Repositories       + ⋯ ⟨ │ engine  drives here         [Open code map] [Manage] ⋯     │ Ask Daoris │
│  │ [ filter             ]   │ The runtime half of the example family.                    │            │
│  │ ▾ work · 27              │ Details   Branches   History   Setup                       │            │
│  │   engine     drives here │ ────────────────────────────────────────────────────────── │            │
│  │     3 of Daoris's        │ Driving   driven here · a tree per session · not held  ▸   │            │
│  │   game       held        │ Work      line main (the workspace's default) ·         ▾  │            │
│  │   tools      not here    │   Its line          main   daoris driver line engine       │            │
│  │   Registered, not        │   How work lands    [Merge|Branch] feature/{quest}-{slug}  │            │
│  │   adopted (20)         ▸ │                     [GitHub pull request ▾]                │            │
│  │ ▸ forge · 2              │                     □ Accept automatically  □ Clean up     │            │
│  │                          │                                               [Save]       │            │
│  │                          │ Sessions  English (Daoris's default) · a standing     ▸    │            │
│  │                          │           answer of 3 lines                                │            │
│  │                          │ Reach     read (the workspace's default) · writes      ▸   │            │
│  │                          │           into nothing else · 2 Claude Code rules          │            │
└──┴──────────────────────────┴────────────────────────────────────────────────────────────┴────────────┘
```

```
 680 px: the list a strip (D118 §3a), the side bar closed       
┌──┬───┬──────────────────────────────────────────┐
│R │ ≡ │ engine   drives here               ⋯     │
│  │ + │ The runtime half of the example family.  │
│  │   │ Details  Branches  History  Setup        │
│  │   │ ──────────────────────────────────────── │
│  │   │ Driving                                ▸ │
│  │   │  driven here · a tree per session ·      │
│  │   │  not held                                │
│  │   │ Work                                   ▾ │
│  │   │  Its line                                │
│  │   │  daoris driver line engine               │
│  │   │  main                         [Clear]    │
│  │   │  How work lands                          │
│  │   │  [Merge|Branch]                          │
│  │   │  feature/{quest}-{slug}                  │
│  │   │  □ Accept automatically  □ Clean up      │
│  │   │                               [Save]     │
│  │   │ Sessions                               ▸ │
│  │   │ Reach                                  ▸ │
└──┴───┴──────────────────────────────────────────┘
```

Below 560 px of main area the header's acts take their own line, as a session's page header does (D126 §3.2), and a
row stacks below 26 rem (WSR4). The tabs are four short words in both languages (详情 · 分支 · 历史 · 配置), whole at
every width (TABS1).

### 4.3 A workspace's page

The header names the workspace, how many repositories it holds and where it syncs (*local only*, or its deployment's
host), with *Wire to a remote…* or *Sync now* as its act. Three tabs:

- **Details**: its repositories as doors; *What a start runs on*, a row per job, each part with the setting that chose
  it, a blocked start's sentence whole (today's card, moved); the accounts each agent may run here, read-only with a door
  to the agent's *Workspaces*.
- **Branches**: D147 §2.2's list across its repositories, Daoris's branches first, with *Fetch all*, *Clean up…* and
  *Bring up to date…* (today's two cards, moved).
- **Setup**, in two sections:

| Section | Rows | Folded, it reads |
|---|---|---|
| **Defaults**, for each of its repositories that sets none of its own | Line · How work lands · Session language · Read by agents outside | *line `main` · lands into the line · English · read* |
| **Remote and reach** | The deployment it syncs with (*Wire…*, *Unwire*) · Claude Code's rules for the workspace | *local only · no rules of its own* |

A browser gets the page with what it may know (D47 §4): its repositories and its remote's name. No branches, no
setup, no account.

### 4.4 Git, inside Repositories

D147's lists and pages are kept whole. What moves is where they live:

- **The list** (D147 §2.2) is a workspace's Branches tab across its repositories, and a repository's Branches tab for
  one. Its order is D112's: those holding Daoris's branches first.
- **A branch, a commit, a file's history and blame, a compare** (§2.3–§2.6) open in the main area, each a record (D118
  §3d), with a way back to the tab it came from in its header (*engine › feature/x*).
- **The acts** (§3) are on the pages that show what they act on. *Create a branch…* is on Branches, *Push* and *Delete*
  on a branch's page.
- **The managed Git offer** (§5) is in the list's ⋯ and the Branches tab's head.
- **Nothing else of D147 moves**: one git process per view, the cache by commit id, no checkout, a push only on the
  person's press, both doors.

D147 §7 rejected *Git inside Projects, as a part of a repository's page*: a graph beside a commit needs the main area
whole, and a repository page with history in it would be two pages in one. The tabs meet both reasons. History is its
own tab with the main area to itself, and each tab is one page. What D147 kept apart was a page, not a place.

## 5. Agents: a product, one account list, its doors a property

### 5.1 The place

**The list** is the agent products this machine knows: those the build carries (Claude Code, Codex, DeepSeek's dsh) and
those a plugin declares (D64), each once, whatever doors reach it. A row is its name and maker, whether it is
installed, and its accounts in a phrase: *3 accounts · 2 signed out*, *1 account*, *not installed*. A row with an
account the person must act on wears the waiting mark. The list has no `＋`: an account is made on its agent's page, and
an agent arrives by its own installer or a plugin (D64). The ⋯ holds *Show agents not installed*.

**Adding an agent adds a row.** The page is one template, and a section appears only where the agent has that concept:
accounts by sign-in or by key, rules only where Daoris hands the agent a rules file, model and effort only where Daoris
knows the tool's settings (AGT6), as many doors as reach it.

### 5.2 The agent's page

The header names the product, its maker and its version, with *Installed* or *Not installed*, and its acts: *Sign in to
another account…*, then ⋯ (*Update…*, *Pin a version…*, *Unpin*, *Install…*). Then the sections:

| Section | What it holds | Folded, it reads |
|---|---|---|
| **Accounts** | One list, whatever doors reach the agent: each account, its state (§5.3) with when it was read, the workspaces that may run on it, what runs on it now, what its agent last said; its acts on its row | never folded |
| **How accounts are used** | The machine's list and *Use accounts*, *Keep for conversations*, *Switch before the limit* (D130 §16.6) | *Make the most of them · switch before the limit* |
| **Workspaces** | Each workspace: *This machine's accounts* or *Its own accounts*, and its list (D130 §3.2, moved from Settings → Agents) | *work: its own (account-2, account-1) · forge: this machine's* |
| **Ways in** | Each door Daoris reaches it over (native, protocol), its version, whether pinned, which one driven work starts on (`driver.json`'s adapter), *Update* and *Pin* on its ⋯ | *native 2.1.4 for driven work · protocol 0.9.1* |
| **What it may do** | Claude Code's rules for every session on this machine, Daoris's defaults, the proposals | *Daoris's 4 defaults on · 1 rule of yours · 1 proposal* |
| **Model and effort** | Per account, the tool's own settings (AGT6) | *the tool's own defaults* |
| **Usage** | What each account has carried (TOOL3), absent never zero (D57) | *since 1 October* |

**A door is a property of the agent, never a second roster** (D53, D57 §c). The accounts are the agent's. A door says
how Daoris holds a session on it. TOOL6g makes the data one list, and this page is where it lands.

```
 1546 px, Agents, Claude Code chosen                                                                
┌──┬────────────────────────┬────────────────────────────────────────────────────────────────────────┐
│A①│ Agents             ⋯ ⟨ │ Claude Code   Anthropic · 2.1.4 · Installed                            │
│  │ ● Claude Code          │                                 [Sign in to another account…]  ⋯       │
│  │   3 accounts ·         │ ────────────────────────────────────────────────────────────────────── │
│  │   2 signed out         │ Accounts                                  read 10:42 · [Read again]    │
│  │   Codex                │  account-1  you@work…  ● signed out · read 10:42     [Sign in]   ⋯     │
│  │   1 account            │             work and forge may run on it                               │
│  │   dsh                  │  account-2  you@home…    cooling until 6 Oct 04:42   [Try now]   ⋯     │
│  │   not installed        │             work may run on it · "your session limit resets …"         │
│  │                        │  account-3  spare@…    ● signed out · read 10:42     [Sign in]   ⋯     │
│  │                        │  The tool's own sign-in  unknown · never read        [Read]      ⋯     │
│  │                        │ How accounts are used   Make the most of them · switch before …  ▸     │
│  │                        │ Workspaces              work: its own (account-2, account-1) ·   ▸     │
│  │                        │                         forge: this machine's                          │
│  │                        │ Ways in                 native 2.1.4 for driven work · protocol  ▸     │
│  │                        │ What it may do          Daoris's 4 defaults on · 1 proposal      ▸     │
│  │                        │ Model and effort        the tool's own defaults                  ▸     │
│  │                        │ Usage                   since 1 October                          ▸     │
└──┴────────────────────────┴────────────────────────────────────────────────────────────────────────┘
```

```
 680 px                                         
┌──┬───┬──────────────────────────────────────────┐
│A①│ ≡ │ Claude Code   Anthropic · 2.1.4          │
│  │   │ Installed                                │
│  │ ● │ [Sign in to another account…]   ⋯        │
│  │ C │ ──────────────────────────────────────── │
│  │ d │ Accounts           read 10:42            │
│  │   │                        [Read again]      │
│  │   │ account-1  you@work…                     │
│  │   │ ● signed out · read 10:42                │
│  │   │ work, forge may run on it                │
│  │   │              [Sign in]  ⋯                │
│  │   │ account-2  you@home…                     │
│  │   │ cooling until 6 Oct 04:42                │
│  │   │              [Try now]  ⋯                │
│  │   │ account-3  spare@…                       │
│  │   │ ● signed out · read 10:42                │
│  │   │              [Sign in]  ⋯                │
│  │   │ How accounts are used                  ▸ │
│  │   │ Workspaces                             ▸ │
│  │   │ Ways in                                ▸ │
│  │   │ What it may do                         ▸ │
└──┴───┴──────────────────────────────────────────┘
```

An account's ⋯ holds *Use by default*, *Model and effort…*, *Sign in again*, *Read again* and *Remove…*, which asks once
and deletes the account's directory (D66 §3). A key account says its handle and has no sign-in.

### 5.3 An account's state is what was last known, and when

| State | When | Hue |
|---|---|---|
| **signed in** | The last read said so | neutral |
| **signed out** | The last read said so, a start was refused for it (AGT3b), or a sign-in did not finish | open's, when a list or a default holds the account (it holds work); neutral otherwise |
| **cooling until …** | Its agent named a limit (D125 §2), until the time it named | neutral, with *Try now* |
| **unknown** | Never read, or the last read failed | neutral, saying which |

Each state says when it was read (*read 10:42*, *never read*). **Absent is never zero** (D57): an account no read has
answered is *unknown*, never *signed in*.

🔴 **An account is never probed on a timer, at a look, at the application's start, or when a view opens.** A probe of
a login state starts the agent's own tool against that account's home, and probing on its own is what signed the
owner's accounts out (TOOL6g's first reading: two probes racing one refresh token). A state is read only:

- on the person's press of *Read again*, for one account or every account of the agent, **one at a time**, never two
  against one home;
- as the result of something that ran: a start's refusal, a sign-in's end, a key added.

A version may be read when the page opens: it is asked of the binary, never against an account's home. A start that
waits reads nothing: it is named from cool-offs and refusals alone (D125 §4).

## 6. What needs you: one place, ahead of everything else

### 6.1 Where it is

**Overview's lead.** The band becomes the page's first content, above the tiles, which stay as they are with the
outstanding quests and the repositories by index size beneath. With nothing waiting, it is one line, *Nothing needs
you*, and what is running (*2 sessions working*), never a card saying all clear. The badge on Overview counts its rows,
and Sessions and Agents keep theirs (§2.1). The asks, quests, sessions and accounts it lists still show where they live;
it is the one place that lists all of them.

### 6.2 What it lists, in three groups

| Group | Kind | Read from | On the row | Its door |
|---|---|---|---|---|
| **Holding work** | A start waiting for accounts | the tick's `starts.waiting`, the cool-offs, the last known states | the accounts that would serve it, and *Sign in* for each signed-out one, or *Let account-N run work…* (D130 §3.3), asking once | the agent's page |
| | A signed-out account a list or default holds, not named by a waiting start | the last known state | *Sign in* | the agent's page at that account |
| | A parked session | the session | *Answer…* (a door: its question needs reading) | Sessions |
| | A quest parked on its failed sessions | the planner's verdicts (SESSUX1i) | *Try again*, saying it starts a session | the quest's page |
| | A go-ahead asked on an ask (KNOWUSE1a) | the ask's go-aheads | *Allow…* and *Refuse…*, each asking once, saying what every session on the ask is then handed | the ask's page |
| | A folder waiting on trust (D73) | the tick | *Trust this folder…*, asking once | the quest's page |
| | A plugin a landing rule names that cannot land (D100's reasons) | the plugin's health | its sentence | the plugin's page |
| **Waiting for your word** | An ask proposed and not published | the asks | *Publish to engine, game* where its declarations named receivers; else *Choose where it goes…* | the ask's page |
| | An ask its intake asked about | the intake's park | *Answer ask #id* (a door) | the ask's page |
| | A departure that waits for your yes (DRIFT1d2) | the quest | *Accept the departure* | the quest's page |
| | A proposal to widen the rules (D74) | the rules file | *Apply…* (asking once) and *Decline* | the agent's *What it may do* |
| | A quest nobody here can take | the registry | none | the quest's page |
| **Ready for you** | Work to review | the session list's *To review* (D126) | *Review* (a door: accepting needs looking, D113) | Sessions, its review open |

Within a group, what has waited longest comes first. A group with no rows is not shown. *Ready for you* shows five rows
and then *N more in Sessions*, since review can be many. The band's foot sentence (*finished work you have not looked
at is not listed here*) goes: work to review is now a row.

### 6.3 How a row reads and acts

- **A row reads `kind · what · where · how long`, then one line of why**: the agent's question, the driver's sentence, or
  the requirement and the departure's reason, two lines at most and whole in its record (D41 §4 *status leads*). Its kind
  wears open's hue: it waits on the person.
- **An act is on the row only where one press is safe and the row says what it does.** One that widens what Daoris
  may do, or removes something, asks once under the row (D41 §4). One that needs reading first is a door, never an act.
  The row's acts and its record's acts are one judge, so they cannot differ.
- **No row starts a process to find out.** Every fact is one the page already has or the tick hands it: sessions,
  quests, asks, verdicts, holds, the rules file, the accounts' last known states. No probe, no fetch, no query to a
  platform.
- **One thing is listed once.** A start waiting only on signed-out accounts carries their *Sign in*, and those accounts
  are not rows of their own. A parked intake is its ask's row (today's rule).
- **What is not known is said.** An account the row names that no read has answered is *unknown, never read*, with
  *Read* for that one account (§5.3). The row never guesses it signed in or out.
- **A browser** shows the rows it can know, asks and quests, with doors only where they exist (today's rule).

```
 1546 px, Overview                                                                                   
┌──┬────────────────────────────────────────────────────────────────────────────────┬──────────────┐
│O④│ Overview                                                                       │ Ask Daoris   │
│  │ What needs you                                                            4    │              │
│  │ Holding work                                                                   │              │
│  │ ● waits for an account   intake for 2 asks · work · 25 min                     │              │
│  │   account-2 cools until 6 Oct 04:42; account-1 and account-3 read signed       │              │
│  │   out at 10:42.     [Sign in account-1]  [Sign in account-3]  Claude Code ›    │              │
│  │ Waiting for your word                                                          │              │
│  │ ● proposed   Continue the production half of the ticket · work · 25 min        │              │
│  │   Its declarations propose engine. Nothing is published until you choose.      │              │
│  │                                    [Publish to engine]  Choose…  Open ›        │              │
│  │ ● proposed   Fix the pipeline's knowledge check · work · 23 min                │              │
│  │   Its declarations propose engine. [Publish to engine]  Choose…  Open ›        │              │
│  │ Ready for you                                                                  │              │
│  │ ● to review  Verify the drill-down against DEV · engine · 1 d    [Review]      │              │
│  │ ────────────────────────────────────────────────────────────────────────────── │              │
│  │ [Adopted 1 of 29]  [Open quests 0]  [Taken 0]  [Entries 522]                   │              │
│  │ Outstanding, oldest first           │ Repositories by index size               │              │
└──┴────────────────────────────────────────────────────────────────────────────────┴──────────────┘
```

```
 680 px                                         
┌──┬───────────────────────────────────────────────┐
│O④│ Overview                                      │
│  │ What needs you                            4   │
│  │ Holding work                                  │
│  │ ● waits for an account · work · 25 min        │
│  │   intake for 2 asks                           │
│  │   account-2 cools until 6 Oct 04:42;          │
│  │   account-1, account-3 signed out (10:42)     │
│  │   [Sign in account-1]                         │
│  │   [Sign in account-3]          Claude Code ›  │
│  │ Waiting for your word                         │
│  │ ● proposed · work · 25 min                    │
│  │   Continue the production half of the ticket  │
│  │   [Publish to engine]  Choose…  Open ›        │
│  │ ● proposed · work · 23 min                    │
│  │   Fix the pipeline's knowledge check          │
│  │   [Publish to engine]  Choose…  Open ›        │
│  │ Ready for you                                 │
│  │ ● to review · engine · 1 d                    │
│  │   Verify the drill-down        [Review]       │
└──┴───────────────────────────────────────────────┘
```

On the owner's install that page would have said, the morning it was asked, both things nobody saw: the asks waiting
for a press, and the intake waiting behind one cooling account while two others read signed out.

## 7. A plugin brings the tools it needs (PLUGTOOL1)

### 7.1 What changes

Azure CLI and GitHub CLI sit in Settings → Tools (D121 §1) beside Git, Node.js and PowerShell, though only the two
pull-request plugins run them (the tools design §1). After this, **a plugin's manifest declares the tools its process
runs; its page shows and manages them; Settings → Tools keeps what Daoris itself runs.** The way a tool is run stays
D121's: one way per tool for the machine, in `tools.json`, whoever declares it, and every child, a plugin's process or
an agent's session, gets the tools' environment (D121 §3, unchanged). What moves is who declares a tool and where its
control lives.

### 7.2 The schema

`plugin.json` gains `tools`, an array:

```json
{
  "id": "azure-devops-pull-request",
  "apiVersion": 1,
  "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] },
  "tools": [
    { "id": "node", "versions": ">=22" },
    { "id": "git", "versions": ">=2.29" },
    {
      "id": "az",
      "versions": ">=2.60",
      "for": "Pushes the branch and opens its pull request.",
      "ready": [
        { "run": ["az", "extension", "show", "--name", "azure-devops", "--output", "none"],
          "says": "Its devops extension is added", "fix": "az extension add --name azure-devops" },
        { "run": ["az", "account", "show", "--output", "none"],
          "says": "Signed in", "fix": "az login" }
      ]
    }
  ]
}
```

| Field | Rule |
|---|---|
| `id` | Required. A tool id. **Daoris's own** (`git`, `node`, `pwsh`): its way stays Daoris's, set in Settings → Tools. **One Daoris knows and does not run itself** (`gh`, `az`, D121 §1): its way is set on the page of the plugin that declares it, and the lists offer its versions. **Any other**: needs `command`, and can be found but never downloaded (§7.6) |
| `name` | What a person calls a tool Daoris does not know. A known id takes its name from Daoris |
| `command` | The file to find for a tool Daoris does not know; the id when absent. A known id takes its file from its way |
| `versionArguments` | For a tool Daoris does not know: what prints its version. Absent, its version is not read and the range is not checked |
| `versions` | A range: `>=2.60`, `>=2.60 <3`, or one exact version. Absent means any. Outside it, the page says so (*needs 2.60 or newer; this is 2.55*). It never refuses the plugin |
| `for` | One sentence: why the plugin runs it. Content, shown as written |
| `ready` | At most four checks. Each `run` is a command whose exit 0 means ready, `says` what that means, and `fix` the command a person runs when it is not. Daoris never runs `fix` |

- **Both twins read `tools` by one table** (the CLI's `plugins.ts`, the driver's `PluginCatalog`), as D140 holds
  `icon`. **A problem in `tools` never refuses the plugin**: it is the tool row's sentence on the page, as an icon's
  problem is. `apiVersion` stays 1, since a field is additive, as `icon` was (D140).
- **A check runs only on the person's press** (*Check*, on the plugin's page) **and at *Try***, one at a time, with ten
  seconds each, its output kept with the trial (D119 §3.2, Tests). Never on a timer, at a look, or before a landing:
  a check may reach the platform or read another tool's sign-in, and that is the person's to ask for.
- **The kit checks it** (D101): `daoris-driver plugins try` says each tool's problem in its own sentence, and
  `plugins new` writes `tools: []` with the README's line on how to fill it.

### 7.3 Where a declared tool is found

Unchanged from D121 §3: the way set in `tools.json`, else the system's `PATH`. A known tool that is managed or a file
joins every child's `PATH` as today, so an agent's session that runs `gh` finds the same one. A tool Daoris does not know is
looked up by its `command` on the tools' `PATH` and the agents' resolver, as a hook's first word is (D121, amending D64).

### 7.4 The two pull-request plugins

- **Azure DevOps pull request** declares Node.js, Git and Azure CLI, with the devops extension and the sign-in as its
  checks, as above.
- **GitHub pull request** declares Node.js, Git and GitHub CLI, with `gh auth status` as its check, *fix* `gh auth login`.

The version floors above are illustrative. PLUGTOOL1b sets each from the flags the plugin's own calls use, and each
README's *What it needs* points at the manifest instead of listing the tools in prose. Built: D150's PLUGTOOL1b note.

### 7.5 On the plugin's page, and in Tools

The page gains a section, **Tools** (工具), after Points:

```
 Tools                                                                              
  Azure CLI   2.66.0 · the system's          needs 2.60 or newer   [System|Managed|Custom]
              Pushes the branch and opens its pull request.                       
              ✓ Its devops extension is added   ✗ Signed in   checked 10:40  [Check]
              az login                                                    (to copy)
  Node.js     24.21.0 · managed     Daoris's own tool, set in Settings › Tools  
  Git         2.53.0 · the system's Daoris's own tool, set in Settings › Tools  
```

- A known tool's row carries today's Tools card controls (D121 §4.1): the way as one choice, nothing applied on choosing,
  a download followed on the row with *Stop download*, *Delete* asking once.
- A tool two installed plugins declare shows on both pages and says *also used by …*. Its way is one.
- **A plugin whose tool is missing or out of range says so in its health line** (D119 §2), and, where a landing rule
  names it, on *What needs you* (§6.2). The landing itself still answers with its own sentence (D100).
- **Settings → Tools** keeps Git, Node.js and PowerShell, then *Kept for no plugin*: a known tool no installed plugin
  declares that still has a way set or a version downloaded, so a removed plugin's download can be deleted. Nothing is
  lost.
- `daoris tool list` names which plugin declares each tool. Every `daoris tool` verb is unchanged.

### 7.6 What a plugin cannot do

A plugin cannot make Daoris download a program. Downloads come from the lists D121 §5 merges, the one built in and the
locations the person adds, and only for a tool Daoris's code names. A plugin names a tool; it does not carry a URL or a
hash. A tool Daoris does not know is the system's or a file the person names.

## 8. Room for what is coming

- **Coworking between agents (COWORK1, held for the owner).** It is about work, so it lives where work is: Sessions (a
  session's relations, the `follows` chain of D149), Quests (a chain) and *What needs you* (a row kind, only where it
  waits on the person). No place is reserved for it. If it needs one, the bar's tenth place is a decision with its own
  counts (§9).
- **More agent kinds.** A row in Agents, the page's sections appearing by what the agent has (§5.1). A plugin-declared
  agent is a row with *declared by …*, its door to the plugin's page.
- **Git as a place.** Inside Repositories (§4.4). If a person ever needs every workspace's branches in one graph, it
  grows from a workspace's Branches tab before it becomes a place.
- **More plugins with tools.** The schema of §7.2, and Tools' *Kept for no plugin* group.

## 9. What is measured

### 9.1 The measures

For each screen, in its own regions (the view's list pane and main area; the frame is counted once, apart):

| Measure | Definition |
|---|---|
| **Concepts** | The distinct glossary terms whose name appears in the screen's visible text: a term's `match` in English, its `zh` name in Chinese (`glossary.json`, the names check's own source) |
| **Controls** | The visible, innermost interactive elements: buttons, links, fields, select triggers, checkboxes, switches, segments, tabs, sliders, menu triggers. A closed menu's items and a folded section's rows are not counted: they are one press away |
| **Words** | `Intl.Segmenter` word-like segments of the visible text, in the screen's language; English and 中文 reported apart, never summed |
| **Screens** | The main area's scroll height over its visible height, at 1546 × 1013 |
| **Presses** | For a task in §9.4, the presses from the screen a person starts on to the act done, and the rows they scan to find it |

### 9.2 The method

One function counts a page's regions. It runs in two places:

1. **Storybook**, over a list of story ids, through Playwright, at 1546 and 680 px, in English light and 中文 dark. A
   build row proves its own surface's counts before the window, and a story's fixture size is said beside its count.
2. **The window**, through `npm run desktop -- eval` against the install started with `--install`, at the install's
   real state. That is where *before* and *after* are judged (D62).

UX6a makes it `tools/ux-count.mjs`, with a test over a fixed page. This design's numbers were taken with a scratch copy
over the stories at `1bc5d3a3`, and scaled to the install's 29 repositories and two workspaces by each list's per-row
cost (a story with rows, less its empty story, over its rows). UX6a's first job is to take them again on the install.

### 9.3 Before and after

English counts. *S* is counted from stories, *I* from the install's screenshots, *E* estimated from the source where no
story renders the screen whole, and *×31* scaled to the install's 29 repository and 2 workspace rows.

| Screen | Before | Concepts | Controls | Words | Screens | After (budget) |
|---|---|---|---|---|---|---|
| Activity bar | I | 11 | 11 | — (tips) | — | 9 places, 9 controls |
| Settings' list | I | 11 | 11 | 13 | — | 7, 7 |
| Settings → Permissions | I (controls), S×31 | 22 | 132 | ≈ 970 | ≈ 3 | retired |
| Settings → Workspace | S×31 | ≈ 30 | ≈ 290 | ≈ 2,000 | ≈ 7 | retired |
| Settings → Agents, Claude Code alone | E | ≈ 20 | ≈ 55 | ≈ 900 | ≈ 2 | → an agent's page, below |
| Settings → Tools, five tools | S×5 | 9 | ≈ 18 | ≈ 260 | ≈ 1 | three tools: ≤ 9, ≤ 12, ≤ 200 |
| A repository's page (driven) | S | 16 | 5 | 91 | < 1 | Details: ≤ 16, ≤ 6, ≤ 120 |
| **One repository's whole setup** | S×31 | its rows in 4 lists and its page, on 3 screens | 18 of ≈ 430 on those screens | — | ≈ 10 across them | Setup: ≤ 14, ≤ 8 folded and ≤ 22 open, ≤ 300, on one screen |
| A workspace's defaults | S | the head rows of 4 lists and the wiring, on 2 screens | ≈ 14 | — | — | Setup: ≤ 12, ≤ 6 folded and ≤ 18 open, ≤ 250 |
| An agent's page, three accounts | — | — | — | — | — | ≤ 14 concepts, ≤ 20 controls folded (≤ 45 open), ≤ 350 words, each account once |
| Overview | I | ≈ 12 | 14 | — | ≈ 1.3 | needs-you leads; the rest unchanged |
| *What needs you*, a row | S (10 rows) | — | 0.9 | 42 | — | ≤ 3 controls, ≤ 30 words |
| A plugin's page (a landing plugin) | S | 19 | 5 | 135 | < 1 | with Tools: ≤ 22, ≤ 12, ≤ 230 |
| Knowledge | — | two places | — | — | — | one place, the two lists unchanged |

The per-row costs behind *×31*: *Lines* 2.8 controls and 12 words a row, *Session language* 1.3 and 16, *How work lands*
5.2 and 18, *Reading and writing across* 4 a repository and 3 a workspace (counted on the install) and 22 words. Each
repository's 18 controls are 13 in Settings' four lists and 5 on its page.

**Measured on the install, 2026-10-07 (UX6a2).** `tools/ux-count.mjs --window` over each place as it opens, on the
owner's install at its real state: 1314 × 828, dark, 中文 and then English (the language toggled and put back). Build
`0a00cdb`. The install's quest and session history had been cleared by hand that morning, so Overview, Sessions and
Quests count empty lists; their *before* is not a working day's. The frame, every place: 13 concepts, 43 controls, 60
words in English (14, 44, 72 in 中文), of which the activity bar is 12 controls.

| Place (main area) | Concepts | Controls | Words (en · 中文) | Screens | List: rows, controls |
|---|---|---|---|---|---|
| Overview | 12 | 2 | 97 · 184 | 1 | — |
| Sessions | 4 | 1 | 27 · 28 | 1 | 0 rows, 3 |
| Quests | 6 | 2 | 49 · 56 | 1 | 0 rows, 5 |
| Repositories (first row's page) | 14 | 5 | 352 · 360 | 1.35 | 2 rows, 8 |
| Map | 13 | 26 | 177 · 197 | 1.15 | — |
| Convergence | 3 | 0 | 33 · 40 | 1 | 0 rows, 3 |
| Search | 5 | 0 | 35 · 37 | 1 | 0 rows, 3 |
| Agents (first agent's page) | 16 | 16 | 175 · 200 | 1.17 | 3 rows, 6 |
| Plugins (first plugin's page) | 18 | 1 | 176 · 183 | 1 | 3 rows, 8 |
| Settings (first domain) | 4 | 5 | 39 · 42 | 1 | 8 rows, 9 |

Against the budgets above: the activity bar's 12 controls (its places, the index refresh, the language and Settings)
are over its 9; Settings' list holds 8 domains, over 7; Knowledge is still two places; an agent's page names 16
concepts, over 14; a repository's page reads 352 words, over the Details budget of 120, because it opens on more than
Details. Not yet measured on the install: 680 px, light, the drill-ins (a workspace's page, a repository's Setup), and
§9.4's presses. The answer files are under `local/scratch/ux-count/` on the machine that took them.

### 9.4 Tasks

| Task | Before | After |
|---|---|---|
| Publish an ask its declarations proposed | 2 presses (the row's door, then the ask's *Publish*) | 1 (the row's *Publish to …*) |
| Sign in an account a waiting start needs | not said anywhere; then Settings, Agents, find it, *Sign in*: 3 presses and a scroll | 1 (the row's *Sign in*), and said |
| See everything set for one repository | 3 screens and 4 lists of 31 rows | 1 tab |
| Change one repository's landing rule | Settings → Workspace, scan 31 rows, 3 controls and *Save* | Repositories → it (filtered) → Setup → *Work*, 3 controls and *Save*; no row scanned |
| See which accounts a workspace may run on, and their state | Settings → Agents, under each workspace, per agent | Agents → the agent → *Workspaces*, or the workspace's Details |
| Install the tool a plugin needs | the plugin's prose, then Settings → Tools, find it, *Managed* | the plugin's page → its tool → *Managed* |

## 10. Considered and rejected

- **Tidying today's screens in place**: Permissions as a dense table, Workspace's lists folded. It cuts words, not
  homes: one repository's setup stays on three screens, which is the owner's fifth message.
- **A per-repository drawer opened from each Settings row.** A drawer is a form (D118 §3d), and the setting stays on a
  page about something else.
- **Git as a place of its own beside Repositories** (D147 §6). Two places listing the same repositories, setup beside
  one and git beside the other, where the owner asked for *repository management (with git)* in the repository screen.
  D147's reasons are met by tabs (§4.4).
- **A list of repositories with their branches nested beneath** (Git's list as the place's list). Three levels in a
  280 px pane, 29 repositories deep; the branches belong in the main area, where the graph is.
- **Agents kept in Settings.** A card per agent in one long scroll, no list and no page, no place for a signed-out
  account's mark, and each new kind longer.
- **An agent per door** (today's roster). Two rosters for one product, and the accounts read twice.
- **A separate *Inbox* place beside Overview.** Two places whose first job is the same. Overview's badge already counts
  what needs you.
- **Live account states**: a probe at a look, at the start, or on a timer. Probing is what signed the owner's accounts
  out (TOOL6g), and a waiting look starts no process (D125 §4).
- **Acting on every needs-you row.** Answering a parked session or accepting a review needs reading; an act there is a
  press without the look D113 and D51 require.
- **Search and Convergence kept apart.** Two places for one index, neither with an act.
- **Map folded into a workspace's page.** It would lose the map across every workspace (D67 §3), and its canvas the
  main area it needs.
- **Plugin tools as prose** (today's *What it needs*): nothing checks it.
- **A plugin carrying a tool's download** (a URL and a hash in its manifest). It makes a plugin a source of programs
  Daoris fetches. D121 §5 keeps sources to the lists the person adds. Revisited when a plugin needs a tool no list has.
- **A plugin's tool on its own process's `PATH` only.** It changes D121 §3's one answer for every child, and a session
  running `gh` would lose the managed one.
- **An *Advanced* switch per page.** A second concept on every page. Folding by section keeps each value visible in its
  summary line.
- **Removing the session views from the side bar off Sessions.** DOCK1a's frame stands; only an ended session stops
  being followed.

## 11. What this amends

- **D66 and D56 §3b**: the bar's places are Overview, Sessions, Quests, Repositories, Map, Knowledge, Agents, Plugins and
  Settings; its foot holds Settings alone.
- **D75**: the menus' items open the new homes (§2.4); Settings' domains are seven.
- **D118 §2**: Repositories' list groups by workspace, with a workspace's page; Knowledge's list switches between two
  modes; Agents gets a list and a page.
- **DOCK1a** (the dock design): off Sessions, an ended session is not followed (§2.5).
- **D147 §6–§7**: the Git place becomes Repositories' Branches and History tabs (§4.4). Every other part of D147 stands.
- **D121 §1 and §4.1**: Daoris's code still names five tools, and Daoris runs three. GitHub CLI and Azure CLI are declared
  by plugins and managed on their pages. §3 and §5 stand.
- **D64 §3 and D101**: the manifest gains `tools`, and the kit checks it.
- **D119 §3.2**: a plugin's page gains Tools after Points.
- **D130 §3.2 and D125 §6**: a workspace's accounts are set on the agent's page, under *Workspaces*.
- **D97 and D116**: Settings' first domain is named *Get started* again; *Setup* names a repository's and a workspace's
  tab.
- **The working surface design §4** and the platform language §5: *What needs you* gains its groups, its kinds and its
  acts (§6).

D55, D47 §4, D50, D52, D37, D24, D49 §4 and D57 §c stand.

## 12. The build

Every row's proof includes its stories first, vitest over the mocked bridge, `npm run verify`, and the parent's look on
the window at 1546 and 680 px in both themes and both languages, with its screens' counts by UX6a's counter against
§9.3. A row that moves a place moves Ask Daoris's place names with it (the `HelpPlaces` twins, `HelpCoverageTests`).

Order: UX6a, UX6b, UX6c; UX6d and UX6e once TOOL6g has landed; UX6f, UX6g; UX6h once GIT1d has landed; UX6i, UX6j.
PLUGTOOL1a, b, c run beside them, c after PLUGUI1c. Rows ready for `TASKS.md`:

- [ ] **UX6a — the counter, and the baseline on the install** (tools). One function counts a screen's concepts,
  controls, words and height, over stories through Playwright and on the window through `desktop -- eval`, so every row
  proves its counts the same way. Contract: design §9.1–§9.2. Proof: the counter's test over a fixed page; the parent
  re-takes §9.3's *before* on the install in both languages.
- [ ] **UX6b — a remembered choice ends with its item; the side bar follows only live work** (web-shell). Quests reopened
  a done quest, Convergence a gone finding, Overview's side bar an ended session's note. A closed or gone item opens
  nothing chosen; off Sessions, session views follow a running or waiting session, else Ask Daoris; a long note folds.
  Contract: §1 rule 6, §2.5. Proof: `listPanes` and `RightDock` tests.
- [ ] **UX6c — What needs you leads Overview and settles what it can** (web-shell). The band becomes the page's lead in
  three groups, gains go-aheads, departures and work to review, and acts where one press is safe: *Publish to …*, *Try
  again*, *Accept the departure*, with trust, go-aheads and widenings asking once. Contract: §6. Proof: `attention`
  tests per kind, the stories, Overview's vitest.
- [ ] **UX6d — accounts on What needs you, from what is known** (web-shell, modules; after TOOL6g; takes TOOL4m's row). A
  start waiting on cooling or signed-out accounts, and a signed-out account a list holds, are rows with *Sign in* and
  *Let … run …*, saying when each was read. No row starts a process. Contract: §6.2–§6.3. Proof: a tick asserting no
  probe; vitest.
- [ ] **UX6e — Agents is a place: one account list per product** (web-shell, web-settings, modules; after TOOL6g). A list
  of agents and a page each: accounts in four states with when each was read, *Read again* one at a time on the press,
  how accounts are used, workspaces, ways in, what it may do. Settings → Agents retires. Contract: §5, §2.4. Proof:
  stories, vitest, the coverage test.
- [ ] **UX6f — a repository's setup on its page** (web-shell, web-settings). The repository's page gains its Details and
  Setup tabs; Setup holds driving, line, landing, session language, standing answer, reach and its rules, folded to what
  Daoris decides. Settings' per-repository rows leave their four lists. Contract: §4.2, §3.1. Proof: `ProjectPage`
  stories per section, vitest, the counts.
- [ ] **UX6g — a workspace's page; Settings → Workspace and Permissions retire** (web-shell, web-settings). Repositories'
  list groups by workspace and gains a filter past twelve; a workspace's page holds its defaults, remote, what a start runs
  on and its rules; clean-up and bring up to date move to its Branches tab. Contract: §4.1, §4.3. Proof: stories,
  vitest, `domains.test.ts`, the coverage test.
- [ ] **UX6h — Git inside Repositories** (web-shell; GIT1e and GIT1f land here, after GIT1d). Branches and History tabs
  carry D147's list and pages for a repository, and Branches a workspace's; a branch, commit, file or compare opens in the
  main area with its way back. No Git place on the bar. Contract: §4.4, D147 §2–§4. Proof: GIT1e–f's stories and tests,
  in place.
- [ ] **UX6i — Knowledge: Search and Convergence as one place** (web-shell). One place whose list head switches between
  Search and Convergence, remembered, each mode keeping its list, main area and memory; every door into either opens its
  mode. Contract: §2.2. Proof: `SearchView` and `ConvergenceView` suites through the place, the palette's go entries,
  `test:web`'s search.
- [ ] **UX6j — the bar's foot and Settings' seven** (web-shell, web-settings). Refresh and the language toggle leave the
  bar for the Daoris menu, the palette and Appearance; Settings keeps Get started, Appearance, AI features, Driver, Tools,
  Browser and Machine log; *Setup* names the tabs. Contract: §2.1, §2.3, §2.4. Proof: `domains.test.ts`, the chrome test,
  the names check.
- [ ] **PLUGTOOL1a — a plugin's manifest declares its tools** (cli, driver). `tools` in `plugin.json`, read by both twins
  by one table: an id, a range, why, at most four checks run only on a press or at *Try*. A problem is shown, never a
  refusal, and the kit checks it. Contract: §7.2–§7.3. Proof: the twins' test tables; `plugins try` on good and bad
  manifests.
- [ ] **PLUGTOOL1b — the two pull-request plugins declare their CLI** (cli, examples). Azure DevOps pull request declares
  Node.js, Git and Azure CLI, its extension and sign-in as checks; GitHub pull request Node.js, Git and GitHub CLI, its
  sign-in as a check. Each floor is read from the flags it uses, and the README points at the manifest. Contract: §7.4.
  Proof: `landing-plugins.test.ts`; the kit's `try`.
- [ ] **PLUGTOOL1c — a plugin's page manages its tools; Tools keeps Daoris's own** (web-shell, web-settings, modules,
  driver; after PLUGUI1c). A Tools section shows each tool's way, version against its range and checks on *Check*;
  Settings → Tools keeps Git, Node.js and PowerShell and what no plugin declares; `tool list` names each declarer.
  Contract: §7.5. Proof: stories, vitest, the CLI's list test.

## 13. What this document's gate does not cover

- **The install was read from eight screenshots**, two of them the same screen. Sessions and Repositories on the install,
  Settings → Agents, Tools, Workspace and the Agents' cards were not seen there: their counts are from stories and the
  source, scaled. UX6a's counter takes them on the install before anything moves.
- **The scaled counts assume every list holds a row per repository.** `Lines`, `Session language` and `How work lands`
  were read from their stories' fixtures, not from the driver's answer on the install.
- **Claude Code's accounts listed twice, once per door** is TOOL6g's reading of the install, relayed in `TASKS.md`; this
  branch did not see the Agents domain on it. **How the driver learns *signed out* without probing at a look** is
  TOOL6g's to settle with its data; this design shows only what is known, and says *unknown* for the rest.
- **The counter's definitions are judgements**: a concept is a glossary term, a word is a segment. They make a before and
  an after comparable; they are not a usability study. The budgets in §9.3 are targets, and they report, never fail (D54).
- **The plugins' version floors** in §7.2 are illustrative, and the `ready` commands are the CLIs' documented forms,
  not run here.
- **The new names** (*Knowledge* as a place, *Get started*, *Details*, *Branches*, *History*, *Setup* as tabs, the three
  groups, the account states) are proposals for D116's check. `verify` checks this document's place in the router and its
  decision's shape. It checks none of the words.
