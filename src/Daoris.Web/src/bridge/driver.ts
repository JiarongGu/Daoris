import { useCallback } from 'react';
import { type QueryClient, type UseMutationResult, useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import type { SyncStanding } from '../data/api';
import type {
  PlaceRow, PlacesAsk, PlacesRead, RoleCatalogue, RoleStructure, StructurePlaceAsk,
} from '../data/wiring';
import { keys, SYNC_SLICE } from '../data/queries';
import type { AccessLevel } from '../settings/repository/Access';
import type { GitAccessLevel, ResolvedGitClass } from '../settings/repository/GitAccess';
import type { CommitRule } from '../settings/repository/Commits';
import type { HeavyGate, HeavyMarks } from '../settings/repository/HeavyGates';
import type { HelperRule } from '../settings/repository/Helpers';
import type { HandOverRule } from '../settings/repository/HandOver';
import type { LanguageOption } from '../settings/repository/Languages';
import type { OpinionRule } from '../settings/repository/Opinions';
import type { ReviewLook, ReviewRule } from '../settings/repository/Reviews';
import type { ReviewShown } from '../work/gates/ReviewChip';
import { type HeavySlot, readHeavySlot } from '../work/heavySlot';
import { call, type Payload, refusedNotReady, type TypedRoute } from './call';
import { notPushed, readPushed, type SliceSpec } from './state/slices';
import { askHeld } from './state/held';

// The driver's standing state and its dials: what this machine drives and holds, what it says
// when a session parks, how many failures park a quest, which agent answers an ask, and a look now.

/**
 * The driver's standing state, as the page reads it. The DAORIS.DRIVER module answers more (its
 * config path, cap, adapter, poll interval); the fields land here when a surface reads them.
 */
export type DriverState = {
  /**
   * Whether the driver's service is up: until it is, every route that reads it refuses *still coming up*.
   * Left out where the shell does not say, which is read as up.
   */
  ready?: boolean;
  /** What *Try again* did, on the answer to `RETRY_QUEST` alone; absent on every other answer. */
  retried?: Retried | null;
  drivable: string[];
  holds: string[];
  /** Repositories whose workflow sets their sessions apart, each in a worktree of its own (its `isolate` step). */
  trees: string[];
  /**
   * Repositories whose checkout the person let Daoris change the files of: off where absent.
   * Left out where none is set: the pageoffers no switch.
   */
  checkoutWrites?: string[];
  /** Session ids with a live process right now — what "stop" can actually reach. */
  running: string[];
  /**
   * Who is driving Daoris's browser: the running sessions the driver handed a server that drives
   * it, from the handing until each ends. Left out where none is set: the pagesays nothing.
   */
  drivingBrowser?: string[];
  /**
   * The set-ups waiting for the person's review here, each with whether Daoris serves its tab now: the strip's
   * chip beside the browser's door. Left out where none is set, and where the loop carries no browser, which shows nothing.
   */
  inReview?: ReviewShown[];
  /** What *Show it again* said, on the answer to `SHOW_REVIEW_AGAIN` alone; absent on every other answer. */
  shownAgain?: { quest: string; message: string } | null;
  /** Whether this machine interrupts the person when a session parks or ends unasked. */
  notify: boolean;
  /** How many failed sessions park a quest; `0` never parks. */
  strikes: number;
  /**
   * How long an account cools when its agent names no time, in minutes: the file's, or the hour.
   * Left out where none is set, and the setting is then not offered.
   */
  coolOff?: number;
  /** How long a session may say nothing before Daoris looks in, in minutes: the file's, or the default; left out where none is set. */
  quiet?: number;
  /** The most files, and the most minutes, a folder's manifest read spends: the file's, or the defaults. */
  manifestFiles?: number;
  manifestMinutes?: number;
  /** The most megabytes a folder may hold to be copied for a session its workflow sets apart. */
  manifestCopy?: number;
  cap?: number;
  /** The tier of a press from a terminal no Daoris session owns: `ask` or `act`; left out where none is set, and the row is then not offered. */
  terminal?: string;
  /** Who changed that tier and what a terminal pressed under act, newest first, read by `terminalActsOf`; absent from an older shell. */
  terminalActs?: unknown;
  /** Quests the person restarted, by id, and the failure count each restarts at. */
  forgiven: Record<string, number>;
  /**
   * The agent an ask the declarations leave opens an intake session on, or "" — asks are
   * answered by declarations only. Never null: the bridge leaves a null out, and absent is how
   * none reads.
   */
  intakeAgent?: string;
  /** The agent Ask Daoris runs on, or "" for none; left out where none is set. */
  helperAgent?: string;
  /**
   * Ask Daoris's access mode as the person chose it: `restricted` or `full`, what each conversation's
   * next turn takes; a value the page does not know reads as restricted (`helpModeOf`). Left out where none is set:
   * the page offers no choice. A conversation's own mode, the one its start got, is its history row's `mode`.
   */
  helpMode?: string;
  /**
   * Restricted mode's configured access as this machine chose it: each grant the driver serves, by its id, with the
   * operations chosen and those it acts on (`built`); the families of acts' (`acts`), and the checkouts'. What each start of Ask
   * Daoris and every press of its acts read; `pending` while a conversation running here is handed another choice, which its next
   * turn takes. And the browser's, read only from its plugin's approval: no `built`, `changed` since approval, and
   * the `plugin` whose tab changes it, absent where none here offers it. Left out where none is set: the pagedraws no configured
   * access.
   */
  helpGrants?: { id: string; chosen: string[]; built?: string[]; pending?: boolean; changed?: boolean; plugin?: string | null }[];
  /**
   * The Ask Daoris conversations running under another access than the one chosen: each ends at its
   * turn's end, and its next words resume it under the access chosen, so the panel keeps it in view as it ends. Absent on a shell
   * that ends none so.
   */
  helpSwapping?: string[];
  /** The Daoris home: the directory every machine-local file lives in. Left out where none is set. */
  home?: string;
  /** The lines as set, by repository — left out where none is set. */
  lines?: { repository: string; branch: string }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceLines?: { workspace: string; branch: string }[];
  /** Reading across as set, by repository — left out where none is set. */
  readAcross?: { repository: string; read: boolean }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceReadAcross?: { workspace: string; read: boolean }[];
  /** The declared relationships: what each repository's sessions may also write into. */
  writeAcross?: { repository: string; to: string[] }[];
  /**
   * Each repository's standing answer: the person's words, handed to every session there, and when they
   * set them, or null where the file does not say — left out where none is set.
   */
  standing?: { repository: string; says: string; at?: string | null }[];
  /**
   * The session languages as set: what each repository's sessions are asked to write to the person in,
   * a code of the driver's table — left out where none is set.
   */
  languages?: { repository: string; language: string }[];
  /** And by workspace, for each repository there that sets none of its own, and its intake. */
  workspaceLanguages?: { workspace: string; language: string }[];
  /**
   * The driver's closed table each option is named from: its code and the name a session's line gives it, so adding a
   * language is a row there and needs no catalogue here. Left out where none is set: the pageoffers no session language.
   */
  languageTable?: LanguageOption[];
  /**
   * The review rules as set: where each repository's work is shown to the person before it lands,
   * or none here — left out where none is set: the pageoffers no review row.
   */
  reviews?: { repository: string; rule: ReviewRule }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceReviews?: { workspace: string; rule: ReviewRule }[];
  /** What a review's procedure look found, on the answer to `SET_REVIEW`'s put alone; absent on every other answer. */
  reviewed?: ReviewLook | null;
  /**
   * The second-opinion rules as set: which other agent reads each repository's work before it lands,
   * or none here — left out where none is set: the pageoffers no second-opinion row.
   */
  opinions?: { repository: string; rule: OpinionRule }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceOpinions?: { workspace: string; rule: OpinionRule }[];
  /**
   * The commit rules as set: how each repository's work is committed — absent on a shell
   * without them, which offers no commit row.
   */
  commits?: { repository: string; rule: CommitRule }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceCommits?: { workspace: string; rule: CommitRule }[];
  /**
   * The helpers rules as set: what a helper each repository's agent asks for does — absent on a shell
   * without them, which offers no helpers row.
   */
  helpers?: { repository: string; rule: HelperRule }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceHelpers?: { workspace: string; rule: HelperRule }[];
  /**
   * The hand-over rules as set: what an agent's ask to hand its session over does in each repository —
   * left out where none is set: the pageoffers no hand-over row.
   */
  handOver?: { repository: string; rule: HandOverRule }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceHandOver?: { workspace: string; rule: HandOverRule }[];
  /**
   * The access setups as set: what each repository's sessions start under, full access or the pauses it
   * names — left out where none is set: the pageoffers no access section. What each repository resolves to is `LINES`' `access`.
   */
  access?: { repository: string; setting: AccessLevel }[];
  /** And by workspace, for each pause a repository there does not name. */
  workspaceAccess?: { workspace: string; setting: AccessLevel }[];
  /** And this machine's, for each pause neither a repository nor its workspace names: no pause and not full where none is set. */
  machineAccess?: AccessLevel;
  /**
   * Git access as set: the classes each repository, workspace and this machine sets, none
   * being Daoris's default — left out where none is set. What each repository resolves to is `LINES`' `gitAccess`.
   */
  gitAccess?: { repository: string; classes: GitAccessLevel }[];
  workspaceGitAccess?: { workspace: string; classes: GitAccessLevel }[];
  machineGitAccess?: GitAccessLevel;
  /**
   * What each running session holds of git access now: its repository's, narrowed by what the person tightened
   * (a class whose source is `session`) — left out where none is set.
   */
  sessionGitAccess?: { session: string; repository?: string | null; workspace?: string | null; classes: ResolvedGitClass[] }[];
  /**
   * This machine's heavy gates: for each repository whose own `daoris.gates.json` marks no
   * heavy gate, the list `run-heavy` runs for it in its order — left out where none is set: the pageoffers no heavy gates section.
   */
  heavyGates?: { repository: string; gates: HeavyGate[] }[];
  /**
   * Which checks each repository's sessions run under Current: `light` or `heavy-at-end` by
   * repository, none being today's — left out where none is set: the pageoffers no sessions' checks row.
   */
  sessionChecks?: { repository: string; checks: string }[];
  /**
   * The person's standing confirmation, as the shell's `StandingNow` says it: read
   * only through `standingOf` (`bridge/person.ts`), since the bridge leaves its nulls out. Left out where none is set and where
   * the shell serves no pipe, which draws no row.
   */
  standingConfirmation?: unknown;
};

/**
 * A pushed slice's snapshot, as last judged and read from memory: `{ slice, epoch, status, partitions }`,
 * or nothing for a slice this shell does not push. Every slice's spec reads it here, so the route has one bridge file. A read: it
 * declares nothing and tells nothing.
 */
export const readSlice = (slice: string) => call('STATE_SLICE', { slice });

/** The partitions the shell pushes the driver's state in (`DriverStateSlice`), composed in this order. */
const DRIVER_PARTITIONS = ['shell', 'choices', 'running'] as const;

/**
 * The driver's state as the shell pushes it: its standing choices and holds (`choices`), what runs and waits for
 * review (`running`) and the shell's own standing (`shell`), each pushed whole when it moves, composed into the one entry the
 * route answers (`STATE`), whose fields the shell's allow-list picks.
 */
export const driverSlice: SliceSpec = {
  name: 'driver',
  key: keys.driver,
  read: () => readSlice('driver'),
  compose: (partitions) => {
    const parts = DRIVER_PARTITIONS.map((name) => partitions.get(name));
    if (!parts.every((part) => typeof part === 'object' && part !== null && !Array.isArray(part))) return undefined;
    return Object.assign({}, ...parts) as DriverState;
  },
};

/**
 * Where each wired circle stands as the shell pushes it: a partition per circle, named as the remotes map names it,
 * each the record `/api/sync` answers for it, pushed whole when a pass, a quest's write or *Sync now* moved it. Composed into one entry
 * by circle, which `useSyncStanding` reads with `standingOf`; a circle with no partition has no remote here. Nothing composed until the
 * owner has judged every wired circle, so a window never reads a wired circle as unwired.
 */
export const syncSlice: SliceSpec = {
  name: SYNC_SLICE,
  key: keys.syncEvery,
  read: () => readSlice(SYNC_SLICE),
  compose: (partitions, judged) => {
    if (!judged) return undefined;
    const standings: Record<string, SyncStanding> = {};
    for (const [circle, part] of partitions) {
      if (typeof part === 'object' && part !== null && !Array.isArray(part)) standings[circle] = part as SyncStanding;
    }
    return standings;
  },
};

export const useDriver = () => {
  // The same detection path ShellSignals uses — one answer to "is a shell here", not two that can
  // drift. (The kit reads the bridge per render; in the desktop the bridge exists before the page.)
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  // Pushed: read from memory, written by the pushes, so neither a focus nor going stale asks the shell again.
  return useQuery({
    queryKey: keys.driver,
    queryFn: () => readPushed<DriverState>(client, driverSlice.name),
    enabled: isAvailable,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
};

/**
 * Whether this window is a shell's: its driver answered, or the bridge is here and its driver's read is still on its way.
 * A shell is known before its driver answers, so until the driver's slice lands its window keeps the shell's places and waits,
 * never drawing a browser's frame and then swapping it. A driver whose read failed reads as no driver.
 */
export const useAttached = (): boolean => {
  const { isAvailable } = useShenora();
  const driver = useDriver();
  return driver.data !== undefined || (isAvailable && !driver.isError);
};

/**
 * Ask again what the driver could not answer before its service is up: its own answers, all keyed under the
 * driver (its state, which now says ready, the lines, the clean-up's list, which repositories a look takes), and every
 * other query it refused as not ready, such as a session's review or preview. A query refused for a reason of its own
 * keeps its answer. The state itself where it is pushed is not asked: its push says it is ready.
 */
export const askAgainWhenReady = (client: QueryClient) => {
  void client.invalidateQueries({ queryKey: keys.driver, predicate: notPushed(client) });
  void client.invalidateQueries({ predicate: (query) => refusedNotReady(query.state.error) });
};

/**
 * Hear the driver say its service is up (`DRIVER_READY`), and ask again then what it refused while it came up.
 *
 * @remarks
 * A screen opened as the shell starts asks at once and is refused *still coming up*. Were only a tick to ask
 * again, the first tick could wait on a remote's sync or on another driver's lock, so the workspace's page would say no
 * repository had a line until something else happened to ask. Said once by the shell, the moment the refusals stop.
 */
export const useDriverReady = () => {
  const client = useQueryClient();
  useShenoraEvent('DAORIS', 'DRIVER_READY', () => askAgainWhenReady(client));
};

/**
 * What is under way as the driver holds it: its presses, whole and numbered as its last
 * `UNDER_WAY` telling, which the state hub reads as it mounts. A read: it declares nothing and tells nothing.
 */
export const readUnderWay = () => call<{ seq?: unknown; presses?: unknown } | null>('UNDER_WAY');

export { CONSIDERATIONS_SLICE, considerationsSlice, useConsidered, useUntrusted, useTrustFolder } from './driverConsiderations';
export type { Considerations, TrustGranted } from './driverConsiderations';

/**
 * One mutation shape for the standing choices, these and the rules' (`bridge/repositories/lines.ts`): edit the file, and the loop
 * looks now. The state it moved reaches every window by its push, the one writer of the driver's entry.
 */
export function useDriverChange<R extends TypedRoute>(type: R): UseMutationResult<DriverState, Error, Payload<R>> {
  return useMutation({
    // A standing choice answers the driver's state.
    mutationFn: (variables: Payload<R>) => call(type, variables) as Promise<DriverState>,
  });
}

/**
 * Whether this machine says so when a session parks or ends unasked: the field `daoris settings notify on|off` edits. It governs
 * the judgement rather than the toast, which is why a headless machine honours it too.
 */
export const useSetNotify = () => useDriverChange('SET_NOTIFY');

/**
 * How long an account cools when its agent names no time: whole minutes, at least one, the same
 * `cooloff` `daoris settings cooloff <minutes>` edits; less than one is the driver's refusal.
 */
export const useSetCoolOff = () => useDriverChange('SET_COOLOFF');

/** How long a session may be quiet before Daoris looks in: the same `quiet` `daoris settings quiet <minutes>` edits. */
export const useSetQuiet = () => useDriverChange('SET_QUIET');

/** One bound of a folder's manifest read, files or minutes: the same `manifest` `daoris settings manifest files|minutes <n>` edits. */
export const useSetManifest = () => useDriverChange('SET_MANIFEST');

/** How many sessions Daoris starts at once: the same `cap` `daoris settings cap <n>` edits; less than one is refused. */
export const useSetCap = () => useDriverChange('SET_CAP');

/** Whether a terminal's press waits for the Confirm or runs at once as the person's: the same `terminal` `daoris settings terminal ask|act` edits. */
export const useSetTerminal = () => useDriverChange('SET_TERMINAL');

/**
 * One kind's keep rule: the same `keep` `daoris settings keep` edits. A value out of its
 * kind's bounds is the driver's `KEEP_OUT_OF_RANGE`, said under the row's editor. It declares the wiring with the state, so the card
 * reads its rules again.
 */
export const useSetKeep = () => useDriverChange('SET_KEEP');

/**
 * *Try again*: let a parked quest run again, counting from where it stands, or release one
 * the person's stop holds — whichever the driver's verdict says applies, refused where neither does. `daoris quest
 * retry <quest>` and its `--session` are the terminal's two halves.
 */
export const useRetryQuest = () => useDriverChange('RETRY_QUEST');

/**
 * *Show it again* on the strip's review chip: the set-up step's newest set-up
 * served to its tab of Daoris's browser again, the tab brought forward where the set-up leaves it, with no session and no model.
 * Refused in the driver's words wherever it would show anything but that build at the rule's address.
 */
export const useShowReviewAgain = () => useDriverChange('SHOW_REVIEW_AGAIN');

/** What *Try again* did: the mark on a parked quest, or the release of the stop that held it. */
export type Retried = { quest: string; did: 'marked' | 'released'; session?: string | null };

/**
 * The notice for a *Try again*, from the driver's answer, as a catalogue key and its values: a release names the stop it
 * released, a mark how many more failures park the quest again. One sentence for each outcome, whichever page pressed it:
 * the quest's or the session's (one act, one word, one route).
 */
export const retryNotice = (quest: string, state: DriverState): [string, Record<string, unknown>] =>
  state.retried?.did === 'released'
    ? ['quests.detail.released', { id: quest, session: state.retried.session ?? '' }]
    : ['quests.detail.retried', { id: quest, count: state.strikes }];

/**
 * Ask Daoris's access mode, `restricted` or `full`: the person's press alone, from the panel's chip or
 * Settings, over the key `daoris ask-daoris mode` sets. It takes each conversation's next turn: one running under another
 * ends at its turn's end, and its next words resume it under this one. No proposal names it, so it is no door of Ask
 * Daoris's.
 */
export const useSetHelpMode = () => useDriverChange('SET_HELP_MODE');

/**
 * One operation of restricted mode's configured access chosen or taken back: a family of acts, or a checkout, the
 * person's press alone. A revoke puts Ask Daoris's next press of that family on a card; either takes each conversation's next turn.
 * No proposal names it.
 */
export const useSetHelpGrant = () => useDriverChange('SET_HELP_GRANT');

// The actors' wire types live in `data/wiring` so the presentational Actors card reads them without reaching the bridge.
export type { ActorEntry, ActorRow } from '../data/wiring';

/** Each seeded role's order as it runs: the staffing each `ROLES` row carries, read from the one role query. Empty in a browser. */
export const useActors = ({ enabled = true }: { enabled?: boolean } = {}) =>
  useRolesAs((catalogue) => catalogue?.roles.flatMap((row) => (row.staffing ? [row.staffing] : [])) ?? [], enabled);

export type { RoleCatalogue, RoleChangePress, RoleRow } from '../data/wiring';

/**
 * The role catalogue: every role with its work, place and choices, the works a role may
 * take, whether this home keeps the catalogue, and the roles Daoris suggests. `daoris role` says the same. Under the driver's
 * key, so every press that changes the driver's state asks it again. Null in a browser, which answers no roles.
 */
const readRoles = (raw: unknown): RoleCatalogue | null => {
  const answer = raw as Partial<RoleCatalogue> | null;
  if (!answer || !Array.isArray(answer.roles)) return null;
  return {
    roles: answer.roles, works: answer.works ?? [], kept: answer.kept === true,
    ...(answer.use && Array.isArray(answer.use.roles) ? { use: answer.use } : {}),
    ...(Array.isArray(answer.policies) ? { policies: answer.policies } : {}),
    ...(answer.supervisor && Array.isArray(answer.supervisor.sets) && Array.isArray(answer.supervisor.counts) ? { supervisor: answer.supervisor } : {}),
    ...(answer.deciders && Array.isArray(answer.deciders.places) && Array.isArray(answer.deciders.elsewhere) ? { deciders: answer.deciders } : {}),
    ...(Array.isArray(answer.policyFields) ? { policyFields: answer.policyFields } : {}),
    ...(Array.isArray(answer.policyPresets) ? { policyPresets: answer.policyPresets } : {}),
    ...(Array.isArray(answer.suggestedChoices) ? { suggestedChoices: answer.suggestedChoices, suggestedChoicesDated: answer.suggestedChoicesDated } : {}),
  };
};

/** The one role query, read through `select`; every reader of the roles shares its key and its one ask. */
function useRolesAs<T>(select: (catalogue: RoleCatalogue | null) => T, enabled: boolean) {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = [...keys.driver, 'roles'] as const;
  return useQuery({
    queryKey,
    // Told by event: the shell answers what it holds, and its read of the store and `driver.json` comes as `ANSWERED`.
    queryFn: () => askHeld('ROLES', client, queryKey, {}, readRoles),
    select,
    enabled: isAvailable && enabled,
  });
}

const sameCatalogue = (catalogue: RoleCatalogue | null) => catalogue;
export const useRoles = ({ enabled = true }: { enabled?: boolean } = {}) => useRolesAs(sameCatalogue, enabled);

// The structure's wire types live in `data/wiring` so the presentational sections read them without reaching the bridge.
export type { ChoiceStateCode, RoleStructure, StructureChoice, StructurePlaceAsk, StructureWork } from '../data/wiring';

const readRoleStructure = (raw: unknown): RoleStructure | null => {
  const answer = raw as Partial<RoleStructure> | null;
  if (!answer || !Array.isArray(answer.places)) return null;
  return {
    works: answer.works ?? [],
    roles: answer.roles ?? [],
    ...(answer.catalogue ? { catalogue: answer.catalogue } : {}),
    places: answer.places.map((place) => ({
      ...place,
      works: (place.works ?? []).map((work) => ({ ...work, choices: work.choices ?? [] })),
    })),
    agents: answer.agents ?? [],
  };
};

/**
 * The roles' structure for the places and works asked: each place's role per work as a start finds it, each work's rule
 * with where it is set, and each choice's state from the last readings alone, so opening a view asks no account. Told by
 * event, under the driver's key with the catalogue, so every press that changes the driver's state asks it again. Null until
 * the shell's first reading lands.
 */
export const useRoleStructure = (places: StructurePlaceAsk[], works?: string[], { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = [...keys.driver, 'roles', 'structure', places, works ?? null] as const;
  return useQuery({
    queryKey,
    queryFn: () => askHeld('ROLE_STRUCTURE', client, queryKey, works ? { places, works } : { places }, readRoleStructure),
    enabled: isAvailable && enabled,
  });
};

/**
 * The structure for one chosen place's chain alone: the place, its workspace and this machine, in place of every place.
 * Told by event under the same key as {@link useRoleStructure}, so a press that changes the roles asks it again.
 */
export const useRoleStructureChain = (chain: StructurePlaceAsk, works?: string[], { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = [...keys.driver, 'roles', 'structure', 'chain', chain, works ?? null] as const;
  return useQuery({
    queryKey,
    queryFn: () => askHeld('ROLE_STRUCTURE', client, queryKey, works ? { chain, works } : { chain }, readRoleStructure),
    enabled: isAvailable && enabled,
  });
};

const readPlaces = (raw: unknown): PlacesRead | null => {
  const answer = raw as Partial<PlacesRead> | null;
  if (!answer || !answer.machine || !Array.isArray(answer.repositories)) return null;
  const place = (row: PlaceRow): PlaceRow => ({ ...row, works: row.works ?? [], needs: row.needs ?? [] });
  return {
    machine: place(answer.machine),
    workspaces: (answer.workspaces ?? []).map((each) => ({ ...each, place: place(each.place) })),
    repositories: answer.repositories.map(place),
    next: answer.next ?? null,
    matching: answer.matching ?? answer.repositories.length,
    undriven: answer.undriven ?? null,
    detail: answer.detail ? place(answer.detail) : null,
    members: answer.members ?? null,
  };
};

/**
 * The places: this machine, each workspace and a page of the driven repositories with the
 * values of each head's line and where each is set, the not-driven ones as counts and, when `fold` names a workspace, a page of its
 * own. Told by event: the driver reads its configuration and the registry alone, so opening asks no git, process or account.
 */
export const usePlaces = (ask: PlacesAsk = {}, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = [...keys.driver, 'places', ask] as const;
  return useQuery({
    queryKey,
    queryFn: () => askHeld('PLACES', client, queryKey, ask, readPlaces),
    enabled: isAvailable && enabled,
  });
};

/**
 * A working role added, changed, removed or given back its seed: the person's press through the driver's role door,
 * whose judge refuses with nothing written. `daoris role` is its terminal twin, Ask Daoris's press of it
 * shown on the Confirm card by the route's view. What starts may move with it, so the route publishes the roster's key too.
 */
export const useSetRole = () => useDriverChange('SET_ROLE');

/**
 * Several role changes made as one: each a `SET_ROLE` press, judged in order and written in one transaction, so the first
 * refusal refuses them all and nothing is written. The Roles mode's *Use these choices* is its press.
 */
export const useSetRoles = () => useDriverChange('SET_ROLES');

/**
 * "Look now": a quest just published, or an ask just made, is looked at on the driver's next breath
 * rather than at its next poll (fifteen seconds by default). Nothing in a browser, which has no driver,
 * and nothing said when it cannot be asked — the poll looks anyway.
 */
export const useNudge = () => {
  const { isAvailable } = useShenora();
  return useCallback(() => {
    if (!isAvailable) return;
    call<null>('NUDGE').catch(() => {
      // The poll is the fallback, and a missed nudge is not the person's problem.
    });
  }, [isAvailable]);
};

/** What a pass said: the wall it hit, and what the remote understood and did not take — the driver's words. */
export type SyncNowReport = { workspace: string; problem?: string | null; notes: string[] };

/**
 * *Sync now*: the tick's own pass for one circle, through the shell's own driver loop —
 * `daoris remote sync --workspace <name>` is the other door to the same pass. Shell-only,
 * because the pass that feeds a checkout's registration and knowledge asks git, and only the machine
 * with the checkout can.
 *
 * A pass that answers, a wall in its report among them, is published by its route, and the state hub asks where each circle
 * stands, the quests, the sessions and the registry again in every window.
 */
export const useSyncNow = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (workspace: string) => call('SYNC_NOW', { workspace }) as Promise<SyncNowReport>,
    onError: () => {
      // A pass refused as an error publishes nothing, and one that failed part way may have recorded where it
      // stood: this window, which pressed it, asks again itself. Where the shell pushes the standings, the pass's end pushes them.
      void client.invalidateQueries({ queryKey: keys.allSync, predicate: notPushed(client) });
      // The quests and the records themselves, where the shell pushes them, are pushed as the pass moved them.
      void client.invalidateQueries({ queryKey: keys.allQuests, predicate: notPushed(client) });
      void client.invalidateQueries({ queryKey: keys.allSessions, predicate: notPushed(client) });
      void client.invalidateQueries({ queryKey: keys.allRegistry });
    },
  });
};

export const useSetDrivable = () => useDriverChange('SET_DRIVABLE');
export const useSetHold = () => useDriverChange('SET_HOLD');
/** The switch *Daoris may change this checkout's files*: off by default, writes nothing to the repository itself. */
export const useSetCheckoutWrites = () => useDriverChange('SET_CHECKOUT_WRITES');
/**
 * A repository's standing answer: the person's words, or none to clear it — the same `standing` `daoris place
 * standing <repo> "…"|--clear` edits. Blank words or words past the bound are the driver's refusal, its sentence verbatim.
 */
export const useSetStanding = () => useDriverChange('SET_STANDING');
/**
 * This machine's heavy gates for a repository: the whole list in the order its gates run, or null to clear it — the
 * same `heavyGates` `daoris place heavy <repo> <name>=<command>…|--clear` edits. A list the mapping would not read (a gate with no
 * name or no command, a name twice, a folder not under the checkout) is the driver's refusal, its sentence verbatim, nothing written.
 */
export const useSetHeavyGates = () =>
  useDriverChange('SET_HEAVY_GATES');
/**
 * Which checks a repository's sessions run: `light`, `heavy-at-end`, or null for the default — the same
 * `sessionChecks` `daoris place checks <repo> light|heavy-at-end|--clear` edits. A value that is no choice is the driver's refusal, its
 * sentence verbatim, nothing written. The person's choice alone: no proposal names it.
 */
export const useSetSessionChecks = () =>
  useDriverChange('SET_SESSION_CHECKS');

/**
 * Whether a repository's own `daoris.gates.json` marks heavy gates, so wins over this machine's list: the
 * driver's reading of that one file in the checkout it holds (`HEAVY_MARKS`), never the page's. A read; `enabled` holds it back until
 * the section it serves is drawn, and the file is asked again whenever the page is, since nothing wakes it on an edit to the file.
 */
export const useHeavyMarks = (repository: string, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.heavyMarks(repository),
    queryFn: () => call('HEAVY_MARKS', { repository }) as Promise<HeavyMarks & { repository: string }>,
    enabled: isAvailable && enabled && repository !== '',
  });
};

