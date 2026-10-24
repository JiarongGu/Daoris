// The shape of how this Daoris is wired (WIRING1b, the this-Daoris design §6): `SYSTEM_WIRING`'s answer. Types only, here and
// not in `bridge/system.ts`, so the cards of Settings → This Daoris, which are drawn from props and reach no bridge
// (`presentational.test.ts`), can name them. The shape is a wire shape: a field is added, never renamed. A fact not read is
// `null` beside its `…Why` code.

/** A fact's why where it is `null`: `window` (only the window knows), `adopted`, `not-measured`, `measuring`, … */
type Why = string | null;

export interface WiringOld { entry: string; path: string; bytes: number | null; why: string }

export interface WiringFolder {
  id: 'app' | 'update' | 'data' | 'profiles' | 'work' | 'cache' | 'run' | 'logs';
  path: string | null; pathWhy: Why; present: boolean; bytes: number | null; bytesWhy: Why; measured: string | null;
  old: WiringOld[];
}

export interface WiringFile {
  id: string; folder: string; path: string | null; pathWhy: Why; present: boolean; bytes: number | null; bytesWhy: Why;
  written: string | null; reads: boolean | null; readsWhy: Why; overriddenBy: string | null; door: string | null; record: boolean;
}

export interface WiringVariable {
  name: string; set: boolean; value: string | null; valueWhy: Why; secret: boolean; prefix: string | null;
  from: 'this-start' | 'inherited' | 'set-by-daoris' | 'not-set' | 'unknown';
  account: string | null; accountSet: boolean | null; machine: string | null; machineSet: boolean | null;
  instead: string | null; readBy: string[];
}

export interface WiringLocated { path: string | null; pathWhy: Why; from: 'variable' | 'beside' | 'home' | 'workspace' | null }

/** A project folder Daoris's sessions left in the person's own tool home (OWNSPACE2): its name there, its size and newest file once measured. */
export interface WiringToolFolder {
  name: string; path: string; bytes: number | null; bytesWhy: Why; partial: boolean; newest: string | null; newestWhy: Why;
  /** Which kind of Daoris's working directory it was keyed by: `tree`, `help` or `intake`. */
  source: string;
  /** The session records that name it: why it is listed as Daoris's. */
  by: string[];
  /** What would keep it from any removal: `session` (live or waiting), `resumable` (a conversation that goes on in this home), `record-unread`; null otherwise. */
  keeps: string | null;
}

export interface SystemWiring {
  schema: number;
  reader: 'window' | 'terminal';
  measuring: boolean;
  measured: string | null;
  partial: boolean;
  partialWhy: Why;
  folder: {
    path: string | null; pathWhy: Why; kind: 'install' | 'development' | 'named' | null;
    account: string | null; accountWhy: Why; accountNames: 'root' | 'data' | 'none' | null; accountNamesWhy: Why;
    notice: string | null; noticeWhy: Why; hostNotice: string | null; hostNoticeWhy: Why;
  };
  folders: WiringFolder[];
  leftOver: { name: string; path: string; bytes: number | null; why: 'earlier-build' | 'not-daoris' }[];
  service: {
    url: string | null; urlWhy: Why; from: 'variable' | 'default' | 'address-file' | 'none';
    held: 'hosted' | 'adopted' | 'none' | null; heldWhy: Why;
    answers: boolean | null; answersWhy: Why; asked: string | null;
    tier: string | null; tierWhy: Why; version: string | null; versionWhy: Why; proved: boolean | null; provedWhy: Why;
    host: WiringLocated; connector: WiringLocated;
  };
  remotes: { workspace: string; url: string | null; urlWhy: Why; keySet: boolean; keyPrefix: string | null; from: 'file' | 'environment' }[];
  remotesWhy: Why;
  loop: {
    // `desktop`, `headless`, `verb`, `migration`, `swap` or `unknown` (HOMELEASE1); a verb says its own word in `what`.
    holder: { kind: string; instance?: boolean; what?: string | null; pid: number; since: string; thisWindow: boolean } | null;
    looked: string | null; lookedWhy: Why; ready: boolean | null; readyWhy: Why; draining: boolean | null; drainingWhy: Why;
    holds: number | null; holdsWhy: Why; pollSeconds: number | null; pollSecondsWhy: Why;
  };
  files: WiringFile[];
  variables: WiringVariable[];
  versions: Record<'program' | 'build' | 'kit' | 'cli' | 'driver' | 'service' | 'connector' | 'dotnet' | 'node', string | null>
    & Record<string, string | null>;
  terminal: {
    launchers: { name: string; present: boolean }[]; launchersWhy: Why;
    onAccountPath: boolean | null; onAccountPathWhy: Why; onMachinePath: boolean | null; onMachinePathWhy: Why;
    firstDaoris: { is: 'this' | 'another' | 'none'; path: string | null };
    firstDriver: { is: 'this' | 'another' | 'none'; path: string | null };
    address: { present: boolean; url: string | null; pid: number | null; since: string | null; taken: boolean | null; why: Why };
  };
  knowledge: { path: string | null; pathWhy: Why; from: 'variable' | 'home' | null; bytes: number | null; bytesWhy: Why; familyRoot: string | null; familyRootWhy: Why };
  logs: { path: string | null; pathWhy: Why; bytes: number | null; bytesWhy: Why; days: number; newest: string | null; newestWhy: Why; engineLog: boolean };
  // What Daoris's sessions left in the person's own tool home (OWNSPACE2, D175 point 3). Added after the first shape: a shell
  // older than it sends none, so the page reads it as absent.
  toolHome?: {
    path: string | null; pathWhy: Why; from: 'process' | 'account' | 'default' | 'named' | null; present: boolean;
    // Why nothing is listed where nothing can be (`linked`, `no-tool-home`, …), the file system's case rule, the folders left out
    // because their name is not proven Daoris's. A list only: Daoris removes nothing here (OWNSPACE2e).
    why: string | null; caseInsensitive: boolean; ambiguous: number; uncertain: number;
    folders: WiringToolFolder[];
  };
}

