import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import type { Session } from '../data/api';
import { Icon, Inline } from '../design/ui';
import { cn } from '../lib/cn';
import { pauseName, recordedFull, recordedPauses, valueName } from '../settings/repository/Access';
import type { DetailRow } from './head/DetailsFold';

/**
 * **A session's access, as it started** (SESSACCESS1, D184 point 1; the UI/UX design §3.3, D185): read from the record's pause map,
 * the one the driver resolved at the start (`Session.access`), and never from today's settings, which may have moved since.
 *
 * @remarks
 * **The chip** (`AccessChip`) is worn on the head's facts line only where `recordedFull` holds: every pause the page knows recorded at
 * its open value, and none it does not know, which is never read as open. A start whose pauses were not all open wears nothing, so an
 * untouched start reads as it did. It is the plain warning chip Ask Daoris's full access wears: the warning line and icon, never red,
 * since full access is a choice, not an outcome. Text, not a control.
 *
 * **The row** (`accessRow`) is *Details*' *Access*: each pause and its value as recorded, in the setup's words, one the page does not
 * know by its code; never where it was set, which the record does not keep. A driven record from before the field says *Not
 * recorded*; a conversation, an intake or a helper started under no setup has no row.
 */
export function AccessChip({ className }: { className?: string }) {
  const { t } = useTranslation();
  return (
    <span
      data-access="full"
      className={cn('inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded-control border border-warn bg-warn/10 px-1.5 text-small text-ink', className)}
    >
      <Icon name="conflict" size={12} className="shrink-0 text-warn" />
      {t('work.access.full')}
    </span>
  );
}

/** Whether a session wears the chip: its record says it started under full access. */
export const startedFull = (session: Pick<Session, 'access'>) => recordedFull(session.access);

/** *Details*' *Access* row for a session: its pauses as recorded, *Not recorded* on a driven record without them, none otherwise. */
export function accessRow(t: TFunction, session: Pick<Session, 'access' | 'kind' | 'quest' | 'ask'>): DetailRow {
  const label = t('work.access.details');
  const access = session.access;
  if (!access || Object.keys(access).length === 0) {
    return { label, value: session.kind !== 'chat' && session.quest && !session.ask ? t('work.access.notRecorded') : null };
  }
  // *Questions* has one value in every setup (AUTONOMY1e), which the setup never offers as a choice, so it has no choice's word.
  const value = (pause: string, recorded: string) =>
    (pause === 'questions' && recorded === 'claude-code' ? t('work.access.questions') : valueName(t, pause, recorded));
  const said = recordedPauses(access)
    .map((each) => t('work.access.asStarted', { pause: pauseName(t, each.pause), value: value(each.pause, each.value) }))
    .join(' · ');
  return { label, value: <Inline text={said} /> };
}
