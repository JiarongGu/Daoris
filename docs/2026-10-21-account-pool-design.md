# Daoris's accounts as one set: every account signed in for Daoris serves every workspace, and the person's own sign-in stays theirs (POOL1)

**Status: proposed, round 3** (D197, 2026-10-21). Written by Claude Opus from the code at `dbac74517`; revised under D191 by
Codex's read of round 1 and by Fable's read of round 2, whose findings and verdicts are §13. Round 3 read the code it cites again
at `20c324794`, the commit Fable read, and carries the steward's settlements of round 2's open questions (D197's note, as round 3
corrected it). Nothing here is built but `HarnessSettings.Change`, which PUSH1f2e1 merged after the read (§6.2). Amends D125, D130,
D152 and D175 once accepted (§10).

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
  point 3). A scope with no list is its default alone and takes no settings (`ScopeOf`, `:867-875`, line 869), so a workspace that
  names one account and no list runs on that account only and rotates nowhere (D130 point 4).
- **A scope naming nobody takes every account of Daoris's.** Where neither the workspace nor the machine names a default or a
  list, the walk takes the agent's accounts, the folders under the home's account homes: one is the scope's one account, with no
  settings; two or more are its list, with the machine's settings (`HarnessRoster.Walked`, `HarnessRoster.cs:973-993`, lines
  989-991; `HarnessSettings.Over`, `:844-845`; D175's OWNSPACE1a note, settlement 2). The page says *Daoris's accounts, in turn*
  for it (`InheritsDaorisAccounts`, `:999-1004`; D175's OWNSPACE1b note, settlement 5). This is already one set; it holds only
  while nothing names an account.
- **A list is the whole set.** An account a scope does not list never carries its starts; the person's pick at a start is the one
  exception (D130 point 4). When every account a workspace may use is cooling and another is not, *What needs you* asks *Let
  account-1 run work…*, which joins it to that list (`AccountAttention.cs:71-73`, `:253`; `AttentionRow.tsx:497-510`, `:549-568`).
- **Settings may lie dormant.** An entry beside no list is read by no start (line 869), but the machine's where the walk takes two
  or more accounts with nothing named (line 991).

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
account of its own, the person's own sign-in (§3), and the sentence named neither it nor what it was replacing. Other words say
the same: *This machine runs {{harness}} as {{account}} by default* (`harness.default.machine`, `en/harness.json:60`).

### 0.3 Three things the code calls *the machine's*

| Name in the code | What it is | Where | Under this design |
|---|---|---|---|
| The person's own sign-in (the tool's own home; `HarnessReport.OwnLogin`, `OwnAccount`; the page's *Your own sign-in*, and `harness.own`, *this machine's own*) | The agent's credential in its own configuration home, the person's login at their own terminal | Asked at the person's press, its row's or the whole roster's: `HarnessSelection.cs:214-223`, `:305-313`, `HarnessRoster.cs:810`, and its usage read `HarnessRoster.cs:2016-2043`; no start runs there: `:1036-1038`, `:1057-1059`; a record begun there resumes there: `:883-940` | **The machine's own account.** Outside the set; Daoris runs nothing there but a conversation begun there (§3) |
| The machine scope's default (`defaults[agent]`, `HarnessSettings.Defaults`; `HarnessReport.MachineDefault`, the page's `tool.machineDefault`) | A Daoris account, a folder under the account homes, named as where the machine scope's starts begin; also what `login`, `trust` and `settings` take where no account is named | `HarnessSettings.cs:26`, `:297-304`, `:835`; `HarnessRoster.cs:506`; `HarnessSelection.cs:270`, `:321`; `AccountNames.cs:118-127`; `AgentsCommand.cs:530-531`, `:598-604`; `agents.ts:63`, `:427`, `:551` | **The set's.** Folded into the order (§6); the entry goes. The one mapping the direction does not settle in words: §12 |
| The machine scope (`rotation`, `rotationUse`; with nothing named, D175's walk over every account) | The list and settings every workspace without its own reads | `HarnessSettings.cs:53-55`, `:807-809`, `:820-845`; `HarnessRoster.cs:973-993` | **The set.** Its order and its one set of settings (§2) |

Two more answer to *this machine* and are not accounts: the machine's default agent, `driver.json`'s adapter, which the start
dialog calls *This machine's default* (`work.start.harnessDefault`, `AgentChip.tsx:96-103`), unchanged; and the cap (§7).

### 0.4 What reaches the person's own sign-in today

- **No sign-in or sign-out by Daoris.** Every sign-in runs into a folder of Daoris's: a new account under a fresh id, a sign-in
  back into an account that is here, never the tool's own home (`AgentActions.cs:186-212`); no code runs an agent's sign-out.
- **No start.** D175 points 1 and 2: a start with no Daoris account waits for a sign-in (`held.no-account`).
- **Two runs of the agent against it, both at a person's press**: the status question and the usage read (CODEXUSE3,
  `HarnessRoster.cs:2016`, `:2042-2043`, `:2124`). Each is asked by the own row's *Read again* (`HarnessSelection.cs:221-223`,
  `:305-313`), and by a refresh of the whole roster (round 3, Fable's F2): `HARNESSES_REFRESH` with no agent
  (`AgentsRoutes.cs:53-56`, from `useRefreshHarnesses`, `agents.ts:283-290`) runs `RosterAsync(config, refresh: true)`, which passes
  `own: refresh` to each agent's report (`HarnessRoster.cs:797`, `:810`), whose press asks the own sign-in's status
  (`PressAsync`, `:413`, `:440`, `:448`) and its usage (`PressUsageAsync`, `:453`, `:2124`); and `HarnessProbe.ProbeAsync` asks it
  unless told not to (`own = true`, `HarnessSelection.cs:246`). The agent may refresh an expired token to answer, which writes its
  credential; two refreshes with one single-use token once signed every account on the install out (`HarnessSelection.cs:215-218`).
  The probe lock keeps two of Daoris's questions apart, never Daoris's from the person's own terminal.
- **A record begun there goes on there** (D175 point 3; `ResumeAsync`, `HarnessRoster.cs:883-940`), the agent running in that home.
- **Two writes**: by a door, `daoris agent trust <agent> <folder> --yes` with no `--profile` and no machine default grants the folder
  in the tool's own trust file (`AgentsCommand.cs:530-535`); and by a press (round 3, Fable's F3), a trust hold on a conversation
  going on in the own home reads the own home's trust file (`Driver.cs:1789-1805`: `ClaudeTrust.PathFor` with no account home,
  `ClaudeTrust.cs:264-268`), and the screen's *Trust this folder* grants the hold in that file (`TRUST_FOLDER`,
  `DriverRoutes.cs:334-346`, through `ClaudeTrust.Grant`, `ClaudeTrust.cs:160-220`).
- **Words that send the person to Daoris to change it**: `note.account.refused-own` and `note.held.refused-own`
  (`en/note.json:36`, `:279`) say to sign it in again *in Settings or with `daoris agent`*, and that Daoris then starts sessions on
  it, which D175 ended.

## 1. The decision in one table

| Today | After |
|---|---|
| A list and settings per scope: the machine's, and each workspace's own | One set per agent, Daoris's accounts, for every workspace; one order, stored whole; one entry of settings |
| A scope's default, or else its list's first, is where its starts begin; a scope with no list is its default alone | The order's first is where starts begin under *One by one, in order*; no default apart from the order |
| An account no list holds runs nothing (but where nothing is named) | Every account signed in for Daoris serves every workspace; a signed-out one is passed until signed in |
| The add-account dialog asks which lists the account joins | It says the account joined Daoris's accounts, and asks only its name |
| *Runs for*, *Use in a workspace…*, *Add to / Remove from … list*, *Use by default…*, the *Workspaces* fold | Gone; *How accounts are used* edits the one order and the one settings |
| `--workspace` on `profile order` and `use`; `profile default`, `profile join`, `login --join`, `--join-machine` | `order` and `use` for the set; the rest refused, saying what to do instead |
| Ask Daoris's `use`, `order` (with `workspace` and `clear`), `default` | `use` and `order` for the set, marked so; `default` retired; an unmarked stored proposal refused at Apply |
| The wait that asks *Let account-1 run work…* | Gone: no account is outside the set |
| The person's own sign-in: no start (D175), read at a press, written by `trust` and a trust hold's press, a conversation begun there going on | No start, no read and no write; only a conversation begun there goes on there (§3) |

## 2. The set

### 2.1 Who is in it

**Every account of the agent that Daoris holds**: the folders under the home's account homes, `HarnessSettings.Profiles`
(`HarnessSettings.cs:552-553`), a lock's or a hidden folder excepted as today (HELPSTUCK1). A sign-in, a key or a rename changes
nothing about membership; *Remove…* (`profile remove`) is the only way out, as it removes the account. A signed-out account, or a
key its provider refused, stays in the set and the walk passes it until it is signed in again, as it passes one today. The
person's own sign-in is never in it (§3). A door's accounts are its owner's (AGT7): `codex-acp` runs on `codex`'s set.

### 2.2 Its order

- **One order per agent, stored whole.** `rotation[agent]` names every account. It is absent only on a file never folded and never
  changed since this build; then the order is the accounts by id. The first change to the set, a fold (§6), a sign-in, a key, a
  removal or an order edited, writes it whole: the order as read, then the change. So an account's place never comes from its id,
  which is random (`AccountNames.NewId`, `AccountNames.cs:303-311`) and would sort a new account anywhere.
- **A new account goes last**, appended by the write its sign-in or key makes. An account the stored order does not name (a folder
  an older build made, §6.4) is read after it, by id, until the next write places it there.
