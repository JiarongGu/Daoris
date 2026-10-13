# Duplicated code in the .NET sources: a survey (2026-10-14)

Evidence for the DEDUP rows in `TASKS.md` and D166's note of 2026-10-14 (the owner: *"we more focus on modularize the code to
reduce duplication and easier to manage"*). A read-only survey of `src/Daoris.Desktop/Daoris.Desktop.Driver/` (`D`),
`src/Daoris.Desktop/Daoris.Desktop.Modules/` (`M`) and `src/Daoris.Service/Daoris.Service.Core/` (`C`), on main after STRUCT3g-i;
tests, build output and the deliberate TypeScript twins excluded. Line numbers are as surveyed and move with the code: a row
re-reads them. Paths after a module's name are under that module's folder.

## What bounds every consolidation

- **Projects.** Modules references the driver; the service's core and the driver reference each other not at all. A helper
  both use would need a shared project; without one, each keeps its own copy, and Core's is its own row.
- **Visibility.** The driver exposes internals only to its tests, so a Platform helper Modules should use is `public`
  (`AtomicFile` and `DaorisHome` are; `DriverHttp` is not).
- **The launcher's link set.** `Setup`'s `StagedBuild.cs`, `StagedBuildFiles.cs` and `InstallSwap.cs` compile into the
  launcher on the base library alone: their private helpers stay, by design.
- **Declared twins stay.** `DaorisHome` (driver and core), the remotes map (`RemoteTarget` and `RemoteConfig`), the accounts'
  files and their TypeScript twins.
- **Core consolidated its JSON reading once** (`C/JsonFields.cs`, REV3 CLEAN1); the driver has no equivalent.

## The clusters, by payoff

### 1. JSON field readers: about 44 copies, drifting

`Text`/`Str`/`String`/`Field` over a `JsonElement` (`TryGetProperty` and a string kind), each private. Driver: Accounts
`AccountReadings.cs:237`, `Usage.cs:188`; Agents `Acp.cs:133,205,1504,1608`, `AcpBackground.cs:133`, `AcpStreams.cs:331`,
`ClaudeStreams.cs:299`, `StructuredOutput.cs:519`; Conversations `Domain/HelperRules.cs:206`, `GoOnFiles.cs:160`,
`HandOverFiles.cs:138`, `HelperFiles.cs:141`; Help `HelpConversations.cs:327`, `HelpProposals.cs:258`; Platform
`DriverConfig.cs:1114`, `MachineLogReader.cs:279`, `NoteCodes.cs:190`; Plugins `Domain/PluginCatalog.cs:387`,
`PluginHealthRules.cs:173`, `PluginSource.cs:126`; Quests `Domain/Evidence.cs:190`; Review `Domain/OpinionRules.cs:455`,
`OpinionView.cs:203`, `ReviewRules.cs:238`, `OpinionDeliveryFiles.cs:123`, `OpinionGateFiles.cs:219`; Service
`ServiceClient.cs:1643`, `RemoteSyncPayloads.cs:581`, `RemoteTarget.cs:134`; Sessions `AbandonRecord.cs:208`, `Trace.cs:301`;
Trees `AutoLanding.cs:406`, `CommitRules.cs:194`, `LandedBranches.cs:717`, `SessionBranches.cs:132`, `SessionGroups.cs:142`;
Workflows `WorkflowRunBindings.cs:179`, `WorkflowNamed.cs:535`, `WorkflowRunFiles.cs:200`. Modules: `CdpCookies.cs:49`,
`DriverModule.Accounts.cs:229`, `DriverModule.Lines.cs:483`, `RegistryModule.cs:218`, `ReviewTabs.cs:387`, `SessionWords.cs:636`.

**Drift.** About 15 check the element is an object first; about 25 do not, and `TryGetProperty` throws on a non-object. Some
answer null for an empty string, others `""`; `GoOnFiles` trims; `Acp.Field` answers the raw text of a non-string. Siblings:
`Number`/`Int`/`Long` (about 10: `DriverConfig.cs:1127` throws on a non-integer, `LandedBranches.cs:527` answers 0 for none),
`Strings` (7, split on keeping empties), `Items` (4), and readers over `JsonNode` (7) and `JsonObject` (5, with `Flag`).

