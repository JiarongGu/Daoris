import { useCallback } from 'react';
import { type InfiniteData, useInfiniteQuery, useMutation, useQuery, useQueryClient, type UseQueryOptions } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { useEffect, useRef } from 'react';
import { type Session, SESSION_ACTIVE } from '../../data/api';
import { CALLS_SLICE, GROUPS_SLICE, keys, SESSIONS_SLICE } from '../../data/queries';
import { searchable } from '../../lib/searchable';
import type { SessionEvent } from '../../work/conversation/conversation';
import type { PartSpent, SessionEntry } from '../../work/entries';
import { GROUP_ORDER, type SessionGroupName, type SessionGrouping } from '../../work/groups';
import type { HelperAskRow, HelperWait } from '../../work/waits/HelperAsks';
import type { HandOverAskRow } from '../../work/waits/HandOverAgentAsk';
import { type ParkQuestion, readQuestion } from '../../work/waits/parkQuestion';
import type { PermissionAnswered, PermissionChoice, PermissionWait } from '../../work/waits/PermissionAsks';
import type { AnswerAloneAnswered, AnswerWait } from '../../work/waits/AnswerAlone';
import type { NewSessionAnswer } from '../../work/say';
import type { SessionWhere } from '../../work/rail/SessionRow';
import { call } from '../call';
import { readSlice } from '../driver';
import { mergeKeyed, readPushed, type SliceSpec, usePushedLive } from '../state/slices';

// A session's life on this machine (MOD3): the person's answer to a parked one, a stop and how it is
// said, and finding sessions by their first words and by what they said.

/** The service's order of its records (`Sessions.ListAsync`): the open ones first, then by when each began. */
const inServiceOrder = (a: Session, b: Session) => {
  const open = Number(!SESSION_ACTIVE.has(a.state)) - Number(!SESSION_ACTIVE.has(b.state));
  if (open !== 0) return open;
  const began = (Date.parse(a.created) || 0) - (Date.parse(b.created) || 0);
  return began !== 0 ? began : a.id < b.id ? -1 : a.id > b.id ? 1 : 0;
};

const isRecord = (value: unknown): value is Session =>
  typeof value === 'object' && value !== null && typeof (value as { id?: unknown }).id === 'string';

/**
 * The session records as the shell pushes them (PUSH1d2, D182): a partition per workspace, each its records by id with the fields the
 * page reads (`SessionsStateSlice.Fields`), a record's move a delta of that record (`mergeKeyed`), composed into the widest list the
 * service answers, in its order; `useSessions` reads each view's list from it. Nothing judged yet composes nothing, which keeps the
 * pull; a slice judged to hold no record is an empty list.
 */
export const sessionsSlice: SliceSpec = {
  name: SESSIONS_SLICE,
  key: keys.sessions(null, true, null),
  read: () => readSlice(SESSIONS_SLICE),
  compose: (partitions, judged) => {
    if (!judged) return undefined;
    return [...partitions.values()]
      .flatMap((part) => (typeof part === 'object' && part !== null && !Array.isArray(part) ? Object.values(part).filter(isRecord) : []))
      .sort(inServiceOrder);
  },
  merge: mergeKeyed,
};

/**
 * The call a running session is making: its title and when it began, as `SESSION_GROUPS` writes `calling`, and whether it waits on
 * the person's answer to its permission request (AUTONOMY1b3b), and whether Daoris told the session it ran past its pipeline's bound
 * (FABRIC-PIPELINE1c), each absent from a host older than the mark.
 */
export type PushedCall = { title: string; since: string; waiting?: boolean; told?: boolean };

const isCall = (value: unknown): value is PushedCall =>
  typeof value === 'object' && value !== null
  && typeof (value as { title?: unknown }).title === 'string' && typeof (value as { since?: unknown }).since === 'string';

/**
 * The running calls as the shell pushes them (WAITSAY1c2, D182): one partition `calling`, session id to its call, judged from the
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
    for (const part of partitions.values()) {
      if (typeof part !== 'object' || part === null || Array.isArray(part)) continue;
      for (const [session, call] of Object.entries(part)) if (isCall(call)) calls[session] = call;
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
 *
 * The sessions, the quests and the driver are asked again by the route's `STATE_CHANGED`, in every window (STATEHUB1d,
 * D169), as every press below that moves a session is.
 */
