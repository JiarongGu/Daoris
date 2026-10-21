# Daoris's accounts as one set: every account signed in for Daoris serves every workspace, and the person's own sign-in stays theirs (POOL1)

**Status: proposed** (D197, 2026-10-21). Written by Claude Opus from the code at `dbac74517`; Codex's read under D191 is §13.
Nothing here is built. Amends D125, D130, D152 and D175 once accepted (§10).

The owner, adding an account on an agent's page, met the add-account dialog's third step, *Let it run work in*: a box for a
workspace's own list of three accounts and a box for *This machine's list*, "used by games; its starts run on account-4 instead
of Daoris's accounts, in turn". The owner found the step confusing and the idea of a machine's list a sign the model is wrong,
and read the sentence as Daoris moving the machine onto another account, which must never happen. The owner chose one set: every
account signed in for Daoris serves every workspace; nothing about lists is left for the person to reason about; and the
machine's own account, the person's own sign-in, stays theirs: Daoris never changes it and adding an account never changes it.

This document says what is true today (§0), the decision in one table (§1), the set (§2), the machine's own account (§3), the
screens and their words (§4), the doors (§5), the store's fold (§6), what stays machine-wide (§7), the consequence (§8), what was
rejected (§9), what this amends (§10), the rows (§11), what no check here covers (§12) and the reads (§13).

## 0. What is true today

### 0.1 How a start reads its accounts

- **Two scopes per agent, each with its own list and settings.** `harnesses.json` keeps, per agent, the machine's default
  (`defaults`), list (`rotation`) and settings (`rotationUse`), and per workspace the same three (`workspaces`,
  `workspaceRotation`, `workspaceRotationUse`) (`HarnessSettings.cs:53-59`, `:83-86`, `:807-813`). A start reads one scope: its
  workspace's when that names a default or a list of its own for the agent, else the machine's (`ResolveScope`, `:820-837`; D130
  point 3). A scope with no list is its default alone (`ScopeOf`, `:867-875`, line 869), so a workspace that names one account and
  no list runs on that account only and rotates nowhere (D130 point 4).
- **A scope naming nobody takes every account of Daoris's.** Where neither the workspace nor the machine names a default or a
  list, the walk takes the agent's accounts on this machine, the folders under the home's account homes, as the machine's scope
  with the machine's settings (`HarnessRoster.Walked`, `HarnessRoster.cs:973-993`; `HarnessSettings.Over`, `:844-845`; D175's
  OWNSPACE1a note, settlement 2). The page says *Daoris's accounts, in turn* for it (`InheritsDaorisAccounts`, `:999-1004`; D175's
  OWNSPACE1b note, settlement 5). This is already one set; it holds only while nothing names an account.
- **A list is the whole set.** An account a scope does not list never carries its starts; the person's pick at a start is the one
  exception (D130 point 4). When every account a workspace may use is cooling and another is not, *What needs you* asks *Let
  account-1 run work…*, which joins it to that list (`AccountAttention.cs:245-266`; `AttentionRow.tsx:497-510`, `:549-568`).

### 0.2 Why the dialog confused

The dialog's step 3 (`PlaceAccount`, `AddAccount.tsx:81-138`) draws a box per list it may join (`joinChoices`, `agents.ts:524-560`):
each workspace with a list or a default of its own, then this machine's, with whom each holds and the workspaces this machine's
serves. Ticking *This machine's list* while the machine names nobody makes a list of that one account (`WithJoined`,
`HarnessSettings.cs:998-1005`, line 1003). Every workspace on the machine's scope then moves from every Daoris account in turn to
that one account; the box's own words say so (`choiceDetail`, `AddAccount.tsx:70-79`; `agents.add.usedByMoves` and
`agents.add.movesMachine`, `en/agents.json:86-87`), but the act is a narrowing offered beside the others as if it were sharing. A
workspace on the machine's list is not offered alone, since the driver refuses that join (`JoinProblemOf`, `:988-991`;
`AccountJoin.cs:7-15`). So to say where a new account may run, the person had to know which workspaces have their own list, what
the machine's list is, that an empty machine scope means all accounts, and that joining it means one. The owner's reading, that
the sentence moved the machine onto another account, came from *This machine's starts run on account-4*: the machine has an
account of its own, the person's own sign-in (§3), and the sentence named neither it nor what it was replacing.

### 0.3 Three things the code calls *the machine's*

