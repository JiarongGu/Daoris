import type { TFunction } from 'i18next';
import type { HelperAskRow } from '../waits/HelperAsks';

/** Where a member's answer went (COWORKUX1d, the co-worker design §6.2, §6.4): to the lead, to the person, or passed by them. */
export type Went = 'lead' | 'person' | 'passed';

/** Where an answered ask's answer went, from the ask's own facts; null while it has none. */
export function wentOf(row: Pick<HelperAskRow, 'state' | 'to' | 'kept' | 'passed'>): Went | null {
  if (row.state !== 'answered') return null;
  if (row.passed) return 'passed';
  return row.to === 'person' || row.kept ? 'person' : 'lead';
}

/** The state word of a member's row (§6.2): a status, never a sentence. */
export function memberState(t: TFunction, row: Pick<HelperAskRow, 'askedBy' | 'state' | 'code'>): string {
  switch (row.state) {
    case 'started': return t('work.team.state.working');
    case 'answered': return t('work.team.state.answered');
    case 'refused': return t('work.team.state.refused');
    case 'waiting':
    case null:
      return row.askedBy === 'agent' && row.code === null ? t('work.team.state.yes') : t('work.team.state.waiting');
    default: return t('work.team.state.waiting');
  }
}

/** One member's row, as the head's *Co-workers* draws it: its agent, its role, its state, and where its answer went. */
export type MemberRow = {
  ask: HelperAskRow;
  agent: string;
  state: string;
  went: Went | null;
  /** The person's answer waits on their *Pass to the lead*. */
  passable: boolean;
};

/** The head's member rows, oldest first: each ask a helper took or will take. */
export function memberRows(t: TFunction, asks: readonly HelperAskRow[], nameOf: (agent: string) => string): MemberRow[] {
  return asks.map((ask) => {
    const agent = ask.startedOn ? nameOf(ask.startedOn) : ask.agent ? nameOf(ask.agent) : t('work.helper.someAgent');
    const went = wentOf(ask);
    return { ask, agent, state: memberState(t, ask), went, passable: went === 'person' && Boolean(ask.kept) };
  });
}

/** The ask a member's record answered, found by its session among its lead's asks; null where none names it. */
export const askOfMember = (asks: readonly HelperAskRow[] | undefined, member: string): HelperAskRow | null =>
  asks?.find((ask) => ask.helper === member) ?? null;

/** The codes *Ask again…* and *Pass to the lead* answer by that the team words (COWORKUX1d); any other is said in the driver's sentence. */
const TEAM_REFUSALS = new Set(['after', 'unanswered', 'lead']);

/** A refusal of *Ask again…* or *Pass to the lead* in the catalogue's words where it has them, else the driver's own. */
export const teamSaid = (t: TFunction, why: string | null, message: string): string =>
  why && TEAM_REFUSALS.has(why) ? t(`work.team.why.${why}`) : message;