export const useResolveSession = () => useMutation({
  mutationFn: (move: { id: string; state: 'completed' | 'declined' | 'stopped'; note?: string }) =>
    call<{ session: string; state: string; message: string }>('RESOLVE_SESSION', {
      id: move.id,
      state: move.state,
      ...(move.note ? { note: move.note } : {}),
    }),
});

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

export const useStopSession = () => useMutation({
  mutationFn: (id: string) => call<StopAnswer>('STOP_SESSION', { id }),
});

/**
 * A quiet row's press answered (SESSWATCH1a): whether it moved, and where it did not, its code (`not-quiet`, `not-quest`, `gone`) and the
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
 * *End and carry on* on a quiet quest session's row (SESSWATCH1a, D200, the session-watch design §2.3): the person's stop, released in
 * the same press, the run ending `stopped` with no strike and the work going on in a new part in the same tree, handed the person's
 * waiting words. `SESSION_QUIET_CARRY_ON`; a refusal is the answer, by its code.
 */
export const useQuietCarryOn = () => useMutation({
  mutationFn: async (id: string) => quietOf(await call<unknown>('SESSION_QUIET_CARRY_ON', { id }), 'stopped'),
});

/**
 * *Leave it running* on a quiet session's row (SESSWATCH1a): the watcher stands down for it until it is heard again, and the row goes.
 * `SESSION_QUIET_LEAVE`; a refusal is the answer, by its code.
 */
export const useQuietLeave = () => useMutation({
  mutationFn: async (id: string) => quietOf(await call<unknown>('SESSION_QUIET_LEAVE', { id }), 'left'),
});

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
 * What each of a line of work's parts spent (SESSCTX1d; the session-context design §7), from this machine's own record as the
 * openings are: the *Parts* strip says it on each part's own row, so a moved session reads as one. Asked again when one of them
 * ends a turn. Nothing in a browser, which holds no record.
 */
