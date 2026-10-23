import { useCallback } from 'react';
import { type QueryClient, useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import type { SyncStanding } from '../data/api';
import type { KeepSet } from '../data/wiring';
import { keys, SYNC_SLICE } from '../data/queries';
import type { AccountWaitTick, Consideration, TrustHold } from '../data/signals';
import type { AccessLevel } from '../settings/repository/Access';
import type { CommitRule } from '../settings/repository/Commits';
import type { HeavyGate, HeavyMarks } from '../settings/repository/HeavyGates';
import type { SessionChecksValue } from '../settings/repository/SessionChecks';
import type { HelperRule } from '../settings/repository/Helpers';
import type { HandOverRule } from '../settings/repository/HandOver';
import type { LandingRule } from '../settings/repository/Landings';
import type { UnreadLanding } from '../projects/setup/SetupParts';
import type { LanguageOption } from '../settings/repository/Languages';
import type { OpinionRule } from '../settings/repository/Opinions';
import type { ReviewLook, ReviewRule } from '../settings/repository/Reviews';
import type { ReviewShown } from '../work/gates/ReviewChip';
import { type HeavySlot, readHeavySlot } from '../work/heavySlot';
import { call, refusedNotReady } from './call';
import { notPushed, pushedOf, readPushed, type SliceSpec, usePushedLive } from './state/slices';

// The driver's standing state and its dials (MOD3): what this machine drives and holds, what it says
// when a session parks, how many failures park a quest, which agent answers an ask, and a look now.

/**
 * The driver's standing state, as the page reads it. The DAORIS.DRIVER module answers more (its
 * config path, cap, adapter, poll interval); the fields land here when a surface reads them.
 */
export type DriverState = {
  /**
   * Whether the driver's service is up (LOOK2a): until it is, every route that reads it refuses *still coming up*.
   * Absent on a shell older than it, which is read as up, as this answer alone said before.
   */
  ready?: boolean;
  /** What *Try again* did, on the answer to `RETRY_QUEST` alone (SESSUX1b); absent on every other answer. */
  retried?: Retried | null;
  drivable: string[];
  holds: string[];
  /** Repositories whose sessions open their own worktree instead of the registered root (D51). */
  trees: string[];
  /** Session ids with a live process right now — what "stop" can actually reach. */
  running: string[];
  /**
   * Who is driving Daoris's browser (BRW8): the running sessions the driver handed a server that drives
   * it, from the handing until each ends. Absent on a shell older than it, which says nothing.
   */
  drivingBrowser?: string[];
  /**
   * The set-ups waiting for the person's review here (REVIEWENV1d), each with whether Daoris serves its tab now: the strip's
   * chip beside the browser's door. Absent on a shell older than it, and where the loop carries no browser, which shows nothing.
   */
  inReview?: ReviewShown[];
  /** What *Show it again* said (REVIEWENV1d), on the answer to `SHOW_REVIEW_AGAIN` alone; absent on every other answer. */
  shownAgain?: { quest: string; message: string } | null;
  /** Whether this machine interrupts the person when a session parks or ends unasked (SURF5b). */
  notify: boolean;
  /** How many failed sessions park a quest (D58); `0` never parks. */
  strikes: number;
  /**
   * How long an account cools when its agent names no time, in minutes (TOOL4g, D125 §2.2): the file's, or the hour.
   * Absent on a shell older than it, and the setting is then not offered.
   */
  coolOff?: number;
  /** How many sessions Daoris starts at once on this machine (CAPSET1); absent on a shell older than the setting's route. */
  cap?: number;
  /** Quests the person restarted, by id, and the failure count each was restarted at. */
  forgiven: Record<string, number>;
  /**
   * The agent an ask the declarations leave opens an intake session on (INT4b), or "" — asks are
   * answered by declarations only. Never null: the bridge leaves a null out, and absent is how a
   * shell older than the intake reads.
   */
  intakeAdapter?: string;
  /** The agent Ask Daoris runs on (HELP1, D89), or "" for none; absent on a shell older than it. */
  helperAdapter?: string;
  /**
   * Ask Daoris's access mode as the person chose it (HELPMODE1a2, D183 point 2): `restricted` or `full`, what each conversation's
   * next turn takes (ACCESSSWAP1); a value the page does not know reads as restricted (`helpModeOf`). Absent on a shell older than
   * it, which offers no choice. A conversation's own mode, the one its start got, is its history row's `mode`.
   */
  helpMode?: string;
  /**
   * Restricted mode's configured access as this machine chose it (HELPMODE1c2b): each grant the driver serves, by its id, with the
   * operations chosen and those it acts on (`built`); housekeeping's, and the chosen settings' (HELPMODE1c3). What each start of Ask
   * Daoris and every call of its tools read; `pending` while a conversation running here was handed another choice, which its next
   * turn takes (ACCESSSWAP1). And the browser's, read only from its plugin's approval: no `built`, `changed` since approval, and
   * the `plugin` whose tab changes it, absent where none here offers it. Absent on a shell older than it, which draws no configured
   * access.
   */
  helpGrants?: { id: string; chosen: string[]; built?: string[]; pending?: boolean; changed?: boolean; plugin?: string | null }[];
  /**
   * The Ask Daoris conversations running under another access than the one chosen (ACCESSSWAP1, D183 point 2): each ends at its
   * turn's end, and its next words resume it under the access chosen, so the panel keeps it in view as it ends. Absent on an older
   * shell, which ended none so.
   */
  helpSwapping?: string[];
  /** The Daoris home (D63): the directory every machine-local file lives in. Absent on an older shell. */
  home?: string;
  /**
   * What establishing the home did on this start, when it is worth saying — state moved in from a
   * profile directory, the account's environment gaining the variable, or the account's home set aside
   * for this install's own (D105). Null when nothing was.
   */
  homeNotice?: string | null;
  /**
   * How that home stands to the account's DAORIS_HOME, which a terminal's daoris reads (LEFT2): the same
   * folder, overridden by this install's own `data/` (D105; the notice says which), or named for this start
   * alone (no notice). Absent on a shell older than it.
   */
  homeAccount?: 'same' | 'overridden' | 'this-start';
  /**
   * The host this shell adopted is serving a page that is not this install's, in the shell's own
   * sentence — or null. Standing, because it is true for as long as that host runs.
   */
  hostNotice?: string | null;
  /** The lines as set (WSR2), by repository — absent on a shell older than them. */
  lines?: { repository: string; branch: string }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceLines?: { workspace: string; branch: string }[];
  /** How work lands as set (WSR1, D87), by repository — absent on a shell older than it. */
  landings?: ({ repository: string } & LandingRule)[];
  /** And by workspace. */
  workspaceLandings?: ({ workspace: string } & LandingRule)[];
  /**
   * The landing rules the file keeps that read as no rule (FABRIC-GRAPH1d), which the rows above skip: by repository — absent on a
   * shell older than it.
   */
  unreadLandings?: ({ repository: string } & UnreadLanding)[];
  /** And by workspace. */
  workspaceUnreadLandings?: ({ workspace: string } & UnreadLanding)[];
  /** Reading across as set (D107), by repository — absent on a shell older than it. */
  readAcross?: { repository: string; read: boolean }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceReadAcross?: { workspace: string; read: boolean }[];
  /** The declared relationships (D107): what each repository's sessions may also write into. */
  writeAcross?: { repository: string; to: string[] }[];
  /**
   * Each repository's standing answer (KNOWUSE1b, D135 §3): the person's words, handed to every session there, and when they
   * set them, or null where the file does not say — absent on a shell older than it.
   */
  standing?: { repository: string; says: string; at?: string | null }[];
  /**
   * The session languages as set (LANG1c, D142 point 7): what each repository's sessions are asked to write to the person in,
   * a code of the driver's table — absent on a shell older than it.
   */
  languages?: { repository: string; language: string }[];
  /** And by workspace, for each repository there that sets none of its own, and its intake. */
  workspaceLanguages?: { workspace: string; language: string }[];
  /**
   * The driver's closed table each option is named from (LANG1c): its code and the name a session's line gives it, so adding a
   * language is a row there and needs no catalogue here. Absent on a shell older than it, which offers no session language.
   */
  languageTable?: LanguageOption[];
  /**
   * The review rules as set (REVIEWENV1a, D154 point 2): where each repository's work is shown to the person before it lands,
   * or none here — absent on a shell older than it, which offers no review row.
   */
  reviews?: { repository: string; rule: ReviewRule }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceReviews?: { workspace: string; rule: ReviewRule }[];
  /** What a review's procedure look found, on the answer to `SET_REVIEW`'s put alone; absent on every other answer. */
  reviewed?: ReviewLook | null;
  /**
   * The second-opinion rules as set (XAGENT1a, D155 point 3): which other agent reads each repository's work before it lands,
   * or none here — absent on a shell older than it, which offers no second-opinion row.
   */
  opinions?: { repository: string; rule: OpinionRule }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceOpinions?: { workspace: string; rule: OpinionRule }[];
  /**
   * The commit rules as set (DOORS1b, D164's amendment point 1): how each repository's work is committed — absent on a shell
   * older than it, which offers no commit row.
   */
  commits?: { repository: string; rule: CommitRule }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceCommits?: { workspace: string; rule: CommitRule }[];
  /**
   * The helpers rules as set (DOORS1b, D163 point 3): what a helper each repository's agent asks for does — absent on a shell
   * older than it, which offers no helpers row.
   */
  helpers?: { repository: string; rule: HelperRule }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceHelpers?: { workspace: string; rule: HelperRule }[];
  /**
   * The hand-over rules as set (MIXAGENT1c, D163 point 3): what an agent's ask to hand its session over does in each repository —
   * absent on a shell older than it, which offers no hand-over row.
   */
  handOver?: { repository: string; rule: HandOverRule }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceHandOver?: { workspace: string; rule: HandOverRule }[];
  /**
   * The access setups as set (AUTONOMY1a, D184 point 1): what each repository's sessions start under, full access or the pauses it
   * names — absent on a shell older than it, which offers no access section. What each repository resolves to is `LINES`' `access`.
   */
  access?: { repository: string; setting: AccessLevel }[];
  /** And by workspace, for each pause a repository there does not name. */
  workspaceAccess?: { workspace: string; setting: AccessLevel }[];
  /** And this machine's, for each pause neither a repository nor its workspace names: no pause and not full where none is set. */
  machineAccess?: AccessLevel;
  /**
   * This machine's heavy gates (HEAVYRUN1b2b, D180's HEAVYRUN1b2a note): for each repository whose own `daoris.gates.json` marks no
   * heavy gate, the list `run-heavy` runs for it in its order — absent on a shell older than it, which offers no heavy gates section.
   */
  heavyGates?: { repository: string; gates: HeavyGate[] }[];
  /**
   * Which checks each repository's sessions run under Current (FABRIC-PIPELINE1b, D192 point 4): `light` or `heavy-at-end` by
   * repository, none being today's — absent on a shell older than it, which offers no sessions' checks row.
   */
  sessionChecks?: { repository: string; checks: string }[];
  /**
   * The person's standing confirmation (TERMTRUST1a, D201; the terminal-trust design §6.1), as the shell's `StandingNow` says it: read
   * only through `standingOf` (`bridge/person.ts`), since the bridge leaves its nulls out. Absent on a shell older than it and where
   * the shell serves no pipe, which draws no row.
   */
  standingConfirmation?: unknown;
};

/**
 * A pushed slice's snapshot, as last judged and read from memory (PUSH1a, D182 point 3): `{ slice, epoch, status, partitions }`,
 * or nothing for a slice this shell does not push. Every slice's spec reads it here, so the route has one bridge file. A read: it
 * declares nothing and tells nothing.
 */
export const readSlice = (slice: string) => call<unknown>('STATE_SLICE', { slice });

/** The partitions the shell pushes the driver's state in (`DriverStateSlice`), composed in this order. */
const DRIVER_PARTITIONS = ['shell', 'choices', 'running'] as const;

/**
 * The driver's state as the shell pushes it (PUSH1a, D182): its standing choices and holds (`choices`), what runs and waits for
 * review (`running`) and the shell's own standing (`shell`), each pushed whole when it moves, composed into the one entry the
 * pull answers (`STATE`), whose fields the shell's allow-list picks. A shell older than the push keeps the pull.
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
 * Where each wired circle stands as the shell pushes it (PUSH1a2, D182): a partition per circle, named as the remotes map names it,
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
  // Pushed (PUSH1a): read from memory, written by the pushes, so neither a focus nor going stale asks the shell again.
  const pushed = usePushedLive(driverSlice.name);
  return useQuery({
    queryKey: keys.driver,
    queryFn: () => readPushed(client, driverSlice.name, () => call<DriverState>('STATE')),
    enabled: isAvailable,
    ...(pushed ? { staleTime: Infinity, refetchOnWindowFocus: false } : {}),
  });
};

/**
 * Whether this window is a shell's (PUSHUX2): its driver answered, or the bridge is here and its driver's read is still on its way.
 * A shell is known before its driver answers, so until the driver's slice lands its window keeps the shell's places and waits,
 * never drawing a browser's frame and then swapping it. A driver whose read failed reads as no driver, as it did before.
 */
export const useAttached = (): boolean => {
  const { isAvailable } = useShenora();
  const driver = useDriver();
  return driver.data !== undefined || (isAvailable && !driver.isError);
};

/**
 * The driver's state a press answered (MOD3), written into this window's cache where the state is pulled. Where it is pushed
 * (PUSH1a), the push is the one writer, so an answer arriving after a newer push can never set it back.
 */
export const writeAnswered = (client: QueryClient, state: DriverState) => {
  if (!pushedOf(client).live(driverSlice.name)) client.setQueryData(keys.driver, state);
};

/**
 * Ask again what the driver could not answer before its service was up (LOOK2a): its own answers, all keyed under the
 * driver (its state, which now says ready, the lines, the clean-up's list, which repositories a look takes), and every
 * other query it refused as not ready, such as a session's review or preview. A query refused for a reason of its own
 * keeps its answer. The state itself where it is pushed is not asked: its push says it is ready (PUSH1a).
 */
export const askAgainWhenReady = (client: QueryClient) => {
  void client.invalidateQueries({ queryKey: keys.driver, predicate: notPushed(client) });
  void client.invalidateQueries({ predicate: (query) => refusedNotReady(query.state.error) });
};

/**
 * Hear the driver say its service is up (`DRIVER_READY`, LOOK2a), and ask again then what it refused while it came up.
 *
 * @remarks
 * A screen opened as the shell starts asks at once and is refused *still coming up*. Before LOOK2a only a tick asked
 * again, and the first tick can wait on a remote's sync or on another driver's lock, so the workspace's page (then Settings → Workspace) said no
 * repository had a line until something else happened to ask. Said once by the shell, the moment the refusals stop.
 */
export const useDriverReady = () => {
  const client = useQueryClient();
  useShenoraEvent('DAORIS', 'DRIVER_READY', () => askAgainWhenReady(client));
};

/**
 * What is under way as the driver holds it (STATEHUB1c2, D169's STATEHUB1c2 note): its presses, whole and numbered as its last
 * `UNDER_WAY` telling, which the state hub reads as it mounts. A read: it declares nothing and tells nothing.
 */
export const readUnderWay = () => call<{ seq?: unknown; presses?: unknown } | null>('UNDER_WAY');

/**
 * Why each open quest is sitting, in the driver's own words, as of its last tick (D46 §3). Written
 * by the tick (`ShellSignals`) and never fetched — the driver says it and nothing else knows — so
 * this query only ever answers what the last tick put there. Empty in a browser, where no tick
 * arrives, and empty before the first one.
 */
export const useConsidered = () => useQuery({
  queryKey: keys.considered,
  queryFn: () => [] as Consideration[],
  staleTime: Infinity,
  gcTime: Infinity,
});

/**
 * The starts the driver held because the agent has not been trusted where they would run (D73), as
 * of its last tick — written by the tick like `useConsidered`, and empty in a browser.
 */
export const useUntrusted = () => useQuery({
  queryKey: keys.untrusted,
  queryFn: () => [] as TrustHold[],
  staleTime: Infinity,
  gcTime: Infinity,
});

/**
 * The starts the driver held on cooling accounts, an ask's intake among them (UX6d), and on signed-out accounts with none
 * cooling (UX6d1), as of its last tick — written by the tick like `useConsidered`, and empty in a browser and before the
 * first tick.
 */
export const useWaits = () => useQuery({
  queryKey: keys.waits,
  queryFn: () => [] as AccountWaitTick[],
  staleTime: Infinity,
  gcTime: Infinity,
});

/** What a grant answered: the key it wrote, whether it wrote, whether a re-read says yes, and why. */
export type TrustGranted = { folder: string; key: string; changed: boolean; verified: boolean; message: string };

/**
 * The person's grant of a folder the driver is holding (D73) — the screen's half of
 * `daoris agent trust … --yes`. The shell writes it only for a hold its last tick produced, in the
 * file that tick read, and the driver looks again at once. Its route publishes the driver's state, which the state hub asks
 * again in every window (STATEHUB1h, D169).
 */
export const useTrustFolder = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (hold: Pick<TrustHold, 'folder' | 'trustFile'>) =>
      call<TrustGranted>('TRUST_FOLDER', { folder: hold.folder, trustFile: hold.trustFile }),
    onSuccess: (granted, hold) => {
      // Off the list now, rather than a tick from now: the grant is what the person just did.
      if (granted.verified) {
        client.setQueryData<TrustHold[]>(keys.untrusted, (holds = []) =>
          holds.filter((held) => held.folder !== hold.folder || held.trustFile !== hold.trustFile));
      }
    },
  });
};

