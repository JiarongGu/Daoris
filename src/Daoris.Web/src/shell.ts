import { useCallback, useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getBridge, useShenora, useShenoraEvent } from '@shenora/react';
import { keys } from './queries';
import type { Consideration, TrustHold } from './signals';
import type { ToolDoor } from './tools';
// The shape lives beside the components that render it, so a molecule can name it without
// importing this module (SURF6).
import type { SessionDiff } from './work/diff';
import { type ChatMessage, type EventPage, mergeEvents, type SessionEvent } from './work/conversation';
import { toUpload } from './attachments';
import type { WiringAnswer } from './map/wiring';
import type { AgentRulesState, RuleListName, RuleScopeName } from './settings/AgentRules';
import type { LineChange, RepositoryLine } from './settings/Lines';
import type { LandingChange, LandingRule, RepositoryLanding } from './settings/Landings';
import type { SweepBranch } from './settings/Sweep';

export type { DiffFile, SessionDiff } from './work/diff';

// The shell's half of the platform (D46 §6). In a browser none of this exists — the bridge is absent,
// the query never runs, and every control gated on it stays unrendered. That is the design, not a
// degradation: the controls act where a driver is attached, and only the desktop has one.

/**
 * The driver's standing state, as the page reads it. The DAORIS.DRIVER module answers more (its
 * config path, cap, adapter, poll interval); the fields land here when a surface reads them.
 */
export type DriverState = {
  drivable: string[];
  holds: string[];
  /** Repositories whose sessions open their own worktree instead of the registered root (D51). */
  trees: string[];
  /** Session ids with a live process right now — what "stop" can actually reach. */
  running: string[];
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
   * profile directory, or the account's environment gaining the variable. Null when nothing was.
   */
  homeNotice?: string | null;
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
};

const call = <TData,>(type: string, payload?: Record<string, unknown>): Promise<TData> =>
  getBridge().invoke<TData>('DAORIS.DRIVER', type, payload ? { payload } : {});

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
 * The person's answer to a session parked at a checkpoint (design §4).
 *
 * @remarks
 * **Three moves, and it goes through the driver.** `awaiting-person` has meant "only the person can
 * clear this" since D46; the ledger allows a fourth move from it — back to `working` — and that one
 * is the driver observing a session that carried on, which a person causes by *answering* it. So
 * this carries exactly `completed`, `declined` and `stopped`, and the host refuses anything else
 * with a sentence.
 *
 * It lands on the driver rather than on the service because the process and the record must move
 * together: this machine lets the process go, and only then does the record say it ended.
 */
export const useResolveSession = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (move: { id: string; state: 'completed' | 'declined' | 'stopped'; note?: string }) =>
      call<{ session: string; state: string; message: string }>('RESOLVE_SESSION', {
        id: move.id,
        state: move.state,
        ...(move.note ? { note: move.note } : {}),
      }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.allSessions });
      void client.invalidateQueries({ queryKey: keys.allQuests });
      void client.invalidateQueries({ queryKey: keys.driver });
    },
  });
};

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

/**
 * Which agent answers an ask the declarations leave (INT4b), or null for none — the field `daoris
 * driver intake <agent>|off` edits (D50). What an intake in each circle would run on moves with it,
 * so that answer is asked again rather than left naming the agent before.
 */
/** Name the agent Ask Daoris runs on, or null for none — `daoris driver helper <agent>|off` (D50, D89). */
export const useSetHelper = () => useDriverChange<{ adapter: string | null }>('SET_HELPER');

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

/** What a pass said: the wall it hit, and what the remote understood and did not take — the driver's words. */
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

/**
 * Every repository's line here and what said so (WSR2). Desktop-only: the guess is read off a
 * checkout, and only this machine holds one.
 */
export const useLines = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.lines,
    queryFn: () => call<{ lines: RepositoryLine[]; landings?: RepositoryLanding[] }>('LINES'),
    enabled: isAvailable,
  });
};

/**
 * Set a repository's line or a workspace's, or clear it with no branch — the file `daoris driver
 * line` edits (D50). What every repository's line is moves with it, so that answer is asked again.
 */
/**
 * Set how work lands in a repository or a workspace, or clear it with no form — the file `daoris driver
 * landing` edits (D50). Every repository's answer moves with it, so that is asked again.
 */
export const useSetLanding = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: LandingChange) => call<DriverState>('SET_LANDING', change),
    onSuccess: (state) => {
      client.setQueryData(keys.driver, state);
      void client.invalidateQueries({ queryKey: keys.lines });
      void client.invalidateQueries({ queryKey: keys.allLandings });
    },
  });
};

/**
 * Every session branch here with what it holds (WSR3, D88) — the clean-up's list. Desktop-only: it is
 * read off this machine's checkouts.
 */
export const useSweepPlan = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.sweep,
    queryFn: () => call<{ branches: SweepBranch[] }>('SWEEP_PLAN'),
    enabled: isAvailable,
    // It asks git in every repository with a checkout here: a minute is fresh enough to read a session by.
    staleTime: 60_000,
  });
};

/** The clean-up's press: only the branches the list showed to go, each judged again by the driver. */
export const useSweep = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (only: string[]) =>
      call<{ results: { branch: SweepBranch; removed: boolean; message: string }[]; removed: number }>('SWEEP', { only }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.sweep });
      void client.invalidateQueries({ queryKey: keys.allSessions });
    },
  });
};

export const useSetLine = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: LineChange) => call<DriverState>('SET_LINE', change),
    onSuccess: (state) => {
      client.setQueryData(keys.driver, state);
      void client.invalidateQueries({ queryKey: keys.lines });
    },
  });
};

/**
 * What the shell can say about a folder on this machine (D48 §7) — the one thing a page cannot find
 * out for itself, because a browser may never learn a filesystem path (D46/D47 §4).
 */