/**
 * How soon a repository's heavy-run slot is asked again: every 5 s while a run holds it or waits for it, so a gate's
 * change and a wait's end are seen within seconds; every 30 s while it is free, since nothing wakes the page when a run starts.
 */
export const heavySlotAgainIn = (slot: HeavySlot | null | undefined): number =>
  slot && (slot.holder || slot.waiters.length > 0) ? 5_000 : 30_000;

const heavySlotQuery = (client: QueryClient, repository: string, enabled: boolean) => ({
  queryKey: keys.heavySlot(repository),
  // Told by event: the shell answers what it holds, and its look at the slot's records comes as `ANSWERED`.
  queryFn: () => askHeld('HEAVY_SLOT', client, keys.heavySlot(repository), { repository }, readHeavySlot),
  enabled: enabled && repository !== '',
  // A route that answers none reads as no heavy run, asked again at the free slot's pace.
  retry: false,
  refetchInterval: (query: { state: { data?: HeavySlot | null } }) => heavySlotAgainIn(query.state.data),
});

/**
 * Who holds a repository's heavy-run slot, at which gate, and who waits: the driver's reading of
 * the records the runs keep beside the slot (`HEAVY_SLOT`), each only while its process runs, never a run's output. A read.
 */
export const useHeavySlot = (repository: string, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  return useQuery(heavySlotQuery(client, repository, isAvailable && enabled));
};

/** The slots of several repositories at once, by name, for the rail and a session's head: none where a slot is not read yet. */
export const useHeavySlots = (repositories: readonly string[]): ReadonlyMap<string, HeavySlot | null> => {
  const { isAvailable } = useShenora();
  const named = [...new Set(repositories.filter((name) => name !== ''))].sort();
  const client = useQueryClient();
  const answers = useQueries({ queries: named.map((repository) => heavySlotQuery(client, repository, isAvailable)) });
  return new Map(named.map((repository, at) => [repository, answers[at]?.data ?? null]));
};
