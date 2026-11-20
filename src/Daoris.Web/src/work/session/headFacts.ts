import type { Session } from '../../data/api';
import type { StateKind, StateLine } from '../head/stateLine';
import type { HeadFacts, PanelState } from './PanelHead';

/**
 * The model that ran, as the record acknowledged it: the newest acknowledged entry, else the newest model the record lists; and the
 * model asked, only where it differs. Never the asked model in the ran one's place: none reported reads null.
 */
export function modelRan(session: Pick<Session, 'ran' | 'asked'>): { model: string | null; asked: string | null } {
  const entries = [...(session.ran?.entries ?? [])].filter((entry) => entry.acknowledged).sort((a, b) => Date.parse(b.at) - Date.parse(a.at));
  const models = session.ran?.models ?? [];
  const model = entries[0]?.acknowledged ?? models[models.length - 1] ?? null;
  const asked = session.asked?.model.value ?? null;
  return { model, asked: asked && asked !== model ? asked : null };
}

/** An ended record by how it ended: the person's stop reads as theirs, anything else as finished. */
const ENDED_BY_YOU: ReadonlySet<string> = new Set(['stopped']);

const STATE_OF: Readonly<Record<StateKind, PanelState>> = {
  'needs-you': 'waiting', step: 'waitsOn', working: 'live', pending: 'idle', held: 'held', failed: 'failed', done: 'finished', unread: 'loading',
};

/**
 * **The head's facts** from what the page already holds: the state from the driver's reader (`stateLineOf`), never the record's
 * word, with its cause as what it does now or whom it waits on; where and who from the record; the model that ran (`modelRan`).
 * The caller names the title, the ask, the maker, the role and the account as the rest of the page names them.
 */
export function headFactsOf({ session, line, title, ask, maker, role, account, branch }: {
  session: Session;
  line: StateLine;
  title: string;
  ask?: string | null;
  maker: string;
  role?: string | null;
  account?: string | null;
  branch?: string | null;
}): HeadFacts {
  const state = line.kind === 'done' && ENDED_BY_YOU.has(session.state) ? 'stopped' : STATE_OF[line.kind];
  const said = line.cause && 'said' in line.cause ? line.cause.said : null;
  const { model, asked } = modelRan(session);
  return {
    state, since: line.since ?? session.updated, doing: state === 'waitsOn' ? null : said, waitsOn: state === 'waitsOn' ? said : null,
    title, ask: ask ?? null, repository: session.repository, branch: branch ?? null, role: role ?? null, maker, model, asked,
    account: account ?? null,
  };
}
