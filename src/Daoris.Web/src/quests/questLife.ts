import type { Quest } from '../data/api';
import type { FlowEdge, FlowNode } from '../design/Flow';
import { QUEST_TONE } from '../design/ui';
import { shownStatus } from './records';

/** A quest's four statuses, in the service's order (`QuestStatus`). */
export const QUEST_STATUSES = ['Open', 'Taken', 'Done', 'Declined'] as const;
type Status = (typeof QUEST_STATUSES)[number];

/**
 * The moves `QuestTransitions.Allows` allows (D46 §3): taken only from open; done and declined from open or taken; nothing
 * leaves a close, and nothing returns to open. A page constant, held to the service's source by `questLife.test.ts`.
 */
export const QUEST_MOVES: readonly (readonly [Status, Status])[] = [
  ['Open', 'Taken'], ['Open', 'Done'], ['Open', 'Declined'], ['Taken', 'Done'], ['Taken', 'Declined'],
];

type Translate = (key: string, values?: Record<string, unknown>) => string;

/**
 * **A quest's life as a flow** (FLOWCHART1a1; the flowchart design §2.1): its four statuses and the moves the table allows,
 * entered at Open by its publish, the status it stands at lit in its own tone (`QUEST_TONE`), and each move said by its
 * cause in its source's foot.
 *
 * @remarks
 * **It claims only what the page holds** (trap 19). Open is reached by every quest, and the status it stands at is reached;
 * what is still to come from there is not. A closed quest may or may not have been taken first, and without its moves
 * (FLOWCHART1a2 reads them) the page cannot say, so its Taken is drawn neither reached nor not. No path is drawn.
 */
export function questFlow(t: Translate, quest: Pick<Quest, 'id' | 'status' | 'submission'>): { label: string; nodes: FlowNode[]; edges: FlowEdge[]; entries: string[] } {
  const status = quest.status;
  const closed = status === 'Done' || status === 'Declined';
  const shown = shownStatus(quest);
  const reached = (each: Status): boolean | undefined => {
    if (each === 'Open' || each === status) return true;
    if (each === 'Taken' && closed) return undefined;
    return false;
  };
  const nodes = QUEST_STATUSES.map((each): FlowNode => ({
    id: each,
    title: t(`status.${each}`),
    reached: reached(each),
    current: each === status,
    ...(each === status ? { state: { word: shown === status ? t('quests.flow.here') : t(`status.${shown}`), tone: QUEST_TONE[status] } } : {}),
  }));
  const edges = QUEST_MOVES.map(([from, to]): FlowEdge => ({ id: `${from}>${to}`, from, to, says: t(`quests.flow.move.${from}.${to}`) }));
  return { label: t('quests.flow.label', { id: quest.id }), nodes, edges, entries: ['Open'] };
}
