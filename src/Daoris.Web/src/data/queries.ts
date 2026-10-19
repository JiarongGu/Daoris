import { useCallback } from 'react';
import { keepPreviousData, useMutation, useQueries, useQuery, useQueryClient, type UseQueryOptions } from '@tanstack/react-query';
import { notPushed, readPushed, usePushedLive } from '../bridge/state/slices';
import {
  api, type AskAction, HELP_REPOSITORY, type QuestAction, type Session, SESSION_ACTIVE, type SetUpRef, type SubmissionRef, type SyncStanding,
  type Verdict,
} from './api';
import { useScope } from './scope';
import { workspaceOf, workspacesOf } from './workspaces';

// The console is a cache over a service; TanStack Query is that cache done correctly (D42):
// deduplicated requests (the badge and the Quests view share one fetch), refetch on focus, and
// invalidation after every mutation instead of hand-rolled reload calls that each view remembered —
// or forgot — to make.

// The single source of query-key truth — invalidations included. A prefix written as a string
// literal at an invalidation site survives a rename silently; one written here does not.
// Every cross-repository key carries the workspace scope (WSP5; D48 §4), so a circle's answers never
// serve another's from the cache. `*` is "every circle" — the door's own meaning of an absent argument.
// The `all*` prefixes are what invalidation addresses: whatever the scope, a change moves them all.
export const keys = {
  status: ['status'] as const,
  /** The driver's last verdict per open quest — written by the tick, never fetched (its own root, so no invalidation reaches it). */
  considered: ['considered'] as const,
  /** The starts the last tick held for the agent's trust (D73) — written by the tick, like `considered`. */
  untrusted: ['untrusted'] as const,
  /** The starts the last tick held on cooling accounts, an intake's among them (UX6d) — written by the tick, like `considered`. */
  waits: ['waits'] as const,
  /** The presses the driver has not answered yet, as it last told them (STATEHUB1c) — written by the state hub, never fetched. */
  underWay: ['underWay'] as const,
  /** Ask Daoris's conversation opened ahead of the person (HELP5) — kept by the page, never fetched. */
  helpReadied: ['help-readied'] as const,
  /** Where the install's update stands (UPDATE1, D139): asked once, then kept by the shell's `UPDATE_STATE`. */
  update: ['update'] as const,
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
  // The running calls the shell pushes (WAITSAY1c2): written by the pushes alone, there is no pull beneath it.
  calls: ['driver', 'calls'] as const,
  /**
   * A repository's or a workspace's Current workflow (WORKFLOW1b) — shell-only, like the lines, and under their key, since
   * Current is drawn from the rules they resolve: every change that asks the lines again asks it again.
   */
  workflow: (scope: 'repository' | 'workspace', name: string) => ['driver', 'lines', 'workflow', scope, name] as const,
  /**
   * The process a session's own tree runs under (PROCESS1e) — shell-only, and under the lines' key, since it is the rules they
   * resolve: every change that asks the lines again asks it again.
   */
  sessionProcess: (session: string) => ['driver', 'lines', 'process', session] as const,
  /**
   * Where a session's, a quest's or an ask's work stands in its workflow (WORKFLOW1c) — shell-only, and under the driver's key,
   * so every tick, which asks the driver again, asks it again: a run is read from records that move at every look.
   */
  workflowRun: (scope: 'session' | 'quest' | 'ask', id: string) => ['driver', 'workflow-run', scope, id] as const,
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
   * The clean-up's list (WSR3, WSR5), as the shell pushes it where it does (PUSH1c4b; its spec is `sweepSlice` in `bridge/lines`):
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
  /** Whether a session's landed branch can be handed to a landing plugin now (WSR5b). */
  handOff: (session: string) => ['driver', 'hand', session] as const,
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
  /** The files in one session's tree, for `@` (CONV4d) — shell-only for the same reason. */
  treeFiles: (session: string) => ['tree-files', session] as const,
  /** One file in a session's tree, read for its preview (PREVIEW1) — shell-only for the same reason. */
  treeFile: (session: string, path: string) => ['tree-file', session, path] as const,
  /** One conversation's model and effort as its agent offers them (AGT6b) — shell-only: it is a live process's. */
  sessionOptions: (session: string) => ['session-options', session] as const,
  /** What the person first said in each session, from this machine's record (RAIL1) — shell-only. */
  allOpenings: ['openings'] as const,
  openings: (ids: string[]) => ['openings', ...ids] as const,
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
   * The second opinions this machine holds (XAGENT1g4c): the service lists them, so not under the driver's key; under the
   * sessions', so whatever asks the sessions again (a new reviewer's record) asks these again.
   */
  opinions: ['sessions', 'opinions'] as const,
  sessions: (repository: string | null, includeClosed: boolean, workspace: string | null) =>
    ['sessions', repository ?? 'all', includeClosed, workspace ?? '*'] as const,
};

