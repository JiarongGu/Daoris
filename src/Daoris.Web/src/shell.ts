import { useCallback, useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getBridge, useShenora, useShenoraEvent } from '@shenora/react';
import { keys } from './queries';
import type { Consideration } from './signals';
// The shape lives beside the components that render it, so a molecule can name it without
// importing this module (SURF6).
import type { SessionDiff } from './work/diff';

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
  /** The Daoris home (D63): the directory every machine-local file lives in. Absent on an older shell. */
  home?: string;
  /**
   * What establishing the home did on this start, when it is worth saying — state moved in from a
   * profile directory, or the account's environment gaining the variable. Null when nothing was.
   */
  homeNotice?: string | null;
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

export const useSetDrivable = () => useDriverChange<{ repository: string; drivable: boolean }>('SET_DRIVABLE');
export const useSetHold = () => useDriverChange<{ repository: string; held: boolean }>('SET_HOLD');
/** Session trees (D51): the same file `daoris driver trees <repo> on|off` edits — two editors, one truth. */
export const useSetTrees = () => useDriverChange<{ repository: string; ownTree: boolean }>('SET_TREES');

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

  // Defensive about the shape, deliberately: a shell older than this surface answers something else
  // entirely, and the console is the part of the drawer that may be missing. The RECORD above it is
  // what the drawer exists to show, and a console cannot be allowed to take it down.
  const take = useCallback((tail: SessionTail | undefined) => {
    setLive(Boolean(tail?.live));
    setDropped(tail?.dropped ?? 0);
    setLines((held) => {
      const fresh = (tail?.lines ?? []).filter((line) => line.sequence > seen.current);
      if (fresh.length === 0) return held;
      seen.current = fresh[fresh.length - 1]!.sequence;
      return [...held, ...fresh].slice(-CONSOLE_LINES);
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
        .then(take)
        .catch(() => {});
      return;
    }

    take({ ...batch, live: true });
  });

  return { lines, live, dropped };
}

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
 * This machine's harnesses, and the accounts they run as (D49 §4, D50).
 *
 * @remarks
 * **Shell-only, like every machine-local surface** — a browser over a keyed remote has no business
 * knowing which tools are installed on somebody's laptop, let alone which accounts they hold. The
 * profile HOME is a filesystem path, which is the sharpest reason this rides the bridge and has no
 * HTTP route (D47 §4).
 *
 * **Daoris manages directories and names, never secrets.** `login` runs the harness's own flow into a
 * profile directory and streams it through the console; nothing here reads, stores or forwards a
 * credential, and `login` is only ever the person pressing something.
 */
export type HarnessProfile = { name: string; home: string; login: 'in' | 'out' | 'unknown' };
export type HarnessReport = {
  harness: string;
  present: boolean;
  version: string | null;
  problem: string | null;
  machineDefault: string | null;
  /** The version this machine pinned, or null for whatever is on `PATH` (TOOL2/D57). */
  pinned: string | null;
  /**
   * The managed binary actually installed at that pin, or null.
   *
   * @remarks
   * `pinned` without `managed` is a pin naming a version nobody installed — which **refuses every
   * spawn** rather than quietly running `PATH`, so a surface must say so rather than imply the pin
   * is in force.
   */
  managed: string | null;
  /**
   * Whether this harness can be pinned at all — false where it declares no package for Daoris to
   * fetch. The control is **absent** there rather than present and refusing: half a control is
   * worse than none, which is the same rule the palette and the parked session's moves follow.
   */
  pinnable: boolean;
  profiles: HarnessProfile[];
};
export type HarnessRoster = {
  settingsPath: string;
  /** Which adapter this machine spawns sessions with — `driver.json`'s, shown beside the roster. */
  adapter: string;
  harnesses: HarnessReport[];
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
      action: 'install' | 'update' | 'login' | 'pin' | 'unpin'
      | 'profile-add' | 'profile-remove' | 'profile-default';
      profile?: string;
      /** Which version to install and pin to — `pin` only (TOOL2/D57). */
      version?: string;
      /** Which circle a default is for — `profile-default` only (D49 §4); absent means the machine's. */
      workspace?: string;
    }) => call<{ harness: string; action: string; exitCode: number }>('HARNESS_ACTION', action),
    onSuccess: () => void client.invalidateQueries({ queryKey: keys.harnesses }),
  });
};

/** Ask the tools again rather than answering from before — the person pressing "look again". */
export const useRefreshHarnesses = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => call<HarnessRoster>('HARNESSES', { refresh: true }),
    onSuccess: (roster) => client.setQueryData(keys.harnesses, roster),
  });
};

/** One message into the session. False means it ended while the person was typing — an answer. */
export const useSendMessage = () =>
  useMutation({
    mutationFn: (message: { id: string; text: string }) =>
      call<{ sent: boolean }>('SESSION_INPUT', message),
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

export const useStopSession = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => call<{ stopped: boolean }>('STOP_SESSION', { id }),
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

export const useMergeSessionTree = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => call<TreeAct>('MERGE_SESSION_TREE', { id }),
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