export const useSessionSpent = (ids: readonly string[] | undefined): Record<string, PartSpent> => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const asked = [...new Set(ids ?? [])].sort();
  const answer = useQuery({
    queryKey: keys.spent(asked),
    queryFn: () => call<{ spent?: Record<string, PartSpent> }>('SESSION_SPENT', { ids: asked }),
    enabled: isAvailable && asked.length > 1,
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
export type { SessionGroupName, SessionGrouping } from '../../work/groups';

/**
 * The session list's groups, under the sessions' key, so whatever asks the listing again (a stop, a landing, a tick)
 * asks this too, as `useSessionWhere` is kept; but the widest where the shell pushes it (PUSH1c3b), which its pushes write.
 */
const groupsKey = (ids?: readonly string[]) => ['sessions', 'groups', ...(ids ? [...new Set(ids)].sort() : ['*'])] as const;

/** One `SESSION_GROUPS` answer: the rows, and the entries beside them where the host is new enough to answer them (D167). */
export type GroupsAnswer = { sessions: SessionGrouping[]; entries: SessionEntry[] | undefined };

/** A row as the slice pushes it: the answer's, with the sort inputs its group is ordered by (`GroupsStateSlice.OrderFields`). */
type PushedRow = SessionGrouping & { repository?: unknown; created?: unknown; updated?: unknown };

/** The sort inputs a pushed row carries, which the answer's rows never do. */
const ORDER_INPUTS = ['repository', 'created', 'updated'] as const;

const isRow = (value: unknown): value is PushedRow => typeof value === 'object' && value !== null
  && typeof (value as { session?: unknown }).session === 'string' && typeof (value as { group?: unknown }).group === 'string';

/** C#'s `Ordinal`: by UTF-16 unit. */
const byUnit = (a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0);

/** C#'s `OrdinalIgnoreCase` folds: each character upper-cased where it maps to one character, as its simple mapping does. */
const folded = (value: unknown) => [...(typeof value === 'string' ? value : '')]
  .map((character) => {
    const upper = character.toUpperCase();
    return [...upper].length === 1 ? upper : character;
  })
  .join('');

/**
 * Two instants as `DateTimeOffset` compares them: the moment whatever its offset, to the tick (`Date.parse` keeps the millisecond,
 * the fraction's further digits the rest), never the strings. The pushed quests and asks are ordered by it too (PUSH1d3).
 */
export const byInstant = (a: unknown, b: unknown) => {
  const millisecond = (value: unknown) => (typeof value === 'string' ? Date.parse(value) || 0 : 0);
  const tick = (value: unknown) =>
    Number(((typeof value === 'string' ? /T\d\d:\d\d:\d\d\.(\d+)/.exec(value)?.[1] : undefined) ?? '').slice(3, 7).padEnd(4, '0'));
  return millisecond(a) - millisecond(b) || tick(a) - tick(b);
};

const groupPlace = (row: PushedRow) => {
  const at = GROUP_ORDER.indexOf(row.group);
  return at < 0 ? GROUP_ORDER.length : at;
};

/**
 * The reader's order (`SessionGroups.Ordered`, D126 §2.1; D182's PUSH1c3a note): the groups in `GROUP_ORDER`; waiting on you and
 * resuming later oldest move first; to review and working by repository without case, then when each began; ended and archived
 * newest move first; every tie by the session's id by unit.
 */
const inReaderOrder = (a: PushedRow, b: PushedRow) => {
  const group = groupPlace(a) - groupPlace(b);
  if (group !== 0) return group;
  const id = byUnit(a.session, b.session);
  switch (a.group) {
    case 'you':
    case 'later':
      return byInstant(a.updated, b.updated) || id;
    case 'review':
    case 'working':
      return byUnit(folded(a.repository), folded(b.repository)) || byInstant(a.created, b.created) || id;
    default:
      return byInstant(b.updated, a.updated) || id;
  }
};

const valuesOf = (partition: unknown): unknown[] =>
  (typeof partition === 'object' && partition !== null && !Array.isArray(partition) ? Object.values(partition) : []);

/**
 * The pushed partitions as `SESSION_GROUPS` answers them: the rows in the reader's order without their sort inputs, and each entry
 * at its leading row's place (`SessionEntries.Read`).
 */
function composeGroups(rows: unknown, entries: unknown): GroupsAnswer {
  const sessions = valuesOf(rows).filter(isRow).sort(inReaderOrder).map((pushed) => {
    const row: Record<string, unknown> = { ...pushed };
    for (const input of ORDER_INPUTS) delete row[input];
    return row as SessionGrouping;
  });
  const place = new Map(sessions.map((row, at) => [row.session, at]));
  const at = (entry: SessionEntry) => place.get(entry.leading) ?? sessions.length;
  return {
    sessions,
    entries: (readEntries(valuesOf(entries)) ?? []).sort((a, b) => at(a) - at(b) || byUnit(a.entry, b.entry)),
  };
}

/**
 * The session list's groups as the shell pushes them (PUSH1c3b, D182's PUSH1c3a note): two keyed partitions, `rows` by session and
 * `entries` by key, a session's move a delta of its row and its entry (`mergeKeyed`; a whole push or `{}` taken as well), composed
 * into `SESSION_GROUPS`' widest answer in the reader's order. Nothing judged, or no rows held yet, composes nothing, which keeps
 * the pull. `calling` is never pushed here: the `calls` slice's is laid over the rows as before (`withCall`).
 */
export const groupsSlice: SliceSpec = {
  name: GROUPS_SLICE,
  key: groupsKey(),
  read: () => readSlice(GROUPS_SLICE),
  compose: (partitions, judged) =>
    (judged && partitions.has('rows') ? composeGroups(partitions.get('rows'), partitions.get('entries')) : undefined),
  merge: mergeKeyed,
};

/**
 * Some sessions' answer read from the widest, as the reader's `only` answers it: their rows, and every entry holding one of them,
 * whole, since its other parts decide its group. None named is the widest itself.
 */
export function groupsIn(answer: GroupsAnswer, ids: readonly string[] | undefined): GroupsAnswer {
  if (!ids) return answer;
  const asked = new Set(ids);
  return {
    sessions: answer.sessions.filter((row) => asked.has(row.session)),
    entries: answer.entries?.filter((entry) => entry.parts.some((part) => asked.has(part.session))),
  };
}

/**
 * Where the shell pushes the groups (PUSH1c3b), every reader reads the one entry its pushes write, from memory, and some sessions'
 * variant is selected from it (`groupsIn`): never stale, never asked on a focus, and no tick reaches it (`notPushed`). Elsewhere (a
 * plain browser, an older shell) each variant is the pull it always was, under its own key; and while a window's first snapshot is on
 * its way, a variant asked then waits for it and is read from it, never pulled beside it.
 */
function useGroupsRead<T>(ids: readonly string[] | undefined, pick: (answer: GroupsAnswer) => T)
  : UseQueryOptions<GroupsAnswer, Error, T, readonly string[]> {
  const client = useQueryClient();
  const pushed = usePushedLive(GROUPS_SLICE);
  const asked = ids ? [...new Set(ids)].sort() : undefined;
  // By the sessions' names, so a caller's new array of the same ids keeps what the cache selected.
  const named = asked?.join('\n');
  const select = useCallback((answer: GroupsAnswer) => pick(pushed ? groupsIn(answer, asked) : answer), [pick, pushed, named]);
  return pushed
    ? {
      queryKey: groupsKey(),
      queryFn: () => readPushed(client, GROUPS_SLICE, () => askGroups()),
      select, staleTime: Infinity, refetchOnWindowFocus: false,
    }
    : {
      queryKey: groupsKey(ids),
      queryFn: async () => {
        const all = await readPushed<GroupsAnswer | null>(client, GROUPS_SLICE, async () => null);
        return all === null ? askGroups(ids) : groupsIn(all, ids);
      },
      select, refetchOnWindowFocus: false,
    };
}

const rowsOf = (answer: GroupsAnswer) => answer.sessions;
const entriesOf = (answer: GroupsAnswer) => answer.entries;

/**
 * Each session's group by state, its word and its line's facts (SESSUX1a), in the order a list shows them; asked for
 * some, those sessions' alone. Shell-only: Sessions is (D47 §4), and no browser has a driver to ask. A list that does
 * not read the groups (the monitor's, SESSUX1c) asks nothing, since each answer may walk git in every tree to review.
 * Pushed where the shell pushes them (PUSH1c3b, `useGroupsRead`).
 */
export const useSessionGroups = (ids?: readonly string[], { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery({ ...useGroupsRead(ids, rowsOf), enabled: isAvailable && enabled });
};

/**
 * The entries as the answer holds them, read defensively: absent where the host is older than the entries, and then the list
 * is the records it always was. A malformed entry is left out rather than guessed at.
 */
const readEntries = (answer: unknown): SessionEntry[] | undefined => (Array.isArray(answer)
  ? answer.filter((row): row is SessionEntry => Boolean(row)
    && typeof (row as SessionEntry).leading === 'string'
    && Array.isArray((row as SessionEntry).parts)
    && (row as SessionEntry).parts.length > 0)
  : undefined);

const askGroups = async (ids?: readonly string[]): Promise<GroupsAnswer> => {
  const answer = await call<{ sessions?: SessionGrouping[]; entries?: unknown }>('SESSION_GROUPS', ids ? { ids: [...new Set(ids)] } : undefined);
  return { sessions: Array.isArray(answer?.sessions) ? answer.sessions : [], entries: readEntries(answer?.entries) };
};

/**
 * The session list's entries (SESSONE1c, D167 point 5): one line of work in one repository, its records its parts, in the
 * reader's group order. The same answer as `useSessionGroups`, under the same key, so asking both asks once, pushed where it
 * is. Undefined while no answer has come and from a host older than entries, and then the list is the records it always was;
 * shell-only.
 */
export const useSessionEntries = (ids?: readonly string[], { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery({ ...useGroupsRead(ids, entriesOf), enabled: isAvailable && enabled });
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
 * its code. The sessions, their groups among them, are asked again by the state hub alone (STATEHUB1d, STATEHUB1f).
 */
export const useArchiveSessions = () =>
  useMutation({
    mutationFn: (move: { ids: readonly string[]; archived: boolean }) =>
      call<ArchiveAnswer>('SESSION_ARCHIVE', { ids: [...new Set(move.ids)], archived: move.archived }),
  });

/**
 * What a delete answered (SESSUX1f, D126 §5.4): the session, what went by name, and what the disk would not let go of by
 * the same names, left over (SESSDEL1), empty or absent when everything went; nothing machine-local.
 */
export type DeleteAnswer = { deleted: string; removed?: string[]; stayed?: string[] };

/**
 * *Delete…* (SESSUX1f, D126 §5.4): a conversation that served no quest, its record through the ledger and what this
 * machine kept of it, by its id alone. Each refusal is a code the catalogue says, naming what kept it. The sessions,
 * their groups among them, are asked again by the state hub (STATEHUB1d).
 */
export const useDeleteSession = () => useMutation({
  mutationFn: (id: string) => call<DeleteAnswer>('SESSION_DELETE', { id }),
});

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
 * and the driver are asked again by the state hub (STATEHUB1d), since the loop takes the choice up at its next look.
 */
export const useGoOnNew = () => useMutation({
  mutationFn: async (id: string): Promise<NewSessionAnswer> => {
    const answer = await call<{ sent?: boolean; why?: string | null; message?: string } | null>('SESSION_GO_ON_NEW', { id });
    return {
      sent: answer?.sent === true,
      why: typeof answer?.why === 'string' ? answer.why : null,
      message: typeof answer?.message === 'string' ? answer.message : '',
    };
  },
});

/** What *Ask another agent…* answered (MIXAGENT2b, D163): the ask's id where it was written, else why not by its code, and the driver's sentence. */
export type HelperAsked = { asked: string | null; why: string | null; message: string };

/** What the person's yes to an agent's ask answered (MIXAGENT2b): whether the ask has it, else why not by its code. */
export type HelperApproved = { approved: boolean; why: string | null; message: string };

/** What the person's no to an agent's ask answered (HELPERNO1): whether the ask has it, else why not by its code. */
export type HelperDeclined = { declined: boolean; why: string | null; message: string };

const word = (value: unknown): string | null => (typeof value === 'string' && value ? value : null);

/**
 * *Ask another agent…* (MIXAGENT2b, D163 point 2; the mixed-agents design §3): the person's own ask of a helper for a session, the
 * file `daoris-driver sessions helper` writes, for the driver's next look to take. The question as written and the agent the person
 * named, none where Daoris chooses. Read field by field, so a refusal keeps its code for the page to word. The sessions and the
 * driver are asked again by the state hub (D169), and with the sessions the session's asks, which sit under their key.
 */
export const useAskHelper = () => useMutation({
  mutationFn: async (ask: { session: string; question: string; agent?: string | null }): Promise<HelperAsked> => {
    const answer = await call<{ asked?: unknown; why?: unknown; message?: unknown } | null>('SESSION_HELPER_ASK', {
      id: ask.session, question: ask.question, ...(ask.agent ? { agent: ask.agent } : {}),
    });
    return { asked: word(answer?.asked), why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '' };
  },
});

/** The asks a session made of helpers, under the sessions' key, so whatever asks the records again asks these too. */
const helpersKey = (session: string) => ['sessions', 'helpers', session] as const;

/** The rows an answer holds, read defensively: one with no id or no question is left out rather than guessed at. */
const readHelpers = (answer: unknown): HelperAskRow[] => {
  const rows = (answer as { helpers?: unknown } | null)?.helpers;
  if (!Array.isArray(rows)) return [];
  return rows.filter((row): row is Record<string, unknown> => Boolean(row) && typeof row === 'object')
    .filter((row) => word(row.id) !== null && typeof row.question === 'string')
    .map((row) => ({
      id: row.id as string,
      question: row.question as string,
      agent: word(row.agent),
      askedBy: word(row.askedBy) ?? 'agent',
      at: typeof row.at === 'string' ? row.at : '',
      state: word(row.state),
      code: word(row.code),
      helper: word(row.helper),
      startedOn: word(row.startedOn),
    }));
};

/**
 * The asks a session made of helpers (MIXAGENT2b), oldest first, each as the driver's look left it, read off this machine's own
 * files: shell-only, and asked only for a session of this machine's. Its page lists them, with the person's yes where an agent's
 * ask waits on it. A look moves them on its own; its record's change, a helper started or an answer handed, asks them again.
 */
export const useSessionHelpers = (session: string | null | undefined) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: helpersKey(session ?? ''),
    queryFn: async () => readHelpers(await call<unknown>('SESSION_HELPERS', { id: session })),
    enabled: isAvailable && Boolean(session),
    refetchOnWindowFocus: false,
  });
};

/**
 * The person's yes to an agent's ask under the `helpers` rule's `ask` (MIXAGENT2b, D163 point 3; design §5): the `approved` state
 * the driver's look reads, by the session and the ask. A refusal is the answer, by its code. The sessions and the driver are asked
 * again by the state hub (D169).
 */
export const useApproveHelper = () => useMutation({
  mutationFn: async (yes: { session: string; ask: string }): Promise<HelperApproved> => {
    const answer = await call<{ approved?: unknown; why?: unknown; message?: unknown } | null>('SESSION_HELPER_APPROVE', {
      session: yes.session, ask: yes.ask,
    });
    return { approved: answer?.approved === true, why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '' };
  },
});

/**
 * The person's no to an agent's ask the driver's look holds waiting on their word (HELPERNO1, D163 point 3): the ask refused
 * `declined` and its note on the asking session's record, at once, by the session and the ask. A refusal is the answer, by its
 * code. The sessions and the driver are asked again by the state hub (D169).
 */
export const useDeclineHelper = () => useMutation({
  mutationFn: async (no: { session: string; ask: string }): Promise<HelperDeclined> => {
    const answer = await call<{ declined?: unknown; why?: unknown; message?: unknown } | null>('SESSION_HELPER_DECLINE', {
      session: no.session, ask: no.ask,
    });
    return { declined: answer?.declined === true, why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '' };
  },
});

/**
 * A session's agent's own ask to hand it over (MIXAGENT1c, D163 point 3), as the driver's look left it, read off this machine's own
 * files: shell-only, and asked only for a session of this machine's; null where its agent asked none. Under the sessions' key, so a
 * look's tick and the session's moves ask it again, as its helpers' asks are.
 */
export const useSessionHandOverAsk = (session: string | null | undefined) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: ['sessions', 'handOverAsk', session ?? ''] as const,
    queryFn: async (): Promise<HandOverAskRow | null> => {
      const ask = (await call<{ ask?: unknown } | null>('SESSION_HAND_OVER_ASK', { id: session }))?.ask as Record<string, unknown> | null;
      if (!ask || typeof ask !== 'object' || word(ask.id) === null || typeof ask.note !== 'string') return null;
      return {
        id: ask.id as string, note: ask.note, at: typeof ask.at === 'string' ? ask.at : '',
        state: word(ask.state), code: word(ask.code), agent: word(ask.agent),
      };
    },
    enabled: isAvailable && Boolean(session),
    refetchOnWindowFocus: false,
  });
};