- **What the order decides.** Under *One by one, in order* (`use: order`) the walk begins at its first and passes to the next
  ready, D125's walk. Under *Make the most of them* (`use: goal`, the default) it is the last tie-break of D130's §16.3 walk.
- **No default beside it.** The order's first says where starts begin. Every reader of the machine's default reads the order's
  first instead (§5.1).
- **The person's pick** at a start (the start dialog's account menu, Ask Daoris's start) stays the one exception, as D130 point 4
  has it: any account of the set, for that start.

### 2.3 Its settings

One entry per agent, `rotationUse[agent]`, read as today (`RotationUse.Read`, which reads the old `early` and `near` as `switch` and
`curve`, `RotationUse.cs:181`): `use` (`goal`, `order`), `keep` (one account kept for conversations, or none), `switch` (`smart`,
`curve`, `off`) and `curve` (D130 §16.6, D165 point 2); a field this build does not know is kept and said, as today. The entry is
the agent's and no list's: clearing the order or removing the last account clears none of it (today `WithRotation` clears it with
its list, `HarnessSettings.cs:400-417`, and `WithoutAccount` through it, `:442-469`); `profile use --clear` still returns it to the
defaults. A `keep` is dropped where its account is removed or the set holds one account, and the removal's answer says so.
`ScopeProblem.Default` goes with the default. The settings are set whatever the set holds: no list is waited on, so the refusal
where a scope has none (`AccountUseNoList`, `AccountsRoutes.cs:502-510`) goes (§2.4).

### 2.4 One projection, every reader

**`HarnessSettings.Load` and `Save` stay as they are**, raw: they read and write the file as it stands, `Understood` the file as
read (KEEPALL1b). **The effective set is one projection**, `AccountSet.Of(settings, accounts)`: given the raw settings and the
agent's accounts, it answers the set's whole order, its settings and its first, folding in memory whatever the raw file still holds
(a workspace section, a default) by §6.1's rules. Every runtime reader reads the projection, never a scope:

- **The walk and its judges**: `HarnessRoster.SelectAsync`, `WalkAsync` and `Walked` (`:973-1059`), `Next` (`:1450-1453`),
  `ResumeAsync` (`:883-940`, with §3's exception), `CurveMove` (`CurveMove.cs:86`).
- **The wiring panel** (round 3, Fable's F6): `HarnessRoster.WiringAsync` (`:1753-1782`), behind `STARTS`
  (`AgentsRoutes.cs:286-320`), reads `ResolveScope` beside the selection for where the scope begins and whose rung named it
  (`:1763-1764`); it reads the projection, its `ProfileFrom` as the records point below says.
- **The modules**: `ScopesOf` and `MeasuredBy` (`AccountsRoutes.cs:113-141`), which answer the one scope; `AccountAttention`'s own
  readers of scope, `WorkspaceScope` and `MachineScope` (`AccountAttention.cs:57`, `:366-371`), `HeldBy` (`:397`), `OwnScopes`
  (`:416`), `RunsFor` and the row's `runs` (`:118`, `:426`), `JoinChoices` (`:460`), and `OutsideOf` with `readFirst` (`:71-73`,
  `:253`): the set's accounts all held, no `runs`, nothing outside.
- **The modules' account edits** (F6): `AccountsRoutes.OrderEdited` and `UseEdited`, with their own-scope helpers `OwnList`,
  `OwnDefault` and `OwnKeep` (`AccountsRoutes.cs:471-537`, `:561-577`), which serve `ACCOUNT_USE` (`:424-430`) and Ask Daoris's
  `SetAccountOrderAsync` and `SetAccountUseAsync` (`HelpRoutes.cs:702-735`): they edit the set's order and settings, `UseEdited`'s
  refusal where no list is (`AccountUseNoList`, `:502-510`) going. `DefaultAllowed` (`:545-558`), which `ProfileDefault`
  (`AgentsRoutes.cs:697-703`) and Ask Daoris's `default` door (`HelpRoutes.cs:690`) ask, leaves with them.
- **The default's sentence** (F6): `AgentsRoutes.DefaultStanding` (`AgentsRoutes.cs:729-739`) over `HarnessSettings.ResolveFrom`
  (`HarnessSettings.cs:272-294`), which `profile-default` answers with, leaves with `profile-default`.
- **What the page was told it inherits** (F6): `HarnessRoster.InheritsDaorisAccounts` (`HarnessRoster.cs:999-1004`), `ACCOUNTS`'
  `inherited` (`AccountsRoutes.cs:104`, `:182-187`), would read a folded order as a list of the scope's own; it leaves, and
  `inherited` with it (§5.2).
- **A sign-in's place words** (F6): `AgentActions.PlacesSaid` (`AgentActions.cs:584-591`), over `ResolveScope(owner, null)`, leaves
  with `PlacesOf`; a sign-in's end says *joined Daoris's accounts* (§4.4).
- **Ask Daoris's facts**, its `use`, `order` and `default` judges (`HelpAgentProposals.cs:172-420`, `:476-492`), and the modules'
  Ask Daoris writes (`HelpRoutes.cs:681-698`, `:728-729`, `:1001`).
- **What a start records.** `RotatedStart.Scope` and the `account.rotated` line's `scope` (`HarnessRoster.cs:1245`,
  `AccountCooling.cs:361-378`) are null for every start now, read as the set; a line written before keeps the workspace it named.
  `StartWiring.ProfileFrom` (`HarnessSelection.cs:733`) answers `Picked`, `Machine` (now the set) or `Unset`, never `Workspace` for
  an account; a version's `VersionFrom` keeps all four.

`ResolveScope`, `JoinProblemOf`, `WithJoined` and `PlacesOf` (`HarnessSettings.cs:820-837`, `:973-1032`), with `DefaultAllowed`
and `DefaultStanding` (F6), leave with their last callers, in the row that moves those callers (§11); their CLI twins leave with
PUSH1f2e where it retires them.

## 3. The machine's own account

**The person's own sign-in is the machine's, outside Daoris's accounts.** It is the agent's login in its own configuration home,
the one the person uses at their own terminal. D175 already keeps every start out of it. This design keeps Daoris out of it
altogether but for one case, and says so where the person adds an account.

1. **Never signed in or out by Daoris.** A sign-in always runs into a folder under the account homes (`AgentActions.cs:186-212`),
   held by a test that every sign-in action's spawn sets the agent's configuration-home variable to a folder under the home. No
   sign-out exists; none is added.
2. **Never started in.** D175 points 1 and 2, unchanged.
3. **Never asked.** The own row's *Read again*, the own usage read and the roster's whole refresh's question of it go (§0.4): each
   ran the agent against the person's home, where it may refresh the person's token beside their own terminal's refresh and sign
   them out. The row shows no state and no who. Nothing can ask it (round 3, Fable's F2): the `own` parameter leaves
   `HarnessRoster.ReportAsync`, `PressAsync` and `PressUsageAsync` (`HarnessRoster.cs:384-453`, `:2111-2126`) and
   `HarnessProbe.ProbeAsync` (`HarnessSelection.cs:246`, `own = true` today), so `RosterAsync` passes none (`own: refresh` today,
   `HarnessRoster.cs:810`), the agent's press asks none (`AgentsRoutes.cs:50`), and the probe answers the own sign-in's kept reading
   unasked (`HarnessSelection.cs:309-310`). The walk's `own` (`ChooseAsync`, `WalkAsync`, `HarnessRoster.cs:927`, `:961-965`),
   which lets a record begun there go on there, is another parameter and stays (point 5). A reading kept from before is not shown,
   since it would only age.
4. **Never written by a door or a press.** `agent trust` with no account named trusts the folder for every account of the set
   (the steward's settlement, D197's note: one set serves every workspace, so a folder trusted for Daoris is trusted for each
   account that may run its work), and with no account at all is refused, never the tool's own trust file (§5.1). **A trust hold
   whose file is the own home's offers no press** (round 3, Fable's F3, the steward's choice). Such a hold arises where a
   conversation goes on there (point 5): its start's selection has no account home, so `ClaudeTrust.PathFor` answers the own home's
   file (`Driver.cs:1789-1805`; `ClaudeTrust.cs:264-268`); the intake's own check (`DriverIntakes.cs:157-167`) keeps the same rule.
   The hold records that its file is the own home's (`TrustHold`, `ClaudeTrust.cs:22`), its row carries `own: true`
   (`NeedsAPerson.cs:468`), the page draws its sentence with no *Trust this folder* (§4.3), and `TRUST_FOLDER` refuses it before
   `ClaudeTrust.Grant` (`DriverRoutes.cs:340-346`), as `TRUST_OWN_HOME`. Its sentence says to trust the folder with the tool itself,
   at the person's terminal, in point 8's words (§4.4). `profile ready --own` keeps ending the own sign-in's old cool-off, which is
   `cooling.json`'s line under the home (D125 point 2), not the tool's home.
5. **The one exception: a record begun there goes on there** (D175 point 3). The set never displaces it. Today it would: a record
   whose account is null is judged against the settings' scope, not the walked one (`HarnessRoster.cs:898-899`), and
   `ResumeAccount.InScope` answers `account is not null && …` against any non-empty list (`ResumeAccount.cs:49-52`), so against a
   folded order it would judge `NextHold.Outside` (`:29`; `HarnessRoster.cs:912`) and `ElsewhereAsync` would carry its words onto
   the set's walk (`:923`, `:932-934`). **The change** (round 3, Fable's F1): `ResumeAccount.Judge` asks no scope of a record with no
   account (`kind == Driven && account is not null && !InScope(…)`, `:29`), as `KeptOf`'s test (`:30`) already passes a null
   account; `InScope` keeps its meaning for a named account. So the fold moves no such record; a cooling, a refusal or a
   signed-out own sign-in still carries its words to a new record on the set's walk, as `ElsewhereAsync` does today
   (`HarnessRoster.cs:923-939`). Going on there runs the agent in the person's own home, its readiness asked as a resume asks it
   today, and the agent may write there as it does at their terminal; nothing else runs there.
