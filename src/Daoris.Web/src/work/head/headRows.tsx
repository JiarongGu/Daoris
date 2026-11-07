import type { TFunction } from 'i18next';
import type { Quest, Session } from '../../data/api';
import type { DetailRow } from './DetailsFold';

/**
 * A conversation's opening is the header's title (`sessionTitle`), which holds one line: past this many characters the
 * header may have cut it, and *Details* holds it whole. A long line is never wrongly said twice; it is said in the
 * fold, which is closed.
 */
const ONE_LINE = 60;

/**
 * The request as written, a row of *Details*: the quest's whole title with the quest's door, or, for
 * a conversation with no quest, its opening where the header's one line may have cut it. Content, shown as it is.
 */
export function requestRow(
  t: TFunction, quest: Quest | null | undefined, opening: string | null | undefined, id: string | null | undefined,
  onOpenQuest: ((quest: Quest) => void) | undefined,
): DetailRow {
  const label = t('work.head.request');
  if (quest?.title) {
    return {
      label,
      value: (
        <>
          <span>{quest.title}</span>
          {onOpenQuest && (
            <button
              type="button"
              onClick={() => onOpenQuest(quest)}
              className="ml-2 cursor-pointer border-0 bg-transparent p-0 font-sans text-small text-accent hover:underline"
            >
              {t('work.head.openQuest', { id: quest.id })}
            </button>
          )}
        </>
      ),
    };
  }
  return { label, value: !id && opening && opening.length > ONE_LINE ? opening : null };
}

/** A session another one's record names, as the frame holds it: its record, and its title (`sessionTitle`). */
export type HeadLink = { session: Session; title: string };

/**
 * A link the record holds, a row of *Details*: the other session's title with a door
 * that attends it, where the frame holds that session; its id in mono with no door where it does not (deleted, or not listed).
 * No row where the record names none.
 */
export function linkRow(
  t: TFunction, label: string, id: string | null | undefined, link: HeadLink | null | undefined,
  onOpenSession: ((session: Session) => void) | undefined,
): DetailRow {
  if (!id) return { label, value: null };
  if (!link || link.session.id !== id) return { label, value: id, mono: true };
  return {
    label,
    value: (
      <>
        <span>{link.title}</span>
        {onOpenSession && (
          <button
            type="button"
            onClick={() => onOpenSession(link.session)}
            className="ml-2 cursor-pointer border-0 bg-transparent p-0 font-sans text-small text-accent hover:underline"
          >
            {t('work.head.openSession')}
          </button>
        )}
      </>
    ),
  };
}
