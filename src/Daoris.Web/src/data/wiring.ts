// The shape of how this Daoris is wired: `SYSTEM_WIRING`'s answer. Types only, here and
// not in `bridge/system.ts`, so the cards of Settings → This Daoris, which are drawn from props and reach no bridge
// (`presentational.test.ts`), can name them. The shape is a wire shape: a field is added, never renamed. A fact not read is
// `null` beside its `…Why` code.

import type { Authored } from '../locales/authored';

/** A fact's why where it is `null`: `window` (only the window knows), `adopted`, `not-measured`, `measuring`, … */
type Why = string | null;

/**
 * How a set-up press of This Daoris ended (`SYSTEM_OPEN_FOLDER`, `SYSTEM_PATH`, `SYSTEM_SERVICE_ADDRESS`): done, whether the
 * account's environment is different now, or refused as data with a code the catalogue words and a sentence for a log.
 */
export interface SetupAnswer { done: boolean; changed: boolean; why?: string | null; message?: string | null }

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

/** A project folder Daoris's sessions left in the person's own tool home: its name there, its size and newest file once measured. */
export interface WiringToolFolder {
  name: string; path: string; bytes: number | null; bytesWhy: Why; partial: boolean; newest: string | null; newestWhy: Why;
  /** Which kind of Daoris's working directory it is keyed by: `tree`, `help` or `intake`. */
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
    // `desktop`, `headless`, `verb`, `migration`, `swap` or `unknown`; a verb says its own word in `what`.
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
    /** The install's folder of launchers (*Put on my PATH* and the copied line name it); null in a development run. */
    bin?: string | null;
    onAccountPath: boolean | null; onAccountPathWhy: Why; onMachinePath: boolean | null; onMachinePathWhy: Why;
    firstDaoris: { is: 'this' | 'another' | 'none'; path: string | null };
    address: { present: boolean; url: string | null; pid: number | null; since: string | null; taken: boolean | null; why: Why };
  };
  knowledge: { path: string | null; pathWhy: Why; from: 'variable' | 'home' | null; bytes: number | null; bytesWhy: Why; familyRoot: string | null; familyRootWhy: Why };
  logs: { path: string | null; pathWhy: Why; bytes: number | null; bytesWhy: Why; days: number; newest: string | null; newestWhy: Why; engineLog: boolean };
  // What Daoris's sessions left in the person's own tool home. A shell
  // older than it sends none, so the page reads it as absent.
  toolHome?: {
    path: string | null; pathWhy: Why; from: 'process' | 'account' | 'default' | 'named' | null; present: boolean;
    // Why nothing is listed where nothing can be (`linked`, `no-tool-home`, …), the file system's case rule, the folders left out
    // because their name is not proven Daoris's. A list only: Daoris removes nothing here.
    why: string | null; caseInsensitive: boolean; ambiguous: number; uncertain: number;
    folders: WiringToolFolder[];
  };
}

/**
 * One version an agent or a tool downloaded under the install's `cache/`: `SYSTEM_DOWNLOADS`' row. `keeps` is why it
 * stays (`pinned`, `used`, `named`, `unread`, `recent`, `running`, and at a press `in-use`), absent or null where nothing
 * uses it; `by` says what keeps it (`machine`, `workspace:<name>`, `session:<id>`, or a file). `unfinished` is one an earlier clearing
 * moved aside and left part of. For one nothing uses, `unusedSince` is when its current unused stretch began and `goes` when the
 * rule takes it; both absent where no pass has found it unused yet, and `goes` under `never`.
 */
export type DownloadedVersion = {
  kind: 'agent' | 'tool'; name: string; version: string; path: string; bytes: number; partial: boolean;
  keeps?: string | null; by?: string[]; unfinished: boolean; unusedSince?: string | null; goes?: string | null;
};

/**
 * `SYSTEM_DOWNLOADS`' answer: every downloaded version, and why none could be listed (`no-home`, `twice`); `days` is the rule's,
 * how long into its unused stretch a version nothing uses is kept, absent from a shell without it and under `never`.
 */
export type DownloadsListing = { why?: string | null; days?: number | null; versions: DownloadedVersion[] };

/** One row a press named, and what it came to: `removed`, `kept` with why, `gone` or `unfinished`. */
export type DownloadCleared = {
  kind: 'agent' | 'tool'; name: string; version: string; unfinished: boolean;
  outcome: 'removed' | 'kept' | 'gone' | 'unfinished'; bytes?: number | null; keeps?: string | null; by?: string[] | null;
};