/**
 * One version an agent or a tool downloaded under the install's `cache/` (CACHECLEAR1): `SYSTEM_DOWNLOADS`' row. `keeps` is why it
 * stays (`pinned`, `used`, `named`, `unread`, `recent`, `running` since KEEP1b, and at a press `in-use`), absent or null where nothing
 * uses it; `by` says what keeps it (`machine`, `workspace:<name>`, `session:<id>`, or a file). `unfinished` is one an earlier clearing
 * moved aside and left part of. KEEP1b: for one nothing uses, `unusedSince` is when its current unused stretch began and `goes` when the
 * rule takes it; both absent where no pass has found it unused yet, and `goes` under `never`.
 */
export type DownloadedVersion = {
  kind: 'agent' | 'tool'; name: string; version: string; path: string; bytes: number; partial: boolean;
  keeps?: string | null; by?: string[]; unfinished: boolean; unusedSince?: string | null; goes?: string | null;
};

/**
 * `SYSTEM_DOWNLOADS`' answer: every downloaded version, and why none could be listed (`no-home`, `twice`); `days` is the rule's
 * (KEEP1b), how long into its unused stretch a version nothing uses is kept, absent from a shell before it and under `never`.
 */
export type DownloadsListing = { why?: string | null; days?: number | null; versions: DownloadedVersion[] };

/** One row a press named, and what it came to: `removed`, `kept` with why, `gone` or `unfinished`. */
export type DownloadCleared = {
  kind: 'agent' | 'tool'; name: string; version: string; unfinished: boolean;
  outcome: 'removed' | 'kept' | 'gone' | 'unfinished'; bytes?: number | null; keeps?: string | null; by?: string[] | null;
};

/**
 * One kind This Daoris's *Disk space* card shows (KEEP1c, the cleanup design §4.4): `SYSTEM_SPACE`'s row. Its size is its own last
 * walk's (`measured`, null where never counted; `partial` with why where the walk stopped short), its `rule` the one that runs
 * (`days`, `never`, `press`, `proof`, or another's `own` with `whose`), `next` when its rule next takes something (`bytes` null where
 * not counted), and `last` its last clean from `keeping.json`. KEEP1f: `volume` where its entry lies on another volume than the
 * Daoris folder's, with that volume's floor; `rule.low` where the floor frees it early. KEEP1d: `settable` where the person sets its rule,
 * with `bounds`, the days and the choices the driver takes (absent from a shell before it, and null for a kind not the person's).
 */
export type SpaceKind = {
  id: string;
  entries: string[];
  bytes: number | null; files: number | null; measured: string | null; partial: boolean; partialWhy: Why; counting: boolean;
  volume: SpaceVolume | null;
  rule: {
    kind: 'days' | 'never' | 'press' | 'proof' | 'own'; days: number | null; whole: boolean | null; default: boolean; whose: string | null;
    low?: boolean;
  };
  settable: boolean;
  bounds?: { min: number; max: number; whole: boolean; never: boolean } | null;
  next: { at: string; count: number; bytes: number | null } | null; nextWhy: Why;
  last: { at: string; door: string; removed: { count: number; bytes: number }; kept: Record<string, number> } | null; lastWhy: Why;
};

