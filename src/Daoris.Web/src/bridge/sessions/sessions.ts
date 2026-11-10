import { type InfiniteData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { useEffect, useRef } from 'react';
import type { Session } from '../../data/api';
import { CALLS_SLICE, keys, SESSIONS_SLICE } from '../../data/queries';
import { searchable } from '../../lib/searchable';
import type { SessionEvent } from '../../work/conversation/conversation';
import type { PartSpent } from '../../work/entries';
import type { SessionGroupName, SessionGrouping } from '../../work/groups';
import type { PermissionAnswered, PermissionChoice, PermissionWait } from '../../work/waits/PermissionAsks';
import type { AnswerAloneAnswered, AnswerRule, AnswerWait } from '../../work/waits/AnswerAlone';
import type { SessionWhere } from '../../work/rail/SessionRow';
import type { SessionRecord } from '../../work/head/sessionRecord';
import type { RelatedOwn, RelatedRow } from '../../work/head/RelatedSessions';
import type { MemoryRow } from '../../work/head/SessionMemory';
import { call } from '../call';
import { word } from './sessionHelpers';
import { sessionsComposer } from './sessionListMerge';
import { readSlice } from '../driver';
import { mergeKeyed, type SliceSpec, usePushedLive } from '../state/slices';
import { askHeld } from '../state/held';

// A session's life on this machine: the person's answer to a parked one, a stop and how it is
// said, and finding sessions by their first words and by what they said.

/**
 * The session records as the shell pushes them: a partition per workspace, each its records by id with the fields the
 * page reads (`SessionsStateSlice.Fields`), a record's move a delta of that record (`mergeKeyed`), composed into the widest list the
 * service answers, in its order; `useSessions` reads each view's list from it. Nothing judged yet composes nothing, which keeps the
 * pull; a slice judged to hold no record is an empty list.
 */
export const sessionsSlice: SliceSpec = {
  name: SESSIONS_SLICE,
  key: keys.sessions(null, true, null),
  read: () => readSlice(SESSIONS_SLICE),
  compose: sessionsComposer(),
  merge: mergeKeyed,
};

/**
 * The call a running session is making: its title and when it began, as `SESSION_GROUPS` writes `calling`, and whether it waits on
 * the person's answer to its permission request, and whether Daoris told the session it ran past its pipeline's bound,
 * each absent where it is not so.
 */
export type PushedCall = { title: string; since: string; waiting?: boolean; told?: boolean; quiet?: string; quietTold?: boolean; heard?: string };

const isCall = (value: unknown): value is PushedCall =>
  typeof value === 'object' && value !== null
  && typeof (value as { title?: unknown }).title === 'string' && typeof (value as { since?: unknown }).since === 'string';

/**
 * The running calls as the shell pushes them: one partition `calling`, session id to its call, judged from the
 * driver's memory as each call starts and ends. A keyed partition is pushed whole once half its keys moved, so an end may be a whole
 * push or `{}`, and both are taken as well as a delta. Composed into one map by session; nothing judged yet composes nothing.
 */
export const callsSlice: SliceSpec = {
  name: CALLS_SLICE,
  key: keys.calls,
  read: () => readSlice(CALLS_SLICE),
  compose: (partitions, judged) => {
    if (!judged) return undefined;
    const calls: Record<string, PushedCall> = {};
    for (const [name, part] of partitions) {
      if (name === 'quiet' || name === 'heard' || typeof part !== 'object' || part === null || Array.isArray(part)) continue;
      for (const [session, call] of Object.entries(part)) if (isCall(call)) calls[session] = call;
    }
    // The watcher's mark laid over each quiet session's call, or a call of no title where it makes none.
    const quiet = partitions.get('quiet');
    if (typeof quiet === 'object' && quiet !== null && !Array.isArray(quiet)) {
      for (const [session, mark] of Object.entries(quiet)) {
        const { since, told } = (mark ?? {}) as { since?: unknown; told?: unknown };
        // Whether Daoris's word to it is kept, which the row says beside the quiet.
        if (typeof since === 'string') calls[session] = { ...(calls[session] ?? { title: '', since }), quiet: since, ...(told === true ? { quietTold: true } : {}) };
      }
    }
    // When the window last heard a session, beside its call, or a call of no title where it makes none.
    const heard = partitions.get('heard');
    if (typeof heard === 'object' && heard !== null && !Array.isArray(heard)) {
      for (const [session, mark] of Object.entries(heard)) {
        const at = (mark as { at?: unknown } | null)?.at;
        if (typeof at === 'string') calls[session] = { ...(calls[session] ?? { title: '', since: at }), heard: at };
      }
    }
    return calls;
  },
  merge: mergeKeyed,
};

/** The pushed calls by session where the slice is live, else undefined, so the pull's `calling` is what is read. */
export const usePushedCalls = (): Record<string, PushedCall> | undefined => {
  const live = usePushedLive(CALLS_SLICE);
  const { data } = useQuery({
    queryKey: keys.calls,
    queryFn: () => new Promise<Record<string, PushedCall>>(() => {}),
    enabled: false,
    staleTime: Infinity,
  });
  return live ? data : undefined;
};

/**
 * A session's grouping with the pushed call laid over its `calling`: where the slice is live (`calls` given) the pushed map is the
 * truth, so a session absent from it is making no call, whatever the pull last said; not live, the grouping is as the pull gave it.
 */
export function withCall(grouping: SessionGrouping | null | undefined, calls: Record<string, PushedCall> | undefined) {
  if (!grouping || !calls) return grouping;
  return { ...grouping, calling: calls[grouping.session] ?? null };
}

/**
 * The person's answer to a session parked at a checkpoint.
 *
 * @remarks
 * **Three moves, and it goes through the driver.** `awaiting-person` means "only the person can
 * clear this"; the ledger allows a fourth move from it — back to `working` — and that one
 * is the driver observing a session that carried on, which a person causes by *answering* it. So
 * this carries exactly `completed`, `declined` and `stopped`, and the host refuses anything else
 * with a sentence.
 *
 * It lands on the driver rather than on the service because the process and the record must move
 * together: this machine lets the process go, and only then does the record say it ended.
 *
 * The sessions, the quests and the driver are asked again by the route's `STATE_CHANGED`, in every window, as every press below that moves a session is.
 */
export const useResolveSession = () => useMutation({
  mutationFn: (move: { id: string; state: 'completed' | 'declined' | 'stopped'; note?: string }) =>
    call('RESOLVE_SESSION', {
      id: move.id,
      state: move.state,
      ...(move.note ? { note: move.note } : {}),
    }) as Promise<{ session: string; state: string; message: string }>,
});

/**
 * What the driver answered a stop: whether it ended anything, and whether what it ended is an
 * orphan — a record that said it ran when nothing on this machine ran it.
 */
export type StopAnswer = { stopped: boolean; orphan?: boolean; elsewhere?: boolean };

/**
 * The notice for a stop, from the driver's answer — never the person's words for an ending that is
 * not theirs. Both doors to a stop (the frame's, the quest record's) say it the same way.
 */
export const stopNotice = (answer: StopAnswer) =>
  answer.orphan ? 'quests.session.orphanEnded'
    : answer.stopped ? 'quests.session.stopped'
      // Another Daoris process here runs it — a terminal's — so the record still says working.
      : answer.elsewhere ? 'quests.session.runElsewhere'
        : 'quests.session.notRunning';

export const useStopSession = () => useMutation({
  mutationFn: (id: string) => call('STOP_SESSION', { id }) as Promise<StopAnswer>,
});

/**
 * A quiet row's press answered: whether it moved, and where it did not, its code (`not-quiet`, `not-quest`, `gone`) and the
 * driver's sentence.
 */
export type QuietAnswer = { done: boolean; why: string | null; message: string };

const quietOf = (answer: unknown, field: 'stopped' | 'left'): QuietAnswer => {
  const said = answer as { stopped?: unknown; left?: unknown; why?: unknown; message?: unknown } | null | undefined;
  return {
    done: said?.[field] === true,
    why: typeof said?.why === 'string' && said.why ? said.why : null,
    message: typeof said?.message === 'string' ? said.message : '',
  };
};

/**
 * *End and carry on* on a quiet quest session's row: the person's stop, released in
 * the same press, the run ending `stopped` with no strike and the work going on in a new part in the same tree, handed the person's
 * waiting words. `SESSION_QUIET_CARRY_ON`; a refusal is the answer, by its code.
 */
export const useQuietCarryOn = () => useMutation({
  mutationFn: async (id: string) => quietOf(await call('SESSION_QUIET_CARRY_ON', { id }), 'stopped'),
});

/**
 * *Leave it running* on a quiet session's row: the watcher stands down for it until it is heard again, and the row goes.
 * `SESSION_QUIET_LEAVE`; a refusal is the answer, by its code.
 */
export const useQuietLeave = () => useMutation({
  mutationFn: async (id: string) => quietOf(await call('SESSION_QUIET_LEAVE', { id }), 'left'),
});

/**
 * What the person first said in each of these sessions: a conversation's identity, as
 * `sessionTitle` takes it — read from this machine's own record, never from the session record, which
 * travels.
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
  const queryKey = keys.openings(ids);
  const answer = useQuery({
    queryKey,
    // Told by event: the shell answers what it holds, and the record's read comes as `ANSWERED`.
    queryFn: () => askHeld('SESSION_OPENINGS', client, queryKey, { ids }, (answer) => answer as { openings?: Record<string, string> }),
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
 * The brief a session's start is given (a go-on's or a hand-over's first message, bounded, its cut said), from the same
 * answer as the openings and this machine's own record; null where its start carried none.
 */
export const useSessionBrief = (session: string | undefined): string | null => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = keys.openings(session ? [session] : []);
  const answer = useQuery({
    queryKey,
    queryFn: () => askHeld('SESSION_OPENINGS', client, queryKey, { ids: session ? [session] : [] }, (answer) => answer as { briefs?: Record<string, string> }),
    enabled: isAvailable && !!session,
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  });
  return (session ? answer.data?.briefs?.[session] : null) ?? null;
};