| Name in the code | What it is | Where | Under this design |
|---|---|---|---|
| The person's own sign-in (the tool's own home; `HarnessReport.OwnLogin`, `OwnAccount`; the page's *Your own sign-in*) | The agent's credential in its own configuration home, the person's login at their own terminal | Read only, at the person's press: `HarnessSelection.cs:305-313`; no start runs there: `HarnessRoster.cs:1036-1038`, `:1057-1059`; a resume of a conversation begun there goes on there: `HarnessRoster.cs:960-965` | **The machine's own account.** Outside the set, never written (§3) |
| The machine scope's default (`defaults[agent]`, `HarnessSettings.Defaults`; `HarnessReport.MachineDefault`, the page's `tool.machineDefault`) | A Daoris account, a folder under the account homes, named as where the machine scope's starts begin; also what `login`, `trust` and `settings` take where no account is named | `HarnessSettings.cs:26`, `:297-304`, `:835`; `HarnessRoster.cs:506`; `HarnessSelection.cs:270`, `:321`; `AccountNames.cs:118-127`; `AgentsCommand.cs:530-531`, `:598-604`; `agents.ts:63`, `:427`, `:551` | **The set's.** Folded into the order as its first; the entry goes (§6). The one mapping the direction does not settle in words: §12 |
| The machine scope (`rotation`, `rotationUse`; with nothing named, D175's walk over every account) | The list and settings every workspace without its own reads | `HarnessSettings.cs:53-55`, `:807-809`, `:820-845`; `HarnessRoster.cs:973-993` | **The set.** Its order and its one set of settings (§2) |

Two more answer to *this machine* and are not accounts: the machine's default agent, `driver.json`'s adapter, which the start
dialog calls *This machine's default* (`work.start.harnessDefault`, `AgentChip.tsx:96-103`), unchanged; and the cap (§7).

### 0.4 What Daoris never does to the person's own sign-in, today

- **No sign-in or sign-out there.** Every sign-in runs into a folder of Daoris's: a new account under a fresh id, a sign-in back
  into an account that is here, never the tool's own home (`AgentActions.cs:186-212`); no code runs an agent's sign-out (a search
  of the driver, the modules and the CLI for one finds none).
- **No start there.** D175 points 1 and 2: a start with no Daoris account waits for a sign-in (`held.no-account`).
- **One write reaches it today:** `daoris agent trust <agent> <folder> --yes` with no `--profile` and no machine default grants
  the folder in the tool's own trust file (`AgentsCommand.cs:530-535`). §5.1 ends it.

## 1. The decision in one table

| Today | After |
|---|---|
| A list and settings per scope: the machine's, and each workspace's own | One set per agent, Daoris's accounts, for every workspace; one order; one entry of settings |
| A scope's default, or else its list's first, is where its starts begin; a scope with no list is its default alone | The order's first is where starts begin under *One by one, in order*; no default apart from the order |
| An account no list holds runs nothing (but where nothing is named) | Every account signed in for Daoris serves every workspace; a signed-out one is passed until signed in |
| The add-account dialog asks which lists the account joins | It says the account joined Daoris's accounts and serves every workspace, and asks only its name |
| *Runs for*, *Use in a workspace…*, *Add to / Remove from … list*, *Use by default…*, the *Workspaces* fold | Gone; *How accounts are used* edits the one order and the one settings |
| `--workspace` on `profile order` and `use`; `profile default`, `profile join`, `login --join`, `--join-machine` | `order` and `use` for the set; the rest refused, saying what to do instead |
| Ask Daoris's `use`, `order` (with `workspace` and `clear`), `default` | `use` and `order` for the set; `default` retired |
| The wait that asks *Let account-1 run work…* | Gone: no account is outside the set |
| The person's own sign-in: read-only, outside every start (D175) | The same, said on its row, and held by tests (§3) |

## 2. The set

### 2.1 Who is in it

**Every account of the agent that Daoris holds**: the folders under the home's account homes, `HarnessSettings.Profiles`
(`HarnessSettings.cs:552-553`), a lock's or a hidden folder excepted as today (HELPSTUCK1). A sign-in, a key or an account renamed
changes nothing about membership; *Remove…* (`profile remove`) is the only way out, as it removes the account. A signed-out
account, or a key its provider refused, stays in the set and the walk passes it until it is signed in again, as it passes one
today. The person's own sign-in is never in it (§3).

A door's accounts are its owner's (AGT7): `codex-acp` runs on `codex`'s set, as it runs on `codex`'s accounts today.

### 2.2 Its order

- **One order per agent, every account in it.** Stored as `rotation[agent]`, the whole order, or absent, meaning the accounts
  by id (`Profiles`' order). An account the stored order does not name (a folder an older build made, §6.4) is read after it, by
  id, and written into it at the next write.
- **A new account goes last.** A sign-in's end, a key added and an account made at the terminal append it where an order is
  stored, and write nothing where none is.
- **What the order decides.** Under *One by one, in order* (`use: order`) the walk begins at its first and passes to the next
  ready, D125's walk. Under *Make the most of them* (`use: goal`, the default) it is the last tie-break of D130's §16.3 walk. Either
  way it is the person's.
- **No default beside it.** Today's default said where a scope's starts begin within its list (D130 point 3); with one order its
  first says that. Every reader of the machine's default reads the order's first instead (§5.1).
- **The person's pick** at a start (the start dialog's account menu, Ask Daoris's start) stays the one exception, as D130 point 4
  has it: any account of the set, for that start.

### 2.3 Its settings

One entry per agent, `rotationUse[agent]`, read as today (`RotationUse.Read`): `use` (`goal`, `order`), `keep` (one account kept
for conversations, or none), `switch` (`smart`, `curve`, `off`) and `curve` (D130 §16.6, D165 point 2); a field this build does not
know is kept and said, as today. Clearing the order no longer clears the settings, since they are the agent's and no list's
(today `WithRotation` clears them with a list, `HarnessSettings.cs:400-417`); `profile use --clear` still returns them to the
defaults. `keep` names an account of the set and is refused where the set has one account (`ScopeProblem.Alone`, as today);
`ScopeProblem.Default` goes with the default.

### 2.4 Who reads it

Every reader of a scope reads the set, with no workspace: `HarnessRoster.SelectAsync` and `WalkAsync` (a start of any workspace),
`Next` (the agent's page, `HarnessRoster.cs:1450-1453`), `CurveMove` (a session moved at its turn's end, `CurveMove.cs:86`), the
modules' `ScopesOf` and `MeasuredBy` (`AccountsRoutes.cs:113-141`), which collapse to the one scope, and Ask Daoris's machine
facts. `ResolveScope` keeps its signature for its callers and answers the set whatever workspace it is given; `JoinProblemOf`,
`WithJoined` and `PlacesOf` go (`HarnessSettings.cs:973-1032`), with their CLI twins, which PUSH1f2e removes in any case.

## 3. The machine's own account

**The person's own sign-in is the machine's, outside Daoris's accounts.** It is the agent's login in its own configuration home,
the one the person uses at their own terminal. D175 already keeps every start out of it; this design keeps every write out of it
as well, and says so where the person adds an account.

1. **Never signed in or out by Daoris.** A sign-in always runs into a folder under the account homes (`AgentActions.cs:186-212`),
   held by a test that every sign-in action's spawn sets the agent's configuration-home variable to a folder under the home
   (POOL1a, §11). No sign-out exists; none is added.
2. **Never started in.** D175 points 1 and 2, unchanged: with no account of Daoris's signed in, a start waits for one.
3. **Never written by a door.** `agent trust` with no account named takes the order's first account, and with no account at all
   is refused, never the tool's own trust file (§5.1; today `AgentsCommand.cs:530-535`). `profile ready --own` keeps ending the
   own sign-in's old cool-off, which is `cooling.json`'s line under the home (D125 point 2), not the tool's home.
4. **Adding an account never changes it**, and the dialog says nothing that could be read as changing it: it names Daoris's
   accounts, never *this machine* (§4.1).
5. **Its row stays last and read-only** (D175's OWNSPACE1b note, settlement 1), and its ⓘ says all of the above (§4.4).
6. **A conversation begun there goes on there** (D175 point 3), as `ResumeAsync`'s own-home path does today.

## 4. The screens and their words

### 4.1 Adding an account

*Add an account…* (添加账户…) keeps its three steps (the UX7 design §4.5), and the third asks only the name:

1. **Before anything starts**, where an account reads signed out or unknown: each such account with *Sign in to account-2*, then
   *Add a new account* and *Never mind*. Its line, `agents.add.back`, loses *the lists that hold it*:
   - EN: *Signing an account back in? Use its row, so it stays the same account.*
   - ZH: *要让一个账户重新登录？请用它所在的行，这样它仍是同一个账户。*
2. **The tool's own sign-in**, unchanged.
3. **Its name**: *Signed in as …* (`agents.add.signedIn`), then one sentence, then *Its name* with who signed in offered (ACCT2),
   then one press, *Done*, which writes the name in the field (an emptied field gives none, as today). The terminal's twin under
   it is `daoris agent profile rename …` where a name was typed, and nothing otherwise.
   - `agents.add.joined` — EN: *{{account}} joined Daoris's accounts and now serves every workspace.* ZH: *{{account}} 已加入
     Daoris 的账户，现在为所有工作区运行工作。*
   - `agents.add.done` — EN: *Done*. ZH: *完成*.
   - A key added (ACCTUX3) leads with *Added the API key …* and says the same sentence.

The step's question, its boxes and *Add to the lists*, *Not now* and *Use in a workspace…* go; nothing is asked that *Not now*
could lose.

### 4.2 The agent's page

- **The rows** (the UX7 design §4.2): *Account · State · Now*, the act, ⋯. *Runs for* goes, since every account runs for every
  workspace; the grid loses that column (`ACCOUNT_COLUMNS`, `AccountRow.tsx:66`). The act by state: *Sign in*, *Read*, *Try now*,
  *New API key* as today; a signed-in account has none (`place`, *Use in a workspace…*, goes from `accountAct`,
  `agents.ts:502-522`). A signed-out account holds work, and is loud, whenever it is in the set, which is always (`heldBy`,
  `agents.ts:61-66`, answers true).
- **The ⋯** keeps *Rename…*, *Model and effort…*, *Sign in again*, *Read again* and *Remove…*; *Use by default*, *Use by default in
  …*, *Clear the default in …*, *Add to …'s list*, *Add to this machine's list* and *Remove from …* go (`AgentPage.tsx:191`,
  `:402-423`).
- **The next start** (ACCTUX4b) is one line for every workspace: the set's (`nextStartSaidFor`, `agents.ts:479-500`, loses its
  workspace branch and `harness.next.takesIn`).
- ***How accounts are used*** (`AccountUse.tsx`) edits the one order (up and down on each row, no *Use* switch) and the one
  settings; its summary is the set's (`useSummary`, `agents.ts:656-671`):
  - `agents.use.summary.all` — EN: *All {{count}} accounts, for every workspace · {{mode}}*. ZH: *全部 {{count}} 个账户，用于所有工作区
    · {{mode}}* (`mode` is `harness.use.mode.goal` or `.order`).
  - `harness.use.listLabel` — EN: *Daoris's accounts, in the order they are used*. ZH: *Daoris 的账户，按使用顺序排列*.
  - `harness.use.listWhy` — EN: *Every account signed in for Daoris serves every workspace, in this order. One by one, in order
    begins at the first; Make the most of them uses the order only to break a tie. A conversation you start on an account is your own
    choice each time.* ZH: *为 Daoris 登录的每个账户都为所有工作区服务，按这个顺序排列。“按顺序逐个用”从第一个开始；“充分利用”只在难分先后时
    参考这个顺序。你在某个账户上开启的对话，每次都是你自己的选择。*
  - Each command hint loses `{{scoped}}`.
- **The *Workspaces* fold goes** (`workspacesSummary`, `agents.ts:673-682`; `AgentPage.tsx:736`): it said which scope each
  workspace read, and there is one.
- **The fold's account** (§6.3), while unsaid, is one block above the rows:
  - `agents.pooled.title` — EN: *Your {{product}} accounts now serve every workspace.* ZH: *你的 {{product}} 账户现在为所有工作区服务。*
  - `agents.pooled.ownList` — EN: *{{workspace}} had its own accounts ({{accounts}}); it now runs on all {{count}}.* ZH:
    *{{workspace}} 原有自己的账户（{{accounts}}）；现在它可以在全部 {{count}} 个账户上运行。*
  - `agents.pooled.oneAccount` — EN: *{{workspace}} ran on {{account}} alone; it now runs on all {{count}}.* ZH: *{{workspace}} 原来只在
    {{account}} 上运行；现在它可以在全部 {{count}} 个账户上运行。*
  - `agents.pooled.unnamed` — EN: *Workspaces that named no accounts ran on {{accounts}}; they now run on all {{count}}.* ZH: *没有指定账户的
    工作区原来在 {{accounts}} 上运行；现在它们可以在全部 {{count}} 个账户上运行。*
  - `agents.pooled.first` — EN: *Starts began at {{account}}; it is first in the order now. With One by one, in order, starts begin
    there.* ZH: *启动原来从 {{account}} 开始；现在它排在顺序的第一位。选择“按顺序逐个用”时，启动从它开始。*
  - `agents.pooled.notKept` — EN: *Not kept: {{workspace}}'s {{setting}}.* ZH: *未保留：{{workspace}} 的{{setting}}。*
  - `agents.pooled.gotIt` — EN: *Got it*. ZH: *知道了*.
- **Your own sign-in's ⓘ** (`harness.own.shared`, `harness.own.sharedUnknown`):
  - EN: *Your own sign-in, {{who}}: the account this tool uses at your own terminal. It is not one of Daoris's accounts: Daoris
    never signs it in or out, adding an account never changes it, and Daoris starts no session in it. Conversations that began there
    go on there.*
  - ZH: *你自己的登录 {{who}}：这是该工具在你自己的终端里使用的账户。它不是 Daoris 的账户：Daoris 从不让它登录或退出，添加账户也不会改变它，
    Daoris 也不在其中启动会话。在那里开始的对话仍在那里继续。*

### 4.3 Elsewhere

- **The start dialog's account menu** (`AgentChip.tsx:53-62`, `:119-131`) lists the set in its order, with no *Not in …'s list*
  group; *Daoris's choice* stays first.
- ***What needs you***: the let-run ask goes with its row's `outside` (`AttentionRow.tsx:497-510`, `:549-568`;
  `AccountAttention.cs:245-266`); a wait on cooling accounts names the first reset, as D125 point 6 has it.
- **A start's record**: no start writes `opening.why.default`, `default-here` or `first` again, nor `held.outside-machine` or
  `outside-workspace` (`NoteCodes.cs:367-370`, `:859-860`). Their codes stay read, since records keep them. `first-here` is still
  written, for the order's first, and its words change; `default-here`'s words change for the records that hold it:
  - `note.opening.why.first-here` — EN: *it comes first in the order of Daoris's accounts*. ZH: *它在 Daoris 的账户顺序中排第一*.
  - `note.opening.why.default-here` — EN: *it was where its starts began*. ZH: *它是当时启动开始的账户*.

### 4.4 Every key, and the glossary

**Retired** from both catalogues, where nothing else reads them (the build confirms each by a search): `agents.add.lists`,
`.machine`, `.ownList`, `.ownDefault`, `.usedBy`, `.usedByMoves`, `.movesMachine`, `.join`, `.notNow`; `agents.place.title`;
`agents.joined`, `agents.left`; `agents.act.place`, `.placeFor`; `agents.col.runs`; `agents.runs.machine`, `.first`, `.none`;
`agents.account.addTo`, `.addToMachine`, `.removeFrom`, `.removeFromMachine`, `.useDefault`, `.useDefaultIn`, `.clearIn`;
`agents.use.summary.machine`, `.workspace`, `.list`, `.ownList`, `.own`; `agents.section.workspaces`; `agents.workspaces.*`;
`harness.use.machine`, `.workspace`, `.member`, `.memberFor`, `.cannotLeave.default`, `.problem.default`, `.empty`, `.emptyOwn`,
`.emptyInherited`; `harness.use.scope.*`; `harness.next.takesIn`; `work.start.outside`, `.outsideMachine`;
`work.attention.ask.letRun`, `.letRunMachine`; `errors.ACCOUNT_SCOPE_DEFAULT`.

**Reworded**: `agents.add.back`, `harness.use.listLabel`, `harness.use.listWhy`, `harness.own.shared`, `harness.own.sharedUnknown`,
`note.opening.why.first-here`, `note.opening.why.default-here`, and each hint carrying `{{scoped}}`.

**New**: `agents.add.joined`, `agents.add.done`, `agents.use.summary.all`, `agents.pooled.*`.

**The glossary** gains *Daoris's accounts* (EN *Daoris's accounts*, ZH *Daoris 的账户*): an agent's accounts signed in for Daoris,
each serving every workspace; your own sign-in is not one of them (D197). Its `avoid` holds *this machine's list* and *machine's
list* (EN) and *本机的列表* (ZH), so `names:check -- --strict` fails on any catalogue that says them again. *Pool* is the design's
word only, never the chrome's.