/**
 * One mutation shape for the standing choices, these and the rules' (`bridge/repositories/lines.ts`): edit the file, and the loop looks now,
 * not at the poll. The window that pressed writes the state it was answered; the route publishes the change, and every window's
 * state hub asks the driver's state and what is read under its key again (STATEHUB1h, D169).
 */
export function useDriverChange<TVariables extends Record<string, unknown>>(type: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (variables: TVariables) => call<DriverState>(type, variables),
    onSuccess: (state) => writeAnswered(client, state),
  });
}

/**
 * Whether this machine says so when a session parks or ends unasked (SURF5b).
 *
 * @remarks
 * The same `driver.json` field `daoris driver notify on|off` edits — D50's two doors onto one
 * truth, like every other control here. It governs the JUDGEMENT rather than the toast, which is
 * why a headless machine honours it too.
 */
export const useSetNotify = () => useDriverChange<{ notify: boolean }>('SET_NOTIFY');

/**
 * How many failed sessions park a quest (D58) — `0` never parks, which is how every machine behaved
 * before it existed. The same field `daoris driver strikes <n>` edits.
 */
export const useSetStrikes = () => useDriverChange<{ strikes: number }>('SET_STRIKES');

/**
 * How long an account cools when its agent names no time (TOOL4g, D125 §2.2): whole minutes, at least one, the same
 * `cooloff` `daoris driver cooloff <minutes>` edits; less than one is the driver's refusal.
 */