export type FolderInspection = {
  path: string;
  name: string;
  exists: boolean;
  adopted: boolean;
  git: boolean;
  summary?: string;
  owns: string[];
  accepts: string[];
  packs: string[];
  join: boolean;
  shareKnowledge: boolean;
};

/**
 * Ask the shell for a folder. Null is the person cancelling the dialog, which is an answer.
 *
 * @remarks
 * Not a query: it opens a modal, so it happens when the person asks and never on a refetch. The
 * registering that follows is an ordinary call to the loopback host — routing it through IPC too
 * would be a second door onto the same judgement.
 */
export const usePickFolder = () =>
  useMutation({
    mutationFn: () =>
      getBridge().invoke<FolderInspection | null>('DAORIS.REGISTRY', 'PICK_FOLDER', {}),
  });

/**
 * Edit a repository's own `daoris.json` through a form instead of a text editor (D48 §7).
 *
 * @remarks
 * Deliberately a different act from re-wiring, and kept visibly apart in the UI: this writes a TRACKED
 * file in that repository, and the diff lands uncommitted for its own review flow. Doctrine — rules,
 * knowledge, skills — stays unwritable from every surface (D31); the manifest is inert data (D26).
 */
export type DeclarationEdit = {
  path: string;
  summary: string;
  owns: string[];
  accepts: string[];
  join: boolean;
  shareKnowledge: boolean;
};

export const useWriteDeclaration = () =>
  useMutation({
    mutationFn: (edit: DeclarationEdit) =>
      getBridge().invoke<FolderInspection>('DAORIS.REGISTRY', 'WRITE_DECLARATION', { payload: edit }),
  });

/**
 * The machine's wiring: which deployment serves each workspace here (D48 §5, D50).
 *
 * @remarks
 * Shell-only, like every control — and for a sharper reason than the others. This is machine-local
 * state with a credential in it, so the service deliberately has no route onto it: a browser over a
 * keyed remote must never be able to read where a machine syncs, let alone re-point it.
 *
 * The key travels IN — the person pastes one here, on their own machine — and never comes back out:
 * `remotes[].key` is the audit prefix the deployment's own `keys list` prints, nothing more.
 */
export type MachineWiring = {
  path: string;
  /** True when the environment names the machine's remote, whole — the file is not read at all. */
  fromEnvironment: boolean;
  remotes: { workspace: string; url: string; key: string }[];
};

const callRemotes = <TData,>(type: string, payload?: Record<string, unknown>): Promise<TData> =>
  getBridge().invoke<TData>('DAORIS.REMOTES', type, payload ? { payload } : {});

export const useRemotes = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.remotes,
    queryFn: () => callRemotes<MachineWiring>('STATE'),
    enabled: isAvailable,
  });
};

/** Wire a workspace to its deployment, or take it off the map. Both answer with the whole wiring. */
function useWiringChange<TVariables extends Record<string, unknown>>(type: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (variables: TVariables) => callRemotes<MachineWiring>(type, variables),
    onSuccess: (state) => client.setQueryData(keys.remotes, state),
  });
}

export const useWireRemote = () =>
  useWiringChange<{ workspace: string; url: string; key: string }>('SET');

export const useUnwireRemote = () => useWiringChange<{ workspace: string }>('REMOVE');

/**
 * Daoris's browser, as a Settings domain (CHR5, CHR7): its favorites, shown in a Daoris folder on its
 * bookmarks bar, and whether other software's Chrome extensions are offered or refused.
 *
 * @remarks
 * Shell-only, like every machine domain: the same files `daoris browser` edits and `daoris-browser`
 * reads each time it starts, which the service has no route onto (D47 §4).
 */
export type BrowserSettingsState = {
  favoritesPath: string;
  favorites: { url: string; title: string }[];
  /** Why the favorites file gave none, when it was there and could not be read. */
  favoritesProblem: string | null;
  settingsPath: string;
  extensions: 'offer' | 'refuse';
  /** Which browser sessions drive and the person opens (BRW12). */
  browser: 'daoris' | 'edge';
  /** Whether this machine has an Edge for that choice to start. */
  edgeFound: boolean;
  /** The profile of Daoris's that Edge runs on — never the person's default, which cannot be driven. */
  edgeProfile: string;
  settingsProblem: string | null;
};

const callBrowser = <TData,>(type: string, payload?: Record<string, unknown>): Promise<TData> =>
  getBridge().invoke<TData>('DAORIS.BROWSER', type, payload ? { payload } : {});

export const useBrowserSettings = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.browserSettings,
    queryFn: () => callBrowser<BrowserSettingsState>('STATE'),
    enabled: isAvailable,
  });
};

/** An edit to the browser's files. Each answers with the whole state, as the remotes' do. */
function useBrowserChange<TVariables extends Record<string, unknown>>(type: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (variables: TVariables) => callBrowser<BrowserSettingsState>(type, variables),
    onSuccess: (state) => client.setQueryData(keys.browserSettings, state),
  });
}

export const useAddFavorite = () => useBrowserChange<{ address: string; title?: string }>('ADD_FAVORITE');

export const useRemoveFavorite = () => useBrowserChange<{ address: string }>('REMOVE_FAVORITE');

export const useSetExtensions = () => useBrowserChange<{ extensions: 'offer' | 'refuse' }>('SET_EXTENSIONS');

export const useSetBrowser = () => useBrowserChange<{ browser: 'daoris' | 'edge' }>('SET_BROWSER');

/**
 * Open one of this build's secondary windows (D55 §b, SURF8): the monitor, or one session detached.
 *
 * @remarks
 * **The one thing a page cannot do for itself**, like naming a folder — a window belongs to the
 * shell. In a browser this simply rejects, which is why every surface that offers it is gated on a
 * shell being here rather than on the call succeeding.
 *
 * **Pressing it twice is safe**: one window per name is the framework's contract, and the second
 * press brings the window forward. That is what a person means by it.
 *
 * Nothing is cached. The person can close one of these with its own close button and no page would
 * hear about it, so an "is the monitor open" answer would be stale more often than it was right.
 */