/**
 * One kind's keep rule as the *Disk space* card's *Change…* sets it (KEEP1d, `SET_KEEP`): its days, `never`, `whole` or not, or its
 * default; or the free-space floor (KEEP1f): a size in GB, a share in percent, `off`, or its default.
 */
export type KeepSet =
  | { kind: 'set-aside' | 'downloads' | 'logs' | 'history'; days?: number; never?: true; whole?: boolean; default?: true }
  | { kind: 'floor'; gb?: number; percent?: number; off?: true; default?: true };

/**
 * A volume as `SYSTEM_SPACE` says it (KEEP1f, the cleanup design §3.2): where it is, its free space and size, the floor it is held to
 * (`bytes` null where it is off; `from` the person's rule: `default`, `size`, `percent` or `off`), whether it is below it, and what the
 * floor freed early in this stretch below.
 */
export type SpaceVolume = {
  path: string; free: number; total: number;
  floor?: { bytes: number | null; from: 'default' | 'size' | 'percent' | 'off' } | null;
  below?: boolean;
  freedEarly?: { bytes: number; at: string } | null;
};

/**
 * A run of *Free up space…* (KEEP1e, the cleanup design §3.3): its id and door, when it started and ended, how it ended (`done` every
 * unit went, `kept` one was kept, `stopped` it ended before its last; null while it runs), and each kind waiting, running or done with
 * what it removed and kept by why.
 */
export type SpaceRun = {
  id: string; door: string; started: string; ended: string | null; outcome: 'done' | 'kept' | 'stopped' | null;
  kinds: { id: string; state: 'waiting' | 'running' | 'done'; removed: { count: number; bytes: number }; kept: Record<string, number> }[];
};

/**
 * `SYSTEM_SPACE`'s answer (KEEP1c): the Daoris folder's volume with its floor (KEEP1f), each kind in the card's order, the
 * press's `run` (KEEP1e: the current or last run in this window's process) and `held`, the run lock's holder, or one a crash left (`gone`).
 */
export type SystemSpace = {
  schema: number;
  volume: SpaceVolume | null;
  volumeWhy: Why;
  kinds: SpaceKind[];
  run: SpaceRun | null;
  held: { pid: number; since: string; door: string; gone: boolean } | null;
};

/**
 * One thing *Free up space…* lists (KEEP1e): its kind and id, which the press sends back; whether it goes early by the person's word;
 * `why` the rule takes it now (`past`, `slim`, `unfinished`) or `early`/`kept` for an early one; its size where known; when its rule
 * takes it; a set-aside folder's workspace and repository, and whether it holds files of its own (`own`).
 */
export type SpaceUnit = {
  kind: string; id: string; early: boolean; why: string | null; bytes: number | null; at: string | null;
  workspace: string | null; repository: string | null; own: boolean | null;
};

/** `SYSTEM_SPACE_FREE`'s list (KEEP1e): what each rule takes now, what could go early in groups, the kinds with presses of their own. */
export type SpacePlan = {
  schema: number;
  at: string;
  now: SpaceUnit[];
  early: {
    group: string; count: number; bytes: number | null; own: number; units: SpaceUnit[];
    /** KEEPSIZE1b: the set-aside folders by class; absent from a driver before it, and on every other group. */
    classes?: SetAsideClasses | null;
  }[];
  doors: string[];
  unread: Record<string, string>;
};

/** `SYSTEM_SPACE_FREE`'s answer to a press: the run's id (`joined` where one was under way), or `held` with its holder, nothing done. */
export type SpaceStarted = {
  run: string | null; joined: boolean; why?: 'held'; message?: string;
  held?: { pid: number; since: string; door: string; gone: boolean } | null;
};

/** One entry of an actor's effective order (D205 §4.0): `from` is what a start's record keeps as its choice. */
export type ActorEntry = { agent: string; model?: string | null; effort?: string | null; from: string };

/** One actor as `ACTORS` answers it (MODELROLE1c, D205 §9.4): set, as before, off, or an entry in driver.json that does not read. */
export type ActorRow = { id: string; state: 'set' | 'before' | 'off' | 'unreadable'; choices: ActorEntry[]; error?: string | null };

/**
 * The set-aside folders of the press's early group by class (KEEPSIZE1b, the keep-size design §5): recorded, its tip held by a ref or the
 * line; awaiting its record; its branch standing; stopped between its steps; what holds its commits not confirmed (`unheld`); and
 * unmarked, from before this build or not a finished tree. `stopped` and `unheld` are absent from a driver before them.
 */
export type SetAsideClasses = { recorded: number; awaiting: number; stands: number; stopped?: number; unheld?: number; unmarked: number };
