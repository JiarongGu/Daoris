import { useEffect, useSyncExternalStore } from 'react';
import { type QueryClient, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../queries';
// The shape lives beside the components that render it, so a molecule can name it without
// importing this module (SURF6).
import { REVIEW_BOUND_MINUTES, type SessionDiff } from '../work/diff';
import type { OpinionDetail, OpinionGate, OpinionWait } from '../work/opinion';
import type { TreeFile } from '../work/preview';
import type { ReviewWaits } from '../work/review';
import { call, pluginBound } from './call';

export type { DiffFile, SessionDiff } from '../work/diff';
export type { TreeFile } from '../work/preview';

// A session's tree on this machine (MOD3): what it did, the files in it, and the acts on it once
// reviewed: land it by its repository's rule, hand its branch on, or discard it (D51, WSR1, WSR5).

/** How long the page waits for a review (REVIEW4): the page's patience, named in minutes beside the shape. */
export const reviewBound = REVIEW_BOUND_MINUTES * 60_000;

/** How long a review nobody looks at stays held: a return within it is served from it (REVIEW4). */
const REVIEW_KEPT = 30 * 60_000;

/** How long a live session's review is fresh: the moment every view's query is (`main.tsx`). */
const LIVE_FRESH = 15_000;

/** A review asked of the host and not answered yet: the one read per session, and when it began. */
type Reading = { answer: Promise<SessionDiff>; since: number };

/**
 * The reads in flight, by session, for each query cache (REVIEW4). The bridge cannot stop a call it has sent, so git
 * reads on after the person leaves; a return within that read waits on it rather than starting a second, and counts
 * from its start. Kept beside the cache whose answer it becomes, so a second cache (a test's) never meets another's.
 */
const reads = new WeakMap<QueryClient, Map<string, Reading>>();
const readsOf = (client: QueryClient): Map<string, Reading> => {
  let held = reads.get(client);
  if (!held) {
    held = new Map();
    reads.set(client, held);
  }
  return held;
};

/** Who is told when a read begins or ends: the panes that say for how long. */
const readers = new Set<() => void>();
const tell = () => readers.forEach((reader) => reader());
const listen = (reader: () => void) => {
  readers.add(reader);
  return () => {
    readers.delete(reader);
  };
};

/**
 * When the host's read of this session's review began, while one is in flight (REVIEW4): the review counts from it, so
 * a return to a read still running says how long it has really been.
 *
 * @remarks Observed rather than read once: the query says it is fetching before its read begins (its first answer is
 * optimistic), so nothing about the query changes when the read does, and a pane that read the start once kept null.
 */
export const useReadingSince = (session: string | null): number | null => {
  const client = useQueryClient();
  return useSyncExternalStore(listen, () => (session ? readsOf(client).get(session)?.since ?? null : null), () => null);
};

/**
 * The review's answer, waited on until the person leaves (REVIEW4).
 *
 * @remarks
 * **Leaving cancels the page's wait, not git.** The query hands its signal here, so a review that unmounts mid-read is
 * cancelled and reverts: no clock runs and no state is held for a pane nobody looks at. The host's read goes on, since
 * nothing on the bridge stops it, and what it answers with nobody waiting is put where a return reads first: it is the
 * answer that return would otherwise ask git for again. A refusal with nobody waiting is dropped; a return asks.
 *
 * **An act that changed the tree asks afresh.** A landing or a discard invalidates the review; a read begun before it
 * answers for the tree as it was, so the read again starts its own, and the earlier one's answer is dropped.
 */
function readDiff(client: QueryClient, session: string, signal: AbortSignal): Promise<SessionDiff> {
  const reading = readsOf(client);
  const key = keys.diff(session);
  let held = reading.get(session);
  if (!held || client.getQueryState(key)?.isInvalidated) {
    const entry: Reading = {
      answer: call<SessionDiff>('SESSION_DIFF', { id: session }, { timeoutMs: reviewBound }),
      since: Date.now(),
    };
    held = entry;
    reading.set(session, entry);
    tell();
    const settle = () => {
      if (reading.get(session) !== entry) return;
      reading.delete(session);
      tell();
    };
    entry.answer.then(
      (diff) => {
        // A read a newer one replaced answers for an older tree: never laid over what the newer one says.
        const current = reading.get(session) === entry;
        settle();
        // Registered before any wait below, so a wait still running sees this first and its own resolve sets the data.
        if (current && client.getQueryState(key)?.fetchStatus !== 'fetching') client.setQueryData(key, diff);
      },
      settle,
    );
  }

  const answer = held.answer;
  return new Promise<SessionDiff>((resolve, reject) => {
    const leave = () => reject(signal.reason);
    if (signal.aborted) {
      leave();
      return;
    }
    signal.addEventListener('abort', leave, { once: true });
    answer.then(
      (diff) => {
        signal.removeEventListener('abort', leave);
        resolve(diff);
      },
      (error: unknown) => {
        signal.removeEventListener('abort', leave);
        reject(error);
      },
    );
  });
}

/** What the review keeps a session's answer by: its record's last move, and whether it still runs. */
export type ReviewRecord = { updated: string; live: boolean };

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
 * **An ended session's answer is kept** (REVIEW4): its committed range does not move once it ended, so a
 * second open is the first's answer, at once, for as long as nothing has moved since — an answer read
 * before the record's last move (it ended, it parked) is read again. A live session's is read again on
 * an open once the moment every view is fresh for has passed, the last answer held beneath it meanwhile;
 * and the record moving past the answer while the review is open reads it again then. A landing, a
 * discard, the clean-up and bringing up to date ask again of their own accord, since each moves a tree.
 * The page waits {@link reviewBound}, not the bridge's 30 seconds.
 *
 * A refusal's code is what the page words it by: "no tree here", "no range recorded", "git could not
 * read it" are different facts with different next moves, and the review says each (`ReviewFailed`).
 */
export const useSessionDiff = (session: string | null, record?: ReviewRecord | null) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const moved = record ? Date.parse(record.updated) : Number.NaN;
  const settled = record && !record.live ? moved : null;
  const query = useQuery({
    queryKey: keys.diff(session ?? ''),
    queryFn: ({ signal }) => readDiff(client, session!, signal),
    enabled: isAvailable && Boolean(session),
    // Final once it ended and read after that, and read again on the next open if it was read before. A live session's
    // is fresh for the moment every view's is (`main.tsx`), so a glance at another tab and back does not run git again.
    staleTime: (held) => {
      if (settled === null) return LIVE_FRESH;
      return held.state.dataUpdatedAt >= settled ? Infinity : 0;
    },
    gcTime: REVIEW_KEPT,
    // A landed session's work does not change under the reader; a running one's does, but a review
    // is read at the end. Refetching on focus would re-run git every time the window is touched.
    refetchOnWindowFocus: false,
    retry: false,
  });

  // The record moved past the answer while the review is open (it ended, parked or went on): read it again now. Only
  // a move of the record asks again, never the answer's own arrival, so the effect follows `moved` alone.
  const { refetch, dataUpdatedAt, isFetching } = query;
  useEffect(() => {
    if (!isFetching && dataUpdatedAt > 0 && moved > dataUpdatedAt) void refetch({ cancelRefetch: false });
  }, [moved]);

  return query;
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
 * One file in the session's tree, read for its preview in the side bar (PREVIEW1, D111).
 *
 * @remarks
 * **Read when a door opens it**, and again on the preview's own *read it again*: the agent may be writing
 * the file, so nothing here is held as fresh for long, and nothing is fetched on focus, which would read
 * the disk every time the window is touched.
 *
 * Shell-only for the diff's reason: it is read off a checkout on this machine. A refusal is the host's
 * sentence — outside the tree, a link that leads out of it, git's own folder, no tree here, not a file now.
 */
export const useTreeFile = (session: string | null, path: string | null) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.treeFile(session ?? '', path ?? ''),
    queryFn: () => call<TreeFile>('SESSION_FILE', { id: session, path }),
    enabled: isAvailable && Boolean(session) && Boolean(path),
    refetchOnWindowFocus: false,
    retry: false,
  });
};

