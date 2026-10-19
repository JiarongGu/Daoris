import type { TFunction } from 'i18next';
import type { Ask } from '../data/api';
import { ago } from '../locales/format';

// The facts line under an ask's title (HEAD2d, D162 §4, the entry-and-heads design §2): where it goes, its age, and the one
// live fact. Built here, not in `work/head/headFacts.ts`, which the quest's and the session's heads edit. Pure, so a head is
// handed its line and every state is a story.

/** Where a title longer than this is cut by its two lines on a main area's usual width: past it, the request is offered whole. */
export const LONG_TITLE = 90;

/**
 * An ask's facts, in order: the workspace it goes to, when it was asked, and the live fact: it waits on the person where a
 * go-ahead its sessions asked is unanswered, else the quests it became. Nothing it has nothing to say: an absent fact is
 * left out, never a dash.
 */
export function askFacts(t: TFunction, ask: Ask): string[] {
  const waiting = (ask.goAheads ?? []).some((goAhead) => goAhead.state === 'asked');
  const became = ask.quests.length;
  return [
    t('asks.card.workspace', { workspace: ask.workspace }),
    t('asks.card.asked', { ago: ago(ask.asked) }),
    ...(waiting ? [t('asks.goAhead.state.asked')] : became > 0 ? [t('asks.head.quests', { count: became })] : []),
  ];
}
