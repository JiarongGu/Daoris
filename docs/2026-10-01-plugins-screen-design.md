# Plugins — a view of their own (PLUGUI1)

**Carried by:** PLUGUI1 in `TASKS.md`. **Status:** design (PLUGUI1a), written before the build, and **D119**
records what it settles. It extends `docs/2026-09-23-plugin-design.md` (D64, D100, D101, D103). It is built on
the frame of `docs/2026-10-01-frame-model-design.md` (D118), whose §7 said what this view takes from the frame.
Every name in it follows `docs/2026-10-01-naming-design.md` (D116) and its glossary.

> *"because plugin will be a big part of daoris so it need to have a panel/screen itself, and develop proper
> ui/ux"* — the owner, 2026-10-01.

Today plugins are one domain of Settings. This design makes them a view of the activity bar. Its list holds
the plugins on this machine, grouped by what they need from the person. Its main area holds a page per
plugin: what it adds, what it did lately, what it keeps, and whether it answers. **It gives a plugin no new
power.** A plugin still declares and speaks, the page draws it with the host's own atoms from what the driver
reads, and no plugin code runs in the page (D64 §7, D52).

## 0. What is there today

Read from the code at `2c6fcf0`.

- **The screen** is Settings → Plugins (`settings/PluginsDomain.tsx`, with `PluginOffers.tsx`, `PluginKit.tsx`
  and `PluginUpdate.tsx` beside it). It holds:
  - a card with the plugins folder's row;
  - one row per installed plugin: its name and version, a *running* or *off* pill, one line of what it
    declares and speaks on, its folder, where it came from, and *Try*, *Update…*, *Turn on* or *Turn off*,
    and *Remove*;
  - under a row, a trial's report or an update's plan while the domain is shown;
  - a card of *Daoris's own plugins* not installed, each with what it needs and *Install*;
  - the kit's card, with *New* and *Try a folder*.
- **The routes** are `DAORIS.DRIVER`'s `PLUGINS`, `PLUGIN_ACTION`, `PLUGIN_UPDATE`, `PLUGIN_INSTALL`,
  `PLUGIN_NEW` and `PLUGIN_TRY` (`DriverModule.Plugins.cs`), and `DAORIS.REGISTRY`'s `PICK_FOLDER`.
- **The terminal** has two homes. The CLI's `daoris plugin list|add|add --offer|update|remove|enable|disable`
  edits the catalogue (`src/Daoris.Cli/src/plugins.ts`, the twin of `PluginInstall.cs`). The driver's
  `daoris-driver plugins new|try` makes and tries plugins (D101).
- **Ask Daoris** proposes `plugin_propose`: add a landed folder or an offer, switch on, switch off, update. Its
  room says how a plugin is made (PLUG9) and names Daoris's own plugins.

What is missing, each found in the code:

| # | Finding | Where |
|---|---|---|
| P1 | No page of its own. A trial's report and an update's plan last only while the domain is shown (frame audit PL4, PL9) | `PluginsDomain.tsx:36–40` |
| P2 | Nothing is drawn while the catalogue loads (PL11) | `PluginsDomain.tsx:44` |
| P3 | An installed plugin's servers, its hook's command and its agents' commands are not answered; only an offer's servers are | `DriverModule.Plugins.cs:37–52` |
| P4 | A plugin's words (what it writes to stderr, its holds, its failures) go to the console's ring under `plugin:<id>`, which no screen reads. They are gone at exit | `HookSet.Say` (`Hooks.cs:772`), `SessionOutput` |
| P5 | The machine log has no plugin event. A call, an answer, a failure or a restart leaves no trace past a restart (D94 §4) | `MachineLog`, `SessionLog` |
| P6 | Nobody keeps a trial's report: the page holds it while shown, and the terminal prints it | `PluginsDomain.tsx:36`, `PluginKitCommand.cs:114` |
| P7 | The screen cannot install from a folder; the terminal and Ask Daoris's card can | `PluginsDomain.tsx` |
| P8 | *Remove* removes on its first press. The platform's rule is that a destructive edit asks once (platform language §4) | `PluginsDomain.tsx:159` |
| P9 | *running* wears done's green. A liveness mark never borrows an outcome's hue (platform language §3) | `PluginsDomain.tsx:112` |
| P10 | Whether a plugin answers is never said. A plugin whose process could not start, exited or answers late shows as on | `HookSet.ReconcileAsync`, `ConsiderAsync` |

## 1. What a person does with plugins

Each act, where it is today, where it goes, and its terminal twin (§4.4 says why each twin lives where it
does).

| The act | Today | On the Plugins view | At a terminal |
|---|---|---|---|
| **Get one** | | | |
| Install one of Daoris's own | Settings' offers card; Ask Daoris's card | the offer's row (*Install*), and the offer's page | `daoris plugin add --offer <id>` |
| Install from a folder | a terminal; Ask Daoris's card, for a folder in a registered checkout | `＋` → *Install from a folder…*, a drawer that shows what it runs before the press | `daoris plugin add <folder>` |
| Make one | Settings' kit card, *New* | `＋` → *Make a plugin…*, a drawer whose second step tries the folder made | `daoris-driver plugins new <id> --point <p>… --in <folder>` |
| Ask for one | Ask Daoris, typed | `＋` → *Ask Daoris for a plugin*: Ask Daoris opens on a first message (§3.4) | `daoris-driver ask --workspace <name> --to <repo> "…"` |
| **Keep it** | | | |
| Switch it on or off | the row's switch | the page's header | `daoris plugin enable\|disable <id>` |
| Update it from its source | the row's *Update…*, then *Update now* | the header's *Update…*, the plan under the header, *Update now* | `daoris plugin update <id> [--yes]` |
| Remove it | the row's *Remove*, one press | the header's *Remove…*, which asks once | `daoris plugin remove <id>` |
| **Check it** | | | |
| Try it | the row's *Try*; the kit's *Try a folder* | the header's *Try*; the list's ⋯ → *Try a folder…* | `daoris-driver plugins try <id\|folder>` |
| Run its own tests | nowhere but its plugins repository's `node --test` | Tests → *Run tests* | `daoris-driver plugins test <id\|folder>` (new) |
| **Understand it** | | | |
| See what it declares | one line: its agents' names and its points | Points, Agents and Servers (§3.2) | `daoris-driver plugins show <id>` (new) |
| See what it did | nowhere | Activity | `daoris-driver plugins activity <id> [--since <span>]` (new) |
| See its health | *running*, *off*, a refused one's sentence | the mark on its row and its strip, and the header's pill and line (§2) | `daoris-driver plugins show <id>`, from the machine log's last word |
| See what it keeps | nowhere (named on removal) | Data folder, with *Open folder* | `daoris-driver plugins show <id>` prints the folder |

**A landing plugin** (D100) is read on the same page. Its `work/land` point names the landing rules that name
it and says whether it can land work here, in `LandingRules.PluginProblem`'s words. Its activity lists the
branches it pushed, with their pull requests, from the landing record (D102).

## 2. Health

Each plugin is in one of five states. **The state is the loop's own record** in the process that runs the
loop: `PluginHealth`, fed by everything in that process that speaks to a plugin. That means the hook set (a
start, an exit, each call), the landing and the hand-off (each `work/land` call). A trial is a check, not the
plugin's work, and feeds it nothing. **From a terminal**, which is another process, the state is the machine
log's last word (§4.2), and the terminal says so and when. One table of cases holds both readings.

| State | When | The line under the header | Its mark | Its pill |
|---|---|---|---|---|
| `running` | On and sound, it speaks at a point the loop asks (`quest/consider`, `session/ended`), and its process is up | *Running since 09:12, listening on quest/consider and session/ended.* | none | neutral |
| `ready` | On and sound, with nothing to keep running: it only declares agents or servers, or speaks only at `work/land`, which a landing starts | *Ready. A landing starts it for each branch it lands.* Or: *Ready. It declares agents and servers, and runs nothing itself.* | none | neutral |
| `failing` | On and sound, and its last word to Daoris was a failure: its process could not start or exited, or it answered late, unreadably, or with an error. Its next good answer clears it. So does the person turning it on or updating it, which starts its record afresh; a restart alone does not, so a plugin that keeps crashing stays failing | The failure, then what it costs. At `quest/consider`: *Every quest it is asked about sits until it answers, or until you turn it off* (it fails closed, D64 §4). At `session/ended`: *Nothing waits on it; the driver goes on.* At `work/land`: *A landing keeps its branch; push it by hand* | waiting on you | open |
| `refused` | On, and the driver refused it: a manifest that does not read, an `apiVersion` this build lacks, or a conflict naming both sides | The driver's sentence, verbatim | waiting on you | open |
| `off` | The person switched it off. A refused one that is off is off too; its sentence stays on its page | *Off. The driver asks it nothing, and hands sessions nothing it declares.* | its initial dimmed | neutral |