export const useStatus = () =>
  useQuery({ queryKey: keys.status, queryFn: ({ signal }) => api.status(signal) });

/** Where the shell's sync slice is read (PUSH1a2; its spec is `syncSlice` in `bridge/driver`): a partition per wired circle. */
export const SYNC_SLICE = 'sync';

/** A circle's name as the service finds its remote (C#'s `OrdinalIgnoreCase`): each character upper-cased where it maps to one. */
const remoteName = (name: string) => [...name].map((character) => {
  const upper = character.toUpperCase();
  return [...upper].length === 1 ? upper : character;
}).join('');

/**
 * One circle's standing read from every wired circle's, as the shell pushes them (PUSH1a2), the way `/api/sync` answers it: the circle
 * named as asked, trimmed (`Workspaces.Normalize`), its record found as the service finds the circle's remote; a circle the shell holds
 * no record of has no remote here, which the route answers `wired: false` and nothing else, its null fields left out as the host does.
 */
export function standingOf(all: unknown, workspace: string): SyncStanding {
  const asked = workspace.trim() || 'default';
  const held = typeof all === 'object' && all !== null && !Array.isArray(all)
    ? Object.entries(all).find(([name]) => remoteName(name) === remoteName(asked))?.[1] as unknown
    : undefined;
  return typeof held === 'object' && held !== null && !Array.isArray(held)
    ? { ...(held as SyncStanding), workspace: asked }
    : { workspace: asked, wired: false, ahead: 0, behind: [], conflicts: [] };
}

/**
 * Where one circle stands with its remote (SYNC6a): ahead, behind, in conflict, when it last synced.
 * Over HTTP from this machine's own host, so a browser on this machine reads it too — only *Sync now*
 * needs the shell. Null asks nothing: "every circle" has no single standing to show.
 *
 * Where the shell pushes the standings (PUSH1a2), the circle's is read from every wired circle's entry with a stable `select`, from
 * memory: never stale and never asked on a focus, and no tick or pass reaches it. Elsewhere (a plain browser, an older shell) it is the
 * pull it always was, waiting for the window's first snapshot where one is on its way and read from it.
 */
export const useSyncStanding = (workspace: string | null) => {
  const client = useQueryClient();
  const pushed = usePushedLive(SYNC_SLICE);
  const select = useCallback((all: unknown) => standingOf(all, workspace ?? ''), [workspace]);
  const read: UseQueryOptions<unknown, Error, SyncStanding, readonly unknown[]> = pushed && workspace !== null
    ? {
      queryKey: keys.syncEvery,
      // No route answers every circle at once: until the shell has judged them, the standing is not answered yet, as a pull to a
      // service still coming up is not, and the first push writes it.
      queryFn: () => readPushed<unknown>(client, SYNC_SLICE, () => Promise.reject(new Error('where each circle stands is not judged yet'))),
      select, staleTime: Infinity, refetchOnWindowFocus: false,
    }
    : {
      queryKey: keys.sync(workspace ?? '*'),
      queryFn: async ({ signal }) => {
        const all = await readPushed<unknown>(client, SYNC_SLICE, async () => null);
        return all === null ? api.syncStanding(workspace!, signal) : standingOf(all, workspace!);
      },
      enabled: workspace !== null,
    };
  return useQuery(read);
};

export const useRepositories = () => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.repositories(workspace),
    queryFn: ({ signal }) => api.repositories(workspace, signal),
  });
};