export const useOpenWindow = () => useMutation({
  mutationFn: (name: string) =>
    getBridge().invoke<{ opened: boolean; windows: string[] }>(
      'DAORIS.WINDOWS', 'OPEN', { payload: { name } }),
});

/**
 * Daoris's own browser (D78): the window the person signs in to and watches an agent use. Not a route
 * into this bundle like the others — it shows pages that are not Daoris's, with no bridge.
 */
export const useOpenBrowser = () => useMutation({
  mutationFn: () =>
    getBridge().invoke<{ opened: boolean; windows: string[] }>('DAORIS.WINDOWS', 'OPEN_BROWSER', {}),
});

/**
 * A session's console, live (D49 §2).
 *
 * @remarks
 * **Desktop-only, structurally.** Output is transcript-class material — it can carry machine paths,
 * and a transcript never leaves the machine that produced it (D47 §4) — so it arrives over the
 * shell's bridge and has no HTTP route at all. A browser over a keyed remote sees the session's
 * record, as it always did, and never its stream.
 *
 * The backlog is asked for once on open; live lines arrive as `SESSION_OUTPUT` batches. The driver's
 * sequence numbers are what make those two sources one stream: anything already seen is dropped, and
 * a gap — a batch that does not continue where the last one ended — is closed by asking the driver
 * for what is missing rather than by rendering a hole nobody can see.
 */
export type ConsoleLine = { sequence: number; text: string };
export type SessionTail = {
  session: string;
  lines: ConsoleLine[];
  sequence: number;
  /** Whether more is coming. False once the session's process has ended. */
  live: boolean;
  /** Lines that fell out of the driver's bounded window before the page asked. Shown, never hidden. */
  dropped: number;
};

/** The page keeps what the driver keeps: enough to read, never a log file in a render tree. */
const CONSOLE_LINES = 500;

export function useSessionConsole(sessionId: string | null) {
  const { isAvailable } = useShenora();
  const [lines, setLines] = useState<ConsoleLine[]>([]);
  const [live, setLive] = useState(false);
  const [dropped, setDropped] = useState(0);
  // The newest sequence the page holds — read inside the event handler, which must not re-subscribe
  // every time a line arrives.
  const seen = useRef(0);
  // Which session this console is for NOW, so an answer asked for another one is dropped (REV3).
  const attended = useRef(sessionId);
  attended.current = sessionId;

  // Defensive about the shape, deliberately: a shell older than this surface answers something else
  // entirely, and the console is the part of the drawer that may be missing. The RECORD above it is
  // what the drawer exists to show, and a console cannot be allowed to take it down.
  //
  // 🔴 MERGED by sequence, whichever source lands first (REV3) — as `mergeEvents` does for the
  // conversation. Filtered by "newer than the newest held", a live batch that beat the backlog's answer
  // moved that mark past the whole backlog, and it was thrown away without a word.
  const take = useCallback((tail: SessionTail | undefined) => {
    setLive(Boolean(tail?.live));
    setDropped(tail?.dropped ?? 0);
    setLines((held) => {
      const known = new Set(held.map((line) => line.sequence));
      const fresh = (tail?.lines ?? []).filter((line) => !known.has(line.sequence));
      if (fresh.length === 0) return held;
      const merged = [...held, ...fresh].sort((a, b) => a.sequence - b.sequence).slice(-CONSOLE_LINES);
      seen.current = Math.max(seen.current, merged[merged.length - 1]!.sequence);
      return merged;
    });
  }, []);

  useEffect(() => {
    seen.current = 0;
    setLines([]);
    setLive(false);
    setDropped(0);
    if (!isAvailable || !sessionId) return;

    let current = true;
    void getBridge()
      .invoke<SessionTail>('DAORIS.DRIVER', 'TAIL_SESSION', { payload: { id: sessionId } })
      .then((tail) => { if (current) take(tail); })
      // A console that failed to load is a quiet absence, not a toast: the record above it is the
      // thing the drawer exists to show, and it is already there.
      .catch(() => {});

    return () => { current = false; };
  }, [isAvailable, sessionId, take]);

  useShenoraEvent('DAORIS', 'SESSION_OUTPUT', (payload) => {
    const batch = payload as SessionTail | undefined;
    if (!sessionId || batch?.session !== sessionId || !batch.lines?.length) return;

    const first = batch.lines[0]!.sequence;
    if (seen.current > 0 && first > seen.current + 1) {
      // Something was missed. Ask for it rather than showing two halves as though they joined.
      void getBridge()
        .invoke<SessionTail>('DAORIS.DRIVER', 'TAIL_SESSION', { payload: { id: sessionId, after: seen.current } })
        // Only while this console is still that session's: the answer may land after a switch.
        .then((tail) => { if (attended.current === sessionId && tail?.session === sessionId) take(tail); })
        .catch(() => {});
      return;
    }

    take({ ...batch, live: true });
  });

  return { lines, live, dropped };
}

/**
 * One thing a session runs beside itself (CONSOLE2): a subagent its harness spawned, or background
 * work it started, as a console stream of its own. `key` is what `useSessionConsole` tails it by.
 */
export type SessionStreamRow = {
  key: string;
  kind: string;
  name: string;
  live: boolean;
  /** How it ended, in the wire's word — null while it runs. */
  state: string | null;
};

/**
 * A session's streams, oldest first (CONSOLE2).
 *
 * @remarks
 * Desktop-only for the console's reason (D47 §4). Asked for on open, and again whenever the driver
 * says one of this session's streams opened or ended (`SESSION_STREAMS`). Defensive about the shape,
 * as the console is: a shell older than this answers something else, and then the session simply has
 * no streams.
 */