/**
 * The review's patch for one file, where the review already holds one (PREVIEW1, D111): read from the
 * review's own answer, never asked of git, so a preview opened from a tool card before anyone looked at
 * the review says nothing of changes rather than running a diff of its own.
 */
export const useReviewedPatch = (session: string | null, path: string | null): string | null => {
  const client = useQueryClient();
  const reviewed = useQuery({
    queryKey: keys.diff(session ?? ''),
    // The review's own read, should anything ever run it from here: one read per session, within the review's bound.
    queryFn: ({ signal }) => readDiff(client, session!, signal),
    // Never asked from here: the review asks, and this reads what it was answered.
    enabled: false,
  });
  return (path && reviewed.data?.files.find((file) => file.path === path)?.patch) || null;
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
 * Where the rule names a plugin (D100), which one pushes it, and the sentence a press would be refused
 * with where that plugin cannot land work now. And the review's gate while it holds the work (REVIEWENV1c), which the page
 * draws where *Accept…* would be (REVIEWENV1g); absent where nothing waits for a review, and from a shell older than it. And the
 * second opinion's gate before it (XAGENT1f), wherever a level asks one, which the page draws beside the review's (XAGENT1g).
 */
export const useLanding = (id: string | null) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.landing(id ?? ''),
    queryFn: () => call<{
      session: string; form?: string; target?: string; source?: string; plugin?: string; problem?: string; review?: ReviewWaits | null;
      opinion?: OpinionGate | null;
      workflow?: import('../workflow/gate').WorkflowGateState | null;
    }>('LANDING', { id }),
    enabled: isAvailable && id !== null,
  });
};