/**
 * A parked session's question to the person (PARKVIEW1, the park's ask design §2), as its agent asked it through `question_ask` and
 * the driver kept it for the park its last turn ended in, read off this machine's own files: shell-only, and asked only for a park of
 * this machine's still waiting on the person (null asks nothing); null where none is kept. Under the sessions' key, so a look's tick
 * and the session's moves ask it again, as its hand-over ask is.
 */
export const useSessionQuestion = (session: string | null | undefined) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: ['sessions', 'question', session ?? ''] as const,
    queryFn: async (): Promise<ParkQuestion | null> => readQuestion(await call<unknown>('SESSION_QUESTION', { id: session })),
    enabled: isAvailable && Boolean(session),
    refetchOnWindowFocus: false,
  });
};

/** What the person's yes or no to their session's agent's ask to hand over came to: whether it stands, its code, the sentence. */
export type HandOverWord = { stands: boolean; why: string | null; message: string; agent: string | null };

/**
 * The person's yes to their session's agent's ask to hand it over (MIXAGENT1c): a conversation this shell holds is handed over at
 * once, anything else is `approved` for the driver's look. A refusal is the answer, by its code. The sessions and the driver are
 * asked again by the state hub (D169).
 */
export const useApproveHandOver = () => useMutation({
  mutationFn: async (yes: { session: string; ask: string }): Promise<HandOverWord> => {
    const answer = await call<{ approved?: unknown; why?: unknown; message?: unknown; agent?: unknown } | null>('SESSION_HAND_OVER_APPROVE', {
      session: yes.session, ask: yes.ask,
    });
    return {
      stands: answer?.approved === true, why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '',
      agent: word(answer?.agent),
    };
  },
});