**.NET's and the terminal's sentences** (English, D194's text table) lose the same words in the same rows: `AccountJoin.cs`,
`HelpAgentProposals.cs:393-397`, `HelpRoomMayPropose.cs:22`, `:38`, `AccountsRoutes.cs:434`, `HelpRoutes.cs:728`, and the
sign-in end's place words (`AgentActions.cs:562-593`), which say *joined Daoris's accounts* instead.

## 5. The doors

### 5.1 The terminal

After PUSH1f2e every `daoris agent profile` verb is the driver's program's (the alone-path design §5), so each change below is
made once, in the driver library, and the TypeScript twins have already left.

| Verb | After |
|---|---|
| `profile order <agent> <account>…` | The set's order: the accounts named go first, in that order, the rest after in their order; each named an account here, once. `--clear` returns to the ids' order and clears no settings. `--workspace` refused (below) |
| `profile use <agent> […]` | The set's settings, the same flags; `--workspace` refused |
| `profile default <agent> …` | Refused: *every account of Daoris's serves every workspace (D197), and the order says where starts begin — `daoris agent profile order <agent> <account>` puts one first* |
| `profile join <agent> <account> …` | Refused: *there is nothing to join: every account signed in for Daoris serves every workspace (D197)* |
| `login … --join W`, `--join-machine` | Refused before anything starts, with `profile join`'s sentence; the account joins by itself |
| `profile ready <agent> <account>\|--own` | Unchanged |
| `agent list` | The set under each agent: its order, how it is used, the next start; then the person's own sign-in, as D175 says it; the fold's account while unsaid (§6.3) |
| `login`, `trust`, `settings` with no account named | The order's first, where today the machine's default; with no account of Daoris's, refused as `settings` is today, never the tool's own home |
| `--workspace W` on `order` or `use` | Refused: *Daoris's accounts serve every workspace (D197), so `--workspace` names nothing here* |

Refusing a flag that no longer means anything, rather than ignoring it, keeps a script from believing it set something (D50: the
terminal says what the screen does).

### 5.2 The bridge

- **`ACCOUNT_USE`** (`AccountsRoutes.cs:413-454`): `order` and `use` for the set, a `workspace` refused; `inherit` goes, refused as
  an unknown action whose sentence lists the rest; `ready` unchanged; a new `said` marks the fold's account said (§6.3).
- **`HARNESS_ACTION`**: `profile-join` and `profile-default` go (`AgentActions.cs:63`; `AgentsRoutes.cs:700-739`); a sign-in's
  `joins` is refused where it is not empty (`AgentsRoutes.cs:517`, `:526`; `AgentActions.cs:616-633`).
- **`ACCOUNTS`**: `scopes` holds the set's one scope; each account's `places` goes, which no store keeps; the fold's account rides
  as `pooled` while unsaid. The page and the host ship together, so no older answer is read.
- **Ask Daoris's coverage** (`HelpCoverageTests`): the page's `profile-join`, `inherit` and *Use by default* rows go with their
  hooks; *Got it*'s press is `Exempt` (the person's acknowledgement, nothing Ask Daoris proposes).

