import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../queries';
// The shape lives beside the components that render it, so a molecule can name it without
// importing this module (SURF6).
import type { SessionDiff } from '../work/diff';
import { call } from './call';

export type { DiffFile, SessionDiff } from '../work/diff';

// A session's tree on this machine (MOD3): what it did, the files in it, and the acts on it once
// reviewed: land it by its repository's rule, hand its branch on, or discard it (D51, WSR1, WSR5).

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
 * Where the rule names a plugin (D100), which one pushes it, and the sentence a press would be refused
 * with where that plugin cannot land work now.
 */
export const useLanding = (id: string | null) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.landing(id ?? ''),
    queryFn: () => call<{ session: string; form?: string; target?: string; source?: string; plugin?: string; problem?: string }>(
      'LANDING', { id }),
    enabled: isAvailable && id !== null,
  });
};

/** What the rule's plugin answered once the branch was made (D100): whether it pushed, the pull request, its words. */
export type PluginLanding = { id: string; pushed: boolean; pullRequest?: string; message: string; failed?: boolean };

/** Accept a session: its work lands as its repository's rule says — merged, or put on a branch to push. */
export const useLandSessionTree = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => call<TreeAct & { branch?: string; plugin?: PluginLanding }>('LAND_SESSION_TREE', { id }),
    onSuccess: (result) => {
      // Only a merge that happened changes what a diff or a removal would say.
      if (result.done) {
        void client.invalidateQueries({ queryKey: keys.diff(result.session) });
        void client.invalidateQueries({ queryKey: keys.allSessions });
        // A branch just made may now be handed on (WSR5b) — its plugin failed, or the rule names none.
        void client.invalidateQueries({ queryKey: keys.handOff(result.session) });
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

/** Hand a session's landed branch to its plugin (WSR5b): the plugin pushes and opens the pull request; a refusal is an answer. */
export const useHandOffPress = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => call<TreeAct & { branch?: string | null; plugin?: PluginLanding | null }>('HANDOFF', { id }),
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
      }
    },
  });
};