/** The person's no to their session's agent's ask to hand it over (MIXAGENT1c): refused `declined`, its note on the record, at once. */
export const useDeclineHandOver = () => useMutation({
  mutationFn: async (no: { session: string; ask: string }): Promise<HandOverWord> => {
    const answer = await call<{ declined?: unknown; why?: unknown; message?: unknown } | null>('SESSION_HAND_OVER_DECLINE', {
      session: no.session, ask: no.ask,
    });
    return { stands: answer?.declined === true, why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '', agent: null };
  },
});

/**
 * What of the agents' asks waits on the person's word across this machine's sessions (MIXAGENT2c, D168): *What needs you*'s rows,
 * oldest first, read off this machine's own files, so shell-only. Under the sessions' key, so every tick asks it again, and the
 * driver's look says a new one in its report, which is a tick. A row with no id, session or question is left out.
 */
export const useHelperWaits = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: ['sessions', 'helperWaits'] as const,
    queryFn: async (): Promise<HelperWait[]> => {
      const rows = (await call<{ waits?: unknown } | null>('HELPER_WAITS'))?.waits;
      if (!Array.isArray(rows)) return [];
      return rows.filter((row): row is Record<string, unknown> => Boolean(row) && typeof row === 'object')
        .filter((row) => word(row.id) !== null && word(row.session) !== null && typeof row.question === 'string')
        .map((row) => ({
          id: row.id as string,
          session: row.session as string,
          question: row.question as string,
          agent: word(row.agent),
          at: typeof row.at === 'string' ? row.at : '',
        }));
    },
    enabled: isAvailable,
  });
};

