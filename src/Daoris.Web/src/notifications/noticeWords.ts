import type { TFunction } from 'i18next';
import { HELP_REPOSITORY } from '../data/api';
import { en } from '../locales/index';

/**
 * Where the shell's notifier reads its words in the catalogue (NOTIFY2d, D196, the notification design §3.3): a headline and a body
 * per kind of notification, and the act of every `Person` route, by which a terminal's press is named. Its twin is the modules'
 * `SessionBalloon`, which reads them by these keys and whose English is held equal to this catalogue's.
 */
const HANDED = ['notifications.system.', 'person.route.'] as const;

/** The end states said by their own words; any other is an ending, never its code in a sentence (§6). */
const ENDS = new Set(['completed', 'failed', 'stopped', 'declined', 'stood-down']);

/**
 * The words the page hands the shell's notifier, in the reader's language: every key under `notifications.system.` and
 * `person.route.`, as the catalogue words it, each placeholder kept for the shell to fill with the event's facts.
 *
 * @remarks
 * The keys are the English catalogue's, every catalogue holding the same (`i18n:check`); a key asked with no value keeps its
 * `{{placeholder}}`, which the shell fills, so no fact of an event is ever composed here.
 */
export function noticeWords(t: TFunction): Record<string, string> {
  return Object.fromEntries(
    Object.keys(en)
      .filter((key) => HANDED.some((prefix) => key.startsWith(prefix)))
      .sort()
      .map((key) => [key, t(key)]),
  );
}

/** An attention as the shell tells it (`SESSION_ATTENTION`): its kind, its facts, and the driver's own English. */
export type AttentionTold = {
  kind?: string;
  headline?: string;
  detail?: string | null;
  repository?: string;
  state?: string | null;
  quest?: string | null;
  strikes?: number | null;
  session?: string | null;
  id?: string;
};

/**
 * The toast a window says for an attention, in the reader's language (NOTIFY2d): its kind's headline, as the shell's balloon says
 * it, and what a session wrote beside it as written (a park's question, an ending's conclusion, a held call's title). A wait for an
 * account says its headline alone, its detail being the driver's English. A kind this build does not word says the driver's own
 * headline, as before. Null where nothing is to be said.
 */
export function attentionToast(t: TFunction, item: AttentionTold): string | null {
  const repository = item.repository ?? '';
  const help = repository === HELP_REPOSITORY;
  const written = item.detail?.trim() ? item.detail : null;
  const head = (() => {
    switch (item.kind) {
      case 'Parked': return t('notifications.system.parked.title', { repository });
      case 'QuestParked': {
        const quest = item.quest ?? '';
        if (item.strikes == null) return t('notifications.system.questParked.title', { repository, quest });
        return item.strikes === 1
          ? t('notifications.system.questParked.titleOne', { repository, quest })
          : t('notifications.system.questParked.titleCount', { repository, quest, count: item.strikes });
      }
      case 'Ended':
        return t(item.state && ENDS.has(item.state) ? `notifications.system.ended.${item.state}` : 'notifications.system.ended.other', { repository });
      case 'Waiting': return t('notifications.system.waiting.title', { repository });
      case 'Answered': return help ? t('notifications.system.answered.help') : t('notifications.system.answered.title', { repository });
      case 'Holding': return help ? t('notifications.system.holding.help') : t('notifications.system.holding.title', { repository });
      default: return null;
    }
  })();
  if (head === null) {
    if (!item.headline) return null;
    return written ? `${item.headline}: ${written}` : item.headline;
  }
  const said = item.kind === 'Waiting' || item.kind === 'Answered' ? null : written;
  return said ? t('notifications.toast.with', { title: head, detail: said }) : head;
}