/**
 * One kind This Daoris's *Disk space* card shows: `SYSTEM_SPACE`'s row. Its size is its own last
 * walk's (`measured`, null where never counted; `partial` with why where the walk stopped short), its `rule` the one that runs
 * (`days`, `never`, `press`, `proof`, or another's `own` with `whose`), `next` when its rule next takes something (`bytes` null where
 * not counted), and `last` its last clean from `keeping.json`. `volume` is set where its entry lies on another volume than the
 * Daoris folder's, with that volume's floor; `rule.low` where the floor frees it early. `settable` is true where the person sets its rule,
 * with `bounds`, the days and the choices the driver takes (absent from a shell without it, and null for a kind not the person's).
 */
export type SpaceKind = {
  id: string;
  entries: string[];
  bytes: number | null; files: number | null; measured: string | null; partial: boolean; partialWhy: Why; counting: boolean;
  volume: SpaceVolume | null;
  rule: {
    kind: 'days' | 'never' | 'press' | 'proof' | 'own'; days: number | null; whole: boolean | null; default: boolean; whose: string | null;
    low?: boolean;
    /** The size limit in GB beside the days, on a kind that takes one; absent from a driver without it. */
    gb?: number | null;
  };
  settable: boolean;
  /** `gb`: the size limit's bounds, on a kind that takes one. */
  bounds?: { min: number; max: number; whole: boolean; never: boolean; gb?: { min: number; max: number } | null; sizeOnly?: boolean } | null;
  next: { at: string; count: number; bytes: number | null } | null; nextWhy: Why;
  /**
   * The set-aside folders no rule may take yet, awaiting their record or unread, or recorded where the registry names no checkout
   * of their repository (`unnamed`, absent from a driver without it); absent from a driver without the count.
   */
  waiting?: { awaiting: number; unread: number; unnamed?: number } | null;
  last: { at: string; door: string; removed: { count: number; bytes: number }; kept: Record<string, number> } | null; lastWhy: Why;
};

/**
 * One kind's keep rule as the *Disk space* card's *Change…* sets it (`SET_KEEP`): its days, `never`, `whole` or not, or its
 * default; or the free-space floor: a size in GB, a share in percent, `off`, or its default.
 */
export type KeepSet =
  | { kind: 'set-aside' | 'downloads' | 'browser-cache' | 'logs' | 'history'; days?: number; never?: true; whole?: boolean; gb?: number; default?: true }
  | { kind: 'floor'; gb?: number; percent?: number; off?: true; default?: true }
  | { kind: 'allowance'; gb?: number; default?: true };

/** The allowance over the whole: its GB, whether it is the default, and the kinds' counts summed (null until each is counted). */
export type SpaceAllowance = { gb: number | null; default: boolean; bytes: number | null; partial: boolean };

/**
 * A volume as `SYSTEM_SPACE` says it: where it is, its free space and size, the floor it is held to
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
 * A run of *Free up space…*: its id and door, when it started and ended, how it ended (`done` every
 * unit went, `kept` one is kept, `stopped` it ended before its last; null while it runs), and each kind waiting, running or done with
 * what it removed and kept by why.
 */
export type SpaceRun = {
  id: string; door: string; started: string; ended: string | null; outcome: 'done' | 'kept' | 'stopped' | null;
  kinds: { id: string; state: 'waiting' | 'running' | 'done'; removed: { count: number; bytes: number }; kept: Record<string, number> }[];
};

/**
 * `SYSTEM_SPACE`'s answer: the Daoris folder's volume with its floor, each kind in the card's order, the
 * press's `run` (the current or last run in this window's process) and `held`, the run lock's holder, or one a crash left (`gone`).
 */
export type SystemSpace = {
  schema: number;
  volume: SpaceVolume | null;
  volumeWhy: Why;
  kinds: SpaceKind[];
  /** Absent where the shell names no allowance. */
  allowance?: SpaceAllowance;
  run: SpaceRun | null;
  held: { pid: number; since: string; door: string; gone: boolean } | null;
};

/**
 * One thing *Free up space…* lists: its kind and id, which the press sends back; whether it goes early by the person's word;
 * `why` the rule takes it now (`past`, `slim`, `unfinished`) or `early`/`kept` for an early one; its size where known; when its rule
 * takes it; a set-aside folder's workspace and repository, and whether it holds files of its own (`own`).
 */
export type SpaceUnit = {
  kind: string; id: string; early: boolean; why: string | null; bytes: number | null; at: string | null;
  workspace: string | null; repository: string | null; own: boolean | null;
  /** A set-aside folder's listed class and the ref its tip is on; the press sends both back, and the run keeps one changed since. */
  class?: string | null; ref?: string | null;
};