/**
 * The held permissions' key (AUTONOMY1b3c): under the sessions', so a session's publication (a turn's cancel, a stop, a session's
 * end, each a release) asks them again with the sessions.
 */
export const PERMISSION_WAITS_KEY = ['sessions', 'permissionWaits'] as const;

/** How often the held permissions are asked while one may come or go: a person at the window answers within seconds. */
const PERMISSIONS_EVERY = 4_000;

/**
 * When the held permissions are asked again on a clock (AUTONOMY1b3c): every few seconds while a session of this machine works, or
 * while one is held, and never otherwise, since neither route publishes (`HeldPermissions` is in the driver's memory). Where the calls
 * are pushed (`pushed`), never (AUTONOMY1b3b): a hold marks its call waiting in the calls slice and its answer or release clears the
 * mark, so the mark's move asks them again (`usePermissionWaits`). The clock stays for a host that pushes no calls.
 */
export const permissionsAgainIn = (live: boolean, held: number, pushed = false): number | false =>
  (!pushed && (live || held > 0) ? PERMISSIONS_EVERY : false);

/**
 * The pushed calls' waiting marks as one word (AUTONOMY1b3b): each waiting session with when its newest hold began, so a hold, an
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
 * What this machine's driven sessions hold for the person under their repository's asking permission prompts (AUTONOMY1b3a, D184
 * point 3): each tool's request with its session, its title and kind as the agent wrote them, and what the person may choose, oldest
 * first, off the driver's memory, so shell-only. `live`: a session of this machine works now, so one may be asked for at any moment.
 *
 * Where the calls are pushed, asked again as their waiting marks move (AUTONOMY1b3b), once whichever reader saw it first: each
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
 * The person's answer to a permission a session holds for them (AUTONOMY1b3a, D184 point 3): an option the agent offered, by its id,
 * for this call alone; Daoris answers no allow by itself (D37, D52). A refusal is the answer, by its code (`unheld`, `unoffered`,
 * `standing`). The route publishes nothing, so the waits are asked again here, and the card goes with the hold.
 */
