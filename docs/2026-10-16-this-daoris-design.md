# This Daoris: one page for how this Daoris is wired, every setting found from it, and setting the wiring up

The design for WIRING1a, D171's companion page, and the rows WIRING1b–e. Written 2026-10-16 from the code at `892059f2`,
cited by file and line, and from D172's layout (`docs/2026-10-16-install-layout-design.md`), whose folders it shows. It
follows the platform UX contract (`docs/2026-09-19-platform-ux.md`, *Forms and settings* and *Doors, names and what the
page may say*) and the translation contract (`.claude/knowledge/translation-parity.md`, the glossary).

## 0. The owner's direction

> *"for every setting and how the server and data are wired we need some place to show it, or to set it up, with good
> UI/UX"*

Three asks: **every wire shown with where it comes from** (§3), **every setting found in one place** (§3.10), and **the
wiring set up from there** (§4).

## 1. What exists, and what it leaves out

- **Settings → Driver** shows the home's path, how it stands to the account's `DAORIS_HOME`, the start's notice and the
  adopted host's notice (`DriverDomain.tsx:81-102`, from `STATE`'s `home`, `homeNotice`, `homeAccount`, `hostNotice`,
  `DriverRoutes.cs:275-293`), the plugins folder, the driver's dials and the update (`Update.tsx`, UPDATE1b).
- **Settings → AI features** shows the service's tier and note verbatim (`/api/status`, `Program.cs:416-428` of the HTTP
  host); **Tools** each program's way and version; **Machine log** the log's lines.