/**
 * What a press at the second opinion's gate came to (XAGENT1f's `OpinionPresses`): whether it was taken, the driver's sentence
 * (a refusal is an answer, as a landing's is), and the gate after it with the findings beside their answers (XAGENT1g).
 */
export type OpinionPressed = {
  session?: string; done: boolean; message: string; opinion?: OpinionGate | string | null; detail?: OpinionDetail | null;
};

/**
 * A session's second opinion at its gate, with each finding beside the working session's answer (XAGENT1g; the second-agent
 * design §9): what the review's *Second opinion* draws. Shell-only: the opinion is this machine's (D47 §4).
 */
export const useOpinionGate = (id: string | null) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.opinion(id ?? ''),
    queryFn: () => call<OpinionPressed & { opinion?: OpinionGate | null }>('OPINION_GATE', { id }),
    enabled: isAvailable && id !== null,
  });
};

/**
 * What of the second opinion's gates waits on the person on this machine (XAGENT1g, design §9): a dispute, a required opinion
 * none could be had for, and commits nobody read where the work lands by itself, oldest first. *What needs you* lists them.
 * A shell older than the route answers nothing, which reads as none. `enabled` lets a caller ask it only while there is work
 * it could name: an opinion is owed only on work Sessions' list places *To review*.
 */
export const useOpinionWaits = (enabled = true) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.opinionWaits,
    queryFn: async () => {
      const answer = await call<{ waits?: OpinionWait[] }>('OPINION_WAITS');
      return Array.isArray(answer?.waits) ? answer.waits : [];
    },
    enabled: isAvailable && enabled,
  });
};

/**
 * A press at the gate (XAGENT1f): the gate moved, so every landing's plan and gate is asked again, and the sessions, whose
 * reviewer a press may have started or stopped. Asked again whatever the driver said: a refusal reads the gate as it stands.
 */
function useOpinionChange<TVariables>(fn: (variables: TVariables) => Promise<OpinionPressed>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSettled: () => {
      void client.invalidateQueries({ queryKey: keys.allLandings });
      void client.invalidateQueries({ queryKey: keys.allSessions });
    },
  });
}

/**
 * *Ask now*, *Try again*, *Ask again* and *Ask the same agent, fresh* (the second-agent design §8.5): a pass on the session's work
 * the driver's next look starts, among the rule's reviewers only, or the working agent in a fresh conversation (`sameAgent`).
 * It spends an account at the person's choice, so no door but theirs presses it (§9).
 */
export const useAskOpinion = () => useOpinionChange(
  ({ id, reviewer, sameAgent, words, occasion }: {
    id: string; reviewer?: string | null; sameAgent?: boolean; words?: string | null; occasion?: 'asked' | 'failure';
  }) => call<OpinionPressed>('ASK_OPINION', {
    id, ...(reviewer ? { reviewer } : {}), ...(sameAgent ? { sameAgent: true } : {}), ...(words ? { words } : {}),
    ...(occasion ? { occasion } : {}),
  }));

/**
 * *Go on anyway…* (§8.5): what is unsettled stays so, the person's answer kept with their words at the commit that would land,
 * and the gate's next item follows. Nothing lands here.
 */
