// The single source of query-key truth — invalidations included. A prefix written as a string
// literal at an invalidation site survives a rename silently; one written here does not.
// Every cross-repository key carries the workspace scope (WSP5; D48 §4), so a circle's answers never
// serve another's from the cache. `*` is "every circle" — the door's own meaning of an absent argument.
// The `all*` prefixes are what invalidation addresses: whatever the scope, a change moves them all.
export const keys = {
  status: ['status'] as const,
  /**
   * Why each open quest is sitting, the starts held for the agent's trust (D73) and those held on cooling accounts (UX6d), as the shell
   * pushes them (FOUNDATION1s, `considerationsSlice` in `bridge/driver`) — written by the pushes, never fetched (its own root, so no
   * invalidation reaches it).
   */
  considerations: ['considerations'] as const,
  /**
   * Who needs the person, as the shell pushes it (PUSH1d4) — written by the pushes, never pulled: no route answers it, and a plain
   * browser has none (its own root, so no broad invalidation reaches it).
   */
  attention: ['attention'] as const,
  /** Done and failed, as the shell pushes it (NOTIFY2f) — written by the pushes, never pulled, as `attention` is. */
  notices: ['notices'] as const,
  /** The presses the driver has not answered yet, as it last told them (STATEHUB1c) — written by the state hub, never fetched. */
  underWay: ['underWay'] as const,
  /** Ask Daoris's conversation opened ahead of the person (HELP5) — kept by the page, never fetched. */
  helpReadied: ['help-readied'] as const,
  /** Where the install's update stands (UPDATE1, D139): asked once, then kept by the shell's `UPDATE_STATE`. */
  update: ['update'] as const,
  /** What a terminal asked the window to confirm over the pipe (RPC1c): asked as the page mounts, then kept by `CONFIRMATIONS`. */
  terminalAsks: ['terminal-asks'] as const,
  /** What waits at the service for the person's confirmation (PERSONDOOR1f): asked only while the page holds the person key. */
  serviceAsks: ['service-asks'] as const,
  /** The same, as the shell tells it (NOTIFY2j): asked as the page mounts, then kept by `SERVICE_CONFIRMATIONS`; a browser polls. */
  serviceTold: ['service-told'] as const,
  /** Whether the host holds a person key the page does not (PERSONDOOR1f's adoption notice): asked once a page holds none. */
  hostKeyed: ['host-keyed'] as const,
  /** Whether the page holds the person key (PERSONDOOR1f): the key itself is `data/api.ts`'s, in memory, never in this cache. */
  personKeyHeld: ['person-key-held'] as const,
  allRepositories: ['repositories'] as const,
  repositories: (workspace: string | null) => ['repositories', workspace ?? '*'] as const,
  allRegistry: ['registry'] as const,
  registry: (workspace: string | null) => ['registry', workspace ?? '*'] as const,
  codeMap: (repository: string) => ['code-map', repository] as const,
  /** Where each circle stands with its remote (SYNC6a) — refetched by every tick, which runs a pass, where it is pulled. */
  allSync: ['sync'] as const,
  sync: (workspace: string) => ['sync', workspace] as const,
  /**
   * Where every wired circle stands, by circle, as the shell pushes it (PUSH1a2, `syncSlice` in `bridge/driver`): under the family's
   * key so its invalidations reach it where it is pulled, and `null` where a circle's name goes, so no circle's own entry is it.
   */
  syncEvery: ['sync', null] as const,
  /**
   * The driver's state, and the prefix of everything read beneath it. The entry itself is written by the shell's pushes where it
   * pushes them (PUSH1a, `bridge/state/slices.ts`) and pulled otherwise; what is read beneath it is pulled.
   */
  driver: ['driver'] as const,
  /** The machine's wiring — shell-only, like the driver's state (D48 §5). */
  remotes: ['remotes'] as const,
  browserSettings: ['browser-settings'] as const,
  lines: ['driver', 'lines'] as const,
  /** Whether a repository's own `daoris.gates.json` marks heavy gates (HEAVYRUN1b2c) — a read of its checkout's file, so under the driver's key. */
  heavyMarks: (repository: string) => ['driver', 'heavy-marks', repository] as const,
  /** Who holds a repository's heavy-run slot and who waits (HEAVYSLOT1) — a read of the records beside the slot, under the driver's key. */
  heavySlot: (repository: string) => ['driver', 'heavy-slot', repository] as const,
  // The running calls the shell pushes (WAITSAY1c2): written by the pushes alone, there is no pull beneath it.
  calls: ['driver', 'calls'] as const,
  /**
   * A repository's or a workspace's Current workflow (WORKFLOW1b) — shell-only, like the lines, and under their key, since
   * Current is drawn from the rules they resolve: every change that asks the lines again asks it again. A route asked for is a read
   * of its own (SETUPUX1o), under the same prefix.
   */
  workflow: (scope: 'repository' | 'workspace', name: string, route?: string | null) =>
    (route ? ['driver', 'lines', 'workflow', scope, name, route] : ['driver', 'lines', 'workflow', scope, name]) as readonly string[],
  /**
   * The process a session's own tree runs under (PROCESS1e) — shell-only, and under the lines' key, since it is the rules they
   * resolve: every change that asks the lines again asks it again.
   */
  sessionProcess: (session: string) => ['driver', 'lines', 'process', session] as const,
  /**
   * Where a session's, a quest's or an ask's work stands in its workflow (WORKFLOW1c) — shell-only, and under the driver's key,
   * so every tick, which asks the driver again, asks it again: a run is read from records that move at every look.
   */
  workflowRun: (scope: 'session' | 'quest' | 'ask' | 'line', id: string) => ['driver', 'workflow-run', scope, id] as const,
  /** How each repository's checkout stands for reading and writing across (D107) — shell-only, like the lines. */
  across: ['driver', 'across'] as const,
  allLandings: ['driver', 'landing'] as const,
  /**
   * Each unit of work's milestone (FABRIC-MILESTONE1b), derived by the driver from the landing, session-branch and tree records:
   * asked again with the landings, the sweep and the sessions, which are what move it. Null is every unit's.
   */
  allWorkState: ['driver', 'work-state'] as const,
  workState: (unit: string | null) => ['driver', 'work-state', unit] as const,
  /**
   * The clean-up's list (WSR3, WSR5), as the shell pushes it where it does (PUSH1c4b; its spec is `sweepSlice` in `bridge/repositories/lines`):
   * its pushes write it, so no tick asks it again; pulled under the driver's key elsewhere.
   */
  sweep: ['driver', 'sweep'] as const,
  landing: (session: string) => ['driver', 'landing', session] as const,
  /**
   * A session's second opinion at its gate, with the findings and their answers (XAGENT1g), and what of every gate waits on
   * the person: under the landings' key, so whatever asks every landing's plan again asks these again too.
   */
  opinion: (session: string) => ['driver', 'landing', 'opinion', session] as const,
  opinionWaits: ['driver', 'landing', 'opinion-waits'] as const,
  /** Where a line of work's one Accept, or an ask's, would land its parts (ACCEPTALL1): under the landings' key, asked again with them. */
  entryLanding: (entry: string) => ['driver', 'landing', 'entry', entry] as const,
  askLanding: (ask: string) => ['driver', 'landing', 'ask', ask] as const,
  /** This machine's harnesses and the accounts they run as — shell-only too (D49 §4). */
  harnesses: ['harnesses'] as const,
  // Under the roster's key, so everything that invalidates the roster asks the wiring again: an
  // account added, removed or chosen changes what a start would take (MAP1b).
  allStarts: ['harnesses', 'starts'] as const,
  starts: (workspaces: string[]) => ['harnesses', 'starts', ...workspaces] as const,
  /**
   * How each agent's accounts are used (TOOL4g): under the roster's key too, so a sign-in, a removal or a default asks the
   * lists, the cool-offs and what each agent last said again.
   */
  accounts: ['harnesses', 'accounts'] as const,
  /** What sessions consumed — shell-only, because per-account usage names a profile (TOOL3). */
  usage: ['usage'] as const,
  /** This machine's plugins — shell-only, since a plugin's folder is a machine path (D64). */
  plugins: ['plugins'] as const,
  /** The machine log read back (LOG1c) — shell-only: the log never leaves the machine (D94). */
  allMachineLog: ['machine-log'] as const,
  machineLog: (since: string, source: string, event: string, level: string) =>
    ['machine-log', since, source, event, level] as const,
  /** What the look cleaned up in a workspace, for its Branches tab (AUTOTIDY1a) — the log's, so shell-only too. */
  tidiedBranches: (workspace: string) => ['machine-log', 'tidied', workspace] as const,
  rules: ['rules'] as const,
  /** One session's landed work, read off the checkout — shell-only for the console's reason (SURF6). */
  diff: (session: string) => ['diff', session] as const,
  /** Every session's review: what a press that removes or moves trees asks again (REVIEW4). */
  allDiffs: ['diff'] as const,
  /** How a session or a quest came to be (TRACE1b) — shell-only: it reads this machine's records of its sessions. */
  trace: (kind: string, id: string) => ['trace', kind, id] as const,
  /**
   * How this Daoris is wired (WIRING1b) — shell-only: this machine's folders, variables and service. Keyed under `system`, the
   * family the design gives WIRING1e's presses' scope (§6); until that scope is declared, read again by its stale time and a focus.
   */
  systemWiring: ['system', 'wiring'] as const,
  /** The Downloads row's list (CACHECLEAR1) — shell-only: the versions under this install's `cache/`, read when the row opens it. */
  systemDownloads: ['system', 'downloads'] as const,
  /** The *Disk space* card (KEEP1c) — shell-only: each kind's size, rule, next and last clean, read again as each kind's walk ends. */
  systemSpace: ['system', 'space'] as const,
  /**
   * Git's reads (GIT1d-b, D147) — shell-only: they read this machine's checkouts. The branch list moves, so it is keyed by its
   * scope alone; every other read is named by whole commit ids (D147 §4.2), so a commit is cached by its commit and never changes.
   */
  gitBranches: (all: boolean, repository: string, workspace: string) => ['git', 'branches', all, repository, workspace] as const,
  allGitBranches: ['git', 'branches'] as const,
  // The checkout's current branch (NEW1e-a): it moves with a `git switch` anywhere, so it is never kept as settled.
  checkoutBranch: (repository: string, workspace: string) => ['git', 'checkoutBranch', repository, workspace] as const,
  // Each one-repository read carries its workspace: two workspaces may each hold a repository of one name (GIT1d-c).
  gitGraph: (repository: string, tips: readonly string[], page: number, sinceParted: boolean, workspace: string) =>
    ['git', 'graph', repository, tips.join(','), page, sinceParted, workspace] as const,
  gitCommit: (repository: string, commit: string, workspace: string) => ['git', 'commit', repository, commit, workspace] as const,
  gitHistory: (repository: string, commit: string, path: string, page: number, workspace: string) =>
    ['git', 'history', repository, commit, path, page, workspace] as const,
  gitBlame: (repository: string, commit: string, path: string, workspace: string) =>
    ['git', 'blame', repository, commit, path, workspace] as const,
  gitCompare: (repository: string, left: string, right: string, everything: boolean, workspace: string) =>
    ['git', 'compare', repository, left, right, everything, workspace] as const,
  // The Changes tab's read (GITAPP2b): the checkout's index and tree move with any act anywhere, so it is read again at every open.
  gitChanges: (repository: string, workspace: string) => ['git', 'changes', repository, workspace] as const,
  /** The files in one session's tree, for `@` (CONV4d) — shell-only for the same reason. */
  treeFiles: (session: string) => ['tree-files', session] as const,
  /** One file in a session's tree, read for its preview (PREVIEW1) — shell-only for the same reason. */
  treeFile: (session: string, path: string) => ['tree-file', session, path] as const,
  /** One conversation's model and effort as its agent offers them (AGT6b) — shell-only: it is a live process's. */
  sessionOptions: (session: string) => ['session-options', session] as const,
  /** What the person first said in each session, from this machine's record (RAIL1) — shell-only. */
  allOpenings: ['openings'] as const,
  openings: (ids: string[]) => ['openings', ...ids] as const,
  /** What each part of a line of work spent, from this machine's record (SESSCTX1d) — shell-only. */
  allSpent: ['spent'] as const,
  spent: (ids: string[]) => ['spent', ...ids] as const,
  /** What sessions said, searched on this machine's record (RAIL1) — shell-only. */
  sessionSearch: (q: string, session?: string) => ['session-search', q, session ?? ''] as const,
  entry: (id: string) => ['entry', id] as const,
  convergence: (minimumSimilarity: number, workspace: string | null) =>
    ['convergence', minimumSimilarity, workspace ?? '*'] as const,
  search: (q: string, localOnly: boolean, workspace: string | null) =>
    ['search', q, localOnly, workspace ?? '*'] as const,
  allQuests: ['quests'] as const,
  allSessions: ['sessions'] as const,
  /**
   * Where each of these sessions' work is now, its tree or its landing (LOOK2b) — shell-only, read off this machine's
   * files. Under the sessions' key, so whatever asks the listing again (a landing, a clean-up, a tick) asks this too.
   */
  sessionsWhere: (ids: string[]) => ['sessions', 'where', ...ids] as const,
  /** The record of the finished tree a session ran in (KEEPSIZE1), read once its tree is let go. */
  sessionRecord: (id: string) => ['sessions', 'record', id] as const,
  /**
   * What a word said now to one session would do (MSG1d, D137 §5.3) — shell-only, the driver's answer. By its state and
   * whether its inbox listens, so it is asked again as the session moves; under the sessions' key, so a word kept or a
   * tick asks it again too.
   */
  sessionReach: (id: string, state: string, listening: boolean) => ['sessions', 'reach', id, state, listening] as const,
  // What a word said now would do, as a shell that tells it live keeps it (MSG1f2): one entry per session, set by
  // `SESSION_QUEUED`, never under the sessions' key, so reading the list again does not ask it again.
  sessionReachTold: (id: string) => ['reach', id] as const,
  allAsks: ['asks'] as const,
  asks: (includeClosed: boolean, workspace: string | null) => ['asks', includeClosed, workspace ?? '*'] as const,
  quests: (repository: string | null, includeClosed: boolean, workspace: string | null) =>
    ['quests', repository ?? 'all', includeClosed, workspace ?? '*'] as const,
  /**
   * One quest's moves (FLOWCHART1a2), read from the service's door when its flowchart opens. Under the quests' key, so whatever
   * asks the quests again (a press, a tick, the change stream's `quests`) asks these again.
   */
  questMoves: (id: string) => ['quests', 'moves', id] as const,
  /**
   * The second opinions this machine holds (XAGENT1g4c): the service lists them, so not under the driver's key; under the
   * sessions', so whatever asks the sessions again (a new reviewer's record) asks these again.
   */
  opinions: ['sessions', 'opinions'] as const,
  sessions: (repository: string | null, includeClosed: boolean, workspace: string | null) =>
    ['sessions', repository ?? 'all', includeClosed, workspace ?? '*'] as const,
};