export const useSetCoolOff = () => useDriverChange<{ minutes: number }>('SET_COOLOFF');

/** How many sessions Daoris starts at once (CAPSET1): the same `cap` `daoris driver cap <n>` edits; less than one is refused. */
export const useSetCap = () => useDriverChange<{ cap: number }>('SET_CAP');

/**
 * One kind's keep rule (KEEP1d, D195 point 2; the cleanup design §2.2): the same `keep` `daoris driver keep` edits. A value out of its
 * kind's bounds is the driver's `KEEP_OUT_OF_RANGE`, said under the row's editor. It declares the wiring with the state, so the card
 * reads its rules again.
 */
export const useSetKeep = () => useDriverChange<KeepSet>('SET_KEEP');

/**
 * *Try again* (RETRY1; SESSUX1b, D126 §3.4): let a parked quest run again, counting from where it stands, or release one
 * the person's stop holds — whichever the driver's verdict says applies, refused where neither does. `daoris driver
 * retry <quest>` and its `--session` are the terminal's two halves.
 */
export const useRetryQuest = () => useDriverChange<{ quest: string }>('RETRY_QUEST');

/**
 * *Show it again* on the strip's review chip (REVIEWENV1d, the review environment design §3.3): the set-up step's newest set-up
 * served to its tab of Daoris's browser again, the tab brought forward where the set-up left it, with no session and no model.
 * Refused in the driver's words wherever it would show anything but that build at the rule's address.
 */