/**
 * The registry — the window's scope by default, or the whole machine's for a choice that is the
 * machine's whatever the window shows: Settings (REV3). Scoped, a window on one circle offered a rule,
 * an account's default and a start only that circle's workspaces and repositories.
 */
export const useRegistry = (reach: 'window' | 'machine' = 'window') => {
  const { workspace: scoped } = useScope();
  const workspace = reach === 'machine' ? null : scoped;
  return useQuery({
    queryKey: keys.registry(workspace),
    queryFn: ({ signal }) => api.registry(workspace, signal),
  });
};

/** A repository's code map (MAP3a) — asked only once a person opens one. */
export const useCodeMap = (repository: string | null) =>
  useQuery({
    queryKey: keys.codeMap(repository ?? ''),
    queryFn: ({ signal }) => api.codeMap(repository!, signal),
    enabled: repository !== null,
  });

/**
 * The circles this deployment holds — the registry unscoped, because it is the authority (D48 §3)
 * and the one reader that must see every workspace is the switcher itself. A row from a host older
 * than workspaces names none and counts as `default`, so one such family stays one circle.
 */
export const useWorkspaces = () =>
  useQuery({
    queryKey: keys.registry(null),
    queryFn: ({ signal }) => api.registry(null, signal),
    select: (rows) => workspacesOf(rows),
  });

/**
 * Every workspace with how many repositories it holds, for the Workspace menu (D75). The same
 * unscoped registry answer `useWorkspaces` reads, so the two can never disagree about which exist.
 */
export const useWorkspaceHoldings = () =>
  useQuery({
    queryKey: keys.registry(null),
    queryFn: ({ signal }) => api.registry(null, signal),
    select: (rows) => {
      const members = new Map<string, string[]>();
      for (const row of rows) {
        const name = workspaceOf(row);
        members.set(name, [...(members.get(name) ?? []), row.repository]);
      }
      return [...members].sort(([a], [b]) => a.localeCompare(b))
        .map(([name, names]) => ({ name, repositories: names.length, members: names.sort() }));
    },
  });

/** Asks (D65 §1a), scoped like every other cross-repository read — an ask is made in a circle. */
/** The asks in scope; `enabled` false asks nothing, for a reader that needs them only sometimes (a park's go-aheads). */
export const useAsks = (includeClosed: boolean, enabled = true) => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.asks(includeClosed, workspace),
    queryFn: ({ signal }) => api.asks(includeClosed, workspace, signal),
    enabled,
  });
};

/**
 * Asking, publishing an ask and closing one. Each can publish a quest, so the quests move with the
 * asks — and they are asked again whether or not the door said yes: a named receiver that refused
 * still leaves the ask kept, with its proposal (INT4a), which the list must then show.
 */
function useAskChange<TVariables>(fn: (variables: TVariables) => Promise<AskAction>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSettled: () => {
      void client.invalidateQueries({ queryKey: keys.allAsks });
      void client.invalidateQueries({ queryKey: keys.allQuests });
    },
  });
}

/**
 * Deleting a record made by mistake (D95): the quests, the asks and where each circle stands all move
 * with it — a delete of an ask takes its quests, and a shared quest's delete is one more thing ahead.
 * Asked again whatever the door said, since a refused delete may still have found the record moved.
 */
function useDelete(fn: (id: string) => ReturnType<typeof api.deleteQuest>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSettled: () => {
      void client.invalidateQueries({ queryKey: keys.allQuests });
      void client.invalidateQueries({ queryKey: keys.allAsks });
      void client.invalidateQueries({ queryKey: keys.allRegistry });
      // Where the shell pushes the standings, a delete's quest write is heard on the change stream and pushed (PUSH1a2).
      void client.invalidateQueries({ queryKey: keys.allSync, predicate: notPushed(client) });
    },
  });
}

export const useDeleteQuest = () => useDelete((id) => api.deleteQuest(id));
export const useDeleteAsk = () => useDelete((id) => api.deleteAsk(id));

export const useAsk = () => useAskChange((body: Parameters<typeof api.ask>[0]) => api.ask(body));
export const usePublishAsk = () =>
  useAskChange(({ id, to }: { id: string; to: string }) => api.publishAsk(id, to));
