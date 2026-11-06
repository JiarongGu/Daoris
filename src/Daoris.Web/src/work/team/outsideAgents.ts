import type { TFunction } from 'i18next';
import { clockOf } from '../../locales/format';

/**
 * An agent outside Daoris on a line's channel (CHANNELJOIN1c, the event-channel design §5.8): the agent it claimed, the repository it
 * claimed, when it joined and when it last read or posted. Shown as itself, never as the person or a session.
 */
export type OutsideAgent = { id: string; agent: string; repository: string | null; joined: string; seen: string | null };

/** The agents the page names by a roster's word; any other is said as the agent wrote it, `other` as *another agent*. */
export const OUTSIDE_OTHER = 'other';

/**
 * One row's line: who it is, that it is outside Daoris, when it joined and when it last read or posted. The agent as the person calls
 * it where the roster knows it, else as it said.
 */
export function outsideRowLine(t: TFunction, agent: OutsideAgent, nameOf: (agent: string) => string, language: string, now = new Date()): string {
  const who = agent.agent === OUTSIDE_OTHER ? t('work.team.outside.other') : nameOf(agent.agent);
  const named = agent.repository ? t('work.team.outside.in', { agent: who, repository: agent.repository }) : who;
  const seen = agent.seen
    ? t('work.team.outside.seen', { time: clockOf(agent.seen, now, language) })
    : t('work.team.outside.unseen');
  return [named, t('work.team.outside.tag'), t('work.team.outside.joined', { time: clockOf(agent.joined, now, language) }), seen].join(' · ');
}