6. **Adding an account never changes it**, and the dialog says nothing that could be read as changing it: it names Daoris's
   accounts, never *this machine* (§4.1).
7. **Its row stays last** (D175's OWNSPACE1b note, settlement 1, less its read), and its ⓘ says all of the above (§4.2).
8. **Words never send the person to Daoris to change it**: the two refusal notes of §0.4 say to sign it in again with the tool
   itself, and a trust hold on its home says to trust the folder with the tool itself, each at the person's terminal (§4.4).

## 4. The screens and their words

### 4.1 Adding an account

*Add an account…* (添加账户…) keeps its three steps (the UX7 design §4.5); the first loses its lists and the third asks only the
name:

1. **Before anything starts**, where an account reads signed out or unknown: each such account in the set's order with *Sign in
   to account-2* (its `runs`, `AgentPage.tsx:306-311`, goes), then *Add a new account* and *Never mind*. Its line, `agents.add.back`:
   - EN: *Signing an account back in? Use its row, so it stays the same account.*
   - ZH: *要让一个账户重新登录？请用它所在的行，这样它仍是同一个账户。*
2. **The tool's own sign-in**, into a folder of Daoris's, unchanged.
3. **Its name**: *Signed in as …* (`agents.add.signedIn`), then one sentence, then *Its name* with who signed in offered (ACCT2),
   then one press, *Done*, which writes the name in the field (an emptied field gives none, as today). The terminal's twin under
   it is `daoris agent profile rename …` where a name was typed, and nothing otherwise.
   - `agents.add.joined` — EN: *{{account}} joined Daoris's accounts: every workspace's work may run on it.* ZH: *{{account}} 已加入
     Daoris 的账户，所有工作区的工作都可以在它上面运行。* *May* is said because a key is checked only at its first session and a kept
     account carries no driven work.
   - `agents.add.done` — EN: *Done*. ZH: *完成*.
   - A key added (ACCTUX3) leads with *Added the API key …* and says the same sentence.

The step's question, its boxes, *Add to the lists*, *Not now* and *Use in a workspace…* go; nothing is asked that *Not now* could
lose. A sign-in's end in a toast (`harness.loginNew.done`) says *Signed in as {{account}}* as it does; `doneNowhere` and
`unnamedNowhere` go, since no account is nowhere.

### 4.2 The agent's page

- **The rows** (the UX7 design §4.2): *Account · State · Now*, the act, ⋯. *Runs for* goes, since every account runs for every
  workspace; the grid loses that column (`ACCOUNT_COLUMNS`, `AccountRow.tsx:66`). The act by state: *Sign in*, *Read*, *Try now*,
  *New API key* as today; a signed-in account has none (`place`, *Use in a workspace…*, goes from `accountAct`,
  `agents.ts:502-522`). Every signed-out account holds work, and is loud, since every one is in the set (`heldBy`,
  `agents.ts:61-66`, answers true for each).
- **The ⋯** keeps *Rename…*, *Model and effort…*, *Sign in again*, *Read again* and *Remove…*; *Use by default*, *Use by default in
  …*, *Clear the default in …*, *Add to …'s list*, *Add to this machine's list* and *Remove from …* go (`AgentPage.tsx:191`,
  `:402-423`).
- **The next start** (ACCTUX4b) is one line for every workspace: the set's (`nextStartSaidFor`, `agents.ts:479-500`, loses its
  workspace branch and `harness.next.takesIn`).
- ***How accounts are used*** (`AccountUse.tsx`) edits the one order (up and down on each row, no *Use* switch) and the one
  settings; its summary is the set's (`useSummary`, `agents.ts:656-671`):
  - `agents.use.summary.all` — EN, one: *1 account, for every workspace · {{mode}}*; other: *All {{count}} accounts, for every
    workspace · {{mode}}*. ZH: *全部 {{count}} 个账户，用于所有工作区 · {{mode}}* (`mode` is `harness.use.mode.goal` or `.order`).
  - `harness.use.listLabel` — EN: *Daoris's accounts, in the order they are used*. ZH: *Daoris 的账户，按使用顺序排列*.
  - `harness.use.listWhy` — EN: *Every account signed in for Daoris serves every workspace, in this order. One by one, in order
    begins at the first; Make the most of them uses the order only to break a tie. A conversation you start on an account is your own
    choice each time.* ZH: *为 Daoris 登录的每个账户都为所有工作区服务，按这个顺序排列。“按顺序逐个用”从第一个开始；“充分利用”只在难分先后时
    参考这个顺序。你在某个账户上开启的对话，每次都是你自己的选择。*
  - `harness.use.orderHint` — EN: *`daoris agent profile order {{agent}} <account>…` puts the accounts named first, in that order.*
    ZH: *`daoris agent profile order {{agent}} <account>…` 把列出的账户按这个顺序排到最前。* Each other hint loses `{{scoped}}`.
- **The *Workspaces* fold goes** (`workspacesSummary`, `agents.ts:673-682`; `AgentPage.tsx:736`): it said which scope each
  workspace read, and there is one.