/**
 * What each part spent, from this machine's own record as the openings are: the *Parts* strip says it on each part's own row, and
 * the rail on a helper's and a continuing part's. Asked again when one of them ends a turn, and only for `least` ids or more.
 * Nothing in a browser, which holds no record.
 */
export const useSessionSpent = (ids: readonly string[] | undefined, least = 2): Record<string, PartSpent> => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const asked = [...new Set(ids ?? [])].sort();
  const answer = useQuery({
    queryKey: keys.spent(asked),
    queryFn: () => call('SESSION_SPENT', { ids: asked }) as Promise<{ spent?: Record<string, PartSpent> }>,
    enabled: isAvailable && asked.length >= least,
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  });

  useShenoraEvent('DAORIS', 'SESSION_EVENTS', (payload) => {
    const batch = payload as { session?: string; events?: SessionEvent[] } | undefined;
    if (!batch?.session || !asked.includes(batch.session)) return;
    if (batch.events?.some((event) => event.kind === 'turn')) void client.invalidateQueries({ queryKey: keys.allSpent });
  });

  return answer.data?.spent ?? {};
};

/**
 * Where each of these sessions' work is now: whether the tree it opened is still here, and its landing,
 * from this machine's own files, in one ask for the rows a list shows.
 *
 * @remarks
 * Asked only of sessions whose record names a tree, with that tree, since the host looks at no folder but the trees its
 * home opened. Not a field of the sessions listing: the listing is the service's records, which travel, and a
 * landing is this machine's (`landings.json`). Keyed under the sessions, so a landing, a clean-up or a tick that
 * asks the listing again asks this again too. Told by event: the shell answers what it held, and its look's answer
 * comes as `ANSWERED` into this query, so the rows say what they said until it does.
 */
