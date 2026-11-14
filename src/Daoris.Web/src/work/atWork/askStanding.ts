import type { TFunction } from 'i18next';
import type { AtWorkQuest } from './types';

// An ask's quests by where they stand (D293): a quest's Done is written at its workflow's end, so an ask counting Done alone would sit
// at zero while its quests are worked, checked and looked at. Pure, read from the quests the at-work answer lists with their takers.

/** How many of an ask's quests stand where; a declined quest stands in none. */
export type AskStanding = { landed: number; you: number; checks: number; working: number; notStarted: number };

const ORDER: readonly (keyof AskStanding)[] = ['landed', 'you', 'checks', 'working', 'notStarted'];

/** Where one quest stands: Done is landed, a taker waiting on the person or on its workflow's step says so, any other taker works it. */
function standOf(quest: AtWorkQuest): keyof AskStanding | null {
  if (quest.status === 'Declined') return null;
  if (quest.status === 'Done') return 'landed';
  const taker = quest.taker;
  if (!taker || taker.untaken || quest.status === 'Open') return 'notStarted';
  if (taker.state === 'awaiting-person') return 'you';
  if (taker.state === 'awaiting-step') return 'checks';
  return 'working';
}

export function askStanding(quests: readonly AtWorkQuest[]): AskStanding {
  const counts: AskStanding = { landed: 0, you: 0, checks: 0, working: 0, notStarted: 0 };
  for (const quest of quests) {
    const stand = standOf(quest);
    if (stand) counts[stand] += 1;
  }

  return counts;
}

/** Each count that is not zero, in its order; null where the ask holds no quest that stands anywhere. */
export function standingLine(t: TFunction, counts: AskStanding): string | null {
  const said = ORDER.filter((stand) => counts[stand] > 0).map((stand) => t(`work.atWork.standing.${stand}`, { count: counts[stand] }));
  return said.length > 0 ? said.join(' · ') : null;
}
