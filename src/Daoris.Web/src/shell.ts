import { useCallback, useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getBridge, useShenora, useShenoraEvent } from '@shenora/react';
import { keys } from './queries';

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

/** One mutation shape for the two toggles: edit the file, and the loop looks now, not at the poll. */
function useDriverChange<TVariables extends Record<string, unknown>>(type: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (variables: TVariables) => call<DriverState>(type, variables),
    onSuccess: (state) => client.setQueryData(keys.driver, state),
  });
}

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
    // The profile is the per-session picker (D49 §4). Omitted is not "no profile": it takes the
    // workspace's default, then the machine's, then the harness's own configuration home — the same
    // resolution a driven session gets, so a conversation is not a second set of rules.
    mutationFn: ({ repository, profile }: { repository: string; profile?: string }) =>
      call<{ sessionId: string | null; message: string }>(
        'START_CHAT', profile ? { repository, profile } : { repository }),
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
  profiles: HarnessProfile[];
};
export type HarnessRoster = {
  settingsPath: string;
  /** Which adapter this machine spawns sessions with — `driver.json`'s, shown beside the roster. */
  adapter: string;
  harnesses: HarnessReport[];
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
    mutationFn: (action: { harness: string; action: 'install' | 'update' | 'login'; profile?: string }) =>
      call<{ harness: string; action: string; exitCode: number }>('HARNESS_ACTION', action),
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