export const useCloseAsk = () =>
  useAskChange(({ id, reason }: { id: string; reason: string }) => api.closeAsk(id, reason));
/** The person answers a go-ahead on their ask (KNOWUSE1a): every session on the ask is handed it at its next start. */
export const useAnswerGoAhead = () =>
  useAskChange(({ id, number, approved, words }: { id: string; number: number; approved: boolean; words?: string }) =>
    api.answerGoAhead(id, number, approved, words));

/** The quests in scope; `enabled` false asks nothing, for a reader that needs them only sometimes (a session branch's title, UX6h2a). */
export const useQuests = (repository: string | null, includeClosed: boolean, enabled = true) => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.quests(repository, includeClosed, workspace),
    queryFn: ({ signal }) => api.quests(repository, includeClosed, workspace, signal),
    enabled,
  });
};

/**
 * The session records the shell pushes (PUSH1d2, D182; its spec is `sessionsSlice` in `bridge/sessions`): the widest list, every
 * repository, closed included, every workspace, under the key the pull of that list reads, from which each view's list is read.
 */
export const SESSIONS_SLICE = 'sessions';

/** The call each running session is making, as the shell pushes it (WAITSAY1c2; its spec is `callsSlice` in `bridge/sessions`). */
export const CALLS_SLICE = 'calls';

/**
 * The session list's groups and entries, as the shell pushes them (PUSH1c3b; its spec is `groupsSlice` in `bridge/sessions`): what
 * `SESSION_GROUPS` answers for every session, under that pull's widest key.
 */
export const GROUPS_SLICE = 'groups';

/** The service's own name for a workspace asked of it (`Workspaces.Normalize`), its ASCII letters folded as its `NOCASE` folds them. */
const circle = (workspace: string) => (workspace.trim() || 'default').replace(/[A-Z]/g, (letter) => letter.toLowerCase());

/**
 * One view's list read from the widest the shell pushes (PUSH1d2), as the service filters `/api/sessions`: the repository named
 * exactly, the workspace as the service compares it, the open ones alone unless the closed are asked for, in the order given. The
 * widest asked for is the list itself.
 */
export function sessionsIn(all: Session[], repository: string | null, includeClosed: boolean, workspace: string | null): Session[] {
  if (repository === null && includeClosed && workspace === null) return all;
  const named = workspace === null ? null : circle(workspace);
  return all.filter((session) => (repository === null || session.repository === repository)
    && (named === null || circle(session.workspace ?? '') === named)
    && (includeClosed || SESSION_ACTIVE.has(session.state)));
}

/**
 * Where the shell pushes the sessions (PUSH1d2), every list is read from the one entry its pushes write, from memory: never stale and
 * never asked on a focus. Elsewhere (a plain browser, an older shell) each list is the pull it always was, under its own key; and while
 * a window's first snapshot is on its way, a list asked then waits for it and is read from it, never pulled beside it.
 */
function useSessionsRead(
  repository: string | null, includeClosed: boolean, workspace: string | null,
): { pushed: boolean; read: UseQueryOptions<Session[], Error, Session[]> } {
  const client = useQueryClient();
  const pushed = usePushedLive(SESSIONS_SLICE);
  const select = useCallback(
    (all: Session[]) => sessionsIn(all, repository, includeClosed, workspace), [repository, includeClosed, workspace]);
  return pushed
    ? {
      pushed,
      read: {
        queryKey: keys.sessions(null, true, null),
        queryFn: () => readPushed(client, SESSIONS_SLICE, () => api.sessions(null, true, null)),
        select, staleTime: Infinity, refetchOnWindowFocus: false,
      },
    }
    : {
      pushed,
      read: {
        queryKey: keys.sessions(repository, includeClosed, workspace),
        queryFn: async ({ signal }) => {
          const all = await readPushed<Session[] | null>(client, SESSIONS_SLICE, async () => null);
          return all === null ? api.sessions(repository, includeClosed, workspace, signal) : sessionsIn(all, repository, includeClosed, workspace);
        },
      },
    };
}

