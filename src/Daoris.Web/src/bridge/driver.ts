import { useCallback } from 'react';
import { type QueryClient, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { keys } from '../queries';
import type { Consideration, TrustHold } from '../signals';
import type { LandingRule } from '../settings/Landings';
import { call, refusedNotReady } from './call';

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
  /** Whether this machine interrupts the person when a session parks or ends unasked (SURF5b). */
  notify: boolean;
  /** How many failed sessions park a quest (D58); `0` never parks. */
  strikes: number;
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
  /** Reading across as set (D107), by repository — absent on a shell older than it. */
  readAcross?: { repository: string; read: boolean }[];
  /** And by workspace, for each repository there that sets none of its own. */
  workspaceReadAcross?: { workspace: string; read: boolean }[];
  /** The declared relationships (D107): what each repository's sessions may also write into. */
  writeAcross?: { repository: string; to: string[] }[];
};

export const useDriver = () => {
  // The same detection path ShellSignals uses — one answer to "is a shell here", not two that can
  // drift. (The kit reads the bridge per render; in the desktop the bridge exists before the page.)
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.driver,
    queryFn: () => call<DriverState>('STATE'),
    enabled: isAvailable,
  });
};

/**
 * Ask again what the driver could not answer before its service was up (LOOK2a): its own answers, all keyed under the
 * driver (its state, which now says ready, the lines, the clean-up's list, which repositories a look takes), and every
 * other query it refused as not ready, such as a session's review or preview. A query refused for a reason of its own
 * keeps its answer.
 */
export const askAgainWhenReady = (client: QueryClient) => {
  void client.invalidateQueries({ queryKey: keys.driver });
  void client.invalidateQueries({ predicate: (query) => refusedNotReady(query.state.error) });
};

/**
 * Hear the driver say its service is up (`DRIVER_READY`, LOOK2a), and ask again then what it refused while it came up.
 *
 * @remarks
 * A screen opened as the shell starts asks at once and is refused *still coming up*. Before LOOK2a only a tick asked
 * again, and the first tick can wait on a remote's sync or on another driver's lock, so Settings → Workspace said no
 * repository had a line until something else happened to ask. Said once by the shell, the moment the refusals stop.
 */
export const useDriverReady = () => {
  const client = useQueryClient();
  useShenoraEvent('DAORIS', 'DRIVER_READY', () => askAgainWhenReady(client));
};

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

/** What a grant answered: the key it wrote, whether it wrote, whether a re-read says yes, and why. */
export type TrustGranted = { folder: string; key: string; changed: boolean; verified: boolean; message: string };

/**
 * The person's grant of a folder the driver is holding (D73) — the screen's half of
 * `daoris agent trust … --yes`. The shell writes it only for a hold its last tick produced, in the
 * file that tick read, and the driver looks again at once.
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
      void client.invalidateQueries({ queryKey: keys.driver });
    },
  });
};

/** One mutation shape for the two toggles: edit the file, and the loop looks now, not at the poll. */
function useDriverChange<TVariables extends Record<string, unknown>>(type: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (variables: TVariables) => call<DriverState>(type, variables),
    onSuccess: (state) => client.setQueryData(keys.driver, state),
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

/** Let a parked quest run again, counting from where it stands — `daoris driver retry <quest>`. */
export const useRetryQuest = () => useDriverChange<{ quest: string }>('RETRY_QUEST');

/** Name the agent Ask Daoris runs on, or null for none — `daoris driver helper <agent>|off` (D50, D89). */
export const useSetHelper = () => useDriverChange<{ adapter: string | null }>('SET_HELPER');

/**
 * Which agent answers an ask the declarations leave (INT4b), or null for none — the field `daoris
 * driver intake <agent>|off` edits (D50). What an intake in each circle would run on moves with it,
 * so that answer is asked again rather than left naming the agent before.
 */
export const useSetIntake = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: { adapter: string | null }) => call<DriverState>('SET_INTAKE', change),
    onSuccess: (state) => {
      client.setQueryData(keys.driver, state);
      void client.invalidateQueries({ queryKey: keys.allStarts });
    },
  });
};

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
 */
export const useSyncNow = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (workspace: string) => call<SyncNowReport>('SYNC_NOW', { workspace }),
    onSettled: () => {
      // A pass can move all of these, and a wall still recorded when it tried.
      void client.invalidateQueries({ queryKey: keys.allSync });
      void client.invalidateQueries({ queryKey: keys.allQuests });
      void client.invalidateQueries({ queryKey: keys.allSessions });
      void client.invalidateQueries({ queryKey: keys.allRegistry });
    },
  });
};

export const useSetDrivable = () => useDriverChange<{ repository: string; drivable: boolean }>('SET_DRIVABLE');
export const useSetHold = () => useDriverChange<{ repository: string; held: boolean }>('SET_HOLD');
/** Session trees (D51): the same file `daoris driver trees <repo> on|off` edits — two editors, one truth. */
export const useSetTrees = () => useDriverChange<{ repository: string; ownTree: boolean }>('SET_TREES');