export const useShowReviewAgain = () => useDriverChange<{ quest: string }>('SHOW_REVIEW_AGAIN');

/** What *Try again* did (D126 §3.4): RETRY1's mark on a parked quest, or the release of the stop that held it. */
export type Retried = { quest: string; did: 'marked' | 'released'; session?: string | null };

/**
 * The notice for a *Try again*, from the driver's answer, as a catalogue key and its values: a release names the stop it
 * released, a mark how many more failures park the quest again. One sentence for each outcome, whichever page pressed it:
 * the quest's or the session's (D126 §3.4: one act, one word, one route).
 */
export const retryNotice = (quest: string, state: DriverState): [string, Record<string, unknown>] =>
  state.retried?.did === 'released'
    ? ['quests.detail.released', { id: quest, session: state.retried.session ?? '' }]
    : ['quests.detail.retried', { id: quest, count: state.strikes }];

/** Name the agent Ask Daoris runs on, or null for none — `daoris driver helper <agent>|off` (D50, D89). */
export const useSetHelper = () => useDriverChange<{ adapter: string | null }>('SET_HELPER');

/**
 * Ask Daoris's access mode, `restricted` or `full` (HELPMODE1b, D183 point 2): the person's press alone, from the panel's chip or
 * Settings, over the key `daoris-driver help mode` sets (D50). It takes each conversation's next turn: one running under another
 * ends at its turn's end, and its next words resume it under this one (ACCESSSWAP1). No proposal names it, so it is no door of Ask
 * Daoris's.
 */