/** Session records are read-only here: the controls act where a driver is attached (D46 §6). */
/** The sessions in scope; `enabled` false asks nothing, for a reader that needs them only sometimes (a review's title, UX6c). */
export const useSessions = (repository: string | null, includeClosed: boolean, enabled = true) => {
  const { workspace } = useScope();
  const { read } = useSessionsRead(repository, includeClosed, workspace);
  return useQuery({ ...read, enabled });
};

/**
 * The second opinions on this machine (XAGENT1g4c), asked only where the sessions list holds a reviewer (`enabled`). A 404 (a
 * host that is not this machine's) or any error answers nothing, and no join is claimed.
 */
export const useOpinions = (enabled: boolean) => useQuery({
  queryKey: keys.opinions,
  queryFn: ({ signal }) => api.opinions(signal),
  enabled,
  retry: false,
});

/**
 * Ask Daoris's conversations (HELP1a, D89), across every workspace: it belongs to none, so the scope a
 * person is working in must not hide the conversation beside it.
 */
export const useHelpSessions = (
  /**
   * How often to ask again while a conversation the person wrote to is still to go on (ASKHIST1): its record moves to working
   * as the driver takes the words up, which a desktop window is told as `sessions` (FABRIC-EVENT5a), so the caller gives it only
   * to a window with no bus. False asks only when the list is stale.
   */
  refetchInterval: number | false = false,
) => {
  // Pushed (PUSH1d2): its record's every move is pushed, so it is never asked again on a clock.
  const { pushed, read } = useSessionsRead(HELP_REPOSITORY, true, null);
  return useQuery({ ...read, refetchInterval: pushed ? false : refetchInterval });
};

/**
 * How a view asks a knowledge read (FRAME1f): only while it is in front, since the application holds Search and
 * Convergence on every view; and, where a new question replaces the last, holding the last answer while the new one
 * is on its way, so the list is never blanked between two answers (platform language §4, *Loading*).
 */
export type KnowledgeAsk = { enabled?: boolean; holding?: boolean };

/** One document, read on demand — an entry's page, cached like every other read. */
export const useEntry = (id: string | null, enabled = true) =>
  useQuery({
    queryKey: keys.entry(id ?? ''),
    queryFn: ({ signal }) => api.entry(id ?? '', signal),
    enabled: enabled && id !== null,
  });

/** Several documents, each read on demand and cached as one entry: a finding's entries, read whole (FRAME1f). */
export const useEntries = (ids: readonly string[], enabled = true) =>
  useQueries({
    queries: ids.map((id) => ({
      queryKey: keys.entry(id),
      queryFn: ({ signal }: { signal: AbortSignal }) => api.entry(id, signal),
      enabled,
    })),
  });

export const useConvergence = (minimumSimilarity: number, { enabled = true, holding = false }: KnowledgeAsk = {}) => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.convergence(minimumSimilarity, workspace),
    queryFn: ({ signal }) => api.convergence(minimumSimilarity, workspace, signal),
    enabled,
    placeholderData: holding ? keepPreviousData : undefined,
  });
};

export const useSearch = (q: string, localOnly: boolean, { enabled = true, holding = false }: KnowledgeAsk = {}) => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.search(q, localOnly, workspace),
    queryFn: ({ signal }) => api.search(q, localOnly, workspace, signal),
    enabled: enabled && q.length >= 2,
    placeholderData: holding ? keepPreviousData : undefined,
  });
};

/**
 * Everything a quest mutation can change: every quests query, the registry's counts, and the asks — an
 * ask is done once every quest it became has closed (USE1c), which the service derives on each read, so
 * closing the last one changes what the asks list says.
 */
function useInvalidateQuestWork() {
  const client = useQueryClient();
  return () => {
    void client.invalidateQueries({ queryKey: keys.allQuests });
    void client.invalidateQueries({ queryKey: keys.allRegistry });
    void client.invalidateQueries({ queryKey: keys.allAsks });
  };
}

export const usePublishQuest = () => {
  const invalidate = useInvalidateQuestWork();
  return useMutation({
    mutationFn: (draft: Parameters<typeof api.publishQuest>[0]) => api.publishQuest(draft),
    onSuccess: invalidate,
  });
};

export const useRespondQuest = () => {
  const invalidate = useInvalidateQuestWork();
  return useMutation({
    mutationFn: ({ id, action, reason }: { id: string; action: 'take' | 'done' | 'decline'; reason: string | null }) =>
      api.respondQuest(id, action, reason),
    onSuccess: invalidate,
  });
};

