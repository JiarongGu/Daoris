import type { Ask, Quest, Session } from '../data/api';
import { SESSION_ACTIVE } from '../data/api';
import { shownStatus } from '../quests/records';

/**
 * One thing an Ask Daoris conversation's Apply published (ASKQUESTS1, the Ask Daoris design §9 item 12), as `HELP_PUBLISHED`
 * answers it: when, the proposal applied, its kind, the workspace it was asked at, and the ask and the quest by the ids the
 * service answered. Ids only: each one's state is read live from the pushed quests, asks and sessions.
 */
export type HelpPublishedEntry = {
  at: string; proposal: string; kind: string; workspace: string | null; ask: string | null; quest: string | null;
};

/** What a record is while the lists are read: its record, `reading` before the list is answered, `gone` once it holds none by its id. */
export type Held<T> = T | 'reading' | 'gone';

/**
 * Where a published quest stands, as its list says it (D185 §3.8: a slice not judged is reading, never empty): its status, `held`
 * where a departure holds its done for the person's yes (DRIFT1d), which waits on them; its session here, the live one on it; and
 * whether its take is another machine's, said only where both machines are known and differ.
 */
export type PublishedQuest = {
  id: string;
  record: Held<Quest>;
  shown: ReturnType<typeof shownStatus> | 'Held' | null;
  session: { id: string; state: Session['state'] } | null;
  elsewhere: boolean;
};

/**
 * Where a published ask stands: its state; whether it waits on the person, a proposal theirs to accept or a go-ahead asked of them
 * (KNOWUSE1a); and the quests it became, each as above.
 */
export type PublishedAsk = { id: string; record: Held<Ask>; waits: boolean; quests: PublishedQuest[] };

/** One row of the list: an ask, with the quests it became under it, or a quest published on its own. */
export type PublishedItem =
  | { kind: 'ask'; key: string; at: string; ask: PublishedAsk }
  | { kind: 'quest'; key: string; at: string; quest: PublishedQuest };

/** What the lists hold, each `undefined` while it is not answered yet: never read as empty (pushed-slices: a read not answered is unknown). */
export type PublishedLists = { quests: Quest[] | undefined; asks: Ask[] | undefined; sessions: Session[] | undefined };

const find = <T extends { id: string }>(all: T[] | undefined, id: string): Held<T> =>
  all === undefined ? 'reading' : all.find((each) => each.id === id) ?? 'gone';

function questOf(id: string, lists: PublishedLists): PublishedQuest {
  const record = find(lists.quests, id);
  if (typeof record === 'string') return { id, record, shown: null, session: null, elsewhere: false };
  // The live session on it here: its driven work, never the intake that answered its ask.
  const session = record.status === 'Taken'
    ? (lists.sessions ?? []).find((each) => each.quest === id && !each.ask && SESSION_ACTIVE.has(each.state)) ?? null
    : null;
  const elsewhere = record.status === 'Taken' && session === null
    && Boolean(record.holder) && Boolean(record.here) && record.holder !== record.here;
  return {
    id, record, shown: record.held ? 'Held' : shownStatus(record),
    session: session ? { id: session.id, state: session.state } : null, elsewhere,
  };
}

function askOf(id: string, also: string | null, lists: PublishedLists): PublishedAsk {
  const record = find(lists.asks, id);
  const ids = [...(typeof record === 'string' ? [] : record.quests), ...(also ? [also] : [])];
  const waits = typeof record !== 'string'
    && (record.state === 'Proposed' || (record.goAheads ?? []).some((goAhead) => goAhead.state === 'asked'));
  return { id, record, waits, quests: [...new Set(ids)].map((quest) => questOf(quest, lists)) };
}

/**
 * The list's rows from what the conversation published, the newest first as the driver answers it: an ask with the quests it
 * became, the one its Apply answered among them, and a quest published on its own. A record named twice is one row, the newest.
 */
export function publishedItems(entries: readonly HelpPublishedEntry[], lists: PublishedLists): PublishedItem[] {
  const seen = new Set<string>();
  const items: PublishedItem[] = [];
  for (const entry of entries) {
    const key = entry.ask ? `ask:${entry.ask}` : `quest:${entry.quest}`;
    if (seen.has(key)) continue;
    seen.add(key);
    items.push(entry.ask
      ? { kind: 'ask', key, at: entry.at, ask: askOf(entry.ask, entry.quest, lists) }
      : { kind: 'quest', key, at: entry.at, quest: questOf(entry.quest!, lists) });
  }
  return items;
}

/** What the folded line counts (ASKQUESTS1), each with its noun in the words; a quest under two rows counted once. */
export type PublishedCounts = {
  quests: number; waits: number; open: number; taken: number; done: number; declined: number; asked: number; gone: number;
  reading: boolean;
};

export function publishedCounts(items: readonly PublishedItem[]): PublishedCounts {
  const counts: PublishedCounts = { quests: 0, waits: 0, open: 0, taken: 0, done: 0, declined: 0, asked: 0, gone: 0, reading: false };
  const counted = new Set<string>();
  const quest = (each: PublishedQuest) => {
    if (counted.has(each.id)) return;
    counted.add(each.id);
    if (each.record === 'reading') counts.reading = true;
    else if (each.record === 'gone') counts.gone += 1;
    else {
      counts.quests += 1;
      if (each.shown === 'Held') counts.waits += 1;
      else if (each.shown === 'Open') counts.open += 1;
      else if (each.shown === 'Taken' || each.shown === 'Submitted') counts.taken += 1;
      else if (each.shown === 'Done') counts.done += 1;
      else if (each.shown === 'Declined') counts.declined += 1;
    }
  };
  for (const item of items) {
    if (item.kind === 'quest') {
      quest(item.quest);
      continue;
    }
    const { record } = item.ask;
    if (record === 'reading') counts.reading = true;
    else if (record === 'gone') counts.gone += 1;
    else {
      if (item.ask.waits) counts.waits += 1;
      // Asked and still to become quests: an ask closed or done with none is no promise of one.
      else if (record.state === 'Open' && item.ask.quests.length === 0) counts.asked += 1;
    }
    item.ask.quests.forEach(quest);
  }
  return counts;
}