export function useSessionStreams(sessionId: string | null): SessionStreamRow[] {
  const { isAvailable } = useShenora();
  const [streams, setStreams] = useState<SessionStreamRow[]>([]);
  const [asked, setAsked] = useState(0);
  const attended = useRef(sessionId);
  attended.current = sessionId;

  useEffect(() => setStreams([]), [sessionId]);

  useEffect(() => {
    if (!isAvailable || !sessionId) return;
    let current = true;
    void getBridge()
      .invoke<{ session?: string; streams?: unknown }>('DAORIS.DRIVER', 'SESSION_STREAMS', { payload: { id: sessionId } })
      .then((answer) => {
        if (!current || attended.current !== sessionId) return;
        const rows = Array.isArray(answer?.streams) ? answer.streams : [];
        setStreams(rows.filter((row): row is SessionStreamRow =>
          typeof row?.key === 'string' && typeof row?.name === 'string' && typeof row?.kind === 'string'));
      })
      .catch(() => {});
    return () => { current = false; };
  }, [isAvailable, sessionId, asked]);

  useShenoraEvent('DAORIS', 'SESSION_STREAMS', (payload) => {
    if (sessionId && (payload as { session?: string } | undefined)?.session === sessionId) setAsked((n) => n + 1);
  });

  return streams;
}

/**
 * A session's conversation (D76, CONV1): its record read back a page at a time, and its live events
 * merged in as the driver writes them.
 *
 * @remarks
 * **The console's two-source rule, for events.** The newest page is asked for once on open, and
 * live batches arrive as `SESSION_EVENTS`. The driver's sequence numbers make them one record:
 * anything already held is dropped, and a batch that skips ahead is a batch that missed something,
 * closed by asking for what came `after` the last held — never shown as two halves joined.
 *
 * **It outlives a restart**, which the console does not: the history is the record under the home,
 * so a conversation that happened while this window was closed still reads back.
 *
 * Desktop-only for the console's reason (D47 §4): a browser has no bridge, and holds nothing here.
 */
export function useSessionEvents(sessionId: string | null) {
  const { isAvailable } = useShenora();
  const [events, setEvents] = useState<SessionEvent[]>([]);
  const [earlier, setEarlier] = useState(false);
  // What the session was first asked, where the newest page does not hold it (SESS1): a long run reads from it.
  const [opening, setOpening] = useState<SessionEvent | null>(null);
  // Where the first failed call began, wherever it is in the run (SESS1 S9) — the jump's target.
  const [firstFailure, setFirstFailure] = useState<number | null>(null);
  // Whether the history has answered, so an empty record reads as "nothing said" only once it is one.
  const [loaded, setLoaded] = useState(false);
  // The newest sequence held, and which session it belongs to — read inside the event handler, which
  // must not re-subscribe every time an event arrives.
  const latest = useRef(0);
  const attended = useRef<string | null>(null);

  const history = useCallback(
    (payload: Record<string, unknown>) =>
      getBridge().invoke<EventPage>('DAORIS.DRIVER', 'SESSION_HISTORY', { payload }),
    [],
  );

  const hold = useCallback((incoming: readonly SessionEvent[]) => {
    if (incoming.length === 0) return;
    latest.current = Math.max(latest.current, incoming[incoming.length - 1]!.seq);
    setEvents((held) => mergeEvents(held, incoming));
  }, []);

  useEffect(() => {
    attended.current = sessionId;
    latest.current = 0;
    setEvents([]);
    setEarlier(false);
    setOpening(null);
    setFirstFailure(null);
    setLoaded(false);
    if (!isAvailable || !sessionId) return;

    let current = true;
    void history({ id: sessionId })
      .then((page) => {
        if (!current || !page) return;
        hold(page.events ?? []);
        setEarlier(Boolean(page.earlier));
        setOpening(page.opening ?? null);
        setFirstFailure(typeof page.firstFailure === 'number' ? page.firstFailure : null);
      })
      // A record that failed to load is a quiet absence: the session's head above it is already there.
      .catch(() => {})
      .finally(() => { if (current) setLoaded(true); });

    return () => { current = false; };
  }, [isAvailable, sessionId, history, hold]);

  useShenoraEvent('DAORIS', 'SESSION_EVENTS', (payload) => {
    const batch = payload as { session?: string; events?: SessionEvent[] } | undefined;
    const id = attended.current;
    if (!id || batch?.session !== id || !batch.events?.length) return;

    const first = batch.events[0]!.seq;
    if (latest.current > 0 && first > latest.current + 1) {
      void history({ id, after: latest.current })
        .then((page) => { if (attended.current === id) hold(page?.events ?? []); })
        .catch(() => {});
      return;
    }

    hold(batch.events);
  });

  /** The page before the oldest held — "load earlier". */
  const loadEarlier = useCallback(async () => {
    const id = attended.current;
    const oldest = events[0]?.seq;
    if (!id || oldest === undefined) return;
    try {
      const page = await history({ id, before: oldest });
      if (attended.current !== id || !page) return;
      setEvents((held) => mergeEvents(held, page.events ?? []));
      setEarlier(Boolean(page.earlier));
    } catch {
      // Nothing more to show is the same to a reader as nothing more held; the button stays.
    }
  }, [events, history]);

  return { events, opening, firstFailure, earlier, loaded, loadEarlier };
}

/** Where a conversation's turns stand, as the driver holds them (CONV4a). */
export type SessionTurns = {
  /** What the person sent that has not reached the harness yet, in the order sent, with its files' names. */
  queued: ChatMessage[];
  /** A turn is on its way to the harness or running there — what stopping the turn acts on. */
  taking: boolean;
};

/** Nothing known: nothing waiting and nothing to stop — what the composer offers until the driver answers. */
export const NO_TURNS: SessionTurns = { queued: [], taking: false };

/** Messages as the driver told them, read defensively: anything that is not one is left out. */
const messagesOf = (list: unknown): ChatMessage[] =>
  (Array.isArray(list) ? list : []).flatMap((item) => {
    const message = item as Partial<ChatMessage> | null;
    if (typeof message?.text !== 'string') return [];
    return [{
      text: message.text,
      files: Array.isArray(message.files) ? message.files.filter((name): name is string => typeof name === 'string') : [],
    }];
  });