**Home.** A public `Json` reader in `Platform/` on `C/JsonFields.cs`'s model, the object guard always, one empty-string rule
chosen per call where callers differ. About 45 files.

### 2. Moments written and read: about 165 sites, drifting

Writers: about 45 `ToString("O", …)` in the driver and Modules; without the invariant culture at Agents `RuleProposals.cs:342`,
Conversations `HelperFiles.cs:78,120`, Service `RemoteSyncPayloads.cs:218`, Modules `DriverModule.Rules.cs:110,111`. Four
private `Stamp()`s (`RegistryFollowing.cs:145`, `WorkspaceSetupFile.cs:188`, `AutoLanding.cs:401` to universal time first;
`OpinionView.cs:215` not); `AccountCooling.cs:214` writes seconds only.

Readers: over 60, mostly `DateTimeOffset.TryParse(…, InvariantCulture, RoundtripKind, …)`. **Without culture or style**, so
read in the machine's culture and turned local: Agents `RuleProposals.cs:301,326`, Conversations `HelperFiles.cs:37,55,98`,
Platform `DriverLock.cs:239`, Help `HelpProposals.cs:127`, `HelpSyncProposals.cs:63`, `ServiceClient.cs:1399`,
`WorkingTree.cs:62`; `DateTimeOffset.Parse` in the current culture at `RemoteSyncPayloads.cs:412`. What a bad moment falls back
to differs (the minimum, now, the file's time, null). A strict `IsoMoments` parse is copied five times identically:
`AccountCooling.cs:236`, `AccountReads.cs:160`, `AccountWindows.cs:218`, `DriverConfig.cs:790`, `WorkflowNamed.cs:555`.

**Home.** A public `Moments` in `Platform/`: write, read (invariant, round trip), strict read. About 60 files. Core's own six
`Moment` readers and its writers without the culture are Core's row.

### 3. A JSON file written: about 53 writer sites and 12 node sites, drifting

The block: a memory stream, an indented `Utf8JsonWriter`, the bytes as a string, `\r\n` to `\n`, a trailing newline,
`AtomicFile.WriteText`, usually its folder made first. **Without the line-ending fix**, so written with CRLF on Windows though
their comments say LF: Accounts `Harnesses.cs:514,1184`, `Usage.cs:185`, Platform `DriverConfig.cs:689`, `DriverLock.cs:162`,
Service `RemoteTarget.cs:124`, Workflows `WorkflowRunFiles.cs:197`. Twelve sites write a `JsonNode` with a new
`JsonSerializerOptions { WriteIndented = true }` each call and the default encoder, which escapes Chinese text and `<` where
the writers with `UnsafeRelaxedJsonEscaping` do not (Agents `Permissions.cs:303,528`, `RuleProposals.cs:344`,
`SpawnServers.cs:45`, Help `HelpProposals.cs:146`, `HelpClearProposals.cs:86`, `HelpSyncProposals.cs:242`, Plugins
`PluginState.cs:71`, Sessions `SessionHomeFiles.cs:327`, Setup `InstallUpdateFile.cs:35`, Modules `BrowserFavorites.cs:142`,
`BrowserSettings.cs:120`, `EngineProfile.cs:272`); about ten private `Indented` options besides.

**Home.** A public `JsonFile` in `Platform/`: the text (LF, a trailing newline, one shared options object), and a write that
makes the folder and writes atomically. About 45 files.

### 4. A child process run with patience: 7 copies, drifting

UTF-8 capture, a linked cancellation after the patience, both streams read, the tree killed on time-out: Accounts
`Harnesses.cs:1445-1500`, Setup `SetupWorld.cs:79-120`, Modules `ToolQuestions.cs:35-85`, Plugins `PluginToolRuns.cs:24-100`,
`HookProcess.cs:83-175`, Trees `WorkingTree.cs:902-1100` (three spawn bodies), Accounts `CodexUsage.cs:226-300`. **The kill is
unguarded** at `Harnesses.cs:1480` and `SetupWorld.cs:107`: a process that ends in the race window throws, and `SetupWorld`'s
outer catch says the tool "would not start". Input is closed in some, not redirected in others. Git itself is one
implementation already (`WorkingTree.GitAsync`, 220 call sites).

**Home.** A public `ChildProcess.RunAsync` in `Platform/`, each caller keeping its own words. About 7 files; `Tools.Hand` and
the git start are driver internals Modules cannot see.

### 5. Stores of one shape: 8 stores, and the accounts' four files

A gate, `All()` that answers none on a missing or unreadable file, `Edit(change)` under the gate writing atomically, and a
newest-N bound: Trees `SessionBranches.cs`, `AutoLanding.cs`, `LandedBranches.cs`; Review `OpinionDues.cs`, `OpinionGates.cs`;
Sessions `SessionArchive.cs`, `AbandonRecord.cs`. The same-key predicate is character-identical in `SessionBranches` and
`LandedBranches`. The accounts' `AccountCooling`, `AccountNames`, `AccountReads` and `AccountWindows` each copy `Load`, `Key`,
`Child`, `Text`, `Flag`, `Moment` and `IsoMoments` verbatim.

**Home.** One helper for the accounts' four (safe, quick); a small generic store for the rest only if each store's quirks fit.

### 6. Path and id validity: about 14 copies, drifting

A repository-relative path (no `..`, not empty, not rooted): Agents `Tools.cs:261` (refuses `\` and `:`),
`RepositoryIndexes.cs:111`, Review `ReviewRules.cs:221`, `ReviewShowing.cs:55,84`, `OpinionPacketReads.cs:153-155`, Workflows
`WorkflowSelection.cs:175` (refuses control characters), Plugins `PluginIcon.cs:63`, Trees `GitReads.cs:458`, Modules
`ReviewTabs.cs:55`; they disagree on `\`, `:`, `.` segments, empty segments and control characters, which is a security
question. A session id as a folder: three rules (`Domain/SessionEvents.cs:333`, `HistoryClearing.Home.cs:33`,
`SessionProcesses.cs:387`). Id patterns: `^[A-Za-z0-9_-]{1,64}$` twice (`HandOverQuest.cs:56`, `OpinionGates.cs:22`, the second
uncompiled per call), the kebab id three times in the driver and three in Core, ending in `$` (which takes a trailing newline)
or `\z`.

**Home.** A `SafeNames` in `Platform/`, each rule decided once. About 14 files.

### 7. Text clipped, one-lined, quoted: about 44 copies, drifting

Clipping (`text[..N] + "…"`, the limit, the off-by-three and the trim differing) in about 20 driver files and two of Modules;
`ReplaceLineEndings(" ").Trim()` five times; a quoted block (`> ` per line) eleven times, drifting in indent and trim.

**Home.** A `TextBounds` in `Platform/` (`Clip`, `OneLine`, `Quote`), each call keeping its own limit. About 28 files.

### 8. HTTP: small

`DriverHttp`'s get, post and delete are one body three times; `ToolInstall.cs:460-535` and
`ClaudeReleaseDownloads.cs:75-210` share a download wrapper and a streaming hash, differing in the exception type. 3 to 4 files.

### 9. Writes past `AtomicFile`

Agents `ClaudeTrust.cs:231-247` (another tool's file, its own temporary name), Plugins `PluginFolders.cs:64` and Agents
`DshProfile.cs:86` (plain writes). `DriverLock`'s create-only move is deliberate. In Core, `QuestFiles.cs:115` writes through a
fixed `.tmp` name, the collision `AtomicFile`'s header describes: a latent race, Core's row.

## Across projects

Character-identical or same-rule copies between the driver and Core: `SafeName` (`Conversations/ChatFiles.cs:117`,
`C/QuestFiles.cs:149`), the evidence path rule (`Quests/Domain/Evidence.cs:262`, `C/QuestEvidence.cs:51`), the help proposals'
validators (the driver's `Help/Proposals/*` and Core's `HelpProposalBox.*`), `ErrorOf` (`Platform/DriverHttp.cs:114`,
`C/Remote.cs:150`). Each stays duplicated unless a shared project is made; Core's own sprawl (six `Moment` readers, five
`Clip(120)`, about sixty `"O"` writers without the culture, four writes past an atomic file) is fixable inside Core.