export const useSessionWhere = (sessions: readonly Session[] | undefined): Record<string, SessionWhere> => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const asked = [...new Map((sessions ?? []).filter((session) => session.tree).map((session) => [session.id, session])).values()]
    .sort((a, b) => a.id.localeCompare(b.id));
  const queryKey = keys.sessionsWhere(asked.map((session) => session.id));
  const answer = useQuery({
    queryKey,
    queryFn: () => askHeld('SESSION_WHERE', client, queryKey, {
      sessions: asked.map((session) => ({ id: session.id, tree: session.tree })),
    }, (answer) => answer as { sessions?: ({ session: string } & SessionWhere)[] }),
    enabled: isAvailable && asked.length > 0,
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  });
  const rows = Array.isArray(answer.data?.sessions) ? answer.data.sessions : [];
  return Object.fromEntries(rows.map((row) => [row.session, { treeGone: row.treeGone === true, landed: row.landed ?? null }]));
};

/**
 * The record of the finished tree a session ran in: its landing, tip, recovery ref and commits, read once its tree is gone,
 * when only the record still says what the work is. Null while none is kept or the tree stands. Told by event, as `useSessionWhere` is.
 */
export const useSessionRecord = (id: string | null, treeGone: boolean): SessionRecord | null => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = keys.sessionRecord(id ?? '');
  const answer = useQuery({
    queryKey,
    queryFn: () => askHeld('SESSION_RECORD', client, queryKey, { id: id ?? '' }, (answer) => answer as { record?: SessionRecord | null }),
    enabled: isAvailable && id !== null && treeGone,
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  });
  return treeGone ? answer.data?.record ?? null : null;
};