### 5.3 Ask Daoris

- **`use`** (`HelpAgentProposals.cs:230-280`): the same fields; `workspace` refused with the terminal's sentence.
- **`order`** (`:48-57`, and its judge): `accounts` the set's order, as the terminal's; `clear` returns to the ids' order;
  `clear` or `accounts` with a `workspace` refused.
- **`default`** (`:172-225`): retired from `Doors` (`:36`); a stored proposal of it is refused at Apply, naming `order`.
- **`ready`**: unchanged.
- **The service** (`HelpProposalBox.ProposeAgent`, `HelpAgentTool`): the tool's schema drops `workspace` for `use` and `order`
  and drops the `default` door, in the same change as the driver's `Doors` (the kinds test holds the two lists equal, TOOL4l-b).
- **The room** (`HelpRoomMayPropose.cs:22`, `:38`, the machine facts) words the set, and its three goldens follow. The places
  `setting-doors.md` lists for a `setting` door (the driver's judge, the service's writer, the tool's words, the coverage row, the
  room's row and its goldens) are the `agent` kind's too, and change together.

A proposal stored before this, with a `workspace` or for `default`, stays readable, as a stored shape is read on, and is refused
at its Apply with the reason, never applied to the set.

## 6. The store: one fold, said once

### 6.1 The fold

One typed operation in the driver library, `AccountSet.Fold(settings, accountsOf)`, pure over the file as read. Per agent named
in any of `defaults`, `rotation`, `rotationUse`, `workspaces`, `workspaceRotation` or `workspaceRotationUse`:

1. **The order**, the union with nothing signed in dropped: the machine's default, then its list; then each workspace by name
   (ordinal), its default, then its list; then every other account of the agent, by id. An account keeps its first place; a name
   that is no account here is dropped, as `OrderProblem` would refuse it (`HarnessSettings.cs:424-435`), and said.
2. **The settings**, field by field (`use`, `keep`, `switch`, `curve`): the machine's value where its entry sets one; else the
   one value every workspace whose entry sets that field agrees on; else none, which is the default. A `keep` that would leave
   the set's one account kept is dropped. A field a workspace set that the set does not take, and any field this build does not
   know in a workspace's entry, is said by name (§6.3). The machine's unknown fields are kept, as today.
3. **What goes**: the agent's `defaults` entry and its entries in `workspaces`, `workspaceRotation` and
   `workspaceRotationUse`; a workspace left with nothing is dropped, as today. `versions` and `workspaceVersions` are untouched.
4. **What is written**: `rotation[agent]` as the whole order, `rotationUse[agent]` as the settings, and `pooled[agent]` (§6.3).

An agent whose file names nothing of these, the install's shape where every workspace already took *Daoris's accounts, in turn*,
folds to nothing and is written nothing: its set is already every account, in the ids' order.

### 6.2 When it runs, and its writer

**At the driver's come-up, once per file that holds anything to fold**, through `HarnessSettings.Change(path, edit)`, which
PUSH1f2e adds so an edit is decided from the file as it stands under its keep lock (the alone-path design §1.4, as KEEPALL1b's
amendment leaves it). Every load also folds in memory (`HarnessSettings.Load`), so a start, a look or a door between the read and
the come-up's write never reads a workspace's scope. The fold is idempotent: a folded file folds to itself. It writes an
`accounts.pooled` line to the machine log (D94) naming each agent, how many accounts, and how many scopes folded, by count and
never by name.

### 6.3 Said once

`pooled[agent]` keeps what the fold changed: when; each folded scope (`workspace` or none for the machine's) with its default and
list as they were; the order written; the settings not kept, each with its scope and value; and `said`. It is a section of
`harnesses.json`, listed in `HarnessSettings.Sections`; an older build keeps it as a section it does not know (`Kept`,
`HarnessSettings.cs:61-65`).

While `said` is false, the agent's page shows its block (§4.2) and `agent list` prints the same lines; *Got it* (`ACCOUNT_USE`
`said`) sets it true and the block is never drawn again. Nothing else announces it: it is news, not a wait (D196 point 7). A
later fold, of a file an older build wrote again (§6.4), replaces it, unsaid.

### 6.4 Builds already shipped

Named, as `compatible-builds.md` asks: an older desktop rolled back to (D139's rollback) over a folded file, and the CLI's
TypeScript writer, which PUSH1f2e removes before POOL1a lands.

- **An older build over the folded file** reads `rotation[agent]` as the machine's list naming every account, no default and no
  workspace scope, so every workspace takes the machine's scope: the same accounts in the same order with the same settings.
  What differs is its own: an account it signs in is outside the list until joined, as it always was.
- **What an older build writes** (a workspace's list, a default, a join) is read by the next newer come-up as a file to fold,
  folded and said again. No version stamp is needed, since the fold is a rule over any file, not a step a stamp would gate.
- **What is left**: between an older build's write and the next newer come-up, the newer build's in-memory fold already reads the
  set, so no newer start honours the older build's list.

## 7. What stays machine-wide

- **The cap**, one number for the machine (D130 point 10; CAPCOUNT1, CAPSET1): what the driver starts, never a conversation.
- **What each account said and when it cools**: `windows.json` and `cooling.json`, per account, as today.
- **The machine's default agent** (`driver.json`'s adapter), which is no account.
- **A workspace's own pinned versions** (`workspaceVersions`): which binary its sessions run, no account.
- **The person's own sign-in** (§3), outside the set.

## 8. The consequence, stated

- **A workspace that had its own list now runs on every account of Daoris's.** A work workspace kept to a work account, or a
  personal one kept off it, is no longer kept: D130's aim E, *keep work where it belongs*, has no setting now.
- **A scope that named one account alone now runs on all of them**, that account first in the order. Under *Make the most of
  them* the others share its work; *One by one, in order* keeps starting on it while it is ready.
- **An account no list held now runs**, every workspace's work.
- **What spends**: Daoris may now start any workspace's work on any account signed in for it, which is what the owner chose. Each
  account's own plan and terms still apply (D130 point 12).
- **What a person who needs a workspace on one account does**: picks that account at the start (the pick stays), or keeps the
  other accounts out of Daoris. The need, if it is met again, comes back as its own decision, never as the lists (§9).

## 9. Considered and rejected

- **Keeping a workspace's own list as an opt-in narrowing.** It keeps the model the owner rejected: which scope a start reads, an
  inherit-or-own choice per workspace, a list that excludes, the dialog's question for any machine with one, and the narrowing
  that confused here. Every person would still have to know whether one exists.
- **Keeping the machine's list under another name** (*Daoris's list*, *the default list*). The machine's list was never the
  problem's name alone: it was a second register beside every account, which excluded accounts it did not name; renamed, the
  dialog still asks whether to join it.
- **A per-account switch in or out of the set.** A list by another name: the set would again be the accounts the person
  remembered to switch on. An account kept fresh for the person's own talk is what *Keep for conversations* is for; one not
  wanted at all is removed.
- **Keeping a default beside the order.** Two answers to where starts begin, which D130 point 3 already had to bind with
  refusals (`ScopeProblem.Default`); the order's first says it once.
- **A fold at every read, never written.** An older build would go on reading the workspace lists the newer one ignores, and the
  two builds would start the same workspace on different accounts.
- **Asking the person before the fold.** The owner decided the model; a question would hold every start behind it and bring the
  lists back for one turn.
- **A notice in D196's record.** `data/notices.json` is NOTIFY2f's, not built; the fold's account lives where the accounts are,
  and NOTIFY2f may carry it later (§12).
- **Dropping the per-scope settings silently**, or taking the most cautious of them: a setting chosen once and gone unseen is
  the failure §6.3 is for; the cautious merge would invent a choice nobody made.
- **Granting `trust` in every account of the set where none is named.** Better for a set, since any account may run any folder,
  but a change of its own; the order's first keeps today's one-account behaviour (§12).

## 10. What this amends

- **D125**: point 4 (`rotation` alone, `workspaceRotation` folded; resolved for every workspace the same), point 8 (`profile
  order` without `--workspace`), and its ACCT1 note (a sign-in's join). The cool-off, a limit not a strike, and the walk stand.
- **D130**: points 3 and 4 (one set per agent; every account in it; no default), point 5 (the wait that asks goes), point 11 (the
  doors), point 12 (*and listed* becomes *signed in for Daoris*), the goal's point 7 (settings per agent), the TOOL4g note (the
  screen's scope controls), and the TOOL4l-a and TOOL4l-b notes (the doors' `workspace` and `clear`). The account-use design §2
  rule 1-3, §3 and §9. The walk (§16.3), what is left by the agent's own word, the cap and ACCTWALK3 stand.
- **D152**: point 4 (*Runs for* and *Use in a workspace…*), point 5 (*asks which lists it joins*), and its UX7b note's step 3; the
  UX7 design §4.2, §4.4's summary row and §4.5's step 3.
- **D175**: OWNSPACE1a's settlement 2 holds for every workspace, not only where nothing is named; OWNSPACE1b's settlement 5
  (`inherited` always true for a real agent; the page no longer says it); OWNSPACE1d's join sentence. Points 1 to 3 stand.

## 11. The rows

All after **PUSH1f2e**, which hands `agent profile …` to the driver's program, removes the TypeScript twins and adds
`HarnessSettings.Change`.

- **POOL1a — one set per agent in the driver** (driver, modules). The fold (§6.1) as `AccountSet.Fold` with its table of shapes,
  run at come-up through `HarnessSettings.Change` and in memory at every load; `pooled` and `ACCOUNT_USE` `said`; `ResolveScope`
  answering the set; the default's readers reading the order's first (`AccountNames.SignInTarget`, `AgentsCommand` `trust` and
  `settings`, `HarnessReport.MachineDefault`, `DefaultStanding`); the walk's outside ask and its notes gone; a new account
  appended; `ScopesOf`, `MeasuredBy` and `ACCOUNTS` answering the one scope. Proof: the fold's table (each scope shape, the
  settings rule, unknown fields, a name no longer an account, idempotence, a file an older build wrote again); an older build's
  read of a folded file as a test over `ResolveScope`'s pre-change rule; a test that every sign-in action's spawn sets the
  configuration-home variable under the home; `held.no-account` unchanged; each touched class seen failing first.
