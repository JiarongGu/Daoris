import type { Quest, QuestHold, QuestMove } from '../data/api';
import type { FlowEdge, FlowNode } from '../design/Flow';
import { QUEST_TONE } from '../design/ui';
import { moment } from '../locales/format';
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
 * One line inside a status (FLOWCHART1a2): a move that changed no status, one the replay did not apply, or a conflict, which is
 * `refused`. Its words carry who made it and when.
 */
export type QuestMoveLine = { id: string; text: string; refused?: boolean; notApplied?: boolean };

/** What the quest's page draws: the flow's own props, and each status's lines, oldest first. */
export type QuestLife = {
  label: string;
  nodes: FlowNode[];
  edges: FlowEdge[];
  entries: string[];
  lines: Record<Status, QuestMoveLine[]>;
};

/** The quest's own fields the flow reads: its status and submission, its hold, and this machine's id where the host says it. */
export type QuestOfLife = Pick<Quest, 'id' | 'status' | 'submission' | 'hold' | 'here'>;

const isStatus = (value: unknown): value is Status => (QUEST_STATUSES as readonly unknown[]).includes(value);

/** The status a move of this kind moves a quest to (`QuestTransitions.Target`): a take, a done and a decline; nothing else. */
const TARGET: Record<string, Status> = { taken: 'Taken', done: 'Done', declined: 'Declined' };

/**
 * What a held quest waits on, in the words its own pill says them (`QuestList`): a review for an unreviewed set-up, the person's
 * word for a step whose source they reopened, and otherwise their yes.
 */
export const heldWord = (t: Translate, hold: QuestHold): string =>
  hold === 'unreviewed' ? t('review.pill') : hold === 'source-reopened' ? t('quests.card.reopened') : t('quests.card.held');

/**
 * **A quest's life as a flow** (FLOWCHART1a1-a2; the flowchart design §2.1): its four statuses and the moves the table allows,
 * entered at Open by its publish, the status it stands at lit in its own tone (`QUEST_TONE`), and each move said by its cause in
 * its source's foot. With its moves read (`GET /api/quests/{id}/moves`), the way it went: each move on its path drawn heavier
 * and said, the statuses it reached, who made each move into them, and inside each status what happened there.
 *
 * @remarks
 * **It claims only what the record holds** (traps 19 and 20):
 * - **Without its moves**, Open is reached by every quest and the status it stands at is reached; a closed quest may or may not
 *   have been taken first, so its Taken is drawn neither reached nor not. An older host keeps no moves (`null`, trap 18), and
 *   neither does an empty history: the statuses are drawn as without them.
 * - **With its moves**, the path is what the log holds, in the order its history replays, never by time: a status is reached
 *   where a move the service's replay applied moved the quest into it, and a history from before the log (its publish and its
 *   close) is drawn as recorded, with no take filled in. A move that did not apply is listed inside the status it reached the
 *   quest at, never drawn on the path; so is every move that changes no status. A take names the machine, never a session.
 * - **A conflict** is listed inside its status, and while no dismissal names it by its machine and sequence, drawn as a refused
 *   edge toward the status it attempted; one that attempted the status it happened in is listed alone, since an edge into itself
 *   draws nothing.
 * - **A held Done** says what it waits on in open's hue (§2.1), as the quest's own pill does: the hue that marks what waits on the
 *   person.
 * - A field a move does not keep is said not recorded, and a kind this page does not know is shown as recorded (trap 10).
 */