export const useSetHelpMode = () => useDriverChange<{ mode: 'restricted' | 'full' }>('SET_HELP_MODE');

/**
 * One operation of restricted mode's configured access chosen or taken back (HELPMODE1c2b, D183's note): housekeeping's, the
 * person's press alone. A revoke refuses Ask Daoris's next call of that tool; either takes each conversation's next turn
 * (ACCESSSWAP1). No proposal names it.
 */
export const useSetHelpGrant = () => useDriverChange<{ grant: string; operation: string; chosen: boolean }>('SET_HELP_GRANT');

/** One entry of an actor's effective order (D205 §4.0): `from` is what a start's record keeps as its choice. */
export type ActorEntry = { agent: string; model?: string | null; effort?: string | null; from: string };

/** One actor as `ACTORS` answers it (MODELROLE1c, D205 §9.4): set, as before, off, or an entry in driver.json that does not read. */
export type ActorRow = { id: string; state: 'set' | 'before' | 'off' | 'unreadable'; choices: ActorEntry[]; error?: string | null };

/**
 * Each actor's order, where it came from (MODELROLE1c): a read of `driver.json`'s `actors`, the same `daoris driver actor` says. Kept
 * under the driver's key, so every press that changes the driver's state asks it again. Empty in a browser.
 */
