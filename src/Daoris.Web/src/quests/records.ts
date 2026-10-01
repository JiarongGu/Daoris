import type { Quest, Session } from '../api';

// Quests' list and its memory as values (FRAME1d, D118 §2, §3f): its filters as kept, its groups, the record a
// page shows, and the session that marks a quest. Pure, so every list a person could have is an argument.

/** Quests' list's filters (D118 §3f): the receiver its quests were asked of, or every one; and closed shown. */
export type QuestFilters = { to: string | null; closed: boolean };

/**
 * The filters as `listPanes.ts` kept them, read defensively: anything unreadable is the default, every receiver
 * and no closed quests, so a stale or a hand-edited value costs nothing.
 */
export function questFilters(kept: Record<string, unknown>): QuestFilters {
  return {
    to: typeof kept.to === 'string' && kept.to !== '' ? kept.to : null,
    closed: kept.closed === true,
  };
}

/** The filters as they are kept: the defaults are nothing kept at all. */
export function keptFilters(filters: QuestFilters): Record<string, unknown> | null {
  if (!filters.to && !filters.closed) return null;
  return { ...(filters.to ? { to: filters.to } : {}), ...(filters.closed ? { closed: true } : {}) };
}

/** A group of the list's quests: where each is in its life (D41 §5). */
export type QuestGroup = { group: 'open' | 'progress' | 'closed'; quests: Quest[] };

/**
 * The quests by where they are in their life: open, taken, then closed where closed ones are shown. An empty group
 * is kept, and the list leaves it out, so the groups' order is one place.
 */
export function questGroups(quests: readonly Quest[], closed: boolean): QuestGroup[] {
  return [
    { group: 'open' as const, quests: quests.filter((quest) => quest.status === 'Open') },
    { group: 'progress' as const, quests: quests.filter((quest) => quest.status === 'Taken') },
    ...(closed
      ? [{ group: 'closed' as const, quests: quests.filter((quest) => quest.status === 'Done' || quest.status === 'Declined') }]
      : []),
  ];
}

/**
 * The freshest attempt per quest: a retry is its own record, and the page shows where things stand now, not the
 * history (the service keeps that). A chat may serve no quest (D49 §3), so only a session that names one marks it.
 */
export function latestSessions(sessions: readonly Session[]): Map<string, Session> {
  const latest = new Map<string, Session>();
  for (const session of sessions) {
    if (!session.quest) continue;
    const held = latest.get(session.quest);
    if (!held || session.updated >= held.updated) latest.set(session.quest, session);
  }
  return latest;
}

/**
 * The record a page shows: the list's copy, or the door's last answer about it where the list has not caught up
 * with the door yet (INT4c's reason) — so a move made elsewhere shows, and a list behind the person's own press
 * does not undo it. On a tie the answer stands: a dismissal moves no time.
 */
export function freshest<T extends { id: string; updated: string }>(listed: T | undefined, held: T | null): T | undefined {
  if (!held || (listed && listed.id !== held.id)) return listed;
  if (!listed) return held;
  return Date.parse(listed.updated) > Date.parse(held.updated) ? listed : held;
}

/**
 * The question a taken quest's taker asked another repository and waits on (D79), found among every quest, closed
 * ones too: once it closes the quest resumes, and the wait is no longer the news.
 */
export function questionOf(quest: Quest, every: readonly Quest[]): { id: string; quest?: Quest } | null {
  return quest.status === 'Taken' && quest.awaits
    ? { id: quest.awaits, quest: every.find((candidate) => candidate.id === quest.awaits) }
    : null;
}

/** Whether a question has been answered: closed, either way. */
export const answered = (question: Quest | undefined) => question?.status === 'Done' || question?.status === 'Declined';