/** The driver's answer, read defensively: anything that is not a queue is nothing waiting and nothing running. */
const turnsOf = (answer: unknown): SessionTurns => {
  const held = answer as { queued?: unknown; taking?: unknown } | null | undefined;
  return { queued: messagesOf(held?.queued), taking: held?.taking === true };
};

/** Whether the driver's answer says anything about a turn: one that does not is no answer at all. */
const answersTurn = (answer: unknown): boolean =>
  typeof (answer as { taking?: unknown } | null | undefined)?.taking === 'boolean';

/**
 * Where each live conversation's turns stand (CONV4b, UX5 U17): whether one is in flight, and what
 * is waiting behind it — asked for once per conversation, then followed as `SESSION_QUEUED`.
 *
 * @remarks
 * **The driver is the authority**, not the record: the record learns a turn began when its first
 * event lands, a waiting message is in no record at all, and a chat's record says `working` for as
 * long as its process lives, between turns too. So the composer's stop and its queue follow this,
 * and so do the rail and the head, which read a chat between turns as idle (`shownState`).
 *
 * **One listener for every conversation**, where the composer's own hook listened for the attended
 * one: the rail needs them all, and two listeners for one event are two subscriptions to keep in
 * step. A conversation the driver has not answered for is absent from the answer, never "not
 * taking": nothing known is not a claim that nothing runs.
 *
 * Each live answer is the whole state, so a missed one costs nothing. Desktop-only: a conversation
 * is a process on this machine.
 */
export function useChatTurns(ids: readonly string[]): Record<string, SessionTurns> {
  const { isAvailable } = useShenora();
  const [turns, setTurns] = useState<Record<string, SessionTurns>>({});
  const key = [...new Set(ids)].sort().join('\n');
  const watched = useRef<ReadonlySet<string>>(new Set());

  useEffect(() => {
    const wanted = key ? key.split('\n') : [];
    watched.current = new Set(wanted);
    // What is no longer watched is forgotten, so a conversation opened again is asked again.
    setTurns((held) => Object.fromEntries(Object.entries(held).filter(([id]) => watched.current.has(id))));
    if (!isAvailable) return;

    let current = true;
    for (const id of wanted) {
      void getBridge().invoke<unknown>('DAORIS.DRIVER', 'SESSION_QUEUE', { payload: { id } })
        .then((answer) => {
          if (current && watched.current.has(id) && answersTurn(answer)) {
            setTurns((held) => ({ ...held, [id]: turnsOf(answer) }));
          }
        })
        // Nothing known is nothing claimed: the composer offers what it can prove.
        .catch(() => {});
    }
    return () => { current = false; };
  }, [isAvailable, key]);

  useShenoraEvent('DAORIS', 'SESSION_QUEUED', (payload) => {
    const state = payload as { session?: string } | undefined;
    const session = state?.session;
    if (!session || !watched.current.has(session) || !answersTurn(state)) return;
    setTurns((held) => ({ ...held, [session]: turnsOf(state) }));
  });

  return turns;
}

/** What stopping a turn did (CONV4a): whether a turn was asked to stop, and what came back unsent. */
export type TurnStop = { cancelled: boolean; withdrawn: ChatMessage[] };

/**
 * Stop the conversation's turn and keep the conversation (CONV4a) — the third verb, beside finishing
 * it and stopping it. What was waiting comes back, for the composer to hand to the person.
 */
export const useCancelTurn = () =>
  useMutation({
    mutationFn: async (id: string): Promise<TurnStop> => {
      const answer = await call<{ cancelled?: unknown; withdrawn?: unknown }>('CANCEL_TURN', { id });
      return { cancelled: answer?.cancelled === true, withdrawn: messagesOf(answer?.withdrawn) };
    },
  });

/**
 * A conversation with an agent in one repository (D49 §3).
 *
 * @remarks
 * Shell-only, because a chat is a process on this machine and processes never leave the driver
 * (D46 §7). The record is the service's — a teammate sees that a chat happened — and only the stream
 * and the typing are here.
 *
 * Daoris makes no model calls: the harness carries the model and the conversation, and these three
 * verbs move text and nothing else (D24, `model-decoupling`).
 */
export const useStartChat = () => {
  const client = useQueryClient();
  return useMutation({
    // Every part after the repository is omittable, and omitted means something: the profile takes
    // the workspace's default, then the machine's, then the harness's own configuration home (D49
    // §4); the adapter takes `driver.json`'s; and the tree falls back to the repository's standing
    // opt-in (D51). The same resolution a driven session gets, so a conversation is not a second
    // set of rules — which is why each is dropped from the payload rather than sent as a null.
    mutationFn: (start: {
      repository: string; profile?: string; adapter?: string; ownTree?: boolean;
    }) => call<{ sessionId: string | null; message: string }>('START_CHAT', {
      repository: start.repository,
      ...(start.profile ? { profile: start.profile } : {}),
      ...(start.adapter ? { adapter: start.adapter } : {}),
      ...(start.ownTree ? { ownTree: true } : {}),
    }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.allSessions });
      void client.invalidateQueries({ queryKey: keys.driver });
    },
  });
};

/**
 * Ask Daoris's conversation (HELP1a, D89): the one this machine is running, carried on, or a new one in
 * its room. Refused in the driver's words while no agent is named for it (D89: off until named).
 */
export const useStartHelp = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => call<{ sessionId: string | null; message: string; running?: boolean }>('START_HELP'),
    onSuccess: () => void client.invalidateQueries({ queryKey: keys.allSessions }),
  });
};

