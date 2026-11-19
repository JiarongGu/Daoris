import type { Quest } from '../../data/api';
import type { AtWorkExchange } from '../atWork/types';
import { type ChannelItem, repliesTo } from '../channel';

/**
 * **A line's exchanges and channel messages as rows** of its session's one timeline: a request at its asking and at its coming
 * back, its actor named; a quest its session published, at its filing and at its close; the session that published its own quest,
 * at the start; and each message of its channel at its moment. A row's moment places it between the turns it
 * fell between (`rowsWithin`); a request whose read keeps no moment (an opinion, a quest waited on) stands under the last turn.
 */
export type LineRow =
  | { kind: 'exchange'; key: string; side: 'asked' | 'back'; at: number | null; exchange: AtWorkExchange }
  | { kind: 'quest'; key: string; side: 'asked' | 'back'; at: number | null; quest: Quest; question: boolean }
  | { kind: 'askedBy'; key: string; at: number; session: string }
  | { kind: 'resumed'; key: string; at: number | null; quest: Quest }
  | { kind: 'message'; key: string; at: number | null; item: ChannelItem; quoted: ChannelItem | null };

/** The protocol's words for a request still on its way: asked, waiting on a word or a slot, being worked. */
const ON_ITS_WAY: ReadonlySet<string> = new Set(['asked', 'waiting', 'approved', 'preparing', 'started', 'ending', 'unread']);

const time = (iso: string | null | undefined): number | null => {
  const ms = iso ? Date.parse(iso) : Number.NaN;
  return Number.isFinite(ms) ? ms : null;
};

/** Whether an exchange came back: the protocol's word says it ended, or its answer's moment is held. */
export const cameBack = (exchange: AtWorkExchange): boolean => Boolean(exchange.answered) || !ON_ITS_WAY.has(exchange.outcome);

/**
 * The rows of a line: its exchanges (less those drawn elsewhere: `carded`, an agent's helper ask drawn as its card at its call), the
 * quests its session published (`asked`, whose `quest:` exchange is the same request), the session that published its own quest, and
 * the answered question its session resumed after, and its channel's messages; oldest first, an untimed row after every timed one in the read's order.
 */
export function lineRowsOf({ exchanges = [], carded, asked = [], askedBy = null, resumedAfter = null, messages = [] }: {
  exchanges?: readonly AtWorkExchange[];
  carded?: ReadonlySet<string>;
  asked?: readonly { quest: Quest; question: boolean }[];
  askedBy?: string | null;
  /** The answered question an earlier session asked, which its quest waited on: the line resumed once it came back. */
  resumedAfter?: Quest | null;
  messages?: readonly ChannelItem[];
}): LineRow[] {
  const rows: LineRow[] = [];
  if (askedBy) rows.push({ kind: 'askedBy', key: `asked-by:${askedBy}`, at: Number.NEGATIVE_INFINITY, session: askedBy });
  if (resumedAfter) rows.push({ kind: 'resumed', key: `resumed:${resumedAfter.id}`, at: time(resumedAfter.updated), quest: resumedAfter });
  const published = new Set(asked.map(({ quest }) => `quest:${quest.id}`));
  for (const exchange of exchanges) {
    // A source the read could not reach is said by the read, never drawn as a request.
    if (exchange.outcome === 'unread' || carded?.has(exchange.id) || published.has(exchange.id)) continue;
    rows.push({ kind: 'exchange', key: `${exchange.id}/asked`, side: 'asked', at: time(exchange.at), exchange });
    if (cameBack(exchange)) rows.push({ kind: 'exchange', key: `${exchange.id}/back`, side: 'back', at: time(exchange.answered), exchange });
  }
  for (const { quest, question } of asked) {
    rows.push({ kind: 'quest', key: `quest:${quest.id}/asked`, side: 'asked', at: time(quest.filed), quest, question });
    if (quest.status === 'Done' || quest.status === 'Declined') {
      rows.push({ kind: 'quest', key: `quest:${quest.id}/back`, side: 'back', at: time(quest.updated), quest, question });
    }
  }
  for (const item of messages) {
    if (item.kind === 'message' && item.id) {
      rows.push({ kind: 'message', key: `message:${item.id}`, at: time(item.at), item, quoted: repliesTo(messages, item.re) });
    }
  }
  return rows
    .map((row, order) => ({ row, order }))
    .sort((a, b) => (a.row.at ?? Number.POSITIVE_INFINITY) - (b.row.at ?? Number.POSITIVE_INFINITY) || a.order - b.order)
    .map(({ row }) => row);
}

/** The rows from `from` (none: from the first) until `until` (none: to the foot, with every untimed row): a turn's span. */
export function rowsWithin(rows: readonly LineRow[], from: string | undefined, until: string | undefined): LineRow[] {
  const low = from === undefined ? Number.NEGATIVE_INFINITY : time(from) ?? Number.NEGATIVE_INFINITY;
  const high = until === undefined ? Number.POSITIVE_INFINITY : time(until) ?? Number.POSITIVE_INFINITY;
  return rows.filter((row) => (row.at === null ? until === undefined : row.at >= low && row.at < high));
}

/** What works for the line now, a line each in the live band: a request it waits on that a session took up. */
export const aliveIn = (exchanges: readonly AtWorkExchange[]): AtWorkExchange[] =>
  exchanges.filter((exchange) => exchange.holds && Boolean(exchange.session));

/** Who an exchange asked, as its rows and its band line name it: the agent as the person calls it in its role, a quest by its repository. */
export function coworkerName(t: Said, exchange: AtWorkExchange, nameOf: (agent: string) => string = (agent) => agent): string {
  const actor = actorOf(exchange);
  const agent = exchange.agent ?? (exchange.to && exchange.to !== exchange.part ? exchange.to : null);
  if (actor) {
    return agent
      ? t(`work.atWork.coworker.${actor}`, { agent: nameOf(agent) })
      : t(`work.atWork.coworker.any${actor[0]!.toUpperCase()}${actor.slice(1)}`);
  }
  return t('work.atWork.what.questOf', { quest: exchange.id.slice(exchange.id.indexOf(':') + 1), repository: exchange.to });
}

type Said = (key: string, options?: Record<string, unknown>) => string;

/** The actor an exchange asked, by what it expects back: its catalogue word under `work.atWork.coworker`. */
export function actorOf(exchange: AtWorkExchange): 'helper' | 'partner' | 'successor' | 'reviewer' | null {
  switch (exchange.expects) {
    case 'answer': return 'helper';
    case 'piece': return 'partner';
    case 'successor': return 'successor';
    case 'opinion': return 'reviewer';
    default: return null;
  }
}