/** `SYSTEM_SPACE_FREE`'s list: what each rule takes now, what could go early in groups, the kinds with presses of their own. */
export type SpacePlan = {
  schema: number;
  at: string;
  now: SpaceUnit[];
  early: {
    group: string; count: number; bytes: number | null; own: number; units: SpaceUnit[];
    /** The set-aside folders by class; absent from a driver without it, and on every other group. */
    classes?: SetAsideClasses | null;
  }[];
  doors: string[];
  unread: Record<string, string>;
};

/** `SYSTEM_SPACE_FREE`'s answer to a press: the run's id (`joined` where one is under way), or `held` with its holder, nothing done. */
export type SpaceStarted = {
  run: string | null; joined: boolean; why?: 'held'; message?: string;
  held?: { pid: number; since: string; door: string; gone: boolean } | null;
};

/** One entry of an actor's effective order: `from` is what a start's record keeps as its choice. */
export type ActorEntry = { agent: string; model?: string | null; effort?: string | null; from: string };

/** A seeded role's order as it runs, as each `ROLES` row carries it (`staffing`): set, as before, off, or an entry that does not read. */
export type ActorRow = {
  id: string; state: 'set' | 'before' | 'off' | 'unreadable'; choices: ActorEntry[]; error?: string | null;
  /** The order a start would walk where the role's policy arranges it otherwise than as stored (Build, Chat); the offer's order. */
  arranged?: ActorEntry[] | null;
};

/** A role's choice as the catalogue stores it: an agent, its model and effort in the tool's own words, absent for account default. */
export type RoleChoice = { agent: string; model?: string | null; effort?: string | null };

/**
 * One role of the catalogue as `ROLES` answers it: the seed's, one the person set, one
 * removed or one whose record does not read; its names as authored, the work it takes, where, and its stored choices.
 */
export type RoleRow = {
  id: string; state: 'seed' | 'set' | 'removed' | 'unreadable'; seeded: boolean; name?: Authored | null;
  takes: string[]; scopeKind?: string | null; scopeId?: string | null; revision: number; error?: string | null; choices?: RoleChoice[] | null;
  /** A seeded role's order as it runs; absent for an added role. */
  staffing?: ActorRow | null;
  /** An appointed role's kind, acts, holder and window; absent for a working role. */
  hold?: RoleHoldRead | null;
};

/** What an appointed role holds: kind, acts, holder (none is not staffed), a supervisor's window and its kinds (none is generic). */
export type RoleHoldRead = { kind: 'supervisor' | 'steward'; acts: string[]; holder?: 'rules' | 'terminal' | null; window?: number; decides?: string[] };

/** A role Daoris suggests adding: the Designer while no role takes design, applied only by the person's press. */

/**
 * One role's last seven days as `ROLES` answers it: starts, those on each choice (`1` to `4`, `before`, `rule-default`), how many
 * fell to a later choice and the walk's codes that moved them. A role with no start is not listed.
 */
export type RoleUse = {
  role: string; starts: number; choices: { choice: string; starts: number }[]; fell: number; why: { code: string; starts: number }[];
  /** The lines the course moved to another choice, by the condition that moved them; a driver without it leaves it out. */
  moved?: { condition: string; moves: number }[];
};

/** The use over `days` of the machine log (at most seven; none with no log); a driver without it leaves the field out. */
export type RoleUseRead = { days: number; roles: RoleUse[] };

/** A choice's entry in a role's policy: keyed by its tuple, with its weight, share, daily budget (starts or reported tokens) and works. */
export type StaffPolicyChoice = {
  agent: string; model?: string | null; effort?: string | null; weight?: number; share?: number | null;
  budget?: { starts?: number; tokens?: number } | null; takes?: string[] | null;
};

/** A role's policy record as `ROLES` answers and `SET_ROLE` takes it (`policy`): the rule and the choices; moves are the supervisor's rules. */
export type StaffPolicy = { rule: 'order' | 'weight'; choices: StaffPolicyChoice[] };

/** A role's next new start as its policy would take it: a probe that reserves nothing; null for a brainstorm. */
export type RoleNext = { choice: string; by: string; said: string; agent: string; model?: string | null; effort?: string | null };

/** One role's policy as `ROLES` answers it: `default` (no row), `set`, or `stale` (set for other choices; the role runs in order). */
export type RolePolicyRow = {
  role: string; policyState: 'default' | 'set' | 'stale'; revision: number; why?: string | null; staffPolicy?: StaffPolicy | null; next?: RoleNext | null;
};

