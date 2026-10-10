import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import type { Session } from '../api';
import { keys } from '../queries';
import { searchable } from '../searchable';
import type { SessionEvent } from '../work/conversation';
import type { SessionGroupName, SessionGrouping } from '../work/groups';
import type { NewSessionAnswer } from '../work/say';
import type { SessionWhere } from '../work/SessionRow';
import { call } from './call';

// A session's life on this machine (MOD3): the person's answer to a parked one, a stop and how it is
// said, and finding sessions by their first words and by what they said.

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

/**
 * Where each of these sessions' work is now (LOOK2b): whether the tree it opened is still here, and its landing (D113),
 * from this machine's own files, in one ask for the rows a list shows.
 *
 * @remarks
 * Asked only of sessions whose record names a tree, with that tree, since the host looks at no folder but the trees its
 * home opened. Not a field of the sessions listing: the listing is the service's records, which travel (D47 §4), and a
 * landing is this machine's (`landings.json`, D102). Keyed under the sessions, so a landing, a clean-up or a tick that
 * asks the listing again asks this again too.
 */
export const useSessionWhere = (sessions: readonly Session[] | undefined): Record<string, SessionWhere> => {
  const { isAvailable } = useShenora();
  const asked = [...new Map((sessions ?? []).filter((session) => session.tree).map((session) => [session.id, session])).values()]
    .sort((a, b) => a.id.localeCompare(b.id));
  const answer = useQuery({
    queryKey: keys.sessionsWhere(asked.map((session) => session.id)),
    queryFn: () => call<{ sessions?: ({ session: string } & SessionWhere)[] }>('SESSION_WHERE', {
      sessions: asked.map((session) => ({ id: session.id, tree: session.tree })),
    }),
    enabled: isAvailable && asked.length > 0,
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  });
  const rows = Array.isArray(answer.data?.sessions) ? answer.data.sessions : [];
  return Object.fromEntries(rows.map((row) => [row.session, { treeGone: row.treeGone === true, landed: row.landed ?? null }]));
};

/**
 * The groups and a session's place among them (SESSUX1a), as the session list reads them: their shapes live with the
 * list's own helpers (`work/groups.ts`), since a molecule may read a shape and never a bridge domain.
 */
export type { SessionGroupName, SessionGrouping } from '../work/groups';

/**
 * The session list's groups, under the sessions' key, so whatever asks the listing again (a stop, a landing, a tick)
 * asks this too, as `useSessionWhere` is kept.
 */
const groupsKey = (ids?: readonly string[]) => ['sessions', 'groups', ...(ids ? [...new Set(ids)].sort() : ['*'])] as const;

/**
 * Each session's group by state, its word and its line's facts (SESSUX1a), in the order a list shows them; asked for
 * some, those sessions' alone. Shell-only: Sessions is (D47 §4), and no browser has a driver to ask. A list that does
 * not read the groups (the monitor's, SESSUX1c) asks nothing, since each answer may walk git in every tree to review.
 */
export const useSessionGroups = (ids?: readonly string[], { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: groupsKey(ids),
    queryFn: async () => {
      const answer = await call<{ sessions?: SessionGrouping[] }>('SESSION_GROUPS', ids ? { ids: [...new Set(ids)] } : undefined);
      return Array.isArray(answer?.sessions) ? answer.sessions : [];
    },
    enabled: isAvailable && enabled,
    refetchOnWindowFocus: false,
  });
};

/**
 * What an archive answered (D126 §5.2): the marks as they now stand; for several sessions at once, the ones it kept and
 * why (a code of the `errors` catalogue); for an unarchive, the ones that were not archived, which is information.
 */
export type ArchiveAnswer = {
  archived: { session: string; at: string }[];
  kept?: { session: string; code: string; group?: SessionGroupName | null }[];
  notArchived?: string[];
};