/**
 * The person's yes to a done's departure from what they required (DRIFT1d2, D133 §4): the service publishes the step the
 * departure held and a quest waiting on it goes on, so the quests and the asks are read again, as a respond's are.
 */
export const useAcceptQuest = () => {
  const invalidate = useInvalidateQuestWork();
  return useMutation({ mutationFn: (id: string) => api.acceptQuest(id), onSuccess: invalidate });
};

/**
 * The person's done (QUESTCLOSE1, D126's note): their words to the service's own door for it, from the quest's page and from
 * a finish at a checkpoint. A chain's next step may be published and the ask it came from may be done now, so the quests and
 * the asks are read again, as a respond's are.
 */
/**
 * What a review's press moves (REVIEWENV1g): the quests (a held step lets go, a set-up step is published), the asks (the
 * person's words are kept there), and every landing's plan, whose gate it answers (REVIEWENV1c), with the sessions' groups that
 * read the same gate. Asked again whatever the door said: a stale verdict was refused because the record moved.
 */
function useReviewChange<TVariables>(fn: (variables: TVariables) => Promise<QuestAction>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSettled: () => {
      void client.invalidateQueries({ queryKey: keys.allQuests });
      void client.invalidateQueries({ queryKey: keys.allAsks });
      void client.invalidateQueries({ queryKey: keys.allLandings });
      // What is read under the sessions' key, but the records where the shell pushes them (PUSH1d2): their pushes write them.
      void client.invalidateQueries({ queryKey: keys.allSessions, predicate: notPushed(client) });
    },
  });
}

/**
 * The person's verdict on a review (REVIEWENV1g, D154 point 8): `reviewed` or `not-yet` on the set-up the view drew, or a skip
 * of the review of a quest's work. The host refuses one whose set-up a newer one followed, in its sentence (REVIEWENV1b3).
 */
export const useReviewVerdict = () => useReviewChange(
  ({ id, verdict, words, setUp }: { id: string; verdict: Verdict; words?: string | null; setUp?: SetUpRef | null }) =>
    api.reviewQuest(id, verdict, words, setUp));

/** The person's *Set it up in `<environment>`* (REVIEWENV1b, design §3.6): a set-up step following a done quest. */
export const usePublishSetUpStep = () => useReviewChange(
  ({ id, environment }: { id: string; environment: string }) => api.setUpStep(id, environment));

/** The person's review choice on an ask, or its intake's proposal applied (REVIEWENV1b, design §1.5). */
export const useChooseAskWorkflow = () =>
  useAskChange(({ id, kind, workflow }: { id: string; kind: string; workflow?: string | null }) => api.chooseAskWorkflow(id, kind, workflow));
export const useChooseAskReview = () =>
  useAskChange(({ id, choice, words }: { id: string; choice: string; words?: string }) => api.chooseAskReview(id, choice, words));

/**
 * The person's reopen of a sealed submission (FABRIC-CLOSE1c3, §E5; D156): the step it published is withdrawn or held, so the quests
 * and the asks are read again, as a respond's are.
 */
export const useReopenQuest = () => {
  const invalidate = useInvalidateQuestWork();
  return useMutation({
    mutationFn: ({ id, submission, note }: { id: string; submission: SubmissionRef; note: string | null }) =>
      api.reopenQuest(id, submission, note),
    onSuccess: invalidate,
  });
};

/** The person's word to go on with a step its source's reopen held (FABRIC-CLOSE1c3): its hold goes, and the driver carries it on. */
export const useContinueQuest = () => {
  const invalidate = useInvalidateQuestWork();
  return useMutation({ mutationFn: (id: string) => api.continueQuest(id), onSuccess: invalidate });
};

export const usePersonDone = () => {
  const invalidate = useInvalidateQuestWork();
  return useMutation({
    mutationFn: ({ id, note }: { id: string; note: string | null }) => api.personDone(id, note),
    onSuccess: invalidate,
  });
};

