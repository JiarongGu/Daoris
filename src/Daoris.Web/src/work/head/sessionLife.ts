import type { Session, SessionState } from '../../data/api';
import type { FlowEdge, FlowNode } from '../../design/Flow';
import { SESSION_ACTIVE, SESSION_TONE, type ShownState, shownKey, shownState } from '../../design/ui';
import { list } from '../../locales/format';
import { sessionOrigin } from '../identity';

/** The record's nine states, in the service's order (`SessionState`), held to its enum by `sessionLife.test.ts`. */
export const SESSION_STATES: readonly SessionState[] = [
  'queued', 'starting', 'working', 'awaiting-person', 'completed', 'declined', 'stood-down', 'failed', 'stopped',
];

/** The states that still hold their repository, and the five a session ends in (`Session.Active`). */
export const LIVE_STATES: readonly SessionState[] = SESSION_STATES.filter((state) => SESSION_ACTIVE.has(state));
export const ENDED_STATES: readonly SessionState[] = SESSION_STATES.filter((state) => !SESSION_ACTIVE.has(state));

/**
 * The moves `SessionLedger.Allowed` allows (D46): forward only, working and awaiting-person each way, an outcome from the
 * live states that can reach it, and stopped from every live state, since the person's hand is never refused. Nothing
 * leaves an ended state here: going on is the ledger's other move, with guards of its own. A page constant, held to the
 * service's source by `sessionLife.test.ts`.
 */
export const SESSION_MOVES: readonly (readonly [SessionState, SessionState])[] = [
  ['queued', 'starting'], ['queued', 'stood-down'], ['queued', 'failed'], ['queued', 'stopped'],
  ['starting', 'working'], ['starting', 'stood-down'], ['starting', 'failed'], ['starting', 'stopped'],
  ['working', 'awaiting-person'], ['working', 'completed'], ['working', 'declined'], ['working', 'stood-down'],
  ['working', 'failed'], ['working', 'stopped'],
  ['awaiting-person', 'working'], ['awaiting-person', 'completed'], ['awaiting-person', 'declined'], ['awaiting-person', 'stopped'],
];

type Translate = (key: string, values?: Record<string, unknown>) => string;

/** The session's own fields the flow reads: its state, and what makes it a record that is never gone on with. */
export type SessionOfLife = Pick<Session, 'id' | 'state'> & Partial<Pick<Session, 'ask' | 'opinion' | 'serves' | 'answer' | 'kind' | 'limit'>>;

/** One of the outcomes the group node names: whether the record is in it, and whether the ledger lets it go on. */
export type OutcomeMember = { state: SessionState; word: string; current: boolean; goesOn: boolean };

/** What the Timeline view's head draws: the flow's own props, and the words of the outcomes node. */
export type SessionLife = {
  label: string;
  nodes: FlowNode[];
  edges: FlowEdge[];
  entries: string[];
  /** The outcomes node's members, in the ledger's order. */
  members: OutcomeMember[];
  /** Who stops a session, said once on the outcomes rather than on an edge from every live state. */
  stop: string;
  /** The outcome that never goes on, said where the group would otherwise imply they all can. */
  neverGoesOn: string;
};

/** The one outcome the ledger refuses to go on from, whatever else holds (`GoOnAsync`): a stand-down has nothing to go on with. */
const NEVER_GOES_ON: SessionState = 'stood-down';

/**
 * Why a record can never go on, in the ledger's own order (`SessionLedger.GoOnAsync`): another machine's, a stand-down, an
 * intake, a reviewer's pass, a helper's. Null where nothing about the record rules it out; the words waiting and the free
 * tree are the guards the page cannot see, so they are said and never judged.
 */
function cannotGoOn(session: SessionOfLife): 'origin' | 'standDown' | 'ask' | 'opinion' | 'serves' | null {
  if (sessionOrigin(session)) return 'origin';
  if (session.state === NEVER_GOES_ON) return 'standDown';
  if (session.ask) return 'ask';
  if (session.opinion) return 'opinion';
  if (session.serves) return 'serves';
  return null;
}

const isState = (value: unknown): value is SessionState => (SESSION_STATES as readonly unknown[]).includes(value);