export const useAnswerPermission = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async (answer: { session: string; request: string; option?: string | null }): Promise<PermissionAnswered> => {
      const said = await call<{ answered?: unknown; why?: unknown; message?: unknown } | null>('SESSION_PERMISSION_ANSWER', {
        session: answer.session, request: answer.request, ...(answer.option ? { option: answer.option } : {}),
      });
      return { answered: said?.answered === true, why: word(said?.why), message: typeof said?.message === 'string' ? said.message : '' };
    },
    onSettled: () => client.invalidateQueries({ queryKey: PERMISSION_WAITS_KEY }),
  });
};

/** The answers-alone waits' key (COWORK1e): under the sessions', so a tick and the person's answer (its publication) ask them again. */
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
    }));
};

/**
 * What the answers-alone bound holds for the person across this machine's sessions (COWORK1e, D188 point 4; the message-urgency design
 * §8.3): each session whose answers would take its line on a third time in a row with nobody between, the agent that answered, the
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
 * The person's answer to a third answer alone held for them (COWORK1e; §8.3): *Go on with this answer* (`goOn`), their word, which lets
 * the answer go on, or *Not now*, which keeps it for them. A refusal is the answer, by its code (`unheld`). The sessions and the driver
 * are asked again by the state hub (D169); the waits are asked again here too, so the wait goes with the answer.
 */