export const useActors = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: [...keys.driver, 'actors'] as const,
    queryFn: async () => {
      const answer = await call<{ actors?: ActorRow[] } | null>('ACTORS');
      return Array.isArray(answer?.actors) ? answer.actors : [];
    },
    enabled: isAvailable,
  });
};

/**
 * One actor's order set, made off (`[]`, Intake and Ask Daoris only) or back to as before (`null`) (MODELROLE1c, D205 §8.2): the
 * person's press, judged by the driver's one judge, written through the legacy keys, Chat and Brainstorm kept where a change of Build's
 * agent would move them. `daoris driver actor` is its terminal twin (D50).
 */
export const useSetActor = () =>
  useDriverChange<{ actor: string; choices: { agent: string; model?: string | null; effort?: string | null }[] | null }>('SET_ACTOR');

/**
 * Which agent answers an ask the declarations leave (INT4b), or null for none — the field `daoris
 * driver intake <agent>|off` edits (D50). What an intake in each circle would run on moves with it,
 * so its route publishes the roster's key too, and that answer is asked again rather than left naming the agent before
 * (STATEHUB1h, D169).
 */
export const useSetIntake = () => useDriverChange<{ adapter: string | null }>('SET_INTAKE');

/**
 * "Look now": a quest just published, or an ask just made, is looked at on the driver's next breath
 * rather than at its next poll (fifteen seconds by default). The DAORIS.DRIVER route has said this
 * since it was written; nothing called it (REV3 CLEAN1). Nothing in a browser, which has no driver,
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
 * *Sync now* (SYNC6b): the tick's own pass for one circle, through the shell's own driver loop —
 * `daoris-driver sync --workspace <name>` is the other door to the same pass (D50). Shell-only,
 * because the pass that feeds a checkout's registration and knowledge asks git, and only the machine
 * with the checkout can.
 *
 * A pass that answers, a wall in its report among them, is published by its route, and the state hub asks where each circle
 * stands, the quests, the sessions and the registry again in every window (STATEHUB1d, D169).
 */