/**
 * A session's memory changes, its writes, ends and proposals as the service keeps them, told by event. Under the sessions'
 * key, so the person's decision on them and each tick ask it again. Empty while none is read.
 */
export const useSessionMemory = (session: string | null | undefined): MemoryRow[] => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = ['sessions', 'memory', session ?? ''] as const;
  const answer = useQuery({
    queryKey,
    queryFn: () => askHeld('SESSION_MEMORY', client, queryKey, { session: session ?? '' }, (answer) => answer as { ops?: MemoryRow[] }),
    enabled: isAvailable && Boolean(session),
    staleTime: 30_000,
  });
  return Array.isArray(answer.data?.ops) ? answer.data.ops : [];
};

/** What the person's decision on a session's memory answered: whether it stood, and the service's words. */
export type MemoryDecided = { decided: boolean; why: string | null; message: string };

/**
 * The person's decision on memory: an end awaiting them approved or declined by its row, which names no session where the service's
 * sweep wrote it, or the ended session's staged changes kept or dropped. A refusal is the answer, in the service's words; the
 * sessions are asked again by the state hub.
 */
export const useDecideSessionMemory = () => useMutation({
  mutationFn: async (decision: { session?: string; op: string; approve: boolean } | { session: string; keep: boolean }): Promise<MemoryDecided> => {
    const answer = (await call('SESSION_MEMORY_DECIDE', decision)) as { decided?: unknown; why?: unknown; message?: unknown } | null;
    return { decided: answer?.decided === true, why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '' };
  },
});

/**
 * The sessions beside one in its repository whose work meets its own, told by event; the driver reads the trees at
 * each ask, so a stale answer is read again on focus. Empty while none is read.
 */
export const useSessionRelatedRead = (session: string | null | undefined, enabled = true): { rows: RelatedRow[]; own: RelatedOwn | null } => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = ['sessions', 'related', session ?? ''] as const;
  const answer = useQuery({
    queryKey,
    queryFn: () => askHeld('SESSION_RELATED', client, queryKey, { session: session ?? '' }, (answer) => answer as { related?: RelatedRow[]; own?: { readAt?: string | null } | null }),
    enabled: isAvailable && enabled && Boolean(session),
    staleTime: 30_000,
  });
  const own = answer.data?.own;
  return {
    rows: Array.isArray(answer.data?.related) ? answer.data.related : [],
    own: own ? { readAt: typeof own.readAt === 'string' ? own.readAt : null } : null,
  };
};

/** The groups and a session's place among them, as the session list reads them: their shapes live with the list's own helpers (`work/groups.ts`). */
export type { SessionGroupName, SessionGrouping } from '../../work/groups';
export { byInstant, groupsIn, groupsSlice, useSessionEntries, useSessionGroups } from './sessionGroups';
export type { GroupsAnswer } from './sessionGroups';

/**
 * What an archive answered: the marks as they now stand; for several sessions at once, the ones it kept and
 * why (a code of the `errors` catalogue); for an unarchive, the ones that are not archived, which is information.
 */
export type ArchiveAnswer = {
  archived: { session: string; at: string }[];
  kept?: { session: string; code: string; group?: SessionGroupName | null }[];
  notArchived?: string[];
};

