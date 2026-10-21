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