- **What the fold changed** (§6.3), while any of it is unsaid, is one block above the rows, a line per change:
  - `agents.pooled.title` — EN: *Your {{product}} accounts now serve every workspace.* ZH: *你的 {{product}} 账户现在为所有工作区服务。*
  - `agents.pooled.ownList` — EN: *{{workspace}} had its own accounts ({{accounts}}); it now runs on all {{count}}.* ZH:
    *{{workspace}} 原有自己的账户（{{accounts}}）；现在它可以在全部 {{count}} 个账户上运行。*
  - `agents.pooled.oneAccount` — EN: *{{workspace}} ran on {{account}} alone; it now runs on all {{count}}.* ZH: *{{workspace}} 原来只在
    {{account}} 上运行；现在它可以在全部 {{count}} 个账户上运行。*
  - `agents.pooled.unnamed` — EN: *Workspaces that named no accounts ran on {{accounts}}; they now run on all {{count}}.* ZH: *没有指定账户的
    工作区原来在 {{accounts}} 上运行；现在它们可以在全部 {{count}} 个账户上运行。* (The machine's scope with a list.)
  - `agents.pooled.unnamedOne` (round 3, Fable's F8) — EN: *Workspaces that named no accounts ran on {{account}} alone; they now run
    on all {{count}}.* ZH: *没有指定账户的工作区原来只在 {{account}} 上运行；现在它们可以在全部 {{count}} 个账户上运行。* (The machine's scope
    with a default and no list: `oneAccount`'s line with the machine's subject, a key of its own since a subject composed into a
    sentence does not translate.)
  - `agents.pooled.inOrder` (round 3, F4) — EN: *Their starts still begin at {{account}}, since the accounts are now used One by
    one, in order.* ZH: *它们的启动仍从 {{account}} 开始，因为账户的使用方式现在是“按顺序逐个用”。* (After `unnamedOne`, where the fold set
    `use: order` for it, §6.1 rule 3.)
  - `agents.pooled.moved` — EN: *{{workspace}}'s starts began at {{account}}; it is number {{place}} in the order now.* ZH:
    *{{workspace}} 的启动原来从 {{account}} 开始；现在它在顺序中排第 {{place}} 位。* (Said for each workspace whose first, as its starts read
    it, is no longer first, §6.1 rule 2.)
  - `agents.pooled.unnamedMoved` (round 3, F8) — EN: *Starts of workspaces that named no accounts began at {{account}}; it is number
    {{place}} in the order now.* ZH: *没有指定账户的工作区的启动原来从 {{account}} 开始；现在它在顺序中排第 {{place}} 位。* (`moved` for the
    machine's scope.)
  - `agents.pooled.joined` (round 3, F10) — EN: *{{account}} was not in the order; it is number {{place}} now, and every
    workspace's work may run on it.* ZH: *{{account}} 原来不在顺序中；现在它排第 {{place}} 位，所有工作区的工作都可以在它上面运行。* (One line
    per account the stored order left out, §6.1 rule 5.)
  - `agents.pooled.notKept` — EN: *Not kept: {{workspace}}'s {{setting}} ({{value}}).* ZH: *未保留：{{workspace}} 的{{setting}}（{{value}}）。*
  - `agents.pooled.notKeptUnnamed` (round 3, with F8's reason) — EN: *Not kept: {{setting}} ({{value}}), set for workspaces that
    named no accounts.* ZH: *未保留：为没有指定账户的工作区设置的{{setting}}（{{value}}）。* (`notKept` for the machine's entry: a `keep`
    dropped, or a `use` rule 3 replaced.)
  - `agents.pooled.gone` — EN: *{{account}} was named but is no account here, so it was left out.* ZH: *{{account}} 曾被指定，但这里已没有这个
    账户，所以未列入。*
  - `agents.pooled.gotIt` — EN: *Got it*. ZH: *知道了*.
- **Your own sign-in's row** shows its name and ⓘ, no state and no *Read again* (§3 point 3). Its ⓘ, `harness.own.shared`
  (`sharedUnknown` goes, since no *who* is read):
  - EN: *Your own sign-in: the account this tool uses at your own terminal. It is not one of Daoris's accounts: Daoris never signs
    it in or out, never asks it anything, and starts no session in it, and adding an account never changes it. A conversation that
    began there before goes on there.*
  - ZH: *你自己的登录：这是该工具在你自己的终端里使用的账户。它不是 Daoris 的账户：Daoris 从不让它登录或退出，从不向它询问任何信息，也不在其中
    启动会话；添加账户也不会改变它。此前在那里开始的对话仍在那里继续。*

### 4.3 Elsewhere

- **The start dialog's account menu** (`AgentChip.tsx:53-62`, `:119-131`) lists the set in its order, with no *Not in …'s list*
  group; *Daoris's choice* stays first.
- ***What needs you***: the let-run ask goes with its row's `outside` and `readFirst`, and the row's `runs` with it (§2.4); a wait
  on cooling accounts names the first reset, as D125 point 6 has it.
- **A trust hold on the own home** (§3 point 4; round 3, Fable's F3): its question (`TrustAsk.tsx`), where the row says `own`,
  shows the folder and one sentence, with no *Trust this folder* and no `trust.file` line, since Daoris writes nothing there:
  - `trust.ownHome` — EN: *This conversation goes on in your own sign-in, outside Daoris's accounts, so Daoris writes nothing in
    its file: trust the folder with the tool itself, at your terminal.* ZH: *这个对话在你自己的登录中继续，不属于 Daoris 的账户，所以 Daoris
    不会写入它的文件：请在你的终端里用该工具本身信任这个文件夹。*
  - `errors.TRUST_OWN_HOME`, `TRUST_FOLDER`'s refusal of such a hold — EN: *That folder is held for your own sign-in, outside
    Daoris's accounts, so Daoris writes nothing in its file: trust it with the tool itself, at your terminal.* ZH: *这个文件夹是为你自己的
    登录而扣住的，不属于 Daoris 的账户，所以 Daoris 不会写入它的文件：请在你的终端里用该工具本身信任它。*
- **A start's record** (`NoteCodes.cs:345-370`, `:859-860`): a start that takes the order's first writes a new reason,
  `opening.why.first-order`; no start writes `default`, `default-here`, `first`, `first-here`, `held.outside-machine` or
  `held.outside-workspace` again. Every old code stays read with its meaning, a list that may have left accounts out; the two whose
  words name the machine's list keep that meaning in words without it:
  - `note.opening.why.first-order` (new) — EN: *it comes first in the order of Daoris's accounts*. ZH: *它在 Daoris 的账户顺序中排第一*.
  - `note.opening.why.first-here` — EN: *it came first in the list its start read*. ZH: *它在这次启动读取的列表中排第一*.
  - `note.opening.why.default-here` — EN: *it was the default its start read*. ZH: *它是这次启动读取的默认账户*.

### 4.4 Every key, and the glossary

**Retired** from both catalogues, where nothing else reads them (the build confirms each by a search): `agents.add.lists`,
`.machine`, `.ownList`, `.ownDefault`, `.usedBy`, `.usedByMoves`, `.movesMachine`, `.join`, `.notNow`; `agents.place.title`;
`agents.joined`, `agents.left`; `agents.act.place`, `.placeFor`; `agents.col.runs`; `agents.runs.machine`, `.first`, `.none`;
`agents.account.addTo`, `.addToMachine`, `.removeFrom`, `.removeFromMachine`, `.useDefault`, `.useDefaultIn`, `.clearIn`;
`agents.use.summary.machine`, `.workspace`, `.list`, `.ownList`, `.own`; `agents.section.workspaces`; `agents.workspaces.*`;
`harness.use.machine`, `.workspace`, `.member`, `.memberFor`, `.cannotLeave.default`, `.problem.default`, `.empty`, `.emptyOwn`,
`.emptyInherited`; `harness.use.scope.*`; `harness.next.takesIn`; `harness.default.*` (`en/harness.json:60-64`);
`harness.loginNew.doneNowhere`, `.unnamedNowhere` (`:44-45`); `harness.own.sharedUnknown`; `work.start.outside`,
`.outsideMachine`; `work.attention.ask.letRun`, `.letRunMachine`; `errors.ACCOUNT_SCOPE_DEFAULT` and its `_workspace`; and, with
the refusals F6 found (round 3), `errors.ACCOUNT_USE_NO_LIST` and `errors.ACCOUNT_DEFAULT_OUTSIDE_LIST`, each with its `_workspace`
(`en/errors.json:15-33`).

**Reworded**, each in both languages: `agents.add.back`, `harness.use.listLabel`, `harness.use.listWhy`, `harness.use.orderHint`,
`harness.own.shared` (above); `note.opening.why.first-here`, `.default-here` (§4.3); and:
- `harness.profile.removeConfirm` (`:37`) — EN: *This deletes the account from this machine, sign-in included, and takes it out of
  Daoris's accounts. Starts that would have run on it run on the others.* ZH: *这会从本机删除该账户（包括其登录），并将它移出 Daoris 的账户。
  原本会在它上面运行的启动将改在其他账户上运行。*
- `harness.login.refusedTip` (`:56`) — EN: *…Put a new API key into this account: it keeps its name and its place in the order.*
  ZH: *……为这个账户放入新的 API 密钥：它保留名称和在顺序中的位置。*
- `note.account.refused-own` (`en/note.json:36`), `note.held.refused-own` (`:279`) — EN: *Its provider refused `{{owner}}`'s own
  sign-in (401). It is your own, outside Daoris's accounts: sign it in again with the tool itself, at your terminal.* (`held`'s
  opens *An earlier session found that its provider refused…*.) ZH: *其提供方拒绝了 `{{owner}}` 你自己的登录（401）。它是你自己的，不属于
  Daoris 的账户：请在你的终端里用该工具本身重新登录。*
- `agents.read.againTip` (`en/agents.json:43`) — EN: *Asks {{agent}} about each of Daoris's accounts, one at a time. Your own
  sign-in is never asked. Opening this page asks no sign-in.* ZH: *逐个询问 {{agent}} 的每个 Daoris 账户。你自己的登录从不被询问。打开这个页面
  不会询问登录状态。*
- `harness.own` (`:38`, *this machine's own*; ZH *本机自己的*) names the person's own sign-in where no *who* is read
  (`settings/accounts/namer.ts:24`); it is the machine's own account in the owner's own sense and stays.

**New**: `agents.add.joined`, `agents.add.done`, `agents.use.summary.all` (one and other), `agents.pooled.*`,
`note.opening.why.first-order`, `trust.ownHome` and `errors.TRUST_OWN_HOME` (§4.3).

**The glossary** gains *Daoris's accounts* (EN *Daoris's accounts*, ZH *Daoris 的账户*): an agent's accounts signed in for Daoris,
each serving every workspace; your own sign-in is not one of them (D197). Its `avoid` holds *this machine's list*, *machine's list*
and *this machine's accounts* (EN), and *本机的列表* and *本机账户* (ZH), so `names:check -- --strict` fails on any catalogue that says
them again. *Pool* is the design's word only, never the chrome's.

**.NET's and the terminal's sentences** (English, D194's text table) lose the same words in the same rows: `AccountJoin.cs`,
`HelpAgentProposals.cs:393-397`, `:416-418`, `HelpRoomMayPropose.cs:22`, `:38`, `AccountsRoutes.cs:434`, `:505-508`, `:551-556`,
`HelpRoutes.cs:728`, and the sign-in end's place words (`AgentActions.cs:562-598`), which say *joined Daoris's accounts* instead.
A trust hold's sentence with no account (`ClaudeTrust.Refusal`, `ClaudeTrust.cs:250-251`) drops `daoris agent trust … --yes`, which
no longer writes the own home, and says: *the conversation goes on in your own sign-in, outside Daoris's accounts: trust the folder
with the tool itself, at your terminal (run `claude` there once and accept its trust prompt)*; `TRUST_FOLDER`'s refusal of such a
hold says `errors.TRUST_OWN_HOME`'s English (round 3, F3).

## 5. The doors

### 5.1 The terminal

After PUSH1f2e every `daoris agent profile` verb is the driver's program's (the alone-path design §5), so each change below is
made once, in the driver library, and the TypeScript twins have already left. PUSH1f2e is being built under a brief to keep
today's account verb shapes (round 3, Fable's F12), so it carries `login --join` and `--join-machine`, `profile join`, `profile
default` and `--workspace` on `order` and `use` into the driver's program as they are; POOL1b turns those, as 2e built them, into
the refusals below.

| Verb | After |
|---|---|
| `profile order <agent> <account>…` | The set's order: the accounts named go first, in that order, the rest after in their order; each named an account here, once. `--clear` returns to the ids' order, written whole, and clears no settings. `--workspace` refused (below) |
| `profile use <agent> […]` | The set's settings, the same flags; `--workspace` refused |
| `profile default <agent> …` | Refused: *every account of Daoris's serves every workspace (D197), and the order says where starts begin — `daoris agent profile order <agent> <account>` puts one first* |
| `profile join <agent> <account> …` | Refused: *there is nothing to join: every account signed in for Daoris serves every workspace (D197)* |
| `login … --join W`, `--join-machine` | Refused before anything starts, with `profile join`'s sentence; the account joins by itself |
| `profile ready <agent> <account>\|--own` | Unchanged |
| `agent list` | The set under each agent: its order, how it is used, the next start; then the person's own sign-in, outside, with no state and never asked (§3 point 3); what the fold changed while unsaid (§6.3) |
| `login`, `settings` with no account named | The order's first, where today the machine's default; with no account of Daoris's, refused as `settings` is today, never the tool's own home |
| `trust <agent> <folder>` with no account named | The folder trusted for every account of the set, each in its own file, the question naming each (the steward's settlement, D197's note); with no account of Daoris's, refused, never the tool's own trust file. `--profile` trusts that account alone, as today |
| `--workspace W` on `order` or `use` | Refused: *Daoris's accounts serve every workspace (D197), so `--workspace` names nothing here* |

Refusing a flag that no longer means anything, rather than ignoring it, keeps a script from believing it set something (D50: the
terminal says what the screen does).

### 5.2 The bridge

- **`HARNESSES`** (`AgentsRoutes.cs:147-177`): each profile's `places` and `nowhere` (`ProfileShown` over `PlacesOf`), the agent's
  `machineDefault` and `workspaceDefaults` go; the own sign-in's `ownLogin`, `ownAccount`, `ownOrg`, `ownPlan` and `ownRead` stay
  for the shape and answer nothing new (§3 point 3).
- **`HARNESS_ENDED`** (`AgentActions.cs:416-420`): `places` goes; a sign-in's end is said by the page in §4.1's words.
- **`HARNESS_ACTION`**: `profile-join` (`AgentsRoutes.cs:442`) and `profile-default` (`:472`, `:486`, `:700-739`) go
  (`AgentActions.cs:63`); a sign-in's `join` is refused where it names anything (`AgentsRoutes.cs:510-532`;
  `AgentActions.cs:616-633`).
- **`HARNESSES_REFRESH`** asks no own sign-in, with an agent or without (round 3, Fable's F2). With an agent and no account (the
  header's *Read again*, `AgentsRoutes.cs:44-51`) it reads every account of Daoris's; with no agent (`AgentsRoutes.cs:53-56`, the
  roster's whole refresh, `useRefreshHarnesses` with no target, `agents.ts:283-290`) it reads every agent's, through `RosterAsync`,
  which today passes `own: refresh` to each report (`HarnessRoster.cs:810`). The `own` parameter leaves `ReportAsync`, `PressAsync`,
  `PressUsageAsync` and `HarnessProbe.ProbeAsync`, whose default is `own = true` today (`HarnessSelection.cs:246`), so neither the
  status question (`HarnessRoster.cs:440`, `:448`) nor the usage read (`:453`, `:2124`) can ask it (§3 point 3).
- **`ACCOUNT_USE`** (`AccountsRoutes.cs:413-454`): `order` and `use` for the set, a `workspace` refused, `use`'s refusal where no list
  is (`AccountUseNoList`) gone (F6); `inherit` goes, refused as an unknown action whose sentence lists the rest; `ready` unchanged;
  `said`, with the ids of the receipts the page showed, marks those said (§6.3; F13).
- **`ACCOUNTS`**: `scopes` holds the set's one scope; each scope's `inherited` (`AccountsRoutes.cs:104`, `:182-187`) goes with
  `InheritsDaorisAccounts`, which would read a folded order as a list of the scope's own (F6); `pooled` carries what the fold changed
  while any of it is unsaid, each receipt's id and each line's kind and values (§4.2's keys), never words.
- **`STARTS`** (`AgentsRoutes.cs:286-320`): each start read through `WiringAsync` over the projection, `profileFrom` never
  `workspace` for an account (§2.4; F6).
- **`TRUST_FOLDER`** (`DriverRoutes.cs:334-346`): a hold whose file is the own home's is refused as `TRUST_OWN_HOME` before anything
  is written; the attention row of such a hold carries `own: true` (`NeedsAPerson.cs:468`) and the page draws no press for it (§3
  point 4, §4.3; F3).
- **Ask Daoris's coverage** (`HelpCoverageTests`): the page's `profile-join`, `inherit` and *Use by default* rows go with their
  hooks; *Got it*'s press is `Exempt` (the person's acknowledgement, nothing Ask Daoris proposes). The own-home trust hold adds no
  press.

The page and the host ship together, so no answer from an older host is read.

### 5.3 Ask Daoris

- **Every new `use` and `order` proposal is marked for the set**: the service writes `set: true` in its file, and the driver
  applies only a proposal so marked. A stored proposal without the mark (one written before, for the machine or a workspace, whose
  `order` said which accounts alone may run: `HelpAgentProposals.cs:416-418`) is refused at its Apply (`:526-532` for `order`, and
  `use`'s) with the reason, *written for a list that is gone (D197): propose it again*, never applied to the set.
- **`use`** (`:230-280`): the same fields; `workspace` refused with the terminal's sentence.
- **`order`** (`:48-57`, its judge `:380-420`): `accounts` the set's order, as the terminal's; `clear` returns to the ids' order;
  `clear` or `accounts` with a `workspace` refused.
- **`default`** (`:172-225`): retired from `Doors` (`:36`); a stored proposal of it is refused at Apply, naming `order`.
- **`ready`**: unchanged, `own` included, which ends a line of `cooling.json` (§3 point 4).
- **The service** (`HelpProposalBox.ProposeAgent`, `HelpAgentTool`): the tool's schema drops `workspace` for `use` and `order`
  and drops the `default` door, and the box writes `set`, in the same change as the driver's `Doors` (the kinds test holds the two
  lists equal, TOOL4l-b).
- **The room** (`HelpRoomMayPropose.cs:22`, `:38`, the machine facts) words the set, and its three goldens follow. The places
  `setting-doors.md` lists for a `setting` door (the driver's judge, the service's writer, the tool's words, the coverage row, the
  room's row and its goldens) are the `agent` kind's too, and change together.

## 6. The store: one fold, said once

### 6.1 The fold's rules

One typed operation in the driver library, `AccountSet.Fold(raw, accounts)`, pure, the same rules `AccountSet.Of` reads by. Per
agent:

1. **When there is anything to fold**: the raw file holds the agent's `defaults` entry, or an entry for it in `workspaces`,
   `workspaceRotation` or `workspaceRotationUse`, or no stored order while it has accounts, or a stored order that leaves out an
   account or names one that is gone. A folded file has none of these, so it folds to itself.
2. **The order**, the union with nothing signed in dropped: the machine's scope, then each workspace's by name (ordinal), each as
   its first, then its list; then every other account, by the stored order where the machine had one, else by id. **A scope's
   first is where its starts began** (round 3, Fable's F9): its `Begins` as `ScopeOf` reads it, its default where its own list holds
   it, else its list's first (`begins = own ∈ list ? own : list[0]`, `HarnessSettings.cs:873`), and with no list its default
   (`:869`). A default its own list does not hold was never where its starts began, so it is placed as every other account is. A
   scope is read as the raw file names it, so a machine scope that named nobody places nothing here. An account keeps its first
   place. A name that is no account here is left out and said (`agents.pooled.gone`). **Which firsts moved** compares each scope's
   `Begins` with the order written: each whose `Begins` is not the order's first is said with its new place (`agents.pooled.moved`,
   and for the machine's scope `unnamedMoved`); a machine scope that named nobody is compared by its walk's first, the first of
   its accounts by id (`Walked`, `HarnessRoster.cs:987-991`).
3. **The settings**, field by field (`use`, `keep`, `switch`, `curve`), over each entry as `RotationUse.Read` normalizes it, so
   `early` and `near` compare as the `switch` and `curve` they mean.
   - **The machine's entry always takes part** (round 3, Fable's F7), read by a start or dormant: it is the entry `profile use`
     without `--workspace` wrote, the one the walk reads where nothing is named (`Over`, `HarnessSettings.cs:844-845`), and the slot
     the set's settings are written to, which an older build reads beside the folded order (§6.4). Each field it sets is the set's.
   - **A workspace's entry takes part only where a start read it**: beside a list of its own (`ScopeOf`, `:869`). A field the
     machine's entry leaves unset is the one value every taking-part workspace entry that sets it agrees on; else none, the default.
   - **An agent whose machine scope named a default and no list** (round 3, Fable's F4, the steward's choice), that default an
     account here, takes `use: order`: every machine-scope start began on that account (`ScopeOf`, `:869`), and under `goal` the
     order's first is only the last tie-break, so `order` keeps them beginning there. A `use` its machine entry set otherwise was
     never read, and is said as not kept; the receipt says why (`agents.pooled.inOrder`).
   - **A `keep`** naming no account of the set, or left alone in a set of one, is dropped and said, whichever entry set it.
   - **What is said** (`agents.pooled.notKept`, with its workspace, or `notKeptUnnamed` for the machine's entry): every value a
     taking-part workspace entry set that the set does not take; every field a dormant workspace entry set; a `keep` dropped; and a
     `use` the previous point replaced. The machine's other values are the set's, so never said. The machine's fields this build
     does not know are kept, as today; a workspace's are said by name.
4. **What goes**: the agent's `defaults` entry and its entries in `workspaces`, `workspaceRotation` and `workspaceRotationUse`; a
   workspace left with nothing is dropped, as today. `versions` and `workspaceVersions` are untouched.
5. **What is written**: `rotation[agent]` the whole order, `rotationUse[agent]` the settings, and a receipt in `pooled[agent]`
   where anything is to be said (§6.3). **Each account a stored order left out**, where the file held one for the agent
   (`rotation[agent]`), is said as the fold places it, one line each with its place (`agents.pooled.joined`; round 3, Fable's F10):
   an older build's sign-in, or an account no list of the machine's held, now runs every workspace's work. Writing a first order
   where the file held none for the agent is not itself said: with nothing named its starts already took every account, and what
   it changes for a scope that named some is that scope's line.

### 6.2 When it runs, and its writer

**Every write folds first** (round 3, Fable's F5, the steward's choice). `HarnessSettings.Change(path, edit)` decides an edit from
the file as it stands under its keep lock (the alone-path design §1.4 and its PUSH1f2e row, as KEEPALL1b's amendment leaves it):
PUSH1f2e1 built it after Fable's read (merged at `915440734`) as the lock, `Load`, the edit and the whole write, every window writer
of `harnesses.json` through it, and the rest of PUSH1f2e brings the terminal's account verbs to it. POOL1a makes it fold before it
edits: under the lock it reads the raw file, runs `AccountSet.Fold` over it, runs the edit over the folded settings, appends the
fold's receipt where it had anything to say, and writes the result before the lock goes. So the first writer in any process is
the folder: the terminal's `agent login` or `profile order` with the host down, a door's edit after an older build's write, or the
come-up. No edit is ever made over a raw file whose scopes remain; one that was, writing the projected union as `rotation[agent]`
beside a workspace's list or a default, would leave the later fold reading the union as the machine's list as it was. A folded
file folds to itself, so the fold adds nothing to a write where there is nothing to fold. A writer of `harnesses.json` outside
`Change` that PUSH1f2e leaves (`Save` stays public) is moved into it by POOL1a.

**The come-up** is `Change(path, identity)`: once, at the driver's come-up, for a file with anything to fold, so a file nobody edits
is folded too. Between a read and the first folding write, every reader sees the set through `AccountSet.Of`, so no start, look
or door reads a workspace's scope. A write that changes the set (a sign-in, a key, a removal, an order) writes the order whole
(§2.2), through `Change` like every other.

**A lock held through the whole wait** (round 3, Fable's F11): `KeepLock.Take` waits, then refuses and writes nothing
(`KeepLock.cs:61-99`). A come-up so refused logs the refusal once to the machine log (an `accounts.pooled` line saying the lock was
held, by the holder its refusal names, its process and since), and the fold is tried again at each next look until one lands,
unless another writer's `Change` has folded the file first. Nothing waits on it: every reader reads the projection meanwhile. A
door's `Change` so refused is refused as it is today, its edit unwritten and said.

Each fold writes one `accounts.pooled` line to the machine log (D94) per agent folded, whichever writer made it: how many accounts,
how many scopes folded, how many settings not kept, by count and never by name.

### 6.3 Said once

`pooled[agent]` is a list of receipts, one per fold that had anything to say. A receipt keeps its `id` (round 3, Fable's F13: a
random id, written once and unique among the agent's receipts); when it was written; each folded scope (`workspace` or none for
the machine's) with its default and list as they were, and the first its starts read; the order written; each first moved, with
its new place; each account the stored order left out, with its place; the account whose machine scope named it alone, where
`use` was set to `order` for it; each setting not kept, with its scope and value; each name left out; and `said`. It is a section
of `harnesses.json`, listed in `HarnessSettings.Sections`; an older build keeps it as a section it does not know (`Kept`,
`HarnessSettings.cs:61-65`).

While any receipt is unsaid, the agent's page shows each (§4.2) and `agent list` prints them; *Got it* (`ACCOUNT_USE` `said`, with
the ids of the receipts it was shown) marks those said, and they are never drawn again. A receipt appended after the page drew is
not among those ids, so a press that never showed it leaves it unsaid; an id no receipt holds is passed over. A later fold
appends its own receipt and never replaces one unsaid. Said receipts beyond the last five are dropped at the next write. Nothing
else announces it: it is news, not a wait (D196 point 7).

### 6.4 Builds already shipped

Named, as `compatible-builds.md` asks: an older desktop rolled back to (D139's rollback) over a folded file, and the CLI's
TypeScript writer, which PUSH1f2e removes before POOL1a lands.

- **An older build over the folded file** reads `rotation[agent]` as the machine's list naming every account, no default and no
  workspace scope, so every workspace takes the machine's scope: the same accounts in the same order with the same settings. What
  differs is its own: an account it signs in is outside the list until joined, as it always was.
- **What an older build writes** (a workspace's list, a default, a join, an account it signed in) is folded by the next newer
  write or come-up, whichever comes first (§6.2), and said again in a new receipt. No version stamp is needed, since the fold is a
  rule over any file, not a step a stamp would gate.
- **What is left**: between an older build's write and the next newer one, the newer build reads the set through
  `AccountSet.Of`, so no newer start honours the older build's list.

## 7. What stays machine-wide

- **The cap**, one number for the machine (D130 point 10; CAPCOUNT1, CAPSET1): what the driver starts, never a conversation.
- **What each account said and when it cools**: `windows.json` and `cooling.json`, per account, as today.
- **The machine's default agent** (`driver.json`'s adapter), which is no account.
- **A workspace's own pinned versions** (`workspaceVersions`): which binary its sessions run, no account.
- **The person's own sign-in** (§3), outside the set.

## 8. The consequence, stated

- **A workspace that had its own list now runs on every account of Daoris's.** A work workspace kept to a work account, or a
  personal one kept off it, is no longer kept: D130's aim E, *keep work where it belongs*, has no setting now.
- **A scope that named one account alone now runs on all of them.** Where several scopes began at different accounts, only one
  can be first: the machine's, else the first workspace's by name. Each other first is placed later, and the receipt says where.
  Under *Make the most of them* every account shares the work; *One by one, in order* begins at the order's first. An agent whose
  machine scope named one account alone is used *One by one, in order*, so its starts keep beginning there, and the receipt says
  so (§6.1 rule 3).
- **An account no list held now runs**, every workspace's work.
- **A setting two scopes chose differently** becomes the machine's, or the default, and is said.
- **What spends**: Daoris may now start any workspace's work on any account signed in for it, which is what the owner chose. Each
  account's own plan and terms still apply (D130 point 12).
- **The own row says less**: who is signed in at the terminal and its state are no longer read (§3 point 3).
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
- **Folding inside `Load`** (round 1). A load that answers folded settings while `Save` writes the difference from what it
  understood would lose the fold's own baseline; the raw file and one projection keep both (Codex's smaller shape, §13).
- **A fold at every read, never written.** An older build would go on reading the workspace lists the newer one ignores.
- **A fold at the come-up alone** (round 2). A raw `Change` before it, the terminal's with the host down or a door's after an older
  build's write, would write the projected union as `rotation[agent]` while the scopes remained, and the later fold's receipt would
  report the union as the machine's list as it was (Fable's F5); every `Change` folds first instead (§6.2).
- **An order absent until edited** (round 1). A new account's random id would sort it anywhere in the order.
- **Asking the person before the fold.** The owner decided the model; a question would hold every start behind it and bring the
  lists back for one turn.
- **A notice in D196's record.** `data/notices.json` is NOTIFY2f's, not built; the receipts live where the accounts are.
- **One receipt replaced by the next fold** (round 1): an unread receipt would be lost.
- **Dropping the per-scope settings silently**, or taking the most cautious of them: a setting chosen once and gone unseen is
  what the receipt is for; the cautious merge would invent a choice nobody made.
- **Leaving `use` as it was where the machine scope named one account** (the steward's first reading, before round 3). Folded as
  the order's first under *Make the most of them*, that account would only break the last tie, and the starts that all began
  there would begin wherever the walk took them (Fable's F4).
- **The machine's dormant entry judged by the took-part test** (round 2). It is the entry the person set for the machine and the
  one an older build reads beside the folded order; dropping its fields because no start read them would undo the person's choice
  for the set (Fable's F7).
- **Keeping the own sign-in's read at the person's press.** It is the person's press, but it runs the agent in their own home,
  which may refresh their token beside their own terminal's and sign them out (§0.4); the owner's *never changes it* weighs more
  than a row's state (§12).
- **Applying a stored `order` proposal to the set.** A list of one that meant *only this account* would mean *this one first*.
- **Granting `trust` in the order's first alone where none is named** (round 2). Any account of the set may run any folder's
  work, so a folder trusted for the first alone would hold the next start the walk takes on another; the steward settled every
  account (D197's note; §5.1).
- **Granting an own-home trust hold from the screen.** The press would write the person's own trust file, which the direction
  rules out; the hold says how to trust the folder with the tool itself instead (Fable's F3; §3 point 4).

## 10. What this amends

- **D125**: point 4 (`rotation` alone, `workspaceRotation` folded; resolved for every workspace the same), point 8 (`profile
  order` without `--workspace`), its ACCT1 note (a sign-in's join) and its CODEXUSE3 note (the own sign-in's windows read on a
  press). The cool-off, a limit not a strike, and the walk stand.
- **D130**: points 3 and 4 (one set per agent; every account in it; no default), point 5 (the wait that asks goes), point 11 (the
  doors), point 12 (*and listed* becomes *signed in for Daoris*), the goal's point 7 (settings per agent), the TOOL4g note (the
  screen's scope controls), and the TOOL4l-a and TOOL4l-b notes (the doors' `workspace` and `clear`; the `set` mark). The
  account-use design §2 rules 1-3, §3 and §9. The walk (§16.3), what is left by the agent's own word, the cap
  and ACCTWALK3 stand.
- **D152**: point 4 (*Runs for* and *Use in a workspace…*), point 5 (*asks which lists it joins*), and its UX7b note's step 3; the
  UX7 design §4.2, §4.3's *Read*, §4.4's summary row and §4.5's steps 1 and 3.
- **D175**: OWNSPACE1a's settlement 2 holds for every workspace; its trust paragraph's hold on the person's own home offers no
  press and is never granted from the screen (§3 point 4); OWNSPACE1b's settlement 1 loses the own row's state and *Read again*,
  and settlement 5 (`inherited`) is always true for a real agent and no longer said, the field leaving `ACCOUNTS`; OWNSPACE1d's
  join sentence goes. Points 1 to 3 stand; point 3's continuation is the one run of the agent in the person's home that is left,
  judged against no scope (§3 point 5).

## 11. The rows

All after **PUSH1f2e**, which adds `HarnessSettings.Change`, hands `agent profile …` to the driver's program and removes the
TypeScript twins. Its first part, PUSH1f2e1, merged after Fable's read (`915440734`): `Change` and every window writer of
`harnesses.json` through it, the account verbs' shapes unchanged. The rest is being built now, by a session of the owner's install,
under a brief to keep today's account verb shapes (round 3, Fable's F12), so the rows below take its verbs and tables as it builds
them. Where PUSH1f2e lands otherwise, the rows wait for it to be read again.

- **POOL1a — one set in the driver and on the bridge** (driver, modules). `AccountSet.Of` and `AccountSet.Fold` with their table of
  shapes; `HarnessSettings.Change` folding before every edit, every writer of `harnesses.json` through it, the come-up as
  `Change(path, identity)`, a come-up a held lock refuses logged and tried again at the next look, and the machine-log line (§6.2);
  the order written whole at every change of the set; `pooled` with each receipt's id, and `ACCOUNT_USE` `said` by id; every reader
  of §2.4 moved to the projection, `AccountAttention`'s twins, the wiring behind `STARTS` and `AccountsRoutes`' edits with them,
  `InheritsDaorisAccounts`, `PlacesSaid` and `DefaultStanding` leaving; §3's own sign-in (the `own` parameter leaving the
  roster's report and the probe, `ResumeAccount.Judge` asking no scope of a null account, the own-home trust hold with no press and
  `TRUST_FOLDER`'s refusal, `agent trust` with no account named granting every account of the set and never the own home); the
  default's readers on the order's first; the bridge's answers and refusals of §5.2 (`HARNESSES`, `HARNESSES_REFRESH`,
  `HARNESS_ENDED`, `HARNESS_ACTION`, `ACCOUNT_USE`, `ACCOUNTS`, `STARTS`, `TRUST_FOLDER`), with `ResolveScope`, `JoinProblemOf`,
  `WithJoined`, `PlacesOf` and `DefaultAllowed` leaving with their last callers. **The tables it rewrites** (F12): the CLI's
  `src/Daoris.Cli/test/fixtures/rotation-use.json`, whose `read` rows resolve a scope per workspace, `order` rows clear a list's
  settings with it and `problem` rows bind a default to its list, each read by `RotationUseTwinTests`: rewritten to the set's rules,
  or retired with their helpers where PUSH1f2e has moved the table into the driver's suite; and, where PUSH1f2e keeps them,
  `AccountJoinTwinTests`' tables (`joinProblem`, `withJoined`, `placesOf`) and `ProfileDefaultTwinTests`' (`DEFAULT_EDITS`),
  retired with `JoinProblemOf`, `WithJoined`, `PlacesOf` and `DefaultEdited`. Proof: the fold's table (each scope shape, the
  order's union with each scope's first as its starts read it and the moved firsts, a default outside its own list, the machine's
  entry dormant and read, the settings rule over normalized workspace entries, a machine scope of one account taking `use: order`
  with its line, a `keep` dropped, a name gone, an account no stored order held placed and said, idempotence, an older build's
  file folded again with a second receipt kept beside an unsaid first); a raw edit through `Change` on an unfolded file leaving it
  folded with the receipt reading the scopes as they were; a come-up refused by a held lock, logged, then folded at the next look;
  *Got it* by id marking only the receipts it names; an older build's reading of a folded file as a test over the pre-change
  `ResolveScope` rule; a new account placed last whatever its id; a test that every sign-in action's spawn sets the
  configuration-home variable under the home, and that the probe and usage read never run with none; the roster's whole refresh
  (`HARNESSES_REFRESH` with no agent) and the driver's `agent list` as PUSH1f2e builds it asking no own sign-in, neither its status
  nor its usage; a null-account record resumed in its home against a folded set, and its cooling, refused and signed-out branches;
  a trust hold whose file is the own home's carrying `own` and refused by `TRUST_FOLDER` with nothing written, and `agent trust`
  with no account named writing each account's file and never the own home's; `held.no-account` unchanged; each touched class
  seen failing first.
- **POOL1b — the terminal and Ask Daoris** (driver, service; after 1a). §5.1's table in `daoris-driver agent profile` and `agent
  list`, with its goldens: the verbs PUSH1f2e built as today's shapes (`login --join` and `--join-machine`, `profile join`,
  `profile default`, `--workspace` on `order` and `use`) turned into §5.1's refusals (F12); §5.3 in the driver's judge, the
  service's tool and box (the `set` mark), the room and its goldens; a stored unmarked proposal refused at Apply. Proof: each
  refusal's words, both ways of the kinds test, the room's goldens.
- **POOL1c — the page** (web; after 1a and 1b). §4: the dialog's steps 1 and 3, the rows and the ⋯, *How accounts are used*, the
  receipts by id and *Got it*, the own row, the own-home trust hold with no press, the start dialog, *What needs you*, every key in
  §4.4 in both catalogues, the glossary's term and its `avoid`. The parent adds the `HelpCoverageTests` rows it names. Proof:
  `agents.test.ts`, `AgentsView.test.tsx`, `AddAccount`, `AccountUse` and `TrustAsk` tests and stories at the main area's two widths
  in en and zh, `i18n:check`, `names:check -- --strict`.
- **POOL1d — the install's fold and dialog, seen** (parent; after 1c, on a republish). Before the republish, the install's
  `harnesses.json` and the own home's credential file's last write are copied into `local/`; after, the folded file is compared
  with §6.1's rules by a scratch script in `local/` that prints pass or fail per rule and no values; a second come-up writes nothing;
  the receipt is said once, *Got it* holds across a restart; the dialog's step 3 and the own row in en and zh; the own home's
  credential file and trust file not written by Daoris across the session. Proof: dated shots, the script's pass lines, the machine
  log's `accounts.pooled` line.

## 12. What no check here covers, and what is open

- **Written from the code, nothing run.** Every file and line above was read at `dbac74517`, and those round 3 cites again at
  `20c324794`, where of round 2's files only `AccountAttention.cs` had moved (NEEDSSCOPE1; its lines above are `20c324794`'s). Since
  then PUSH1f2e1 (`915440734`) has moved lines in `HarnessSettings.cs` (by twenty-eight after line 177), `HarnessSelection.cs`,
  `AccountsRoutes.cs`, `AgentsRoutes.cs`, `HelpRoutes.cs`, `AgentActions.cs`, `AgentsCommand.cs`, `AccountCooling.cs` and
  `AccountNames.cs`; the cites stay `20c324794`'s, and each names its member. No test ran and the window was not looked at.
- **Settled by the steward** (D197's note, before Fable's read, as round 3 corrected it):
  - **The machine's default mapped to the set** (§0.3). The machine's own account is the person's own sign-in, outside the set;
    `defaults[agent]` is a Daoris account the machine scope began on, and folds into the order as its first. Where the machine
    scope named it alone, the fold sets `use: order`, so its starts keep beginning there (§6.1 rule 3); the note's `use: list`
    names no mode, and round 3's correction under it says so.
  - **The own sign-in's read** (§3 point 3) is dropped, which takes *Read again* and the own usage read from D175's OWNSPACE1b row
    and CODEXUSE3, and, with Fable's F2, the roster's whole refresh's question of it.
  - **`trust` with no account named** grants the folder for every account of the set (§3 point 4, §5.1); the own home is never
    written, by the door or by a hold's press.
- **PUSH1f2e as it lands.** Its brief keeps today's account verb shapes, and the rows take them so (§11). PUSH1f2e1 put every
  window writer of `harnesses.json` through `Change`; the tables the rest moves or keeps, and the terminal's writers coming to
  `Change`, are read again when it merges, before POOL1a starts.
- **The receipts in Notifications** once NOTIFY2f's record exists: a row for later, not this design's.
- **The install's shape** is not read here; §8's consequences are said by shape, and POOL1d reads the real one.

## 13. The reads (D191)

**Codex, round 1** (read at `ea0fc2cb7`, prompt and output in `local/` scratch): twenty-one findings, fifteen blocking. Each was
checked against the code at `dbac74517`; all hold, and each is taken into round 2 as the table says. Since the design changes a
stored record's shape (`harnesses.json`'s sections and a new `pooled`), a decided rule others rest on (D130's scopes, D175's own
sign-in) and the walk's inputs, it changes a foundation in D191's amendment's classes; whether Fable reads it is the steward's.

| # | Finding | Blocking | Verdict | Taken |
|---|---|---|---|---|
| C1.1 | A sole implicit account takes no settings (`HarnessRoster.cs:989-990`); settings beside no list lie dormant, which a fold would wake | no | holds | §0.1's last point; §6.1 rule 3 takes part only entries a start read |
| C2.1 | The own sign-in is run at a press (status `HarnessSelection.cs:217`, usage `HarnessRoster.cs:2041`), and the agent may refresh its token there | yes | holds | §0.4; §3 point 3: neither read is asked any more; §12 |
| C2.2 | A null-account record is judged against the resolved scope (`HarnessRoster.cs:898`, `:932`); the set could displace it | yes | holds | §3 point 5: in scope against the set, its other branches unchanged, its continuation the one run left; POOL1a's proof |
| C3.1 | An absent order sorts a new random id anywhere (`AccountNames.cs:308`, `HarnessSettings.cs:581`) | yes | holds | §2.2: the order written whole at the first change of the set |
| C3.2 | Several scopes' firsts cannot all stay first; §8 promised they would | yes | holds | §6.1 rule 2 and §8 say which is first; `agents.pooled.moved` says each other one's place |
| C3.3 | Settings agreement needs normalized values, dormant entries and a missing `keep` ruled | yes | holds | §6.1 rule 3 |
| C3.4 | Folding inside `Load` with the folded shape as `Understood` loses the migration's baseline (`HarnessSettings.cs:188`) | yes | holds | §2.4 and §6.2: `Load` stays raw, `AccountSet.Of` projects, `Change` folds the raw file under the lock |
| C3.5 | No concrete trigger for idempotence; a later receipt replaced an unread one | yes | holds | §6.1 rule 1; §6.3's receipts appended, never replaced unsaid |
| C4.1 | `places` lives in `HARNESSES` (`AgentsRoutes.cs:147-177`) and `HARNESS_ENDED` (`AgentActions.cs:418`), not `ACCOUNTS` | yes | holds | §5.2 names each answer and what leaves it |
| C4.2 | `AccountAttention` has its own `HeldBy`, `OwnScopes`, `RunsFor`, `JoinChoices` and scope reads (`:57`, `:114`, `:393-456`) | yes | holds | §2.4's modules point; §4.3; POOL1a |
| C4.3 | A stored machine-wide `order` proposal `[A]` meant *only A* (`HelpAgentProposals.cs:417`, applied unchanged at `:532`) | yes | holds | §5.3: new proposals marked `set`; unmarked ones refused at Apply |
| C4.4 | `first-here` reused would give old notes the set's meaning; `ProfileFrom` and `RotatedStart.Scope` need theirs | yes | holds | §4.3: `first-order` new, old codes keep their meaning in words without the list's name; §2.4's records point |
| C4.5 | Removing an account clears settings through `WithRotation` (`HarnessSettings.cs:451`) | no | holds | §2.3: settings outlive the order; a `keep` left alone is dropped and said |
| C5.1 | Catalogue inventory incomplete (`harness.profile.removeConfirm`, `loginNew.*Nowhere`, `login.refusedTip`, `harness.default.*`); glossary bans miss *this machine's accounts* and 本机账户 | no | holds | §4.4's lists and glossary |
| C5.2 | `note.account.refused-own` and `note.held.refused-own` send the person to Daoris to sign their own sign-in in again | yes | holds | §3 point 8; §4.4's rewording |
| C5.3 | *Now serves every workspace* overstates a key unchecked and a kept account; Chinese for `default-here` awkward; the count summary needs a singular | no | holds | §4.1's *may run*; §4.3's new words; §4.2's one and other forms |
| C6.1 | POOL1a removed helpers POOL1b's routes still called | yes | holds | §11: the bridge's consumers move in 1a; the helpers leave with their last callers |
| C6.2 | POOL1b needs the stored-proposal discriminator settled first | yes | holds | §5.3's `set` mark, in 1b with its refusal at Apply |
| C6.3 | POOL1c needs 1b and the answers' shapes; step 1's `runs` (`AgentPage.tsx:310`) and `orderHint`'s sentence were missed | yes | holds | §11: 1c after 1a and 1b; §5.2's shapes; §4.1 step 1; §4.2's `orderHint` |
| C6.4 | POOL1d's counts and shots cannot prove the fold's rules or an untouched own home | no | holds | §11 POOL1d: a private comparison printing pass or fail per rule, a second come-up, *Got it* across a restart, the own home's last write |
| C7.1 | A smaller shape: raw persistence, one projection for every reader, one locked migration, a stable receipt, a whole order | no | holds | taken whole: §2.2, §2.4, §6 |

**Fable, round 2** (read at `20c324794`, the steward having asked most of §2.4, §3 and §6; prompt and output in `local/` scratch):
thirteen findings, five blocking. Each was checked against the code at `20c324794`; all hold, and each is taken into round 3 as
the table says. Where Fable offered options the steward chose: F3, the
own-home hold offers no press (option a); F4, `use: order` for a machine scope of one account (option b); F5, every `Change`
folds first; F12, the rows take PUSH1f2e's verbs as it builds them.

| # | Finding | Blocking | Verdict | Taken |
|---|---|---|---|---|
| F1 | `ResumeAccount.InScope` is not true for a null account against the set: with a non-empty list it answers `account is not null && …` (`ResumeAccount.cs:29`, `:49-52`), so a driven record begun in the own home judges `Outside` and `ElsewhereAsync` carries its words onto the walk (`HarnessRoster.cs:898-899`, `:912`, `:923`, `:932-934`) | yes | holds | §3 point 5: `Judge` asks no scope of a null account, `KeptOf` (`:30`) already passing one; POOL1a's proof |
| F2 | *Nothing else sets `own`* is false: `RosterAsync(config, refresh: true)` passes `own: refresh` (`HarnessRoster.cs:797`, `:810`), so `HARNESSES_REFRESH` with no agent asks every agent's own sign-in, its status and its usage (`:413`, `:440`, `:448`, `:453`, `:2124`; `AgentsRoutes.cs:53-56`; `agents.ts:283-290`); `ProbeAsync` defaults to `own = true` (`HarnessSelection.cs:246`, `:309-313`) | yes | holds | §0.4, §3 point 3 and §5.2: the `own` parameter leaves the report, the press, the usage read and the probe; POOL1a's proof over the roster's whole refresh |
| F3 | The trust hold of the one run left resolves `ClaudeTrust.PathFor(trustFile, null, …)` to the own home, and `TRUST_FOLDER` writes `ClaudeTrust.Grant` into the person's own file (`Driver.cs:1789-1805`; `ClaudeTrust.cs:160-`, `:264-268`; `DriverRoutes.cs:334-346`) | yes | holds | §3 point 4 (option a): no press, `TRUST_FOLDER` refuses it as `TRUST_OWN_HOME`, its sentence says to trust the folder with the tool itself; §0.4, §4.3, §4.4, §5.2; POOL1a's proof row |
| F4 | The steward's note keeps starts on the machine's default *under `use: list`*, which is no mode: the modes are `goal` and `order` (`HarnessSettings.cs:788-806`, `:867-869`, `:975`; `RotationUse.cs:32`) | yes | holds | a dated correction under the note in D197; §6.1 rule 3 (option b): `use: order` for a machine scope of one account, said (`agents.pooled.inOrder`); §8; §9; §12 |
| F5 | Only the come-up folds, so a raw `Change` before it (the terminal's `agent login` or `profile order` with the host down, or after an older build's file) writes `rotation[agent]` whole as the projected union while the scopes remain, and the later fold's receipt reports the union as the machine's list as it was (`HarnessSettings.cs:401-417`; `KeepLock.cs:11-30`) | yes | holds | §6.2: every `Change` folds before its edit, the come-up being `Change(path, identity)`; §6.4; §9 |
| F6 | Readers §2.4 does not name: `WiringAsync` and the wiring panel (`HarnessRoster.cs:1753-1782`); `AccountsRoutes`' `OrderEdited`, `UseEdited`, `DefaultAllowed`, `OwnList`, `OwnDefault`, `OwnKeep` (`:104`, `:471-577`), behind `ACCOUNT_USE`, Ask Daoris's writes and `ProfileDefault`; `DefaultStanding` over `ResolveFrom` (`AgentsRoutes.cs:697-703`, `:729-739`); `InheritsDaorisAccounts` and `ACCOUNTS`' `inherited` (`HarnessRoster.cs:999-1004`); `PlacesSaid` (`AgentActions.cs:584-591`) | no | holds | §2.4's five new points; §2.3; §4.4's retired refusals; §5.2's `ACCOUNT_USE`, `ACCOUNTS` and `STARTS` |
| F7 | The machine's dormant `rotationUse` entry should always take part, its set fields winning and `keep` as ruled; only workspace entries pass the took-part test and are said (`HarnessSettings.cs:442-469`, `:844-845`, `:867-875`) | no | holds | §6.1 rule 3; §9 |
| F8 | No receipt line for a machine scope with a default and no list: `oneAccount` should take the subject *workspaces that named no accounts*, as `moved` does | no | holds | §4.2's `unnamedOne`, a key of its own since a composed subject does not translate, with `unnamedMoved` and `notKeptUnnamed` for the machine's other lines |
| F9 | The order should begin with each scope's `Begins` as `ScopeOf` reads it (`begins = own ∈ list ? own : list[0]`, `HarnessSettings.cs:872-873`), then its list, and the moved firsts should compare `Begins` | no | holds | §6.1 rule 2 |
| F10 | A fold that places an account a stored order left out says nothing, though that account now runs every workspace's work | no | holds | §6.1 rule 5 and §4.2's `agents.pooled.joined`, one line per account; only a first order on a file with none is silent |
| F11 | Nothing says what the come-up's fold does when it meets a held lock | no | holds | §6.2: the refusal logged, the fold tried again at the next look, or made first by the next writer's `Change` |
| F12 | PUSH1f2e is being built under a brief to keep today's account verb shapes, so the rows cannot take those verbs as gone | no | holds | §5.1's lead and §11: POOL1b turns 2e's verbs into the refusals; POOL1a names the CLI fixture tables it rewrites and checks that 2e's driver `agent list` asks no own sign-in; §12 |
| F13 | *Got it* marks receipts by the times they were shown, which is no identity | no | holds | §6.3: each receipt an id, *Got it* by id; §5.2 |