export const useSyncNow = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (workspace: string) => call<SyncNowReport>('SYNC_NOW', { workspace }),
    onError: () => {
      // A pass refused as an error publishes nothing (D169 point 5), and one that failed part way may have recorded where it
      // stood: this window, which pressed it, asks again itself. Where the shell pushes the standings, the pass's end pushes them (PUSH1a2).
      void client.invalidateQueries({ queryKey: keys.allSync, predicate: notPushed(client) });
      // The quests and the records themselves, where the shell pushes them, are pushed as the pass moved them (PUSH1d3, PUSH1d2).
      void client.invalidateQueries({ queryKey: keys.allQuests, predicate: notPushed(client) });
      void client.invalidateQueries({ queryKey: keys.allSessions, predicate: notPushed(client) });
      void client.invalidateQueries({ queryKey: keys.allRegistry });
    },
  });
};

export const useSetDrivable = () => useDriverChange<{ repository: string; drivable: boolean }>('SET_DRIVABLE');
export const useSetHold = () => useDriverChange<{ repository: string; held: boolean }>('SET_HOLD');
/** Session trees (D51): the same file `daoris driver trees <repo> on|off` edits — two editors, one truth. */
export const useSetTrees = () => useDriverChange<{ repository: string; ownTree: boolean }>('SET_TREES');
/**
 * A repository's standing answer (KNOWUSE1b): the person's words, or none to clear it — the same `standing` `daoris driver
 * standing <repo> "…"|--clear` edits. Blank words or words past the bound are the driver's refusal, its sentence verbatim.
 */