/**
 * Archive sessions on this machine, or bring them back: a mark under the home, never the record,
 * which travels.
 *
 * @remarks
 * Each session is judged by where the host places it as it is asked: a live one, one waiting on you and one with work to
 * review are refused, since archive never hides what needs the person. Asked of one session, the refusal is the answer;
 * asked of several, as *Archive what ended*'s second press asks, the rest are archived and each kept one comes back with
 * its code. The sessions, their groups among them, are asked again by the state hub alone.
 */
export const useArchiveSessions = () =>
  useMutation({
    mutationFn: (move: { ids: readonly string[]; archived: boolean }) =>
      call('SESSION_ARCHIVE', { ids: [...new Set(move.ids)], archived: move.archived }) as Promise<ArchiveAnswer>,
  });

/**
 * What a delete answered: the session, what went by name, and what the disk would not let go of by
 * the same names, left over, empty or absent when everything went; nothing machine-local.
 */
export type DeleteAnswer = { deleted: string; removed?: string[]; stayed?: string[] };

/**
 * *Delete…*: a conversation that served no quest, its record through the ledger and what this
 * machine kept of it, by its id alone. Each refusal is a code the catalogue says, naming what kept it. The sessions,
 * their groups among them, are asked again by the state hub.
 */
export const useDeleteSession = () => useMutation({
  mutationFn: (id: string) => call('SESSION_DELETE', { id }) as Promise<DeleteAnswer>,
});

/**
 * *Open folder*: the folder a session worked in, its own tree or its repository's checkout, opened
 * in the system's file manager. By its id alone: the module names the folder from the session's record, and the page
 * never prints a path. It changes nothing, so nothing is asked again.
 */
export const useOpenSessionFolder = () => useMutation({
  mutationFn: (id: string) => call('SESSION_OPEN_FOLDER', { id }) as Promise<{ opened: boolean }>,
});

export {
  useApproveHandOver, useApproveHelper, useAskHelper, useAskPartner, useDeclineHandOver, useDeclineHelper, useHelperWaits,
  usePartnerAct, useSessionHandOverAsk, useSessionHelpers, useSessionPartners, useSessionQuestion,
} from './sessionHelpers';
export type { HandOverWord, HelperApproved, HelperAsked, HelperDeclined, PartnerActed, PartnerAsked } from './sessionHelpers';

/**
 * The held permissions' key: under the sessions', so a session's publication (a turn's cancel, a stop, a session's
 * end, each a release) asks them again with the sessions.
 */
export const PERMISSION_WAITS_KEY = ['sessions', 'permissionWaits'] as const;

/** How often the held permissions are asked while one may come or go: a person at the window answers within seconds. */
const PERMISSIONS_EVERY = 4_000;

/**
 * When the held permissions are asked again on a clock: every few seconds while a session of this machine works, or
 * while one is held, and never otherwise, since neither route publishes (`HeldPermissions` is in the driver's memory). Where the calls
 * are pushed (`pushed`), never: a hold marks its call waiting in the calls slice and its answer or release clears the
 * mark, so the mark's move asks them again (`usePermissionWaits`). The clock stays for a host that pushes no calls.
 */
export const permissionsAgainIn = (live: boolean, held: number, pushed = false): number | false =>
  (!pushed && (live || held > 0) ? PERMISSIONS_EVERY : false);

/**
 * The pushed calls' waiting marks as one word: each waiting session with when its newest hold began, so a hold, an
 * answer and a release each change it; null where the calls are not pushed.
 */
const waitingMarks = (calls: Record<string, PushedCall> | undefined): string | null =>
  calls ? Object.entries(calls).filter(([, each]) => each.waiting === true).map(([session, each]) => `${session} ${each.since}`).sort().join('\n') : null;

const readChoice = (value: unknown): PermissionChoice | null => {
  if (!value || typeof value !== 'object') return null;
  const choice = value as Record<string, unknown>;
  const id = word(choice.id);
  return id ? { id, name: typeof choice.name === 'string' ? choice.name : id, kind: typeof choice.kind === 'string' ? choice.kind : '' } : null;
};