- **Why failing and refused wear the waiting hue, not red.** Waiting on the person is open's hue everywhere
  (platform language §3), and both wait on the person's act: turn it off, fix it, update it. A single failed
  call in Activity is an outcome, and wears declined's red there.
- **Why running wears no live mark.** A live mark means a turn is running (platform language §3). A hook
  process is up between calls, as a live chat is between turns. So its mark is quiet and its word says it. This
  retires the *running* pill in done's green (P9).
- **An update waiting is not a state.** It is a second pill beside the name, *update available*, in the
  neutral tone: the accent is never a status (platform language §3), and an update waits on nobody. It shows
  when the plugin's source declares something different in the five rows D103 compares. A source whose files changed
  while its declarations did not shows no chip, and *Update…* still takes its files (D103).
- **The activity bar's Plugins place counts the plugins waiting on you** (failing or refused while on), in
  open's tone, as Sessions counts its sessions waiting on the person (platform language §2).

## 3. The view on the frame

Plugins is a view of `VIEWS` (`commands.ts`). It is shell-only, since a plugin is this machine's (D64; D47 §4).
It uses the `plug` icon, and sits after Search, above Settings at the bar's foot.

### 3.1 The list pane

On FRAME1b's `ListPane`, within the bounds every list but Settings takes: 264–420 px, 280 to start.

- **The header** holds the list's name, *Plugins*, then `＋`, ⋯ and close.
- **`＋` offers three kinds, in this order:**
  1. *Ask Daoris for a plugin*: a plugin is made as an ask at the repository that holds plugins (PLUG9),
     so asking is the way one gets made with its tests. It comes first.
  2. *Make a plugin…*: the kit's drawer, for a person making one by hand.
  3. *Install from a folder…*: a drawer.

  FRAME1a §7 listed the three without an order. This order is this design's.
- **⋯ holds** *Try a folder…* and *Open the plugins folder*.
- **The groups**, each with its count, and a group with none is absent:
  1. *Waiting on you*: failing or refused, while on;
  2. *On*: running or ready;
  3. *Off*;
  4. *Daoris's own plugins*: the offers the install carries that this machine has not installed.

  Within a group the rows go by name, as the person reads it, then by id. **The order is what the person
  acts on**: what needs them, what is working, what they turned off, what they could add. That is why the
  groups are by state and not by kind: a plugin may declare agents, hand servers and speak at once. *This
  refines FRAME1a §7* (running, off, refused).
- **A row** holds the plugin's name (its manifest's, content, never translated), its version in the mono
  meta face, and its state's word where it has one. Its second line says what it adds, as fragments:
  *2 points · 1 agent · 1 server*. An offer's row carries *Install* inline, as a ghost button.
- **Chosen**: the row wears the list's selection, and the main area shows its page. The chosen key is the
  plugin's id, or `offer:<id>` for an offer, since an offer and an installed plugin may share an id. Once
  an offer is installed, the list chooses the installed plugin.
- **The strip**, closed by the person or drawn by the window, holds a `StripMark` per installed plugin (FRAME1b's
  atom): its initial and its mark (§2). Waiting on you wears the waiting mark; off, a dimmed initial; on, no
  mark. The strip's `＋` opens the same three kinds. Offers are not on the strip. A strip longer than the
  window scrolls, as the activity bar does, and never hides a plugin.
- **Keys and memory** are FRAME1a's: ↑ ↓ Home End Enter; Ctrl+B and a press on the current place toggle the
  list. `daoris.list.plugins.closed`, `.width` and `.chosen` are remembered. The list has no filter and no
  search: no machine holds a screen of plugins today (§8).
- **Its states** (FRAME1a §3h):
  - a first load shows skeleton rows (P2);
  - an error keeps the last answer and shows one toast;
  - with no answer ever, the list says the sentence in place;
  - **empty** (no plugins, no offers): *No plugins on this machine*, a body saying a plugin adds an agent,
    hands sessions a server or speaks at a point, and the first two of `＋`'s kinds as its actions.

### 3.2 The main area: a plugin's page

On FRAME1c's `ViewMain`, laid out by its own width.

**The header** (FRAME1a §3b):
- **The title** is the plugin's name. Beside it sit its version, its state's pill and, when one waits, *update
  available*.
- **The meta line** is its id in the mono face. **The one line** is its description (content, shown whole at the
  reading measure). *Changed by NAME2 (2026-10-02): it was cut to one line, whole in its tip, and on the install a
  description ended in an ellipsis; every page header's line wraps now.*
- **Its acts, in this order:**
  1. the switch, a button named for its act: *Turn off* while on, *Turn on* while off;
  2. *Try*, only for a plugin that speaks at a point;
  3. *Update…*, only for a plugin with a source record;
  4. *Remove…*, in the danger variant.
- **An absent act is absent, never disabled.** None is loud by default. *Update now*, in the plan the first
  press opens under the header, is the one primary control.
- ***Remove…* asks once** (P8). Its first press opens a sentence under the header: *Removes its folder from this
  machine. What it kept stays at `<data folder>`, yours to delete.* For a plugin a landing rule names, it adds:
  *The rule stays as written, and refuses until the plugin is back* (D100). The moves are *Remove plugin*
  (danger) and *Never mind*. A running plugin's process is stopped first, as today.

**The health line** comes first under the header: §2's line, on a warn rail when the plugin is failing or
refused, and quiet otherwise. **Status leads** (platform language §4).

**The sections**, in this order. Points, Agents and Servers appear only where the plugin declares that
thing. The rest are on every page.

1. **Points** (挂点). A row per declared point:
   - its name, in the mono face;
   - what kind of question it is, in the kit's words (*a decision, before a planned start spends anything*);
   - how long the driver waits (10 s, 10 s, two minutes);
   - *listening* or *not listening*, from the handshake of the process that runs now. A point declared and
     not listened on says that the driver asks it nothing there.

   For `work/land`, the row adds two things:
   - **the landing rules that name the plugin**, by workspace or repository with their pattern, from
     `driver.json`. With none, it says so and offers *Open How work lands*, the Workspace domain's part;
   - **whether the plugin can land work here**: *can land work here*, or `LandingRules.PluginProblem`'s
     sentence.
2. **Agents** (智能体). A card per agent the plugin declares. Each holds:
   - its name and its command as written;
   - its way in, *protocol*;
   - its permission mode in the agent's own word, or *its wire names none* for a null posture (ACP3);
   - the account variable, the package and the install command;
   - what the roster knows of it (installed, its version, signed in), read from the roster's own cache
     (`HARNESSES`), and *Open Agents*.
3. **Servers** (服务器). A row per server:
   - its name, and its command as written, with `${plugin}`, `${data}` and `${browser}` left as written;
   - its environment variables **by name only**, never a value, since a value may be a key;
   - that it is handed to every session beside Daoris's own knowledge host. For `${browser}`: withheld where
     no shell answers (D78).
4. **Activity** (运行记录). One period at a time: the last day, the last 7 days (the start) or the last 30 days.
   - **The summary.** A row per point, with its answers counted by word, its failures counted by kind, and its
     median and slowest answer time. Then the process's starts, exits and failures to start. Then, for its
     agents, the sessions started on each. For its servers, the sessions each was handed to. For a landing
     plugin, *Branches it pushed*: each branch, its repository, its pull request as a link, and when.
   - ***Recent events***: the newest 50 lines of the period. Each has its time, its event's word, its point,
     its answer or failure, and the time it took.
   - ***Said since Daoris started***: the plugin's own words this run, live, in a `MonoWell` with its dropped
     count. They are the console's ring under `plugin:<id>` (P4): what it wrote to stderr, its holds with their
     reasons, and its failures in the driver's sentences. The machine log never holds them (§4.2), so they
     reach no further back than this start, and the heading says so.