/**
 * This machine's harnesses, and the accounts they run as (D49 §4, D50).
 *
 * @remarks
 * **Shell-only, like every machine-local surface** — a browser over a keyed remote has no business
 * knowing which tools are installed on somebody's laptop, let alone which accounts they hold. The
 * profile HOME is a filesystem path, which is the sharpest reason this rides the bridge and has no
 * HTTP route (D47 §4).
 *
 * **A sign-in stays the tool's.** `login` runs the harness's own flow into a profile directory and
 * streams it through the console; nothing here reads, stores or forwards a sign-in, and `login` is
 * only ever the person pressing something. The one secret that crosses this bridge is an API key a
 * person types (`key-add`, D67 §1), once, inward — answered only by its last four characters.
 */
export type HarnessRoster = {
  settingsPath: string;
  /** Which adapter this machine spawns sessions with — `driver.json`'s, shown beside the roster. */
  adapter: string;
  /**
   * One row per door, in `tools.ts`'s one type for it (REV3 CLEAN1: this payload had two types, and
   * five readers cast one to the other).
   */
  harnesses: ToolDoor[];
};

/**
 * What sessions on this machine consumed (TOOL3/D57 §4) — measured before it is managed.
 *
 * @remarks
 * 🔴 **Machine-local, by an inherited rule.** Per-account usage names a credential profile, and a
 * profile name is already served only over loopback — so this rides the bridge like the console and
 * the diff, and has no HTTP route at all.
 *
 * **The source is the protocol door.** ACP reports context pressure per turn; the pipe door gives
 * text. So a pipe-door session appears here not at all, and a surface says *not measured* rather
 * than showing a zero.
 *
 * **No price is claimed.** Daoris does not know what a token costs (D24); these are counts somebody
 * else's tool volunteered.
 */
export type UsedSession = {
  session: string;
  repository: string;
  harness: string;
  /** The account it ran as, or null for the harness's own configuration home. */
  profile: string | null;
  /** Context held at its high-water mark, and the window it was held against. */
  used: number;
  size: number;
  when: string;
};
export type UsedAccount = {
  harness: string;
  profile: string | null;
  sessions: number;
  used: number;
};

export const useUsage = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.usage,
    queryFn: () => call<{ sessions: UsedSession[]; accounts: UsedAccount[] }>('USAGE'),
    enabled: isAvailable,
  });
};

export const useHarnesses = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.harnesses,
    queryFn: () => call<HarnessRoster>('HARNESSES'),
    enabled: isAvailable,
    // Detection spawns a process per harness, so this is not something to re-run on every focus. The
    // roster has a refresh, and every action refreshes it — which is when it can actually have changed.
    staleTime: Infinity,
  });
};

/**
 * This machine's plugins (D64): folders under the home's `plugins/`, each with what it declares,
 * what it speaks on, whether its process is up, and why it contributes nothing when it does not.
 *
 * @remarks
 * Shell-only for the usual reason — a plugin's folder is a machine path (D47 §4) — and read off
 * the same catalogue the driver reads each tick, so what the page shows is what the loop has.
 */
export type PluginEntry = {
  id: string;
  name: string;
  version: string;
  description: string;
  /** The person's word (`plugins.json`), independent of whether the plugin is sound. */
  enabled: boolean;
  /** Why it contributes nothing, in the driver's own sentence; null when sound. */
  problem: string | null;
  /** The harnesses it declares — configurations of the ACP door. */
  harnesses: string[];
  /** The points it listens on, when it speaks. */
  points: string[];
  /** Whether its hook process is up right now. */
  running: boolean;
  folder: string;
  data: string;
};
export type PluginCatalog = { folder: string; plugins: PluginEntry[] };

export const usePlugins = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.plugins,
    queryFn: () => call<PluginCatalog>('PLUGINS'),
    enabled: isAvailable,
  });
};

/**
 * What an agent Daoris starts may do (PERM1, D72): Claude Code's own rules in Daoris's scopes, from the
 * one file under the home the driver composes each spawn from. Desktop only — the file is machine-local.
 */
export const useRules = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.rules,
    queryFn: () => call<AgentRulesState>('RULES'),
    enabled: isAvailable,
  });
};

/** The screen's half of `daoris agent rules` (D50): an edit to the same file, answered with the state after it. */
export const useRuleAction = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (action:
      | { action: 'add'; list: RuleListName; rule: string; scope: RuleScopeName; name?: string }
      | { action: 'remove'; rule: string; scope: RuleScopeName; name?: string }
      | { action: 'default'; id: string; on: boolean }) =>
      call<AgentRulesState>('RULE_ACTION', action),
    onSuccess: (state) => client.setQueryData(keys.rules, state),
  });
};

/**
 * The screen's half of `daoris agent rules accept|decline` (PERM2, D74): the person's answer to an
 * agent's proposal, answered with the rules after it. 🔴 The only way a widening an agent proposed applies.
 */
export const useRuleProposal = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (answer: { id: string; accept: boolean; note?: string }) =>
      call<AgentRulesState>('RULE_PROPOSAL', answer),
    onSuccess: (state) => client.setQueryData(keys.rules, state),
  });
};

/** The screen's half of `daoris plugin enable|disable|remove` (D50): a row, or the folder gone with the data named. */
export const usePluginAction = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (action: { id: string; action: 'enable' | 'disable' | 'remove' }) =>
      call<{ id: string; action: string; data: string | null }>('PLUGIN_ACTION', action),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.plugins });
      // A declared harness came or went with it, so the roster is asked again too.
      void client.invalidateQueries({ queryKey: keys.harnesses });
    },
  });
};

/**
 * Install, update, or log a profile in — each by that harness's OWN mechanism.
 *
 * @remarks
 * Never automatic and never mid-session (D49 §4): a tool that changed under a running loop is a
 * moving target nobody diffed. The output arrives as `SESSION_OUTPUT` under `<harness>:<action>`, so
 * the same console component renders it.
 */