/** The rows of `PERMISSION_WAITS`, read field by field: one naming no session or no request has nothing to answer, and is left out. */
const readPermissionWaits = (rows: unknown): PermissionWait[] => {
  if (!Array.isArray(rows)) return [];
  return rows.filter((row): row is Record<string, unknown> => Boolean(row) && typeof row === 'object')
    .filter((row) => word(row.session) !== null && word(row.request) !== null)
    .map((row) => ({
      session: row.session as string,
      request: row.request as string,
      call: word(row.call),
      title: word(row.title),
      kind: word(row.kind),
      choices: Array.isArray(row.choices) ? row.choices.map(readChoice).filter((choice): choice is PermissionChoice => choice !== null) : [],
      since: typeof row.since === 'string' ? row.since : '',
      until: word(row.until),
    }));
};

/**
 * What this machine's driven sessions hold for the person under their repository's asking permission prompts: each tool's request with its session, its title and kind as the agent wrote them, and what the person may choose, oldest
 * first, off the driver's memory, so shell-only. `live`: a session of this machine works now, so one may be asked for at any moment.
 *
 * Where the calls are pushed, asked again as their waiting marks move, once whichever reader saw it first: each
 * reader of the waits hears the same push, and one asking already on its way is the others' too. Elsewhere, on the clock.
 */
export const usePermissionWaits = (live: boolean) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const calls = usePushedCalls();
  const marks = waitingMarks(calls);
  const seen = useRef(marks);
  useEffect(() => {
    if (seen.current === marks) return;
    seen.current = marks;
    if (marks !== null) void client.invalidateQueries({ queryKey: PERMISSION_WAITS_KEY, exact: true }, { cancelRefetch: false });
  }, [client, marks]);
  return useQuery({
    queryKey: PERMISSION_WAITS_KEY,
    queryFn: async () => readPermissionWaits((await call<{ waits?: unknown } | null>('PERMISSION_WAITS'))?.waits),
    enabled: isAvailable,
    refetchInterval: (query) => permissionsAgainIn(live, query.state.data?.length ?? 0, calls !== undefined),
  });
};

/**
 * The person's answer to a permission a session holds for them: an option the agent offered, by its id,
 * for this call alone; Daoris answers no allow by itself. A refusal is the answer, by its code (`unheld`, `unoffered`,
 * `standing`). The route publishes nothing, so the waits are asked again here, and the card goes with the hold.
 */
export const useAnswerPermission = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async (answer: { session: string; request: string; option?: string | null }): Promise<PermissionAnswered> => {
      const said = (await call('SESSION_PERMISSION_ANSWER', {
        session: answer.session, request: answer.request, ...(answer.option ? { option: answer.option } : {}),
      })) as { answered?: unknown; why?: unknown; message?: unknown } | null;
      return { answered: said?.answered === true, why: word(said?.why), message: typeof said?.message === 'string' ? said.message : '' };
    },
    onSettled: () => client.invalidateQueries({ queryKey: PERMISSION_WAITS_KEY }),
  });
};

/** The answers-alone waits' key: under the sessions', so a tick and the person's answer (its publication) ask them again. */
export const ANSWER_WAITS_KEY = ['sessions', 'answerWaits'] as const;

/** The rows of `ANSWER_WAITS`, read field by field: one naming no session has nothing to answer, and is left out. */
const readAnswerWaits = (rows: unknown): AnswerWait[] => {
  if (!Array.isArray(rows)) return [];
  return rows.filter((row): row is Record<string, unknown> => Boolean(row) && typeof row === 'object')
    .filter((row) => word(row.session) !== null)
    .map((row) => ({
      session: row.session as string,
      agent: word(row.agent),
      words: Array.isArray(row.words) ? row.words.filter((id): id is string => typeof id === 'string') : [],
      since: typeof row.since === 'string' ? row.since : '',
      rule: readAnswerRule(row.rule),
    }));
};

/** The rule that held an answer, field by field: one that does not read is none, and the wait stands without it. */
const readAnswerRule = (raw: unknown): AnswerRule | null => {
  const rule = raw as Record<string, unknown> | null;
  if (!rule || typeof rule !== 'object' || typeof rule.place !== 'string' || typeof rule.at !== 'number' || typeof rule.n !== 'number') return null;
  return { place: rule.place, at: rule.at, n: rule.n, count: typeof rule.count === 'string' ? rule.count : 'answers-alone', do: typeof rule.do === 'string' ? rule.do : 'hold' };
};

