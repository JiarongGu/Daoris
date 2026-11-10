import type { TFunction } from 'i18next';
import type { Session } from '../../data/api';
import type { DetailRow } from './DetailsFold';

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