/** The catalogue as `ROLES` answers it: its roles, the work a role may take, whether this home keeps it, and what is suggested. */
export type RoleCatalogue = {
  roles: RoleRow[]; works: string[]; kept: boolean; use?: RoleUseRead;
  /** Each role's policy, the fields this build enforces and the presets the doors take; absent from a driver without them. */
  policies?: RolePolicyRow[]; policyFields?: string[]; policyPresets?: string[];
  /** The choices Daoris suggests for the seeded roles and the day of the reference they are read from; absent from a driver without them. */
  suggestedChoices?: { role: string; choices: RoleChoice[] }[]; suggestedChoicesDated?: string;
  /** The supervisor's rules: every place's set and what each ruled count takes; absent from a driver without them. */
  supervisor?: SupervisorRulesRead;
};

/** One supervisor rule as `ROLES` answers it and a press carries it: when `count` reaches `at`, `do`; `upTo` minutes for `wait`, `roles` none for every role. */
export type SupervisorRuleWire = {
  id?: string; count: string; window?: string; at: number; do: string; upTo?: number | null; place?: string; roles?: string[] | null;
};

/** A place's rules written whole, with the revision they were read at (0 for a place that holds none). */
export type SupervisorRuleSetRead = { place: string; revision: number; rules: SupervisorRuleWire[] };

/** What a ruled count takes at the door: its window, its actions and its bounds. */
export type SupervisorCountRead = { count: string; window: string; actions: string[]; least: number; most: number };

/**
 * The supervisor's rules as `ROLES` answers them; `wider` is each place's wider places before this machine as the driver's judge reads them
 * (a repository's workspace), absent from a driver without them.
 */
export type SupervisorRulesRead = { sets: SupervisorRuleSetRead[]; counts: SupervisorCountRead[]; wider?: Record<string, string[]> };

/** A role's change as `SET_ROLE` takes it: set with its record and choices, removed, given back its seed, or every seeded role given back (`default-all`, whose id is `*`). */
export type RoleChangePress = {
  change: 'set' | 'remove' | 'default' | 'default-all'; id: string; revision?: number | null;
  role?: {
    name: Authored; takes: string[]; scope: { kind: string; id?: string | null };
    /** An appointed role's record: `filled`, its acts (`authority`), its holder (null is none) and the supervisor's window. */
    filled?: 'appointed'; authority?: string[]; holder?: string | null; window?: number; decides?: string[];
  };
  choices?: RoleChoice[] | null;
  /** A preset's name, a record, or null to clear; absent leaves the policy as it is. Only `SET_ROLE`/`SET_ROLES` write it. */
  policy?: string | StaffPolicy | null;
  /** The policy revision the person read: a save against a moved one is refused, nothing written. */
  policyRevision?: number | null;
  /** The supervisor's rules at one place, written whole against the revision read (id `supervisor`); no other field of a role's press rides with them. */
  place?: string; rules?: SupervisorRuleWire[]; rulesRevision?: number;
};

/**
 * The set-aside folders of the press's early group by class: recorded, its tip held by a ref or the
 * line; awaiting its record; its branch standing; stopped between its steps; what holds its commits not confirmed (`unheld`); and
 * unmarked, from an earlier build or not a finished tree; and `unread`, whose manifest does not read. `stopped`, `unheld` and `unread` are
 * absent from a driver without them.
 */
export type SetAsideClasses = {
  recorded: number; awaiting: number; stands: number; stopped?: number; unheld?: number; unread?: number; unmarked: number;
};

/** A place the roles' structure is asked for: a workspace, a repository in it, or neither for this machine. */
export type StructurePlaceAsk = { workspace?: string; repository?: string };

/** What a choice's last readings say: one that may run by the account it would run on, else the start's pass code. */
export type ChoiceStateCode = 'ready' | 'unknown' | 'not-read' | 'unavailable' | 'not-offered' | 'cooling' | 'signed-out' | 'held';

/** One choice of a work's order at a place, and its state with the reading's time it rests on. */
export type StructureChoice = {
  agent: string;
  model?: string;
  effort?: string;
  /** `1` to `4`, `before`, or `rule-default` for a rule's agent no choice names. */
  from: string;
  state: { code: ChoiceStateCode | string; at?: string; account?: string; until?: string };
};

/** One work at one place: its role or why its starts hold, its choices, its rule and where it is set, each part's failure. */
export type StructureWork = {
  work: string;
  role?: { id: string; name: Authored; scope: { kind: string; id?: string }; revision: number };
  held?: string;
  choices: StructureChoice[];
  choicesFailed?: string;
  policy?: {
    consent: string;
    agents: string[];
    concurrency?: number;
    minutes?: number;
    /** `repository`, `workspace`, `machine` or `default`. */
    source: string;
    requirement?: { kind: string } & Record<string, unknown>;
  };
  policyFailed?: string;
};