/**
 * What the answers-alone bound holds for the person across this machine's sessions: each session whose answers would take its line on a third time in a row with nobody between, the agent that answered, the
 * answers' ids and since when, oldest first, read off this machine's own marks, so shell-only. Under the sessions' key, so every tick
 * asks it again, and the driver's look that holds one is a tick.
 */
export const useAnswerWaits = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: ANSWER_WAITS_KEY,
    queryFn: async () => readAnswerWaits((await call<{ waits?: unknown } | null>('ANSWER_WAITS'))?.waits),
    enabled: isAvailable,
  });
};

/**
 * The person's answer to a third answer alone held for them: *Go on with this answer* (`goOn`), their word, which lets
 * the answer go on, or *Not now*, which keeps it for them. A refusal is the answer, by its code (`unheld`). The sessions and the driver
 * are asked again by the state hub; the waits are asked again here too, so the wait goes with the answer.
 */
export const useAnswerAlone = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async (answer: { session: string; goOn: boolean }): Promise<AnswerAloneAnswered> => {
      const said = (await call('SESSION_ANSWER_ALONE', {
        session: answer.session, goOn: answer.goOn,
      })) as { answered?: unknown; why?: unknown; message?: unknown } | null;
      return { answered: said?.answered === true, why: word(said?.why), message: typeof said?.message === 'string' ? said.message : '' };
    },
    onSettled: () => client.invalidateQueries({ queryKey: ANSWER_WAITS_KEY }),
  });
};

/** Where a search found its words: the session, the event it began at, whose words, and a window of them. */
export type SessionHit = { session: string; seq: number; kind: string; snippet: string };

/** How many sessions a page of a search holds, asked for by name: the driver's own default, so a page is the same either way. */
export const SEARCH_PAGE = 16;

/** A search's answer as the driver gave it: `total` and `next` are absent from a shell without pages. */
type SearchAnswer = { query: string; hits: SessionHit[]; cut: boolean; total?: number; next?: number | null };

/** A search's pages as the one answer the page reads: every hit in order, `total` and `next` the last page's, null where a shell sent none. */
export type SessionSearchResult = { query: string; hits: SessionHit[]; cut: boolean; total: number | null; next: number | null };

const countOf = (value: unknown): number | null =>
  typeof value === 'number' && Number.isInteger(value) && value >= 0 ? value : null;

const joinSearch = (data: InfiniteData<SearchAnswer, number>): SessionSearchResult => {
  const last = data.pages.at(-1);
  return {
    query: last?.query ?? '',
    hits: data.pages.flatMap((page) => (Array.isArray(page.hits) ? page.hits : [])),
    cut: last?.cut ?? false,
    total: countOf(last?.total),
    next: countOf(last?.next),
  };
};

/**
 * What sessions said, searched: the person's words and the agent's, on this machine's own
 * record, a page of sessions at a time: `fetchNextPage` asks for the one at the driver's `next`, and `data` is every
 * page read, joined. Asked from two letters on, or one Han character (`searchable`) — one other letter matches
 * everything — and the caller debounces, so a word typed is one question, not one per key.
 *
 * @remarks
 * A shell without pages ignores `offset` and `limit` and answers no `total` or `next`: its answer is the one page, with its
 * `cut`, and `total` is null so the page knows to say that instead of counting.
 */
export const useSessionSearch = (query: string, session?: string) => {
  const { isAvailable } = useShenora();
  const q = query.trim();
  return useInfiniteQuery({
    queryKey: keys.sessionSearch(q, session),
    // Within one session, where one is named: its words and its calls by their titles.
    queryFn: ({ pageParam }) => call('SESSION_SEARCH', {
      q,
      ...(session ? { session } : {}),
      limit: SEARCH_PAGE,
      ...(pageParam > 0 ? { offset: pageParam } : {}),
    }) as Promise<SearchAnswer>,
    initialPageParam: 0,
    getNextPageParam: (page: SearchAnswer) => countOf(page.next),
    select: joinSearch,
    enabled: isAvailable && searchable(q),
    staleTime: 10_000,
    refetchOnWindowFocus: false,
    retry: false,
  });
};