export const useSetStanding = () => useDriverChange<{ repository: string; says?: string }>('SET_STANDING');
/**
 * This machine's heavy gates for a repository (HEAVYRUN1b2b): the whole list in the order its gates run, or null to clear it — the
 * same `heavyGates` `daoris-driver heavy <repo> <name>=<command>…|--clear` edits. A list the mapping would not read (a gate with no
 * name or no command, a name twice, a folder not under the checkout) is the driver's refusal, its sentence verbatim, nothing written.
 */
export const useSetHeavyGates = () =>
  useDriverChange<{ repository: string; gates: HeavyGate[] | null }>('SET_HEAVY_GATES');
/**
 * Which checks a repository's sessions run (FABRIC-PIPELINE1b, D192 point 4): `light`, `heavy-at-end`, or null for as before — the same
 * `sessionChecks` `daoris-driver checks <repo> light|heavy-at-end|--clear` edits. A value that is no choice is the driver's refusal, its
 * sentence verbatim, nothing written. The person's choice alone: no proposal names it.
 */
export const useSetSessionChecks = () =>
  useDriverChange<{ repository: string; checks: SessionChecksValue | null }>('SET_SESSION_CHECKS');

/**
 * Whether a repository's own `daoris.gates.json` marks heavy gates, so wins over this machine's list (HEAVYRUN1b2c, D180's note): the
 * driver's reading of that one file in the checkout it holds (`HEAVY_MARKS`), never the page's. A read; `enabled` holds it back until
 * the section it serves is drawn, and the file is asked again whenever the page is, since nothing wakes it on an edit to the file.
 */
export const useHeavyMarks = (repository: string, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.heavyMarks(repository),
    queryFn: () => call<HeavyMarks & { repository: string }>('HEAVY_MARKS', { repository }),
    enabled: isAvailable && enabled && repository !== '',
  });
};

/**
 * How soon a repository's heavy-run slot is asked again (HEAVYSLOT1): every 5 s while a run holds it or waits for it, so a gate's
 * change and a wait's end are seen within seconds; every 30 s while it is free, since nothing wakes the page when a run starts.
 */
export const heavySlotAgainIn = (slot: HeavySlot | null | undefined): number =>
  slot && (slot.holder || slot.waiters.length > 0) ? 5_000 : 30_000;

const heavySlotQuery = (repository: string, enabled: boolean) => ({
  queryKey: keys.heavySlot(repository),
  queryFn: async () => readHeavySlot(await call<unknown>('HEAVY_SLOT', { repository })),
  enabled: enabled && repository !== '',
  // A shell older than the route answers none, which reads as no heavy run, asked again at the free slot's pace.
  retry: false,
  refetchInterval: (query: { state: { data?: HeavySlot | null } }) => heavySlotAgainIn(query.state.data),
});

/**
 * Who holds a repository's heavy-run slot, at which gate, and who waits (HEAVYSLOT1, the UI/UX design §3.5): the driver's reading of
 * the records the runs keep beside the slot (`HEAVY_SLOT`), each only while its process runs, never a run's output. A read.
 */
export const useHeavySlot = (repository: string, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery(heavySlotQuery(repository, isAvailable && enabled));
};

/** The slots of several repositories at once, by name, for the rail and a session's head: none where a slot is not read yet. */
export const useHeavySlots = (repositories: readonly string[]): ReadonlyMap<string, HeavySlot | null> => {
  const { isAvailable } = useShenora();
  const named = [...new Set(repositories.filter((name) => name !== ''))].sort();
  const answers = useQueries({ queries: named.map((repository) => heavySlotQuery(repository, isAvailable)) });
  return new Map(named.map((repository, at) => [repository, answers[at]?.data ?? null]));
};