5. **Tests** (测试).
   - ***Last trial***: when it ran, from which door, and each check with its verdict and the driver's own
     sentence. Then the summary, and what the plugin wrote to stderr (P6).
   - ***Tests in its folder***: the test files its folder carries, *Run tests*, and the last run's verdict,
     time and output (§4.3).
   - A plugin that speaks at no point says there is nothing to try; its tests in its folder still run.
6. **Data folder** (数据文件夹). The folder (`PathText`), whether it exists, its files and bytes counted up to a
   bound, and its newest change, with *Open folder*. A line says what the folder is: an update never touches
   it, and a removal leaves it (D64 §3).
7. **Source** (来源). Where it came from, in the four ways D103 says it: a folder, Daoris's own plugins, no
   record, or a record that does not read. Then when it was installed, whether an update waits, and its
   install folder with *Open folder*.
   - An update that waits shows what would change, the five rows D103 compares, before any press.
   - With nothing different, it says its source declares the same.
   - With a source that cannot be read, it gives the refusal verbatim.

**A refused plugin's page**: the driver's sentence leads. Points, Agents and Servers then show what its
manifest declares as written, marked *not taken*, where the manifest reads (a conflict, or a declaration this
build refuses). One that does not read shows the sentence alone. Nothing of a refused plugin is taken
(`PluginCatalog.Load`).

**The page's foot** is its terminal twins, one quiet line each, as the Settings cards had:
`daoris plugin enable|disable|update|remove <id>`, and `daoris-driver plugins show|activity|try|test <id>`.
The two doors stay in sight (D50).

**An offer's page** holds:
- a header with the offer's name, version, *not installed* and *Install*;
- its description;
- Points, Agents and Servers from its manifest;
- *What it needs*, its README's bullets verbatim (D103);
- a line saying that installing runs nothing, and that a landing plugin runs only where a landing rule names it.

An offer that cannot be installed as it stands gives the driver's sentence in its place.

**The main area's states** (FRAME1a §3b):
- **Nothing chosen**: *Choose a plugin*, a body saying what a plugin's page shows, and the list's `＋` kinds
  as its actions. This is FRAME1a §7's *the page offers the kit*.
- **Loading**: the header comes at once from the list's own answer, and each section shows skeleton rows
  until the page's answer comes. The page never flashes empty.
- **Gone**: the chosen plugin is no longer installed, removed from a terminal or by hand. `PLUGIN` refuses it
  as `PLUGIN_UNKNOWN`, and the page says *This plugin is no longer here*. It is read by the code, never by
  the sentence (D48 §6).
- **An error**: the last answer is kept, with one toast.

### 3.3 Each section's states

