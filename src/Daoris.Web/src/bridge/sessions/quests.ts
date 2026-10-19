import type { Ask, Quest } from '../../data/api';
import { ASKS_SLICE, keys, QUESTS_SLICE } from '../../data/queries';
import { readSlice } from '../driver';
import { mergeKeyed, type SliceSpec } from '../state/slices';
import { byInstant } from './sessions';

// The quests and the asks as the shell pushes them (PUSH1d3, D182), each as the sessions are (PUSH1d2): a partition per workspace, its
// records by id with the fields the page reads, a record's move a delta of that record, composed into the widest list its route answers,
// from which `useQuests` and `useAsks` read each view's list (`questsIn`, `asksIn`). A file's path is never pushed: `kept` says whether
// this machine keeps it, which `fileKept` reads beside a pulled path.

const byId = (a: { id: string }, b: { id: string }) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0);

/** Where a quest's status sits in the service's order (`Quests.ListAsync`): open, then taken, then the rest. */
const standing = (quest: Quest) => (quest.status === 'Open' ? 0 : quest.status === 'Taken' ? 1 : 2);

/** The service's order of its quests: open, then taken, then the rest, each by when it was filed; a tie by its id. */
const questOrder = (a: Quest, b: Quest) => standing(a) - standing(b) || byInstant(a.filed, b.filed) || byId(a, b);

/** The service's order of its asks (its store's `ListAsync`, `ORDER BY asked DESC`): the newest asked first; a tie by its id. */
const askOrder = (a: Ask, b: Ask) => byInstant(b.asked, a.asked) || byId(a, b);

const isRecord = (value: unknown): value is { id: string } =>
  typeof value === 'object' && value !== null && typeof (value as { id?: unknown }).id === 'string';

/** Every record of every partition held, in `order`; nothing before the owner judged, and an empty list once it judged none. */
const composed = <T extends { id: string }>(order: (a: T, b: T) => number) =>
  (partitions: ReadonlyMap<string, unknown>, judged: boolean): T[] | undefined => {
    if (!judged) return undefined;
    return [...partitions.values()]
      .flatMap((part) => (typeof part === 'object' && part !== null && !Array.isArray(part) ? Object.values(part).filter(isRecord) as T[] : []))
      .sort(order);
  };

/** The quests as the shell pushes them (`QuestsStateSlice`), composed into what `/api/quests` answers with the closed, every workspace's. */
export const questsSlice: SliceSpec = {
  name: QUESTS_SLICE,
  key: keys.quests(null, true, null),
  read: () => readSlice(QUESTS_SLICE),
  compose: composed<Quest>(questOrder),
  merge: mergeKeyed,
};

/** The asks as the shell pushes them (`AsksStateSlice`), composed into what `/api/asks` answers with the closed, every workspace's. */
export const asksSlice: SliceSpec = {
  name: ASKS_SLICE,
  key: keys.asks(true, null),
  read: () => readSlice(ASKS_SLICE),
  compose: composed<Ask>(askOrder),
  merge: mergeKeyed,
};