export const useHarnessAction = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (action: {
      harness: string;
      action: 'install' | 'update' | 'login' | 'login-new' | 'key-add' | 'pin' | 'unpin'
      | 'profile-add' | 'profile-remove' | 'profile-default';
      profile?: string;
      /**
       * An API key — `key-add` only (AGT3, D67 §1). It crosses the bridge once, inward; the answer
       * and the roster name it only by its last four characters.
       */
      key?: string;
      /** Which version to install and pin to — `pin` only (TOOL2/D57). */
      version?: string;
      /** Which circle a default is for — `profile-default` only (D49 §4); absent means the machine's. */
      workspace?: string;
    }) => call<{
      harness: string; action: string; exitCode?: number; started?: boolean;
      /** For `key-add`: the account made, and the key's handle. */
      profile?: string; key?: string;
    }>('HARNESS_ACTION', action),
    onSuccess: () => void client.invalidateQueries({ queryKey: keys.harnesses }),
  });
};

/** How a process action ended — as news, after the request that started it was answered. */
export type HarnessEnded = {
  harness: string;
  action: string;
  profile: string | null;
  exitCode: number;
  /** The driver's own sentence when the process failed after it had started; null when it simply exited. */
  problem: string | null;
  /** Who signed in, for a sign-in — the tool's own answer (D66 §3); null when it did not say. */
  account?: string | null;
  /** For a sign-in to another account: whether it left one behind — only when it finished. */
  kept?: boolean | null;
};

/**
 * A harness action's end, as news (2026-09-23). An install waits on a network and a login on a
 * person in a browser — longer than any request may take on the bridge — so `HARNESS_ACTION`
 * answers `started` and the end arrives here, the same way a conversation's ending does (D49 §3).
 * The roster is asked again when it does.
 */
export const useHarnessEnded = (handler: (ended: HarnessEnded) => void) => {
  const client = useQueryClient();
  useShenoraEvent('DAORIS', 'HARNESS_ENDED', (payload) => {
    const ended = payload as HarnessEnded | undefined;
    if (!ended?.harness || !ended.action) return;
    void client.invalidateQueries({ queryKey: keys.harnesses });
    handler(ended);
  });
};

/**
 * Answer the prompt a running harness action printed — the sign-in code a login asks to have pasted
 * (2026-09-23). The host refuses it, naming the action, when nothing is running under that name, so
 * a code pasted after the login ended never looks delivered.
 */
export const useHarnessInput = () =>
  useMutation({
    mutationFn: (input: { harness: string; action: string; text: string }) =>
      call<{ sent: boolean }>('HARNESS_INPUT', input),
  });

/** Stop a running harness action — a login the person is not going to finish. */
export const useHarnessCancel = () =>
  useMutation({
    mutationFn: (target: { harness: string; action: string }) =>
      call<{ cancelled: boolean }>('HARNESS_CANCEL', target),
  });

/** Ask the tools again rather than answering from before — the person pressing "look again". */
export const useRefreshHarnesses = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => call<HarnessRoster>('HARNESSES', { refresh: true }),
    onSuccess: (roster) => {
      client.setQueryData(keys.harnesses, roster);
      // Looking again also lets a refused account through (AGT3b), which changes what a start takes.
      void client.invalidateQueries({ queryKey: keys.allStarts });
    },
  });
};

/** What a start in each of these workspaces would take (MAP1b). Desktop-only: it is this machine's. */
export const useStarts = (workspaces: string[]) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.starts(workspaces),
    queryFn: () => call<WiringAnswer>('STARTS', { workspaces }),
    enabled: isAvailable && workspaces.length > 0,
  });
};

/**
 * One message into the session. False means it ended while the person was typing — an answer.
 *
 * @remarks
 * Its files (CONV4c) go as names and bytes, the way a quest's uploads do, and the driver keeps them
 * for the conversation outside its tree — the page never learns where. A message with none sends none.
 */
export const useSendMessage = () =>
  useMutation({
    // A preface (HELP1b) is where the person is, handed to the agent ahead of the words; absent for most.
    mutationFn: async (message: { id: string; text: string; files?: File[]; preface?: string }) => {
      const files = message.files?.length ? await Promise.all(message.files.map(toUpload)) : [];
      return call<{ sent: boolean }>('SESSION_INPUT', {
        id: message.id, text: message.text, ...(files.length > 0 ? { files } : {}),
        ...(message.preface ? { preface: message.preface } : {}),
      });
    },
  });

/**
 * Finish a conversation: the harness gets end-of-input, says what it was going to say, and exits.
 * `useStopSession` is the other verb and means something else — the person cut it off.
 */
export const useEndChat = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => call<{ ended: boolean }>('END_CHAT', { id }),
    onSuccess: () => void client.invalidateQueries({ queryKey: keys.allSessions }),
  });
};

/**
 * What the driver answered a stop: whether it ended anything, and whether what it ended was an
 * orphan — a record that said it ran when nothing on this machine ran it (2026-09-25).
 */
export type StopAnswer = { stopped: boolean; orphan?: boolean; elsewhere?: boolean };

/**
 * The notice for a stop, from the driver's answer — never the person's words for an ending that was
 * not theirs. Both doors to a stop (the frame's, the quest record's) say it the same way.
 */
export const stopNotice = (answer: StopAnswer) =>
  answer.orphan ? 'quests.session.orphanEnded'
    : answer.stopped ? 'quests.session.stopped'
      // Another Daoris process here runs it — a terminal's — so the record still says working (REV3).
      : answer.elsewhere ? 'quests.session.runElsewhere'
        : 'quests.session.notRunning';

export const useStopSession = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => call<StopAnswer>('STOP_SESSION', { id }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.allSessions });
      void client.invalidateQueries({ queryKey: keys.driver });
    },
  });
};

/**
 * What a session actually did (SURF6, design §5).
 *
 * @remarks
 * **Shell-only, structurally** — the same rule as the console (D47 §4): it is read off a checkout on
 * this machine, so a browser has nothing to ask and is never asked to. `enabled` gates on the bridge
 * for that reason rather than as an optimisation.
 *
 * **It is not fetched until a person looks.** A diff costs several `git` processes, and the rail
 * changes far more often than anyone opens a review — so this is keyed by session and left to the
 * pane that renders it, never prefetched alongside the record.
 *
 * A refusal is the host's own sentence and reaches the person verbatim: "no tree here", "no range
 * recorded", "git could not read it" are three different facts, and each names which.
 */