export const useOpinionAnyway = () => useOpinionChange(
  ({ id, words }: { id: string; words?: string | null }) => call<OpinionPressed>('OPINION_ANYWAY', { id, ...(words ? { words } : {}) }));

/** *I looked myself…* (§8.5): the person's own reading kept in place of another agent's, with their words. */
export const useOpinionMyself = () => useOpinionChange(
  ({ id, words }: { id: string; words?: string | null }) => call<OpinionPressed>('OPINION_MYSELF', { id, ...(words ? { words } : {}) }));

/** *Stop* (§8.5): the reviewer reading the opinion stopped, as the person's stop; that pass then says it gave none. */
export const useStopOpinion = () => useOpinionChange(
  ({ opinion }: { opinion: string }) => call<OpinionPressed>('STOP_OPINION', { opinion }));

/** What the rule's plugin answered once the branch was made (D100): whether it pushed, the pull request, its words. */
export type PluginLanding = { id: string; pushed: boolean; pullRequest?: string; message: string; failed?: boolean };

/**
 * Accept a session: its work lands as its repository's rule says — merged, or put on a branch to push. A rule naming a
 * plugin hands the branch to it inside the press, so the page waits as long as that plugin may (`pluginBound`, LEFT3).
 *
 * Where *Accept…* was drawn with what the second opinion left unsettled, it sends back the gate's token (`answers`, XAGENT1f),
 * so the press answers what it showed; a gate that moved since answers nothing, and the door refuses in its sentence.
 */
export const useLandSessionTree = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (land: string | { id: string; answers?: string | null }) => {
      const { id, answers } = typeof land === 'string' ? { id: land, answers: null } : land;
      return call<TreeAct & { branch?: string; plugin?: PluginLanding }>(
        'LAND_SESSION_TREE', { id, ...(answers ? { answers } : {}) }, { timeoutMs: pluginBound });
    },
    onSettled: (_result, _error, land) => {
      // A press that answered the second opinion's gate, or was refused because it moved: every gate is read again (XAGENT1g).
      if (typeof land !== 'string' && land.answers) void client.invalidateQueries({ queryKey: keys.allLandings });
    },
    onSuccess: (result) => {
      // Only a merge that happened changes what a diff or a removal would say.
      if (result.done) {
        void client.invalidateQueries({ queryKey: keys.diff(result.session) });
        void client.invalidateQueries({ queryKey: keys.allSessions });
        // A branch just made may now be handed on (WSR5b) — its plugin failed, or the rule names none.
        void client.invalidateQueries({ queryKey: keys.handOff(result.session) });
        // LAND4: the clean-up's list said its branch held work no branch of the person's did, which a session's head reads
        // once the reader offers nothing to land; it holds none now.
        void client.invalidateQueries({ queryKey: keys.sweep });
      }
    },
  });
};

/**
 * Whether the branch this session's landing made can be handed to a landing plugin now (WSR5b): the
 * branch, the plugin it would go to (the rule's), and the sentence a press would be refused with. A
 * session whose landing made no branch answers none. Shell-only: it reads this machine's record and checkout.
 */
export const useHandOff = (id: string | null) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.handOff(id ?? ''),
    queryFn: () => call<{
      session: string; branch?: string | null; repository?: string; plugin?: string | null;
      problem?: string | null; pullRequest?: string | null; commits?: number;
    }>('HANDOFF_PLAN', { id }),
    enabled: isAvailable && id !== null,
  });
};

/**
 * Hand a session's landed branch to its plugin (WSR5b): the plugin pushes and opens the pull request; a refusal is an
 * answer. The page waits as long as the plugin may (`pluginBound`, LEFT3), not the bridge's 30 seconds.
 */
export const useHandOffPress = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) =>
      call<TreeAct & { branch?: string | null; plugin?: PluginLanding | null }>('HANDOFF', { id }, { timeoutMs: pluginBound }),
    onSuccess: (result) => {
      void client.invalidateQueries({ queryKey: keys.handOff(result.session) });
      void client.invalidateQueries({ queryKey: keys.sweep });
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
        // Its branch went with its tree: the clean-up's list, which the session's head reads what it left from (SQUASHTIDY1b).
        void client.invalidateQueries({ queryKey: keys.sweep });
      }
    },
  });
};
