import type { Ask, Quest, Session } from '../api';
import type { ChoiceStanding } from '../work/listPanes';

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

/**
 * A group of the list's quests: where each is in its life (D41 §5), and what a hold keeps waiting on the person, their yes to a
 * departure (DRIFT1d2) or their review of a set-up step (REVIEWENV1g).
 */
export type QuestGroup = { group: 'held' | 'open' | 'progress' | 'closed'; quests: Quest[] };

/**
 * The quests by where they are in their life: what a departure holds for the person's yes first, then open, taken, then
 * closed where closed ones are shown. An empty group is kept, and the list leaves it out, so the groups' order is one place.
 *
 * @remarks
 * **A held quest is outstanding** (DRIFT1d, D133 §4): closed done, and listed by the service without closed ones, since
 * what follows it waits on the person. Grouped by its status it fell in *Closed*, which the default list does not show, so
 * the person never found it; it is first, as the asks are, since it waits on them, and never in *Closed* too.
 */
export function questGroups(quests: readonly Quest[], closed: boolean): QuestGroup[] {
  return [
    { group: 'held' as const, quests: quests.filter((quest) => quest.held === true) },
    { group: 'open' as const, quests: quests.filter((quest) => quest.status === 'Open') },
    { group: 'progress' as const, quests: quests.filter((quest) => quest.status === 'Taken') },
    ...(closed
      ? [{
        group: 'closed' as const,
        quests: quests.filter((quest) => (quest.status === 'Done' || quest.status === 'Declined') && quest.held !== true),
      }]
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
 * Whether the chosen quest still waits on something (UX6b, design §1 rule 6), so Quests reopens on it: open or taken, or
 * closed while a departure holds it for the person's yes (DRIFT1d) or a move another machine made waits on their dismissal
 * (SYNC6c). `read` is whether every quest has answered, since a quest missing from a list still on its way has not gone.
 */
export function questStanding(quest: Quest | undefined, read: boolean): ChoiceStanding {
  if (!quest) return read ? 'gone' : 'unread';
  const closed = quest.status === 'Done' || quest.status === 'Declined';
  return closed && quest.held !== true && !quest.conflicts?.length ? 'closed' : 'live';
}

/**
 * Whether the chosen ask still waits on something (UX6b): proposed, open or published, or done with a go-ahead the person
 * has not answered (KNOWUSE1a). A closed ask, and a done one whose quests have all closed, wait on nothing.
 */
export function askStanding(ask: Ask | undefined, read: boolean): ChoiceStanding {
  if (!ask) return read ? 'gone' : 'unread';
  const closed = ask.state === 'Done' || ask.state === 'Closed';
  return closed && !(ask.goAheads ?? []).some((goAhead) => goAhead.state === 'asked') ? 'closed' : 'live';
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