/**
 * Archive sessions on this machine, or bring them back (SESSUX1a, D126 §5.2): a mark under the home, never the record,
 * which travels.
 *
 * @remarks
 * Each session is judged by where the host places it as it is asked: a live one, one waiting on you and one with work to
 * review are refused, since archive never hides what needs the person. Asked of one session, the refusal is the answer;
 * asked of several, as *Archive what ended*'s second press asks, the rest are archived and each kept one comes back with
 * its code.
 */
export const useArchiveSessions = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (move: { ids: readonly string[]; archived: boolean }) =>
      call<ArchiveAnswer>('SESSION_ARCHIVE', { ids: [...new Set(move.ids)], archived: move.archived }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.allSessions });
    },
  });
};

/**
 * What a delete answered (SESSUX1f, D126 §5.4): the session, what went by name, and what the disk would not let go of by
 * the same names, left over (SESSDEL1), empty or absent when everything went; nothing machine-local.
 */
export type DeleteAnswer = { deleted: string; removed?: string[]; stayed?: string[] };

/**
 * *Delete…* (SESSUX1f, D126 §5.4): a conversation that served no quest, its record through the ledger and what this
 * machine kept of it, by its id alone. Each refusal is a code the catalogue says, naming what kept it. The sessions,
 * their groups among them, are asked again.
 */
export const useDeleteSession = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => call<DeleteAnswer>('SESSION_DELETE', { id }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.allSessions });
    },
  });
};

/**
 * *Open folder* (SESSUX1d, D126 §3.5): the folder a session worked in, its own tree or its repository's checkout, opened
 * in the system's file manager. By its id alone: the module names the folder from the session's record, and the page
 * never prints a path (platform language §4). It changes nothing, so nothing is asked again.
 */
export const useOpenSessionFolder = () => useMutation({
  mutationFn: (id: string) => call<{ opened: boolean }>('SESSION_OPEN_FOLDER', { id }),
});

/**
 * *Go on in a new session* (MSG1g, D137 §2.2): words a resume holds while the account their session ran on cools go on in a
 * new session at the driver's next look, handed them, without that conversation. By the session's id alone: the driver
 * judges it and keeps the person's choice, the door `daoris-driver sessions go-on-new` calls too (D50). Read field by field,
 * so a refusal keeps its code for the page to word, and a shell older than the route answers no choice kept. The sessions
 * and the driver are asked again, since the loop takes the choice up at its next look.
 */
export const useGoOnNew = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async (id: string): Promise<NewSessionAnswer> => {
      const answer = await call<{ sent?: boolean; why?: string | null; message?: string } | null>('SESSION_GO_ON_NEW', { id });
      return {
        sent: answer?.sent === true,
        why: typeof answer?.why === 'string' ? answer.why : null,
        message: typeof answer?.message === 'string' ? answer.message : '',
      };
    },
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.allSessions });
      void client.invalidateQueries({ queryKey: keys.driver });
    },
  });
};

/** Where a search found its words: the session, the event it began at, whose words, and a window of them. */
export type SessionHit = { session: string; seq: number; kind: string; snippet: string };

/**
 * What sessions said, searched (RAIL1): the person's words and the agent's, on this machine's own
 * record, bounded by the host and saying so (`cut`). Asked from two letters on, or one Han character
 * (`searchable`, RAILSRCH1b) — one other letter matches everything — and the caller debounces, so a word typed is one question, not one per key.
 */
export const useSessionSearch = (query: string, session?: string) => {
  const { isAvailable } = useShenora();
  const q = query.trim();
  return useQuery({
    queryKey: keys.sessionSearch(q, session),
    // Within one session, where one is named (SESS1 S9): its words and its calls by their titles.
    queryFn: () => call<{ query: string; hits: SessionHit[]; cut: boolean }>(
      'SESSION_SEARCH', { q, ...(session ? { session } : {}) }),
    enabled: isAvailable && searchable(q),
    staleTime: 10_000,
    refetchOnWindowFocus: false,
    retry: false,
  });
};