/**
 * **A session's states as a flow** (FLOWCHART1b; the flowchart design §2.2): the four live states and the moves
 * `SessionLedger.Allowed` allows between them, the working ⇄ awaiting-person return, and one node of outcomes whose members
 * are words, entered at queued. The state the record is in is lit in its shown word's own tone (`SESSION_TONE`), and each
 * move is said by who makes it.
 *
 * @remarks
 * **It claims only what the record holds** (trap 19):
 * - **No path.** The record keeps where a session stands, not how it got there, so no state before the lit one is drawn
 *   reached or not; only the outcomes are not reached while the record is live. No state is given an entry time either:
 *   `updated` is the last write, not when the session entered its state.
 * - **The outcomes are one node.** The reached member is lit and the person's stop is said once on the node, not drawn from
 *   each live state. Going on leaves only the members the ledger lets it leave, so the node says a stand-down never goes on,
 *   and a record that can never go on has the move drawn refused, with its reason.
 * - **The shown word** (*idle*, *parked*, *awaiting reply*, *answered*, *going on*) is the page's reading over the record's
 *   state, and appears on the lit node in its own tone, where it differs from the node's own name.
 * - A state this page does not know is lit as recorded (trap 10), with no move drawn to or from it.
 */
export function sessionFlow(t: Translate, session: SessionOfLife, shown?: ShownState): SessionLife {
  const state = session.state;
  const known = isState(state);
  const live = known && SESSION_ACTIVE.has(state);
  const word: ShownState = shown ?? shownState(session, undefined);
  const lights = !known ? 'unknown' : live ? state : 'ended';

  const pill = (own: boolean) => {
    const tone = SESSION_TONE[word] ?? 'neutral';
    // A word this page does not name is shown as recorded; the node's own name already says the plain state.
    if (!(word in SESSION_TONE)) return { word: String(word), tone };
    return { word: own && word === state ? t('work.life.here') : t(shownKey(word)), tone };
  };

  const nodes: FlowNode[] = [
    ...LIVE_STATES.map((each): FlowNode => ({
      id: each,
      title: t(`sessionState.${each}`),
      current: lights === each,
      ...(lights === each ? { state: pill(true) } : {}),
    })),
    {
      id: 'ended',
      title: t('work.life.ended'),
      current: lights === 'ended',
      // A live record has not ended; an ended one says nothing of the states before it (no path).
      ...(known && live ? { reached: false } : {}),
      ...(lights === 'ended' ? { state: pill(false) } : {}),
    },
    ...(!known
      ? [{
        id: 'unknown', title: t('work.life.unknown', { state }), current: true,
        state: { word: String(state), tone: 'neutral' as const },
      }]
      : []),
  ];

  const endsFrom = (from: SessionState) => SESSION_MOVES.filter(([each, to]) => each === from && !SESSION_ACTIVE.has(to)).map(([, to]) => to);
  const cannot = cannotGoOn(session);
  const edges: FlowEdge[] = [
    ...SESSION_MOVES.filter(([, to]) => SESSION_ACTIVE.has(to)).map(([from, to]): FlowEdge => ({
      id: `${from}>${to}`, from, to, says: t(`work.life.move.${from}.${to}`),
    })),
    ...LIVE_STATES.filter((from) => endsFrom(from).length > 0).map((from): FlowEdge => ({
      id: `${from}>ended`, from, to: 'ended',
      says: t('work.life.toEnded', { who: list(endsFrom(from).map((each) => t(`work.life.by.${each}`)), undefined, 'or') }),
    })),
    cannot
      ? { id: 'ended>working', from: 'ended', to: 'working', style: 'refused', says: t('work.life.cannot', { why: t(`work.life.cannot.${cannot}`) }) }
      : {
        id: 'ended>working', from: 'ended', to: 'working', style: 'conditional',
        says: t('work.life.goOn', { never: t('work.life.neverGoesOn') }),
      },
  ];

  const members = ENDED_STATES.map((each): OutcomeMember => ({
    state: each, word: t(`sessionState.${each}`), current: state === each, goesOn: each !== NEVER_GOES_ON,
  }));
  return {
    label: t('work.life.label', { id: session.id }),
    nodes, edges, entries: ['queued'], members,
    stop: t('work.life.stop'),
    neverGoesOn: t('work.life.neverGoesOn'),
  };
}