/**
 * Answer a driven session that parked to ask the person (STANDDOWN2): its record stays parked with
 * their words (ANSWER1b), and the same session goes on with them at the driver's next tick (D131). The
 * sessions are read again, so the page shows it going on (ANSWER1c) rather than still waiting.
 */
export const useAnswerSession = () => {
  const invalidate = useInvalidateQuestWork();
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, answer }: { id: string; answer: string | null }) => api.answerSession(id, answer),
    onSuccess: () => {
      invalidate();
      // The record's move is pushed where the shell pushes the sessions (PUSH1d2): the service's change stream tells it.
      void client.invalidateQueries({ queryKey: keys.allSessions, predicate: notPushed(client) });
    },
  });
};

/** Dismiss one conflict (SYNC6c) — and where each circle stands moves with it. */
export const useDismissConflict = () => {
  const invalidate = useInvalidateQuestWork();
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, machine, sequence }: { id: string; machine: string; sequence: number }) =>
      api.dismissConflict(id, machine, sequence),
    onSuccess: () => {
      invalidate();
      // Where the shell pushes the standings, the dismissal's quest write is heard on the change stream and pushed (PUSH1a2).
      void client.invalidateQueries({ queryKey: keys.allSync, predicate: notPushed(client) });
    },
  });
};

/**
 * Bring this machine's lost take forward (SYNCMERGE1c2, D178 point 3), invalidating as a dismissal does. Read again on a refusal too:
 * a bring-forward that lost again is refused, and it left the quest a conflict it did not have (the fresh take's).
 */
export const useForwardConflict = () => {
  const invalidate = useInvalidateQuestWork();
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, machine, sequence }: { id: string; machine: string; sequence: number }) =>
      api.forwardConflict(id, machine, sequence),
    onSettled: () => {
      invalidate();
      void client.invalidateQueries({ queryKey: keys.allSync, predicate: notPushed(client) });
    },
  });
};

/** What a re-scan can change: the index and what reads it — never a quest, a session, or the tick's own answers. */
const FED_BY_THE_INDEX = new Set<unknown>([
  keys.status[0], keys.allRepositories[0], keys.allRegistry[0], 'code-map', 'entry', 'convergence', 'search',
]);

export const useRefreshIndex = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => api.refresh(),
    // 🔴 Only what the index feeds (REV3). Everything was invalidated once — the tick-written answers
    // too, whose "fetch" is an empty list, so each refresh wiped why a quest is sitting and the trust
    // holds until the next tick, and ran `git` again for an open review.
    onSuccess: () => void client.invalidateQueries({ predicate: (query) => FED_BY_THE_INDEX.has(query.queryKey[0]) }),
  });
};

/**
 * The registration lifecycle (D48 §3/§7).
 *
 * A registration change moves who is on the map, what each is wired to, and what the index will read
 * next — so all three invalidate the same broad set rather than each guessing which views care.
 */
function useInvalidateRegistry() {
  const client = useQueryClient();
  return () => {
    void client.invalidateQueries({ queryKey: keys.allRegistry });
    void client.invalidateQueries({ queryKey: keys.allRepositories });
  };
}

/**
 * Import a folder's repositories (D75's *Import a folder…*), into a named workspace when one is given
 * (D77), then re-read what the registry holds.
 */
export const useImportFolder = () => {
  const invalidate = useInvalidateRegistry();
  return useMutation({
    mutationFn: ({ folder, workspace }: { folder: string; workspace?: string }) => api.importFolder(folder, workspace),
    onSuccess: invalidate,
  });
};

export const useRegisterRepository = () => {
  const invalidate = useInvalidateRegistry();
  return useMutation({
    mutationFn: (body: Parameters<typeof api.registerRepository>[0]) => api.registerRepository(body),
    onSuccess: invalidate,
  });
};

export const useWireRepository = () => {
  const invalidate = useInvalidateRegistry();
  return useMutation({
    mutationFn: ({ repository, workspace }: { repository: string; workspace: string }) =>
      api.wireRepository(repository, workspace),
    onSuccess: invalidate,
  });
};

export const useRetireRepository = () => {
  const invalidate = useInvalidateRegistry();
  return useMutation({
    mutationFn: (repository: string) => api.retireRepository(repository),
    onSuccess: invalidate,
  });
};