export const useSessionDiff = (session: string | null) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.diff(session ?? ''),
    queryFn: () => call<SessionDiff>('SESSION_DIFF', { id: session }),
    enabled: isAvailable && Boolean(session),
    // A landed session's work does not change under the reader; a running one's does, but a review
    // is read at the end. Refetching on focus would re-run git every time the window is touched.
    refetchOnWindowFocus: false,
    retry: false,
  });
};

/**
 * What the person first said in each of these sessions (RAIL1): a conversation's identity, as
 * `sessionTitle` takes it — read from this machine's own record, never from the session record, which
 * travels (D47 §4).
 *
 * @remarks
 * **Keyed by the whole set**, sorted, so every region that names sessions from the same list — the rail,
 * the head — asks once between them. A conversation is named by its first message, so when one arrives
 * for a session not yet named, the answer is asked for again.
 */
export const useSessionOpenings = (sessions: readonly { id: string }[] | undefined): Record<string, string> => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const ids = [...new Set((sessions ?? []).map((session) => session.id))].sort();
  const answer = useQuery({
    queryKey: keys.openings(ids),
    queryFn: () => call<{ openings?: Record<string, string> }>('SESSION_OPENINGS', { ids }),
    enabled: isAvailable && ids.length > 0,
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  });
  const openings = answer.data?.openings ?? {};

  useShenoraEvent('DAORIS', 'SESSION_EVENTS', (payload) => {
    const batch = payload as { session?: string; events?: SessionEvent[] } | undefined;
    if (!batch?.session || openings[batch.session]) return;
    if (batch.events?.some((event) => event.kind === 'user' && event.origin === 'person')) {
      void client.invalidateQueries({ queryKey: keys.allOpenings });
    }
  });

  return openings;
};

/** Where a search found its words: the session, the event it began at, whose words, and a window of them. */
export type SessionHit = { session: string; seq: number; kind: string; snippet: string };

/**
 * What sessions said, searched (RAIL1): the person's words and the agent's, on this machine's own
 * record, bounded by the host and saying so (`cut`). Asked from two letters on — one letter matches
 * everything — and the caller debounces, so a word typed is one question, not one per key.
 */
export const useSessionSearch = (query: string, session?: string) => {
  const { isAvailable } = useShenora();
  const q = query.trim();
  return useQuery({
    queryKey: keys.sessionSearch(q, session),
    // Within one session, where one is named (SESS1 S9): its words and its calls by their titles.
    queryFn: () => call<{ query: string; hits: SessionHit[]; cut: boolean }>(
      'SESSION_SEARCH', { q, ...(session ? { session } : {}) }),
    enabled: isAvailable && q.length >= 2,
    staleTime: 10_000,
    refetchOnWindowFocus: false,
    retry: false,
  });
};

/** The files a person may `@` in a session's tree, and how many more the host's bound left out. */
export type TreeFiles = { session: string; files: string[]; unlisted: number };

/**
 * The files in the session's tree, for the composer's `@` (CONV4d).
 *
 * @remarks
 * **Asked for only while a mention is being written** (`wanted`), and then kept by session, so the
 * list is one `git` call per tree rather than one per keystroke. It goes stale after a while, because
 * the agent writes files as it works and a file it wrote a minute ago is worth offering.
 *
 * Shell-only for the diff's reason: it is read off a checkout on this machine. A refusal is the
 * host's sentence, and it says the typed path still reaches the agent.
 */
export const useTreeFiles = (session: string | null, wanted: boolean) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.treeFiles(session ?? ''),
    queryFn: () => call<TreeFiles>('SESSION_FILES', { id: session }),
    enabled: isAvailable && Boolean(session) && wanted,
    staleTime: 15_000,
    refetchOnWindowFocus: false,
    retry: false,
  });
};

/**
 * The two acts on a reviewed session (SURF6b, D51 rules 6–7).
 *
 * @remarks
 * **A refusal is an answer**, not an error: the repository's checkout is busy, the tree holds work
 * nobody merged, there is nothing to merge. Each comes back as `{ done: false, message }` with the
 * tree layer's own sentence, because the person's next move differs for each — so these resolve
 * rather than throw, and the surface renders `message` whichever way it went.
 *
 * **Discard asks twice on purpose.** The unforced call is what produces the sentence naming what
 * would be lost; `force` is the person saying it again, meaning it. Nothing destroys work as a side
 * effect of tidying.
 */
export type TreeAct = { session: string; done: boolean; message: string };

/**
 * What accepting this session would do under its repository's landing rule (WSR1, D87): merge into
 * the line, or the branch it would make — said before the press. Only for a session with a tree here.
 */
export const useLanding = (id: string | null) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.landing(id ?? ''),
    queryFn: () => call<{ session: string; form?: string; target?: string; source?: string }>('LANDING', { id }),
    enabled: isAvailable && id !== null,
  });
};

/** Accept a session: its work lands as its repository's rule says — merged, or put on a branch to push. */
export const useLandSessionTree = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => call<TreeAct & { branch?: string }>('LAND_SESSION_TREE', { id }),
    onSuccess: (result) => {
      // Only a merge that happened changes what a diff or a removal would say.
      if (result.done) {
        void client.invalidateQueries({ queryKey: keys.diff(result.session) });
        void client.invalidateQueries({ queryKey: keys.allSessions });
      }
    },
  });
};

export const useDiscardSessionTree = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (act: { id: string; force?: boolean }) =>
      call<TreeAct>('DISCARD_SESSION_TREE', { id: act.id, ...(act.force ? { force: true } : {}) }),
    onSuccess: (result) => {
      if (result.done) {
        void client.invalidateQueries({ queryKey: keys.diff(result.session) });
        void client.invalidateQueries({ queryKey: keys.allSessions });
      }
    },
  });
};