- **A terminal finds the running Daoris** by `<home>/running.json` (D171's FINDDAORIS1 note), and nothing on the window
  shows that file, the launchers in `app/bin/`, or whether they are on a `PATH` (D124 §10: nothing puts them there).
- **Nothing answers** the folders' sizes, which variables are in effect and where each came from, who holds the home's
  lease (`HomeLease.HeldBy`, HOMELEASE1), when the loop last looked (`DriverLoop.Look`, `DriverLoop.cs:356`),
  whether this window hosted or adopted its service (`HostSupervisor.cs:8`), which host and connector programs were
  found and how (`ServiceHostLocator.Locate`, `ServiceHostLocator.cs:100`; `KnowledgeConnector.cs:23, 41, 153`), or the
  versions of the programs this install carries. `/api/status` answers no version.
- **Settings are spread** over seven Settings domains (`settings/domains.ts:46-65`), the agents' pages, the workspace and
  repository pages and the Plugins place (D150), with nothing to search them by.

## 2. The place

**Settings → This Daoris** (本机), a machine domain (`machine: true`): the desktop only, never offered to a browser (D47
§4), so no host route answers any of it and the page may print this machine's paths (PathText), as Driver's home row does.
It is the first of the machine domains, after AI features: `{ id: 'wiring', label: 'settings.domain.wiring' }`.

- **It shows; the domain that sets a thing keeps its control.** A row whose value is set elsewhere carries a door to
  where it is set (*Open Driver*, *Open Tools*, the workspace's page), as every way in opens the domain its fact is set in.
- **What moves here from Driver**: the home row with its two notices, which become the head of §3.1. Driver keeps a row
  *Daoris home* whose control is *Open This Daoris*.
- **Cards, a setting a row** (`SettingRow`): a label, a one-line hint that is the terminal's twin as code, the value at the
  right, the paragraph on the info glyph. Each card below is one of the page's sections, in this order.

## 3. Every wire and its source

Each row says **what** and **where it came from**, in one of a few words a test can hold: *this start* (the window's own
process was handed it), *your account* (the account's variable, which a new terminal also gets), *this machine* (the
machine's variable), *set by Daoris* (D63's own setting of the home), *a file* (named), or *not set* (and what is used
instead).

### 3.1 Folders

| Fact | Read from | Shown as |
|---|---|---|
| The Daoris folder | the loop's home (`DriverLoop`), D172 point 2 | its path; *installed*, *development run* or *named for this start*; how it stands to the account's `DAORIS_HOME` (`HomeAccount`), and where the account names an install's `data/`, *read as this install* |
| The start's notice and the adopted host's notice | `homeNotice`, `hostNotice` | the two lines Driver shows today, under the folder |
| Each top folder (D172's seven, and `update/`) | the layout's table (HOMELAYOUT2) | its window name (§8), its path, its size and when it was measured, *Open* |
| An entry still at its old place | the layout's rule (D172 point 5) | under its folder: its name, its size, and why it stays (*a conversation may resume in it*, *a repository's `.mcp.json` names it*, *could not be moved: in use*) |
| What is not Daoris's, or an earlier build's | names the layout does not hold (`webview2/`, `salvage/`, …) | one row each with its size: *left by an earlier build* or *not written by Daoris*; nothing offers to delete it |

**Sizes are measured, never guessed.** Opening the page asks once; a walk runs in the background, one at a time, not
following links or junctions, and the answer carries each folder's bytes and when they were counted. Until then a row says
*Measuring…* (static text, FREEZE1); *Measure again* asks anew. A folder that cannot be read says so and how far it got.

### 3.2 The service

| Fact | Read from | Shown as |
|---|---|---|
| Its address | `DAORIS_SERVICE_URL`, else `http://localhost:5177` (`Program.cs:70-71` of the desktop) | the address, its source |
| Hosted or adopted | `HostSupervisor` | *started by this window*, *already running, adopted*, or *none* |
| Whether it answers | the supervisor's last probe | *answering* with when, or *not answering* with why |
| Its tier and version | `/api/status`'s `tier`; `version`, a field WIRING1b adds | as the service says them |
| The host program | `ServiceHostLocator.Locate`: `DAORIS_HTTP_HOST`, beside the application, the home's `bin/` | its path and which of the three |
| The connector handed to sessions | `KnowledgeConnector`: `DAORIS_MCP_HOST`, `app/daoris-knowledge/`, the home's `bin/` | its path and which |
| The person key (PERSONDOOR1) | whether the host proved it holds it | *proved* or *not proved*; never the key |
| Each workspace's remote | `remotes.json`, or `DAORIS_REMOTE_*` (which then win whole, WSP3) | the workspace, the address, the key's audit prefix, the source; *Open the workspace* |

### 3.3 The loop

| Fact | Read from | Shown as |
|---|---|---|
| Who holds the home | `HomeLease.HeldBy` (`LeaseHolder`: kind, process, since) | *this window*, *a terminal's `daoris-driver drive`*, or an act holding it (`LeaseHolder`'s other kinds), with its process and since when, or *nobody* |
| When it last looked | `DriverLoop.Look` | a time (`format.ts`), and *starting* until the service is up (`STATE`'s `ready`) |
| What holds its starts | `STATE`'s `holds` and the update's drain | *draining for an update*, the holds' count, a door to Driver |
| How often it looks | `driver.json`'s `pollSeconds` | the value, *Open Driver* |

### 3.4 Settings files

One row per file of D172's `data/` and `profiles/` that a person's choice lives in: `driver.json`, `harnesses.json`,
`accounts.json`, `permissions.json`, `tools.json`, `plugins.json`, `browser/settings.json`, `update.json`, `remotes.json` and
`keys.json`. Each says **what it holds** in one line (a catalogue word per file, §8), its folder, its size, when it was last
written, whether it **reads** (a file that does not parse says *cannot be read: the screens that edit it refuse until it
is fixed*), the variable that **overrides** it where one is set (`DAORIS_DRIVER_CONFIG`, …), and the **door** to where it
is edited. A *Records* fold lists the records (`usage.json`, `landings.json`, `sessions/`, `knowledge.db`, …) the same
way, with no door. The page never shows a file's contents.

### 3.5 Variables

Every variable Daoris reads, from one list in the code (WIRING1b holds it against a search of the sources): the home and
its per-file overrides, `DAORIS_SERVICE_URL`, the hosts' `DAORIS_HTTP_HOST` and `DAORIS_MCP_HOST`, the remote trio, the
knowledge service's (`DAORIS_KNOWLEDGE_ROOT`, the `DAORIS_EMBED_*` and `DAORIS_KNOWLEDGE_*` of `ServiceFactory.cs:44-51`),
`DAORIS_MODE`, `DAORIS_WORKSPACE`. A set one is a row: its value (a path or an address), its source (§3), and, where the
window's value differs from the account's, both. An unset one is in a fold, with what is used instead. A variable that
holds a key (`DAORIS_REMOTE_KEY`, `DAORIS_SERVICE_KEY`) says *set*, with the key's audit prefix, and never its value. The
service's own variables are what this window handed the service it started; for an adopted service the row says *the
service's own, read when it started*.

### 3.6 Versions and the update

| Fact | Read from | Shown as |
|---|---|---|
| The program | the application's informational version | the version and build, as the release workflow stamped it |
| The UI kit | its assembly's version | e.g. *Shenora 0.21.1* |
| The doctrine tool | `app/cli/node_modules/daoris/package.json` | its version, or *not carried* |
| The driver's verbs | `app/daoris-driver/`'s assembly | its version, or *not carried* |
| The service and its connector | `/api/status`'s `version`; the connector's file | each version |
| .NET and Node.js | the runtime; Node as Tools resolves it | each version; *Open Tools* |
| The update | `STATE`'s update (UPDATE1b) | one line, what is staged and how the last swap ended, *Open Driver* |

### 3.7 A terminal

| Fact | Read from | Shown as |
|---|---|---|
| The launchers | `app/bin/daoris`, `daoris.cmd`, `daoris-driver`, `daoris-driver.cmd` (`desktop-publish.mjs:303-352`) | each present or not; a development run has none, and says so |
| On a `PATH` | the account's `PATH` and the machine's | *on your account's PATH*, *on this machine's PATH*, or *on neither* |
| What a new terminal runs | the first `daoris` and `daoris-driver` the account's then the machine's `PATH` finds, by `PATHEXT` | *this install's*, *another* (its path), or *none* |
| The address file | `run/running.json` (D171, D172), read as a terminal reads it | its address, process and since, and whether a terminal takes it: *taken*, or why not (*its process has ended*, *not a loopback address*, *not answering as this home's service*) |
| The account's `DAORIS_HOME` and `DAORIS_SERVICE_URL` | §3.5 | the two rows again, since a terminal reads them first (D171 point 1) |

### 3.8 The knowledge store

Its file (`DAORIS_KNOWLEDGE_DB`, else `data/knowledge.db`) with its source and its size counting the `-wal`; the family
root the hosted service indexes (`DAORIS_KNOWLEDGE_ROOT`, what the window handed it); the tier, from §3.2, with *Open AI
features*.

### 3.9 The logs

The machine log's folder, its size, the days it keeps (30), its newest day, *Open*, and *Open Machine log*; the engine's
log in the window's memory (`profiles/window/chromium/cef.log`), present or not.

### 3.10 Every setting (WIRING1d)

A search field over every setting Daoris has, wherever it is set: each row its label in the reader's language, its
current value, **where it is set** (*default*, *driver.json*, *a workspace* with how many, *a repository* with how many,
*the environment*) and the door to the domain or page that edits it, at the part it names. The search matches a setting's
label and hint in **both** languages and its terminal twin, so `strikes`, *strikes* and 次数 find the same row. Settings'
domain list gains a search field at its head that opens this section with the query.

- **Where the list comes from**: a read, `SYSTEM_SETTINGS`, answering each setting's id, value, source and the counts of
  the workspaces and repositories that set it; and the page's catalogue (`settings/everySetting.ts`) of each id's label,
  hint and door. A census test holds the two to each other, and holds every `SettingRow` in the Settings domains and the
  places' setting pages to the catalogue.
- **It edits nothing.** A setting is changed where it lives; the row is a door.

## 4. Setting it up (WIRING1e)

| Action | Press | What it changes | Said first | Ask Daoris (`HelpCoverageTests`) |
|---|---|---|---|---|
| *Open* a folder | `SYSTEM_OPEN_FOLDER` `{ folder }`: a D172 folder's or a file's id, never a path; the module resolves it | nothing | — | `Exempt`: opens a window, changes nothing to propose |
| *Copy a terminal line* | none: the page's clipboard | nothing | — | no row: no press |
| *Put on my PATH* | `SYSTEM_PATH` `{ on: true }` | appends the install's `app/bin` to **your account's** `PATH`, never the machine's, and tells running programs the environment changed | yes, inline (`InlineConfirm`): the folder, *your account's PATH only*, that terminals opened after it find `daoris` and `daoris-driver`, and which `daoris` comes first if another does | `Exempt`: the account's own environment, the person's act, as the update's three words are (D139's UPDATE1e note) |
| *Take off my PATH* | `SYSTEM_PATH` `{ on: false }` | removes exactly the entry it would add | yes, inline | the same |
| *Change the service's address* | `SYSTEM_SERVICE_ADDRESS` `{ url }`, or `{ url: null }` for the default | sets or clears **your account's** `DAORIS_SERVICE_URL`; takes effect when Daoris starts again | yes, inline: the old value and the new, and that a terminal reads it too | `Exempt`, as *Put on my PATH* |

- **The terminal line** is offered for PowerShell, Command Prompt and Git Bash (a segmented choice), setting
  `DAORIS_HOME` and `DAORIS_SERVICE_URL` for that terminal only and putting `app/bin` first on its `PATH`, so a script or a
  terminal from before D171's address file reaches this Daoris. It names this machine's paths and goes nowhere but the
  clipboard.
- **Refusals are data** (`{ why, message }`, bridge-presses row 1), each a code worded in both languages: `PATH_NOT_INSTALL`
  (a development run has no launchers), `PATH_NOT_WINDOWS`, `PATH_UNREADABLE` (the account's `PATH` could not be read, so
  nothing was written), `SERVICE_NOT_LOOPBACK` (the local service is sent this machine's paths, D46, D171's note),
  `SERVICE_MALFORMED`, `FOLDER_UNKNOWN`, `FOLDER_NOT_OPENED`.
- **Amends D124's rejection of the account's `PATH`**, which was Daoris putting it there for every child: here it is the
  person's press, said first, undone by the next, and Daoris never changes it by itself (D171's WIRING1a note).

## 5. What is never shown

- **A secret**: a deployment key is its audit prefix, as the deployment's own `keys list` prints it (`remotes.ts:16`); an
  agent's API key is its handle (`HarnessKeys`, `HarnessSelection.cs:23-37`); a variable holding a key is *set* and that
  prefix. The read route never answers a variable outside its list, and no list holds a value of a key.
- **Any file's contents** under `profiles/`, nor a sign-in, a token or a cookie: that folder is sized, never read.
- **The whole `PATH`**: only whether it holds the install's `app/bin`, and the first `daoris` and `daoris-driver` it finds.
- **Anything to a browser**: no HTTP route, no host field; a browser is offered no such domain.

## 6. The read and its terminal twin (WIRING1b)

**`SYSTEM_WIRING`**, a read in a new route domain, `system` (`SystemRoutes.cs`, `bridge/system.ts`, its line in
`DriverModule.Domains` and its README row; bridge-presses, *A read*): no `[Publishes]`, a `StatePublisherTests` row with
`""` and `null`, a `useQuery` keyed `['system', 'wiring']`. The answer, every field added and never renamed (a wire shape):

```
{ schema: 1,
  folder:   { path, kind: install|development|named, account: same|overridden|this-start, accountNames: root|data|none,
              notice, hostNotice },
  folders:  [ { id: app|update|data|profiles|work|cache|run|logs, path, present, bytes, measured,
                old: [ { entry, path, bytes, why } ] } ],
  leftOver: [ { name, path, bytes, why: earlier-build|not-daoris } ],
  service:  { url, from, held: hosted|adopted|none, answers, asked, tier, version, proved,
              host: { path, from }, connector: { path, from } },
  remotes:  [ { workspace, url, keyPrefix, from: file|environment } ],
  loop:     { holder: { kind, pid, since, thisWindow } | null, looked, ready, draining, holds, pollSeconds },
  files:    [ { id, folder, path, present, bytes, written, reads, overriddenBy, door } ],
  variables:[ { name, value | null, secret, prefix | null, from, account | null } ],
  versions: { program, build, kit, cli, driver, service, connector, dotnet, node },
  terminal: { launchers: [ { name, present } ], onAccountPath, onMachinePath, firstDaoris, firstDriver,
              address: { present, url, pid, since, taken, why } },
  knowledge:{ path, from, bytes, familyRoot },
  logs:     { path, bytes, days, newest, engineLog } }
```

- A fact that could not be read is the field's `null` with a sibling `…Why` code, never an absent field.
- `bytes` is `null` while measuring; `{ measure: true }` in the payload starts a walk, and the answer says `measuring`.
- **The service's `/api/status` gains `version`.** The rehearsals compare that answer: WIRING1b searches
  `tools/*rehearsal*.mjs` for its neighbours (`tier`, `semantic`, `note`, `proof`) before adding it.
- **The presses** of §4 are in the same domain and declare a new scope, `system`, added after the others with its key
  family `['system', …]` in `stateHub.ts`, so every window reads the wiring again after one.

**`daoris-driver wiring [--json] [--measure]`** (D50), in the driver host, reading the same facts through one reader in the
driver library (`Daoris.Driver.Platform`), which the route calls too. What only the window knows (hosted or adopted, the
window's own process's variables, the kit's version) the terminal answers `null` with `why: window`, and its text says
*the window knows*. `--json` prints the route's shape, so the twins share one test table.

## 7. Screen states

| State | What the page shows |
|---|---|
| First load | skeleton rows in the cards' place, as every machine domain's first load |
| Ready | every card |
| Measuring | each folder's size *Measuring…*; the rest as ready |
| A shell older than the page | one line: *This window answers no wiring: it is older than this page.* Driver's home row is its fallback |
| The service not answering | §3.2 leads with why, *Read again*; the rest as ready |
| A fact that could not be read | that row in its own words; no other row changes |
| A development run | *development run* at the head; §3.7 says a development run has no launchers; §3.6's update says it is not updated |
| Entries at old places | under each folder, as §3.1 |
| No match in *Every setting* | an empty state, *No setting matches "…"*, and that either language finds a setting |
| 1546 and 680 px, light and dark, English and 中文 | the frame's list pane closed to its strip at 680; rows stack below 26rem; paths break at their separators |

## 8. Both languages

Keys name the place (`translation-parity`); the glossary gains **This Daoris** (本机) beside *this machine*, which already
says 本机, with a `means` that keeps the two apart (the window's place for this install, against the computer), and
`names:check -- --strict` is run on it.

| Key | Kind | English | 中文 |
|---|---|---|---|
| `settings.domain.wiring` | domain | This Daoris | 本机 |
| `settings.wiring.folders` | card | Folders | 文件夹 |
| `settings.wiring.folder.app` | row | Program | 程序 |
| `settings.wiring.folder.update` | row | Update | 更新 |
| `settings.wiring.folder.data` | row | Your settings and records | 你的设置和记录 |
| `settings.wiring.folder.profiles` | row | Sign-ins | 登录信息 |
| `settings.wiring.folder.work` | row | Trees and kept files | 工作树和保留的文件 |
| `settings.wiring.folder.cache` | row | Downloads | 下载内容 |
| `settings.wiring.folder.run` | row | While running | 运行时文件 |
| `settings.wiring.folder.logs` | row | Machine log | 本机日志 |
| `settings.wiring.leftOver` | row | Not Daoris's, or an earlier build's | 不属于 Daoris 或旧版本留下的 |
| `settings.wiring.measuring` | status | Measuring… | 正在计算大小… |
| `settings.wiring.service` | card | Service | 服务 |
| `settings.wiring.loop` | card | Driver loop | 驱动循环 |
| `settings.wiring.files` | card | Settings files | 设置文件 |
| `settings.wiring.variables` | card | Variables | 环境变量 |
| `settings.wiring.versions` | card | Versions | 版本 |
| `settings.wiring.terminal` | card | Terminal | 终端 |
| `settings.wiring.knowledge` | card | Knowledge store | 知识库 |
| `settings.wiring.logs` | card | Logs | 日志 |
| `settings.wiring.every` | card | Every setting | 所有设置 |
| `settings.wiring.open` | button | Open folder | 打开文件夹 |
| `settings.wiring.copyLine` | button | Copy terminal line | 复制终端命令 |
| `settings.wiring.pathOn` | button | Put on my PATH | 加入我的 PATH |
| `settings.wiring.pathOff` | button | Take off my PATH | 从我的 PATH 移除 |
| `settings.wiring.serviceAddress` | button | Change address… | 更改地址… |

Each source word (*this start*, *your account*, …), each file's line, each refusal code and each *why* is a key under
`settings.wiring.from.*`, `settings.wiring.file.*`, `settings.wiring.refused.*` and `settings.wiring.why.*`. A service's or
a notice's sentence is passed through verbatim, as the home's notice is today.

## 9. Not chosen

- **A page of its own outside Settings**: what is true about this machine is Settings' (the glossary's *settings*), and a
  second place for the home row would be two answers to one question.
- **Editing settings on this page**: two doors to one control on one screen; the row is a door to the one that edits it.
- **A file in `data/` for the service's address**: a second source beside the variable a terminal reads first (D171
  point 1); the account's variable is one fact for the window and the terminal.
- **Clearing a folder from this page**: what may be cleared is cleared by its own act (D153, D88, D102); *Downloads* has
  no such act yet (a proposed row).
- **Showing a key's value once, to copy it**: a key goes in and never comes out (`DriverDomain.tsx:35-36`).

## 10. Build rows

- **WIRING1b** (modules/driver/service; after HOMELAYOUT2, which the folders read): §6, with the variables' list held
  against a search of the sources, and `/api/status`'s `version`.
- **WIRING1c** (web): §2, §3.1–3.9, §5, §7, §8; Driver's home row moved; Ask Daoris's go door to Settings → This Daoris
  (`HelpGoProposals.cs:338`).
- **WIRING1d** (web/modules): §3.10.
- **WIRING1e** (web/modules/desktop): §4, each press's coverage row named for the parent at the merge.

## 11. What only the window and a real run prove

The sizes against Explorer's on the owner's install; a terminal opened after *Put on my PATH* finding `daoris-driver`;
the address file's verdict with Daoris running and closed; both themes and languages at 1546 and 680 px. This document's
gate checks its links and the router, not that the facts it names are answered: WIRING1b's tests are that proof.