- **POOL1b — the doors** (driver, service, modules; after 1a). §5.1's table in `daoris-driver agent profile` and `agent list`,
  with its goldens; `HARNESS_ACTION`'s `profile-join`, `profile-default` and `joins`; `ACCOUNT_USE`'s `inherit` and `workspace`;
  Ask Daoris's `use`, `order` and `default` in the driver's judge, the service's tool and box, the room and its goldens, a stored
  proposal refused at Apply. Proof: each refusal's words, both ways of the kinds test, the room's goldens, `HelpCoverageTests`.
- **POOL1c — the page** (web, after 1a; 1b's routes for its presses). §4: the dialog's step 3, the rows and the ⋯, *How accounts
  are used*, the fold's block and *Got it*, the start dialog, *What needs you*, the own row's ⓘ, every key in §4.4 in both
  catalogues, the glossary's term and its `avoid`. Proof: `agents.test.ts`, `AgentsView.test.tsx`, `AddAccount` and `AccountUse`
  tests and stories at the main area's two widths in en and zh, `i18n:check`, `names:check -- --strict`, the window's
  `HelpCoverageTests` row for *Got it*.
- **POOL1d — the install's fold and dialog, seen** (parent; after 1c, on a republish). The install's file folded as §6.1 says (its
  shape read before and after, by counts, no names), the block said once, the dialog's step 3 in en and zh, the own row's ⓘ. Proof:
  dated shots and the machine log's `accounts.pooled` line.

## 12. What no check here covers, and what is open

- **Written from the code, nothing run.** Every file and line above was read at `dbac74517`; no test ran and the window was not
  looked at.
- **The machine's default mapped to the set** (§0.3). The owner's direction names the person's own sign-in as the machine's own
  account; this design reads `defaults[agent]` as a Daoris account the machine scope began on, so it folds into the order as its
  first. Were the steward to read the owner's *machine's own account* as that default instead, the fold would keep it as the
  order's first and `use: order` for that agent, so starts keep beginning on it; open to the steward.
- **`trust` with no account named** (§9's last item): the order's first or every account; the steward's.
- **The fold's account in Notifications** once NOTIFY2f's record exists: a row for later, not this design's.
- **The install's shape** is not read here; §8's consequences are said by shape, and POOL1d reads the real one.

## 13. The reads (D191)

Codex reads this design and D197 before the steward settles it; the table of findings and verdicts is filled in from that read.
