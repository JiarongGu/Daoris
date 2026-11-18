import { settle, toTurns } from '../conversation/conversation';
import type { Block, SessionEvent, Turn } from '../conversation/conversationTypes';

/**
 * A subagent's own record as turns: its events joined with the starts and ends of what it ran beside itself, ordered by the moment
 * and numbered afresh, each key set apart by the child's id so none is taken for one of the session's own blocks. Its own
 * subagents are those whose start names it as their parent; a task is drawn where its starting call is one of the child's.
 */
export function childTurns(child: string, events: readonly SessionEvent[], lifetimes: readonly SessionEvent[], live: boolean): Turn[] {
  const its = new Set(lifetimes.filter((event) => event.kind === 'child' && event.parent === child).map((event) => event.id));
  const joined = [...events, ...lifetimes.filter((event) => event.kind === 'task' || its.has(event.id))]
    .map((event, order) => ({ event, order }))
    .sort((a, b) => Date.parse(a.event.at) - Date.parse(b.event.at) || a.order - b.order)
    .map(({ event }, index) => ({ ...event, seq: index + 1 }));
  const apart = (key: string) => `${child}:${key}`;
  const block = (item: Block): Block => ({
    ...item,
    key: apart(item.key),
    ...(item.beside ? {
      beside: {
        ...item.beside,
        ...(item.beside.endKey ? { endKey: apart(item.beside.endKey) } : {}),
        ...(item.beside.startKey ? { startKey: apart(item.beside.startKey) } : {}),
      },
    } : {}),
  });
  return settle(toTurns(joined).turns, live).map((turn) => ({ ...turn, key: apart(turn.key), items: turn.items.map(block) }));
}