| Section | Loading | Empty | Error |
|---|---|---|---|
| Points, Agents, Servers | skeleton rows | absent: the plugin declares none | the page's error (§3.2) |
| Activity | skeleton rows | *Nothing in the machine log for this plugin in the last 7 days*. For a plugin that only declares: Daoris never calls it, and its agents' and servers' counts are its activity. Words empty: *It has said nothing since Daoris started* | the reader's sentence in place, such as no log on this machine; the words keep what arrived |
| Tests | the trial's own sentence while it runs, up to two minutes for a landing; *Run tests* busy, up to five minutes | *Not tried on this machine*, with *Try*; *Its folder carries no tests* | the kit's or the runner's refusal, verbatim |
| Data folder | skeleton row | *It keeps nothing yet* (no folder), and no *Open folder* | the count's bound said (*more than 10,000 files*), never a guess |
| Source | skeleton row | *No record of where it came from*, with how to give it one (today's sentence) | the record's problem, or the update's refusal, verbatim |

### 3.4 The drawers: forms only

A form stays a drawer (FRAME1a §3d).

- ***Make a plugin*** is the kit's form as it stands: ID, points, folder, *Choose…*, *New*. Once *New* has
  written the folder, the drawer's second step is *Try a folder* with that folder filled, and the report
  under it. It installs nothing, and says so. The list's ⋯ → *Try a folder…* opens the same drawer at its
  second step.
- ***Install from a folder***:
  1. *Choose…* (the system's picker), or a typed folder and *Read*.
  2. What the folder's plugin would run, shown before the press as Ask Daoris's plugin card shows it: its id,
     name and version, the command it starts, its points, agents and servers. Or the refusal, verbatim.
  3. *Install* and *Never mind*.

  Installing adds and never replaces. An id already installed is refused, naming *Update…* and
  `daoris plugin add <folder>`, which replaces at a terminal. On success, the list chooses the new plugin,
  and the toast says nothing of it runs before the driver's next look.
- ***Ask Daoris for a plugin*** is no drawer. It opens Ask Daoris on a first message, as SETUP1b's `askSetup`
  does: *I'd like a new plugin for Daoris. Ask me what it should do and where it speaks, then propose it as an
  ask at the repository that holds plugins.* The message is whole, since an opening is sent as it is handed.
  The room's *Making a plugin* section guides what follows (PLUG9).

### 3.5 The side bar and the panel: nothing of the view's own

FRAME1a §3c holds here: **the side bar and the panel hold only what is the same on every view**, and the
Plugins view adds nothing to either. Two things looked like candidates, and neither goes there.

- **A plugin's live words look like console material**, and the panel holds the console. But the panel's
  console follows the attended session. A plugin's words beside it would make the panel change with the
  view, which DOCK1a made it the frame's to prevent. They stay in Activity.
- **A trial's report looks like output.** FRAME1a §7 already keeps it on the plugin's page.

What the view gives the side bar is what every view gives it. Ask Daoris is told which plugin is chosen
(`where.ts`, FRAME1i). The page's ⋯ → *Ask Daoris* opens it on a first message about that plugin (§6,
PLUGUI1h).

### 3.6 The narrow window

The frame's rules are FRAME1a §3g's. The page lays out by the main area's own width, through FRAME1c's
container queries.

| The window | The list | The main area | The page |
|---|---|---|---|
| 1280 px, side bar open | beside, 280 px | about 570 px | one column; the header's acts beside the title |
| 1280 px, side bar closed | beside | about 920 px | one column: prose at its 65ch measure, the tables at the column's width |
| 680 px | a strip by itself, which opens over the main area | about 540 px | the header's acts on their own line under the title |

- **Below 560 px of the main area**, the header's acts go on their own line under the title.
- **Below 480 px:**
  - a field's label goes above its value;
  - the Activity summary becomes one block per point;
  - a trial check's name goes above its sentence;
  - the recent events drop their time-taken column into the event's line.

### 3.7 In a browser

The view is absent, as Sessions is: shell-only in `VIEWS`, and neither the palette's row nor the Daoris menu's
item is offered there. The browser suite already asserts what a browser must never learn, and the view's
absence joins that assertion.

### 3.8 The names

Every name follows D116. Its kind decides its form and its budget. Its concept is the glossary's term, in the
names NAME1b gives the catalogues, which the owner approved on 2026-10-01. A plugin's own name, description and
README are content, never renamed. A count is English characters and Chinese units (D116 §4). Every row is
within its budget. Existing keys keep their key and are marked *(exists)*.

*Renamed by NAME2 (2026-10-02), as the arrows below show: the switch is 启用 and 停用, and the off state 已停用. 开启
and 关闭 paired the switch with close's name (an ask's and a quest's 已关闭), and on the install the page's 关闭 read
as close. The glossary's terms `turn on` and `turn off` hold every label that names the switch; the English pair is
unchanged.*

| Key | Kind | English | 中文 | Measured / budget |
|---|---|---|---|---|
| `nav.plugins` | nav | Plugins | 插件 | 7, 2 / 16, 5 |
| `command.go.plugins` | command | Plugins | 插件 | 7, 2 / 40, 16 |
| `plugin.list.ask` | menu | Ask Daoris for a plugin | 向问道衍提插件需求 | 23, 9 / 24, 10 |
| `plugin.list.make` | menu | Make a plugin… | 制作插件… | 14, 5 / 24, 10 |
| `plugin.list.install` | menu | Install from a folder… | 从文件夹安装… | 23, 7 / 24, 10 |
| `plugin.list.tryFolder` | menu | Try a folder… | 试运行文件夹… | 13, 7 / 24, 10 |
| `plugin.list.openFolder` | menu | Open the plugins folder | 打开插件文件夹 | 23, 7 / 24, 10 |
| `plugin.page.ask` | menu | Ask Daoris | 问道衍 | 10, 3 / 24, 10 |
| `plugin.group.waiting` | section | Waiting on you ({{count}}) | 等你处理（{{count}}） | 19, 7 / 32, 12 |
| `plugin.group.on` | section | On ({{count}}) | 已开启 → **已启用**（{{count}}） | 7, 6 / 32, 12 |
| `plugin.group.off` | section | Off ({{count}}) | 已关闭 → **已停用**（{{count}}） | 8, 6 / 32, 12 |
| `plugin.offers.title` *(exists)* | section | Daoris's own plugins | Daoris 自带的插件 | 20, 8.5 / 32, 12 |
| `plugin.section.points` | section | Points | 挂点 | 6, 2 / 32, 12 |
| `plugin.section.agents` | section | Agents | 智能体 | 6, 3 / 32, 12 |
| `plugin.section.servers` | section | Servers | 服务器 | 7, 3 / 32, 12 |
| `plugin.section.activity` | section | Activity | 运行记录 | 8, 4 / 32, 12 |
| `plugin.section.tests` | section | Tests | 测试 | 5, 2 / 32, 12 |
| `plugin.section.data` | section | Data folder | 数据文件夹 | 11, 5 / 32, 12 |
| `plugin.section.source` | section | Source | 来源 | 6, 2 / 32, 12 |
| `plugin.section.needs` | section | What it needs | 所需条件 | 13, 4 / 32, 12 |
| `plugin.activity.pushes` | section | Branches it pushed | 推送过的分支 | 18, 6 / 32, 12 |
| `plugin.activity.recent` | section | Recent events | 最近事件 | 13, 4 / 32, 12 |
| `plugin.activity.said` | section | Said since Daoris started | 本次运行的输出 | 25, 7 / 32, 12 |
| `plugin.tests.trial` | section | Last trial | 上次试运行 | 10, 5 / 32, 12 |
| `plugin.tests.own` | section | Tests in its folder | 文件夹中的测试 | 19, 7 / 32, 12 |
| `plugin.kit.title` *(exists; a section, now the drawer's title)* | title | Make a plugin | 制作插件 | 13, 4 / 40, 16 |
| `plugin.kit.tryFolder` *(exists; the same)* | title | Try a folder | 试运行文件夹 | 12, 6 / 40, 16 |
| `plugin.install.title` | title | Install from a folder | 从文件夹安装 | 21, 6 / 40, 16 |
| `plugin.field.id` | field | ID | ID | 2, 1 / 36, 14 |
| `plugin.field.version` | field | Version | 版本 | 7, 2 / 36, 14 |
| `plugin.field.command` | field | Command | 命令 | 7, 2 / 36, 14 |
| `plugin.field.namedIn` | field | Named in landing rules | 所在落地规则 | 22, 6 / 36, 14 |
| `plugin.field.wayIn` | field | Way in | 接入方式 | 6, 4 / 36, 14 |
| `plugin.field.permission` | field | Permission mode | 权限模式 | 15, 4 / 36, 14 |
| `plugin.field.account` | field | Account variable | 账户变量 | 16, 4 / 36, 14 |
| `plugin.field.package` | field | Package | 软件包 | 7, 3 / 36, 14 |
| `plugin.field.installCommand` | field | Install command | 安装命令 | 15, 4 / 36, 14 |
| `plugin.field.env` | field | Environment | 环境变量 | 11, 4 / 36, 14 |
| `plugin.field.installedAt` | field | Installed | 安装时间 | 9, 4 / 36, 14 |
| `plugin.field.folder` | field | Folder | 文件夹 | 6, 3 / 36, 14 |
| `plugin.field.size` | field | Size | 大小 | 4, 2 / 36, 14 |
| `plugin.field.changed` | field | Last change | 最近更改 | 11, 4 / 36, 14 |
| `plugin.source.folder` *(exists)* | field | Added from | 添加自 | 10, 3 / 36, 14 |
| `plugin.folder` *(exists)* | field | The plugins folder → **Plugins folder** | 插件文件夹 | 14, 5 / 36, 14 |
| `plugin.period.day` | choice | Last day | 最近一天 | 8, 4 / 16, 6 |
| `plugin.period.week` | choice | Last 7 days | 最近 7 天 | 11, 4.5 / 16, 6 |
| `plugin.period.month` | choice | Last 30 days | 最近 30 天 | 12, 5 / 16, 6 |
| `plugin.enable`, `plugin.disable` *(exist)* | button | Turn on, Turn off | 开启, 关闭 → **启用, 停用** | 8, 2 / 20, 8 |
| `plugin.kit.try` *(exists)* | button | Try | 试运行 | 3, 3 / 20, 8 |
| `plugin.update.ask`, `.apply`, `.cancel` *(exist)* | button | Update…, Update now, Not now | 更新…, 立即更新, 暂不 | 10, 4 / 20, 8 |
| `plugin.forget` *(exists)* | button | Remove → **Remove…** | 移除 → **移除…** | 7, 3 / 20, 8 |
| `plugin.removeMeanIt` | button | Remove plugin | 确认移除插件 | 13, 6 / 20, 8 |
| `plugin.offers.install` *(exists)*, `plugin.install.apply` | button | Install | 安装 | 7, 2 / 20, 8 |
| `plugin.install.read` | button | Read | 读取 | 4, 2 / 20, 8 |
| `plugin.kit.choose` *(exists)* | button | Choose… | 选择… | 7, 3 / 20, 8 |
| `plugin.tests.run` | button | Run tests | 运行测试 | 9, 4 / 20, 8 |
| `plugin.data.open`, `plugin.source.open` | button | Open folder | 打开文件夹 | 11, 5 / 20, 8 |
| `plugin.page.toAgents` | button | Open Agents | 打开智能体 | 11, 5 / 20, 8 |
| `plugin.page.toLanding` | button | Open How work lands | 打开落地方式 | 19, 6 / 20, 8 |
| `plugin.settings.open` | button | Open Plugins | 打开插件 | 12, 4 / 20, 8 |
| `plugin.running`, `plugin.off` *(exist)* | status | running, off | 运行中, 已关闭 → **已停用** | 7, 3 / 16, 5 |
| `plugin.health.ready` | status | ready | 就绪 | 5, 2 / 16, 5 |
| `plugin.health.failing` | status | failing | 异常 | 7, 2 / 16, 5 |
| `plugin.health.refused` | status | refused | 未载入 | 7, 3 / 16, 5 |
| `plugin.update.waits` | status | update available | 可更新 | 16, 3 / 16, 5 |
| `plugin.offers.notInstalled` *(exists)* | status | not installed | 未安装 | 13, 3 / 16, 5 |
| `plugin.notTaken` | status | not taken | 未生效 | 9, 3 / 16, 5 |
| `plugin.listening`, `plugin.notListening` | status | listening, not listening | 监听中, 未监听 | 13, 3 / 16, 5 |
| `plugin.answer.allow`, `.hold` | status | allowed, held back | 放行, 拦下 | 9, 2 / 16, 5 |
| `plugin.answer.answered`, `.pushed`, `.notPushed` | status | answered, pushed, not pushed | 已应答, 已推送, 未推送 | 10, 3 / 16, 5 |
| `plugin.failure.late`, `.unreadable`, `.errored`, `.exited`, `.unstartable` | status | late, unreadable, errored, exited, did not start | 超时, 无法读取, 报错, 已退出, 未能启动 | 13, 4 / 16, 5 |
| `plugin.event.started`, `.stopped`, `.served`, `.tried`, `.tested` | status | started, stopped, handed, tried, tested | 已启动, 已停止, 已提供, 已试运行, 已测试 | 7, 4 / 16, 5 |
| `plugin.tests.passed`, `.failed` | status | passed, failed | 通过, 未通过 | 6, 3 / 16, 5 |
| `plugin.empty.headline` | headline | No plugins on this machine | 本机没有插件 | 26, 6 / 40, 16 |
| `plugin.none.headline` | headline | Choose a plugin | 请选择插件 | 15, 5 / 40, 16 |
| `plugin.gone.headline` | headline | This plugin is no longer here | 此插件已不在本机 | 29, 8 / 40, 16 |
| `plugin.install.placeholder` | placeholder | a folder holding plugin.json | 含有 plugin.json 的文件夹 | 28, 12.5 / 40, 16 |

**Why these names, where a reason is owed:**
- **refused, 未载入.** 拒绝 is the rules' *deny* and the system's refusal (glossary, `decline`). What the person
  meets is that the plugin is not loaded, and the sentence under it says why.
- **held back, 拦下.** A plugin's decision at `quest/consider` is not the person's *hold* of a repository
  (暂停), so it takes a word of its own in each language.
- **not taken, 未生效.** 采用 is *adopt*'s word (glossary), and a refused plugin's declarations are simply not
  in effect.
- **failing, 异常.** A condition takes a bare word (D116 §3b). 失败 is an outcome, the *failed* session's.
- **Activity, 运行记录.** The section names what it holds, records of the plugin's runs. It is not an English
  question carried over.
- **Ask Daoris for a plugin, 向问道衍提插件需求.** A door says its destination's name, 问道衍. The act is an
  ask (需求) for a plugin.
- **Open Agents, 打开智能体; Open How work lands, 打开落地方式.** A door is 打开 and the place's name (D116
  §3b), in the names NAME1b gives those places.
- **Plugins folder.** A field names its value with no article, as *Daoris home* does.

**The glossary gains four terms**, each with its reason:
- `try` — en *try*, zh 试运行. Start a plugin as the driver would, send it a sample at each point, and read
  every answer with the driver's reader. zh avoids 测试, which is a plugins repository's own tests.
- `Daoris's own plugins` — en *Daoris's own plugins*, zh 「Daoris 自带的插件」. The plugins the install
  carries, installed only by a press. `offer` is its code word, never a label. Its avoid list is empty,
  since `--offer` is a flag people type.
- `held back` — en *held back*, zh 拦下. zh avoids 暂停.
- `data folder` — en *data folder*, zh 数据文件夹. What a plugin keeps; an update never touches it, and a
  removal leaves it.

**The glossary's doors:**
- `command.go.plugins` → `nav.plugins`;
- `menu.plugins` → `nav.plugins`, where it was `settings.domain.plugins`;
- `plugin.list.ask` → `help.title`, and `plugin.page.ask` → `help.title`;
- `plugin.page.toAgents` → `settings.domain.agents`;
- `plugin.page.toLanding` → `settings.landing.title`;
- `plugin.settings.open` → `nav.plugins`.

**The kind map** lists every key above under its kind. The sentences are the build's to write in both
languages: the health lines, the section bodies, the empty bodies, the terminal lines and the toasts. A
sentence has no budget; a toast keeps to two lines (110 and 55).

## 4. What the host answers

### 4.1 The routes

**Every plugin route stays `DAORIS.DRIVER`'s**, in `DriverModule.Plugins.cs` under MOD5's three things: a
`[DriverRoute]` handler, its name in the Desktop README's row, and a call from `bridge/plugins.ts`. Why not a
module of its own, as `BrowserModule` is:
- `BrowserModule` stands apart because its files are not the driver's.
- A plugin's are: the loop starts, stops and asks its process (`RunningPlugins`, `StopPluginAsync`, `Nudge`),
  and the catalogue is the driver's reader.

**Each reader lives in the driver library, and the route and the terminal call the same one.** LOG1c did
this for the machine log.

| Route | Status | Payload | Answers | Used by |
|---|---|---|---|---|
| `PLUGINS` | extended (see below) | none | the catalogue, the kit's points, the offers | the list, the strip, the bar's count |
| `PLUGIN` | new | `{ id }` | the page (see below), from `PluginPage.Read` | the page |
| `PLUGIN_ACTIVITY` | new | `{ id, since }` | from `PluginActivity.Read`: the summary per point by answer and failure, with median and slowest times; starts, exits and failures to start; sessions per declared agent (`session.started`'s `adapter`); sessions per server (`plugin.served`); pushes from the landing record; the newest 50 lines; the count skipped | Activity |
| `TAIL_SESSION` | unchanged | `{ id: "plugin:<id>", after }` | the plugin's words this run, and live as `SESSION_OUTPUT` | Activity's words |
| `PLUGIN_ACTION` | unchanged | `{ id, action }` | switched, or removed with its data folder named | the switch, *Remove plugin* |
| `PLUGIN_UPDATE` | unchanged | `{ id, apply? }` | the plan, or the update made | *Update…*, *Update now* |
| `PLUGIN_INSTALL` | unchanged | `{ offer }` | the offer installed | *Install* on an offer |
| `PLUGIN_READ` | new | `{ folder }` | what the folder's plugin declares, read by `PluginInstall.Read` and `Placement` as Ask Daoris's judge reads it, or `{ refusal }`, the same words, as an answer | *Install from a folder*, before the press |
| `PLUGIN_ADD` | new | `{ folder }` | `PluginInstall.Add`: added, never replaced; the loop nudged | *Install* in that drawer |
| `PLUGIN_NEW` | unchanged | `{ id, points, folder }` | the folder written | *Make a plugin* |
| `PLUGIN_TRY` | keeps the last (§4.3) | `{ id }` or `{ folder }` | the trial | *Try*, *Try a folder* |
| `PLUGIN_TEST` | new | `{ id }` | from `PluginChecks.TestAsync`: the tests' verdict, exit code, time and output (§4.3) | *Run tests* |
| `PLUGIN_OPEN_FOLDER` | new | `{ id?, which: install\|data\|plugins }` | opened, through the window kit's launcher (`OpenFolder`), as the log's folder is | *Open folder*, *Open the plugins folder* |
| `PICK_FOLDER` (`DAORIS.REGISTRY`) | unchanged | none | a folder, or none | *Choose…* |

**`PLUGINS` gains, per installed plugin:**
- `servers` (names) and `hook` (its command and declared points);
- `listening`: the points the running process said it listens on;
- `health`: `{ state, since, failure: { where, kind, at } | null }`, from the loop's `PluginHealth` (§2);
- `update`: `waits`, `current`, or null with no readable record. It comes from `PluginInstall.PlanUpdate`, a
  file read per plugin with a source.

The list stays light; the page asks for the rest.

**`PLUGIN` answers:**
- the manifest as written: each agent's name, command, posture or null, profile variable, package and install
  command; the hook's command and points; each server's name, command, and environment variable names;
- each point's kind and wait (`PluginKit.Points`) and whether the running process listens there;
- for `work/land`, the landing rules naming it (`driver.json`'s repository and workspace rules) and
  `LandingRules.PluginProblem`'s answer;
- the source record and whether an update waits;
- when it was installed (its install folder's time);
- its data folder: whether it exists, its files and bytes counted up to 10,000 entries or two seconds with
  `more` set past that, and its newest change;
- the test files its folder carries;
- the checks kept for it (§4.3).

A refused plugin's manifest is read as written, marked not taken.

**An environment variable's value never leaves the driver.** `PLUGIN` answers a server's `env` by name only,
and nothing on the page shows the raw manifest (§8).

**New refusals**, each with MOD5's three things: a code in `Refusals`, both catalogues, and its throw site.
- `PLUGIN_FOLDER_NOT_OPENED`: the system would not open it.
- `PLUGIN_NOTHING_KEPT`: an information-class refusal (D48 §6), for a data folder that was never made. The
  button is absent then; this answers a race.
- `PLUGIN_TESTS_NO_NODE`: no `node` on the PATH the application runs with.
- `PLUGIN_TESTS_NONE`: the folder carries no test file.

`PLUGIN_UNKNOWN` (exists) is the gone state.

### 4.2 The machine log's plugin events

New events in D94's catalogue, written where Daoris speaks to a plugin. They go to the `desktop` source from
the shell's loop and routes, and to `driver` from the headless host, as `SessionLog`'s do. Every value is a
name, a count, a flag or null (D94 §3).

| Event | Level | Written by | Data |
|---|---|---|---|
| `plugin.started` | info | the hook set; a landing or a hand-off, for its one frame | `plugin`, `points` (the points it listens on, joined by commas), `ms` (start to handshake), `by` (`loop`, `landing`, `hand`) |
| `plugin.stopped` | info | the hook set; a landing's one frame done | `plugin`, `why` (`off`, `removed`, `updated`, `changed`, `ended`) |
| `plugin.called` | info | the hook set, the landing, the hand-off | `plugin`, `point`, `answer` (`allow`, `hold`, `answered`, `pushed`, `not-pushed`), `ms` |
| `plugin.failed` | warn | the same | `plugin`, `where` (`start`, `process`, or the point), `kind` (`unstartable`, `exited`, `late`, `unreadable`, `errored`), `code` (an exit code, or null), `ms` |
| `plugin.served` | info | the driver, where it hands a session its servers | `plugin`, `server`, `session`, `handed` (false for `${browser}` with no shell) |
| `plugin.tried` | info, or warn when failed | the kit, at both doors | `plugin`, `passed`, `checks`, `failed`, `ms`, `door` (`screen`, `terminal`) |
| `plugin.tested` | info, or warn when failed | the tests' runner, at both doors | `plugin`, `passed`, `code`, `ms`, `door` |

**Never logged** (D94 §5):
- a hold's reason, and an answer's `message`;
- a pull request's address;
- a frame's contents;
- a command line or an environment value;
- anything the plugin wrote to stderr.

The plugin's words stay where they are kept today, the console's ring for this run. A pull request stays in
the landing record, which Activity reads.

**Health from the log** (`PluginHealth.FromLog`) is the last of `plugin.started`, `.stopped`, `.called` and
`.failed` for the plugin. An `app.stopped` of the source that ran it, written after them, means its process
went with that source. The in-process record and this reading share one table of cases.

### 4.3 What is kept

- **A plugin's last checks.** `<home>/plugins/.checks/<id>.json` holds the last trial and the last test run of
  an installed plugin, written atomically by the driver library at both doors. Each records when, from which
  door, the version tried, the verdict, and the sentences and output.
  - `.checks/` is a dot-folder, which the catalogue skips, as it skips `.data/` and `.trials/`.
  - Removal and update delete it, since it describes a copy that is gone.
  - A folder's trial or tests are printed and shown, never kept: a folder has no page.
- **The tests' run.** `daoris-driver plugins test <id|folder>` and `PLUGIN_TEST` share one runner:
  1. It copies the folder (an installed plugin's install folder) to `<home>/plugins/.trials/<run>/`, so a
     checkout is never written into and an install folder never grows a test's leavings.
  2. It runs `node --test` there, with `TEMP`, `TMP` and `TMPDIR` pointed inside the run, since nothing of
     Daoris's lives under the user profile (D63).
  3. It ends the process tree after five minutes, in the job the driver already uses (`ProcessJob`).
  4. It keeps the output's last 200 lines, with how many fell out.
  5. It removes the copy after.

  It exits 0 when the tests passed, 1 when they failed, and 2 when it could not run: no node, no tests, no
  folder, no home.

### 4.4 Two doors (D50)

Whatever the view can set, a terminal can, and each of the view's reads has a terminal twin too.

| On the view | At a terminal | |
|---|---|---|
| the list | `daoris plugin list` | exists |
| a plugin's page | `daoris-driver plugins show <id> [--json]` | new |
| the switch | `daoris plugin enable\|disable <id>` | exists |
| *Update…*, *Update now* | `daoris plugin update <id> [--yes]` | exists |
| *Remove…*, *Remove plugin* | `daoris plugin remove <id>` | exists |
| *Install* on an offer | `daoris plugin add --offer <id>` | exists |
| *Install from a folder…* | `daoris plugin add <folder>`, which replaces an installed plugin; the view never does, and says so | exists |
| *Make a plugin…* | `daoris-driver plugins new <id> --point <p>… --in <folder>` | exists |
| *Try*, *Try a folder…* | `daoris-driver plugins try <id\|folder>` | exists |
| *Run tests* | `daoris-driver plugins test <id\|folder>` | new |
| Activity | `daoris-driver plugins activity <id> [--since <30m\|2h\|3d>] [--json]` | new |
| *Open folder* | none: opening a folder sets nothing, and `plugins show` prints both paths | exempt |
| *Ask Daoris for a plugin* | `daoris-driver ask --workspace <name> --to <repo> "…"`, the ask Ask Daoris proposes | exists |

**Why the new verbs are `daoris-driver`'s.**
- `test` starts a process, and only `toolchain.ts` spawns in the CLI (D101's reason, unchanged).
- `activity` reads the machine log, whose one reader, `MachineLogReader`, is the driver's.
- `show` reads what only the driver knows: health, whether the process listens, readiness to land.

The CLI's `daoris plugin` keeps the catalogue's edits, where its twin reader already is. `daoris plugin list`
gains one line pointing at `plugins show` for health. Two spellings of one act would be a second twin of
`plugins.ts` (§8).

Each new verb is a function in the driver library that prints to the writer it is given, as
`PluginKitCommand` is, so a test runs the whole door in-process. Its exit codes are the family's: 0 clean,
1 a check failed (`test`), 2 it could not do what was asked.

### 4.5 Ask Daoris (D110)

Every control the view holds is a door, a door owed, or exempt with its reason. `HelpCoverageTests` reads the
Plugins view as a screen beside Settings' domains (§5).

| Control on the view | Hook, or local | Its answer |
|---|---|---|
| the switch | `usePluginAction` enable, disable | door: `plugin` enable, disable |
| *Remove…* | `usePluginAction` remove | exempt: a discard (D89), as today |
| *Update…*, *Update now* | `usePluginUpdate` | door: `plugin` update |
| *Install* on an offer | `usePluginInstall` | door: `plugin` add, by offer |
| *Install from a folder…* | `usePluginAdd` (new) | door: `plugin` add. The card's folder is one in a registered checkout, which a helper can name. The view's is any folder the person picked, which the helper does not invent |
| *Read* | `usePluginRead` (new) | exempt: it reads a folder the person picked and changes nothing |
| *Make a plugin…* | `usePluginNew` | exempt: a plugin is made as an ask at the repository that holds plugins (PLUG9), as today |
| *Try*, *Try a folder…* | `usePluginTry` | exempt: a trial changes nothing, as today |
| *Run tests* | `usePluginTest` (new) | exempt: it runs the plugin's code as the person, as a trial does, and changes nothing to propose |
| *Choose…* | `usePickFolder` | exempt: the system's picker, as today |
| *Open folder*, *Open the plugins folder* | `useOpenPluginFolder` (new) | exempt: it opens the file manager and changes nothing, as the log's folder does |
| *Ask Daoris for a plugin*, *Ask Daoris* | local: the opener | exempt: it opens Ask Daoris itself, and what follows is the ask Ask Daoris proposes (`ask_propose`) |
| a door into the view | `open('plugins', id)` | door: `go`, naming the view and the plugin |

Settings → Driver's *Open Plugins* (§5) is a local control of that domain, answered as the `go` door.

**No door is owed.** The one form a helper cannot propose, any folder on the machine, is left out by
design: a helper can invent a path.

**The go.** HelpPlaces' views gain `plugins`, and a go may name a plugin by id or an offer by `offer:<id>`,
through FRAME1i's `item`. The judge refuses an id this machine does not hold, naming the ones it does. A go
naming Settings → `plugins` is refused, naming the view. `help/places.ts` (the twin) opens it with
`open('plugins', id)`.

**These doors into a plugin's page are the opener's first callers:**
- Agents' *declared by plugin* chip;
- the plugin a landing rule names in Workspace → How work lands;
- a plugin proposal card's id.

**The room's plugin list gains** each plugin's health and whether an update waits, from the facts the
proposals are judged against. The doors table then names `plugins show|activity|test` and the Plugins view.

## 5. What leaves Settings

**Settings keeps only what is a setting.** Nothing about a plugin is set in Settings. Where plugins are looked
for is a fact of the Daoris home, so it stays as a read-only row beside the home. The Plugins domain retires
from `SETTINGS_DOMAINS`. Two places named *Plugins* would be one word for two things (D116 §2).

| Control today, in Settings → Plugins | Where it goes |
|---|---|
| The plugins folder's row, with its terminal hint and its reason | **Stays in Settings**, as a row in Settings → Driver under the Daoris home: *Plugins folder*, the path, `daoris plugin list` as its hint, and *Open Plugins* |
| A plugin row's switch | the page's header |
| A row's *Try*, and the report under it | the header's *Try*; the report in Tests, kept (§4.3) |
| A row's *Update…*, the plan under it, *Update now* and *Not now* | the header's *Update…*; the plan under the header |
| A row's *Remove* | the header's *Remove…*, asking once |
| A row's source line | the page's Source |
| A refused row's sentence | the page's health line |
| *Daoris's own plugins*: needs and *Install* | the list's last group; an offer's page |
| The kit's *New* | `＋` → *Make a plugin…* |
| The kit's *Try a folder* | the list's ⋯ → *Try a folder…*, and the kit drawer's second step |

**Where the old anchors point:**

| The anchor | Today | After |
|---|---|---|
| The Daoris menu's *Plugins* (`settings:plugins`) | Settings → Plugins | the Plugins view; its glossary door names `nav.plugins` |
| Ask Daoris's go to Settings → `plugins` | Settings → Plugins | refused by the judge, naming the view. A card written before the move still opens the view: `placeDoor` maps the old place to `open('plugins')` |
| `plugins` in `PLACE_DOMAINS` and `HelpPlaces.Domains` | a domain | moved to `PLACE_VIEWS` and `HelpPlaces.Views`, the twins changing together |
| `where.ts`'s domain names | *Plugins*, a domain | the view, and its chosen plugin (FRAME1i) |
| A remembered Settings domain, `daoris.settings = "plugins"` | opens Settings → Plugins | Settings opens at its default domain, as it does for any domain it lacks |
| The card ids `settings-plugin-kit` and `settings-plugin-offers` | named by no door | gone with their cards |
| Agents' *declared by plugin* chip | text | a door to the plugin's page |
| Workspace → How work lands' plugin chooser | names a plugin | stays, since the rule is a setting; the plugin it names becomes a door to its page |
| The words *Settings → Plugins* in the room's doors table and its goldens, the service's `plugin_propose` hint, the CLI's kit refusal (`plugins.ts`), the driver's usage and its comments, and `tools/`' comments | name the domain | name the Plugins view |
| `settings.domain.plugins` | a key in both catalogues and the glossary's `settings.domain.*` | removed from both catalogues |
| `HelpCoverageTests` | reads Settings' domains | reads Settings' domains and the Plugins view (`plugins/PluginsView.tsx` and what it imports), with today's rows moved and the view's new controls answered for |

The coverage test's source list and the domain's removal land in one row (PLUGUI1c). A removed domain whose
rows still name it fails the test, and so does a view whose controls it does not read.

## 6. The build

These rows are ready for `TASKS.md`. Their lanes are DEV2's ids.

**Every row's proof:**
- the stories of what it adds, each smoke-tested by `composeStories`;
- vitest over the mocked bridge;
- the fast half of each .NET suite it touches;
- `npm run verify`;
- for every row that changes the window, the parent's look on the window after the merge: both themes, both
  languages, at 1280 px and 680 px.

**Order:**
1. PLUGUI1b waits on FRAME1c. PLUGUI1d touches no page file, so it can run beside PLUGUI1b.
2. Then PLUGUI1c (after b), and PLUGUI1e (after d).
3. Then PLUGUI1f (after c and e), then PLUGUI1g.
4. PLUGUI1h after PLUGUI1c and FRAME1i.

PLUGUI1c shares the Web settings lane with FRAME1g: one waits for the other.

- [ ] **PLUGUI1b — the Plugins view on the frame, on today's answers.** Waits on FRAME1c. Covers P1, P2, P8 and
  P9.
  - **What:**
    - `VIEWS` gains `plugins`: shell-only, the `plug` icon, after Search. `nav.plugins` and `command.go.plugins`
      are added. The Daoris menu's *Plugins* opens the view.
    - `plugins/` holds `PluginsView`, with its organisms `PluginList`, `PluginPage` and `OfferPage` on
      `ListPane` and `ViewMain`. The glossary gains §3.8's kinds, terms and doors.
    - The list: the groups from today's `enabled`, `problem` and `running`. `failing` and `ready` arrive with
      PLUGUI1f, so a sound plugin whose process is not up shows no word until then. The strip's
      `StripMark`s, `＋` (*Ask Daoris for a plugin*, *Make a plugin…*), and ⋯ (*Try a folder…*).
    - The page: the header's four acts, with *Remove…* asking once. Points (declared), Agents (names),
      Source, Tests (the trial's report, held by the page while the view lives), Data folder (its path),
      and the foot's terminal lines.
    - The offer's page; the kit drawer, importing `settings/PluginKit.tsx` as it stands; *Ask Daoris for a
      plugin*'s opener.
    - The chosen plugin, and the list's closing and width, remembered.
    - Settings → Plugins stays until PLUGUI1c: for one row's time, the screen has two doors to the same acts.
  - **Lanes:** `web-shell`.
  - **Proof:**
    - the stories:
      - `PluginList`: empty; loading; an error; each group; a refused row; a 中文 name; the strip; laid over;
      - `PluginPage`: running; off; refused; a landing plugin; an agents-only plugin; no source record; an
        update's plan; a refused plan; a trial passed and failed; *Remove…* asking;
      - `OfferPage`, with needs and with a problem;
      - nothing chosen, gone, and loading;
    - `PluginsView.test.tsx`: choose; switch; update's two presses; install an offer and the list choosing it;
      remove's two presses; gone by `PLUGIN_UNKNOWN`;
    - `commands.test.ts` (*Plugins* shell-only) and `appMenus.test.ts` (the menu opens the view);
    - `names:check` reporting no finding for the new keys, which the glossary's kinds and doors list;
    - `test:web`'s disclosure spec: no Plugins place in a browser (the parent runs it at the merge).
  - **Look:** the list's groups; the strip at 680 px, and laid over; a plugin's page beside Ask Daoris; an
    offer's page; *Remove…*'s second press; the kit drawer.
- [ ] **PLUGUI1c — Settings keeps only what is a setting.** After PLUGUI1b. Covers §5.
  - **What:**
    - The domain retires. `PluginKit`, `PluginOffers` and `PluginUpdate`, with their stories and tests, move
      to `plugins/`.
    - The plugins folder's row joins Settings → Driver, with *Open Plugins*.
    - Agents' chip and How work lands' named plugin become doors.
    - The anchors of §5.
    - `HelpPlaces` and `places.ts` move `plugins` to the views, and a go to Settings → `plugins` is refused
      naming the view.
    - `HelpCoverageTests` reads the view, with today's rows and a local row for the opener, and answers
      Settings → Driver's *Open Plugins* as a local `go` door.
    - The room's doors table and goldens, the service's hint, the CLI's refusal and the driver's usage say
      *Plugins*.
  - **Lanes:**
    - `web-settings`: `domains.ts`, `DriverDomain`, `AgentsDomain`, `Landings`, and `settings.domain.plugins`
      in both `settings.domain.json` catalogues;
    - `web-shell`: the moved files, `places.ts` and `where.ts`;
    - `driver`: `HelpPlaces`, `HelpGoProposals`, `HelpRoomDoors`, the goldens, `HelpCoverageTests`, and the
      usage;
    - `service`: `KnowledgeTools.Help.Plugin.cs` and `HelpProposalBox.Plugin.cs`;
    - `cli`: `plugins.ts` and its test.
  - **Proof:**
    - `domains.test.ts`;
    - `SettingsView.test.tsx`: a remembered `plugins` opens the default domain;
    - `DriverDomain.test.tsx`: the row and its door;
    - `AgentsDomain.test.tsx` and `Landings.test.tsx`: the doors;
    - `places.test.ts` and `HelpGoProposalsTests` holding one table: a go to `plugins`; Settings → `plugins`
      refused; an old card opening the view;
    - `HelpCoverageTests`: a control on the view answered for, and a removed one failing;
    - `HelpRoomGoldenTests`, the service's help tests, and `plugins.test.ts`.
  - **Look:** Settings' list without Plugins, in both languages; the Driver domain's new row; the chip opening a
    plugin's page.
- [ ] **PLUGUI1d — the machine log's plugin events, health, and `plugins show|activity`.** Can run beside PLUGUI1b.
  Covers P4 at its source, P5 and P10.
  - **What:**
    - §4.2's events, written by `HookSet`, `LandingPlugins`, the hand-off, the handing of servers and the kit,
      never with a word. `plugin.tested` waits for PLUGUI1g's runner.
    - `PluginHealth`: the loop's record, and `FromLog`, over one table of cases.
    - `PluginActivity.Read` and `PluginPage.Read`.
    - `daoris-driver plugins show <id> [--json]` and `plugins activity <id> [--since] [--json]`.
    - The machine log design's §4 gains the rows.
  - **Lanes:** `driver`, with the headless host's `TreesConsole`; `modules` only where the loop builds
    `HookSet` (`DriverLoop`) and the landings build `LandingPlugins` (`DriverModule.Trees`, `.Help`), to hand
    each the loop's `MachineLog` and `PluginHealth`.
  - **Proof:**
    - `HookSetLogTests`: each event, and a hold's reason and a plugin's stderr absent from every line;
    - `PluginHealthTests`: one table, read from the record and from a log;
    - `PluginActivityTests`: a fixture log's summary, a torn line skipped and counted, a period;
    - `PluginPageTests`: no environment value read out, the data count's bound, landing readiness;
    - `PluginsCommandTests`: both verbs in-process, with their exits.
  - **Look:** nothing on the window.
- [ ] **PLUGUI1e — the host answers the page.** After PLUGUI1d. Covers P3 and P7's host half.
  - **What:** `PLUGINS` extended; `PLUGIN`; `PLUGIN_ACTIVITY`; `PLUGIN_READ`; `PLUGIN_ADD`;
    `PLUGIN_OPEN_FOLDER`. Also the Desktop README's rows, and the new refusals with their three things.
  - **Lanes:** `modules`; `driver` only where a reader needs a public seam.
  - **Proof:**
    - `DriverModulePluginsTests`: each answer, `PLUGIN_ADD` refusing an installed id, `PLUGIN_READ`'s refusal
      given as an answer, the open's refusals;
    - `DriverModuleRoutesTests`;
    - the refusal catalogue tests.
  - **Look:** nothing on the window.
- [ ] **PLUGUI1f — the page whole.** After PLUGUI1c and PLUGUI1e. Covers P3, P4, P7 and P10 on the window.
  - **What:**
    - health's marks, words and lines (§2); the bar's count; *update available*;
    - Points, Agents and Servers in full, with their doors; Activity, with the words live; Data folder; Source,
      with the plan inline;
    - *Install from a folder…*; the list's ⋯ → *Open the plugins folder*;
    - §3.6's container rules.
  - **Lanes:** `web-shell`; `driver` for `HelpCoverageTests`' rows (`usePluginAdd`, `usePluginRead`,
    `useOpenPluginFolder`).
  - **Proof:**
    - the stories:
      - each health state, with its line;
      - *update available*;
      - a server with environment names;
      - a landing plugin with its rules and its pushes, and one that cannot land;
      - Activity: quiet, busy, failing, with the words live, and no log;
      - the data folder: absent, bounded;
      - the drawer: reading, refused, installed;
    - vitest at a narrow container (480 and 560 px);
    - `HelpCoverageTests`.
  - **Look:**
    - a failing plugin's page and the bar's count;
    - Activity at 680 px;
    - the drawer;
    - the in-app browser's plugin and a landing plugin, installed from the offers on the parent's scratch
      machine.
- [ ] **PLUGUI1g — a plugin's checks: the last trial kept, and its own tests.** After PLUGUI1f. Covers P6.
  - **What:**
    - `PluginChecks`: kept and cleared (§4.3).
    - `daoris-driver plugins test` and `PLUGIN_TEST`.
    - `PLUGIN` answering the checks.
    - The Tests section in full, with *Run tests*.
    - `plugin.tested`, written by the runner at both doors.
  - **Lanes:** `driver`, `modules`, `web-shell`, and `driver` again for the coverage test's row.
  - **Proof:**
    - `PluginChecksTests`:
      - a trial kept at both doors, then replaced;
      - removed with the plugin, and deleted by an update;
      - the copy under the home, `TEMP` pointed inside it;
      - a test that hangs, ended at its bound;
      - a folder's run never kept;
    - the host's verb test and the route's;
    - the stories: never tried; passed; failed; no tests; tests running; tests failed with their output;
    - `HelpCoverageTests`.
  - **Look:** *Run tests* on the offers' landing plugin, whose own tests fake its platform's tool; a failed trial.
- [ ] **PLUGUI1h — Ask Daoris reaches the Plugins view.** After PLUGUI1c and FRAME1i.
  - **What:**
    - the go names a plugin or an offer, judged against this machine's;
    - the room's plugin list carries health and updates, and its doors name the new verbs and the view;
    - the page's ⋯ → *Ask Daoris*, with its first message;
    - a proposal card's plugin id becomes a door to the page.
  - **Lanes:** `driver`; `service`, only if FRAME1i left the go's wire without `item`; `web-shell`.
  - **Proof:**
    - `HelpGoProposalsTests`: a plugin, an offer, an unknown id refused;
    - `HelpRoomGoldenTests`, `where.test.ts`, `places.test.ts`, `ProposalCard.test.tsx`;
    - `HelpCoverageTests`: the local row.
  - **Look:** from Overview, Ask Daoris asked to open a named plugin, and asked about a failing one.

## 7. What does not change

- **D64 §7: a plugin cannot add a view.** This view is Daoris's own. It reads plugins as data and draws them
  with the host's atoms, and no plugin code runs in the page (D52). A plugin's name, description, README
  lines and words are content, shown through `Inline` as they are.
- **D47 §4:** the view is shell-only, and a browser learns nothing of it.
- **D89:** a removal stays the person's own press. D101's kit, D103's source record and offers, D100's
  landing and D102's hand-off are unchanged, and so is every file under the home (D63).
- **D118:** the frame. **D116:** the names.
- **The wire** (D64 §4) and **the points** (D101): unchanged. Nothing here asks a plugin anything new.

## 8. Not chosen

- **Keeping plugins a Settings domain, with a bigger row each.** The owner asked for a screen. A domain has no
  list, no main area and no memory, and a record under its row lasts only while the domain is shown (PL4,
  PL9).
- **Grouping by kind**: hooks, agents, servers. A plugin may be all three, and the person acts on its state.
- **A plugin's words in the panel's console**, and **its page in the side bar.** Both would make a frame
  region change with the view (§3.5; FRAME1a §3c).
- **A module of its own, `DAORIS.PLUGINS`**, as `BrowserModule` is. The loop owns the processes, and the
  catalogue is the driver's (§4.1).
- **A plugin's words in the machine log.** D94 §5: the log never holds anyone's words, and a second copy would
  have none of the ring's bounds. The words stay this run's, and the page says so.
- **Health from the log alone.** A log write that fails is dropped by design (D94 §2). The process that runs
  the loop knows exactly, and the log is for the process that does not.
- **A `--plugin` filter on `daoris-driver logs` and on `LINES`** as the page's source. The page needs a summary,
  the landing record and the sessions per agent. That is a reader of its own, and `activity` prints it.
- **Aliases for the CLI's verbs under `daoris-driver plugins`** (`list`, `add`, `update`…). Two spellings of
  one act would be a second twin of `plugins.ts`.
- **A toggle atom for the switch.** It would be a new control the platform draws for one place. A button named
  for its act reads the same in both languages, and a check beside a title reads as a selection.
- **The raw `plugin.json` on the page.** An environment value may be a key. The page reads the manifest as a
  person reads it, and the file is one *Open folder* away.
- **A plugin's README rendered on its page.** Its *What it needs* is read already. The rest is its author's
  documentation for someone making or reviewing it, opened from its folder.
- **Running a plugin's own tests in place**, in its install folder or its source checkout. The install folder
  is replaced whole by an update, and a checkout is another repository's working tree. The copy is the
  runner's own.
- **A history of trials.** A trial checks what the plugin answers now. Its history is `plugin.tried`'s lines,
  counted in Activity.
- **A search or a filter in the list.** No machine holds a screen of plugins today. The look that first finds
  one asks for it.
- **A failing plugin in Overview's *What needs you***, for now. The bar's count says it once, and every quest a
  failing plugin holds already carries its reason.
- **A marketplace, a registry, or a catalogue of others' plugins.** D24, D57 and D64 §7 stand: a plugin is a
  folder somebody put on a machine.
- **Dropping a folder on the list to install it.** A stray drop is absorbed (INT2), and the picker is the door.

## 9. What only the window and a real plugin can prove

- **The widths:** the room rule's numbers at 680 px, the header's acts at the 560 px and 480 px thresholds, the
  budgets in both languages. The names' budgets are estimates until the window measures them (D116 §4).
- **Health against a real plugin**, one whose process exits, answers late or crashes at start. The tests use
  fakes on the wire's channel.
- **The tests' run of a real plugins repository's suite**, with the scratch `TEMP`, and `node` on the PATH
  that a start from the desktop hands the application (USE1g). No real plugins repository exists yet
  (D101).
- **A landing plugin's pushes in Activity**, from a real platform's pull request. None has run against one
  (D100, D102, D103).