export function questFlow(t: Translate, quest: QuestOfLife, moves?: QuestMove[] | null): QuestLife {
  const status = quest.status;
  const closed = status === 'Done' || status === 'Declined';
  const shown = shownStatus(quest);
  const path = moves && moves.length > 0 ? moves : null;
  const who = (machine: string) => (machine === quest.here ? t('quests.flow.thisMachine') : t('quests.flow.machine', { machine }));
  const line = (move: QuestMove, what: string) => {
    const said = t('quests.flow.line', { what, who: who(move.machine), when: moment(move.at) });
    if (!move.hold) return said;
    return move.hold === 'none' ? t('quests.flow.holdLet', { line: said }) : t('quests.flow.holdSet', { line: said, held: heldWord(t, move.hold) });
  };

  // The way it went: each move the replay applied into a status, and the statuses it reached.
  const into = new Map<Status, QuestMove>();
  const went = new Set<string>();
  for (const move of path ?? []) {
    if (!move.applied) continue;
    if (move.kind === 'published') into.set('Open', move);
    const to = TARGET[move.kind];
    if (to && isStatus(move.from)) {
      into.set(to, move);
      went.add(`${move.from}>${to}`);
    }
  }

  const reached = (each: Status): boolean | undefined => {
    if (each === 'Open' || each === status) return true;
    if (path) return into.has(each);
    if (each === 'Taken' && closed) return undefined;
    return false;
  };

  const nodes = QUEST_STATUSES.map((each): FlowNode => {
    const moved = path ? into.get(each) : undefined;
    const held = each === 'Done' && status === 'Done' && quest.hold ? quest.hold : null;
    return {
      id: each,
      title: t(`status.${each}`),
      reached: reached(each),
      current: each === status,
      ...(each === status
        ? {
          state: held
            ? { word: heldWord(t, held), tone: QUEST_TONE.Open }
            : { word: shown === status ? t('quests.flow.here') : t(`status.${shown}`), tone: QUEST_TONE[status] },
        }
        : {}),
      ...(moved ? { actor: line(moved, said(t, moved, path!, who)) } : {}),
    };
  });

  // Inside each status, oldest first: every move that is not a move into a status, and every one the replay did not apply.
  const lines: Record<Status, QuestMoveLine[]> = { Open: [], Taken: [], Done: [], Declined: [] };
  const dismissed = new Set((path ?? []).filter((move) => move.kind === 'dismissed' && move.applied && move.dismisses)
    .map((move) => `${move.dismisses!.machine}:${move.dismisses!.sequence}`));
  const refused: FlowEdge[] = [];
  for (const move of path ?? []) {
    const onPath = move.applied && (move.kind === 'published' || (TARGET[move.kind] !== undefined && isStatus(move.from)));
    if (onPath) continue;
    const at: Status = isStatus(move.from) ? move.from : 'Open';
    const text = line(move, said(t, move, path!, who));
    const id = `${move.machine}:${move.sequence}`;
    lines[at].push({
      id,
      text: move.applied ? text : t('quests.flow.notApplied', { line: text }),
      ...(move.kind === 'conflict' ? { refused: true } : {}),
      ...(move.applied ? {} : { notApplied: true }),
    });
    if (move.kind === 'conflict' && move.applied && isStatus(move.attempted) && move.attempted !== at && !dismissed.has(id)) {
      refused.push({
        id: `conflict:${id}`, from: at, to: move.attempted, style: 'refused',
        says: t('quests.flow.refused', { conflict: conflictOf(t, move, who) }),
      });
    }
  }

  const edges = [
    ...QUEST_MOVES.map(([from, to]): FlowEdge => {
      const says = t(`quests.flow.move.${from}.${to}`);
      return went.has(`${from}>${to}`)
        ? { id: `${from}>${to}`, from, to, says: t('quests.flow.went', { says }), taken: true }
        : { id: `${from}>${to}`, from, to, says };
    }),
    ...refused,
  ];
  return { label: t('quests.flow.label', { id: quest.id }), nodes, edges, entries: ['Open'], lines };
}

/** A conflict as a dismissal and its refused edge name it: whose move, to what. */
function conflictOf(t: Translate, conflict: QuestMove, who: (machine: string) => string): string {
  return t('quests.flow.conflictOf', { who: who(conflict.machine), attempted: t(`status.${conflict.attempted}`) });
}

/** What one move did, in the catalogue's words for its kind (trap 12: every kind the service has is worded, held by its test). */
function said(t: Translate, move: QuestMove, moves: QuestMove[], who: (machine: string) => string): string {
  switch (move.kind) {
    case 'conflict':
      return isStatus(move.attempted) ? t('quests.flow.kind.conflict', { attempted: t(`status.${move.attempted}`) }) : t('quests.flow.conflictUnsaid');
    case 'dismissed': {
      const named = move.dismisses
        ? moves.find((each) => each.kind === 'conflict' && each.machine === move.dismisses!.machine && each.sequence === move.dismisses!.sequence)
        : undefined;
      return named && isStatus(named.attempted)
        ? t('quests.flow.kind.dismissed', { conflict: conflictOf(t, named, who) })
        : t('quests.flow.dismissedElsewhere');
    }
    case 'waited':
      return move.waitsOn ? t('quests.flow.kind.waited', { quest: move.waitsOn }) : t('quests.flow.waitedUnsaid');
    case 'submitted':
      return move.session ? t('quests.flow.kind.submitted', { session: move.session }) : t('quests.flow.submittedUnsaid');
    case 'evidenced':
      return t('quests.flow.kind.evidenced', {
        result: t(['found', 'missing', 'commit'].includes(move.evidence ?? '') ? `quests.flow.evidence.${move.evidence}` : 'quests.flow.evidence.unsaid'),
      });
    case 'setup':
      return t('quests.flow.kind.setup', {
        where: t(move.setUp === 'local' || move.setUp === 'deployed' ? `quests.flow.setUp.${move.setUp}` : 'quests.flow.setUp.unsaid'),
      });
    case 'verdict':
      return move.verdict
        ? t('quests.flow.kind.verdict', {
          verdict: ['reviewed', 'not-yet', 'skipped'].includes(move.verdict) ? t(`quests.flow.verdict.${move.verdict}`) : move.verdict,
        })
        : t('quests.flow.verdictUnsaid');
    case 'published': case 'taken': case 'done': case 'declined': case 'deleted': case 'accepted': case 'bound': case 'reopened':
    case 'rebound':
      return t(`quests.flow.kind.${move.kind}`);
    default:
      return t('quests.flow.kind.unknown', { kind: move.kind });
  }
}