/** Where a place's effective value is set: *set here*, *from the workspace*, *from this machine* or *default*. */
export type PlaceSourceCode = 'here' | 'workspace' | 'machine' | 'default';

export type PlaceValue = { value?: string | null; source: PlaceSourceCode };

/** Why a place needs a look, judged in the driver from the configuration alone. */
export type PlaceNeed = { code: string; detail?: string | null };

/** A role's scope: where it sits (`machine`, `workspace` or `repository`) and the workspace or repository named, as its record holds it. */
export type PlaceRoleScope = { kind: 'machine' | 'workspace' | 'repository'; id?: string | null };

/**
 * One work at a place: the role a start finds, with its names and scope, and the rule's consent. `source` is the rule's, never
 * the role's.
 */
export type PlaceWork = {
  work: string;
  role?: string | null;
  held?: string | null;
  consent?: string | null;
  source: PlaceSourceCode;
  policyFailed?: string | null;
  roleName?: Authored | null;
  roleScope?: PlaceRoleScope | null;
};

/** The shipped default a place's workflow follows, by its id, and each option the place says otherwise. */
export type PlaceDefault = { default?: string | null; differs: string[] };

/** How a place's work lands: the delivery preset its land matches, and each option of its own; null where the version is not read. */
export type PlaceLands = { preset?: string | null; own: string[] };

/** A repository's set-up: `adopted`, `running` (its set-up quest is open or taken) or `none`. */
export type PlaceAdoption = 'none' | 'running' | 'adopted';

/** A place as `PLACES` answers it: this machine (no workspace), a workspace (no repository) or a driven repository. */
export type PlaceRow = {
  workspace?: string | null;
  repository?: string | null;
  driven: boolean;
  held: boolean;
  trees: boolean;
  checkout: boolean;
  adopted: boolean;
  /** The head's line; none for this machine. */
  values?: (Record<'line' | 'landing' | 'workflow' | 'landed' | 'commits' | 'opinion' | 'handOver' | 'language' | 'access', PlaceValue> & {
    follows?: PlaceDefault | null;
    /** How the followed workflow lands. */
    lands?: PlaceLands | null;
    /** Whether a review environment holds its work for your look: `on` or `off`; none set reads null. */
    look?: PlaceValue;
    /** What a helper its agent asks for does: `ask`, `on` or `off`. */
    helpers?: PlaceValue;
    /** The standing answer's words, a repository's own. */
    answer?: PlaceValue;
  }) | null;
  works: PlaceWork[];
  needs: PlaceNeed[];
  /** A repository's set-up; absent for this machine and a workspace, and from a driver without it. */
  adoption?: PlaceAdoption | null;
};

/** A repository of a workspace, for its chooser: all of them, not the page in hand. */
export type PlaceMember = { repository: string; driven: boolean; checkout: boolean; adoption: PlaceAdoption };

export type PlacesAsk = {
  limit?: number;
  after?: string;
  search?: string;
  needsLook?: boolean;
  fold?: string;
  foldAfter?: string;
  foldSearch?: string;
  /** One place whole, driven or not: a repository of a workspace, or the workspace when no repository is named. */
  detail?: { workspace?: string; repository?: string };
  /** A workspace's whole member list. */
  members?: string;
};

/**
 * The places: driven ones a page at a time, with a not-driven one whose adoption runs among them,
 * the others as counts and a page of one fold. `running` of a workspace's `notDriven` stand among the driven.
 */
export type PlacesRead = {
  machine: PlaceRow;
  workspaces: { place: PlaceRow; driven: number; notDriven: number; running?: number }[];
  repositories: PlaceRow[];
  next?: string | null;
  matching: number;
  undriven?: { workspace: string; rows: { workspace: string; repository: string; checkout: boolean }[]; next?: string | null; total: number } | null;
  /** The place asked for whole; null where none is asked or it is not known. */
  detail?: PlaceRow | null;
  members?: { workspace: string; rows: PlaceMember[] } | null;
};

/** The roles' structure as `ROLE_STRUCTURE` answers it. */
export type RoleStructure = {
  works: string[];
  roles: RoleCatalogue['roles'];
  /** Where the store's roles do not read: every work then holds, said. */
  catalogue?: { why: string };
  places: { workspace?: string; repository?: string; works: StructureWork[] }[];
  /** Each agent a choice names; `installed` absent where its binary is not asked yet. */
  agents: { agent: string; known: boolean; installed?: boolean; version?: string }[];
};