export const useAnswerAlone = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async (answer: { session: string; goOn: boolean }): Promise<AnswerAloneAnswered> => {
      const said = await call<{ answered?: unknown; why?: unknown; message?: unknown } | null>('SESSION_ANSWER_ALONE', {
        session: answer.session, goOn: answer.goOn,
      });
      return { answered: said?.answered === true, why: word(said?.why), message: typeof said?.message === 'string' ? said.message : '' };
    },
    onSettled: () => client.invalidateQueries({ queryKey: ANSWER_WAITS_KEY }),
  });
};

/** Where a search found its words: the session, the event it began at, whose words, and a window of them. */
export type SessionHit = { session: string; seq: number; kind: string; snippet: string };

/** How many sessions a page of a search holds, asked for by name (RAILPAGE1b): the driver's own default, so a page is the same either way. */
export const SEARCH_PAGE = 16;

/** A search's answer as the driver gave it: `total` and `next` are absent from a shell before pages (RAILPAGE1a). */
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
 * What sessions said, searched (RAIL1): the person's words and the agent's, on this machine's own
 * record, a page of sessions at a time (RAILPAGE1b): `fetchNextPage` asks for the one at the driver's `next`, and `data` is every
 * page read, joined. Asked from two letters on, or one Han character (`searchable`, RAILSRCH1b) — one other letter matches
 * everything — and the caller debounces, so a word typed is one question, not one per key.
 *
 * @remarks
 * A shell before pages ignores `offset` and `limit` and answers no `total` or `next`: its answer is the one page, with the `cut`
 * it always had, and `total` is null so the page knows to say that instead of counting.
 */
export const useSessionSearch = (query: string, session?: string) => {
  const { isAvailable } = useShenora();
  const q = query.trim();
  return useInfiniteQuery({
    queryKey: keys.sessionSearch(q, session),
    // Within one session, where one is named (SESS1 S9): its words and its calls by their titles.
    queryFn: ({ pageParam }) => call<SearchAnswer>('SESSION_SEARCH', {
      q,
      ...(session ? { session } : {}),
      limit: SEARCH_PAGE,
      ...(pageParam > 0 ? { offset: pageParam } : {}),
    }),
    initialPageParam: 0,
    getNextPageParam: (page: SearchAnswer) => countOf(page.next),
    select: joinSearch,
    enabled: isAvailable && searchable(q),
    staleTime: 10_000,
    refetchOnWindowFocus: false,
    retry: false,
  });
};
