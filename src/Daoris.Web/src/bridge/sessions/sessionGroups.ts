import { useCallback } from 'react';
import { useQuery, useQueryClient, type UseQueryOptions } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { GROUPS_SLICE } from '../../data/queries';
import type { SessionEntry } from '../../work/entries';
import { GROUP_ORDER, type SessionGrouping } from '../../work/groups';
import { readSlice } from '../driver';
import { mergeKeyed, readPushed, type SliceSpec } from '../state/slices';

/**
 * The session list's groups, under the sessions' key, so whatever asks the listing again (a stop, a landing, a tick)
 * asks this too; but the widest where the shell pushes it, which its pushes write.
 */
export const groupsKey = (ids?: readonly string[]) => ['sessions', 'groups', ...(ids ? [...new Set(ids)].sort() : ['*'])] as const;

/** One `SESSION_GROUPS` answer: the rows, and the entries beside them (D167); undefined where the partition held none. */
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
 * the fraction's further digits the rest), never the strings. The pushed quests and asks are ordered by it too.
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
 * The reader's order (`SessionGroups.Ordered`, D126 §2.1): the groups in `GROUP_ORDER`; waiting on you and
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
 * The entries as the answer holds them, read defensively: absent where the host is older than the entries, and then the list
 * is the records it always was. A malformed entry is left out rather than guessed at.
 */
const readEntries = (answer: unknown): SessionEntry[] | undefined => (Array.isArray(answer)
  ? answer.filter((row): row is SessionEntry => Boolean(row)
    && typeof (row as SessionEntry).leading === 'string'
    && Array.isArray((row as SessionEntry).parts)
    && (row as SessionEntry).parts.length > 0)
  : undefined);

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
 * The session list's groups as the shell pushes them (D182's PUSH1c3a note): two keyed partitions, `rows` by session and
 * `entries` by key, a session's move a delta of its row and its entry (`mergeKeyed`; a whole push or `{}` taken as well), composed
 * into `SESSION_GROUPS`' widest answer in the reader's order. Nothing judged, or no rows held yet, composes nothing, which is
 * waited for. `calling` is never pushed here: the `calls` slice's is laid over the rows (`withCall`).
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
 * Every reader reads the one entry the shell's pushes write, from memory, and some sessions' variant is selected from it
 * (`groupsIn`): never stale, never asked on a focus, and no tick reaches it (`notPushed`).
 */
function useGroupsRead<T>(ids: readonly string[] | undefined, pick: (answer: GroupsAnswer) => T)
  : UseQueryOptions<GroupsAnswer, Error, T, readonly string[]> {
  const client = useQueryClient();
  const asked = ids ? [...new Set(ids)].sort() : undefined;
  // By the sessions' names, so a caller's new array of the same ids keeps what the cache selected.
  const named = asked?.join('\n');
  const select = useCallback((answer: GroupsAnswer) => pick(groupsIn(answer, asked)), [pick, named]);
  return {
    queryKey: groupsKey(),
    queryFn: () => readPushed<GroupsAnswer>(client, GROUPS_SLICE),
    select, staleTime: Infinity, refetchOnWindowFocus: false,
  };
}

const rowsOf = (answer: GroupsAnswer) => answer.sessions;
const entriesOf = (answer: GroupsAnswer) => answer.entries;

/**
 * Each session's group by state, its word and its line's facts, in the order a list shows them; asked for
 * some, those sessions' alone. Shell-only: Sessions is (D47 §4), and no browser has a driver to ask. A list that does
 * not read the groups (the monitor's) asks nothing, since each answer may walk git in every tree to review.
 * Pushed where the shell pushes them (`useGroupsRead`).
 */
export const useSessionGroups = (ids?: readonly string[], { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery({ ...useGroupsRead(ids, rowsOf), enabled: isAvailable && enabled });
};

/**
 * The session list's entries (D167 point 5): one line of work in one repository, its records its parts, in the
 * reader's group order. The same answer as `useSessionGroups`, under the same key, so asking both asks once, pushed where it
 * is. Undefined while no answer has come and where the answer carries no entries, and then the list is the records;
 * shell-only.
 */
export const useSessionEntries = (ids?: readonly string[], { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery({ ...useGroupsRead(ids, entriesOf), enabled: isAvailable && enabled });
};
