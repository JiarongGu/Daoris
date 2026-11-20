import { useTranslation } from 'react-i18next';
import { Inline } from '../../design/ui';
import { type Answered, InlineConfirm } from '../InlineConfirm';

/**
 * **A stop's ask**: it ends work in flight, so it asks once, under the head (a destructive edit asks once). It says what follows
 * by what the session is, then the move, *Stop session*, and *Never mind*. The sentence is the catalogue's, and its backticks are
 * code (`Inline`). It is the one inline confirmation: open until the stop lands, a refusal said inside it.
 */
export function StopAsk({ sentence, busy = false, onStop, onClose }: {
  sentence: string;
  /** A stop on its way: the presses wait for it. */
  busy?: boolean;
  /** The second press, told how the stop ended. */
  onStop: (answered: Answered) => void;
  /** Put down, or the stop landed. */
  onClose: () => void;
}) {
  const { t } = useTranslation();
  return (
    <InlineConfirm
      className="mt-2.5"
      label={t('work.stop.title')}
      says={<Inline text={sentence} />}
      meanIt={t('work.act.stopMeanIt')}
      busy={busy}
      onConfirm={onStop}
      onClose={onClose}
    />
  );
}

/**
 * **A delete's ask**: nothing brings a deleted conversation back, so it asks once, under the head as the stop's ask does: what
 * goes, then *Delete session* and *Never mind*. The page offers *Delete…* only where the driver said it would be taken, so the
 * sentence is one for every session that reaches it. The one inline confirmation.
 */
export function DeleteAsk({ busy = false, parts = 1, onDelete, onClose }: {
  /** A delete on its way: the presses wait for it. */
  busy?: boolean;
  /** How many parts of its line the delete takes with it: more than one is said, never left for the person to find out. */
  parts?: number;
  /** The second press, told how the delete ended. */
  onDelete: (answered: Answered) => void;
  /** Put down, or the delete landed. */
  onClose: () => void;
}) {
  const { t } = useTranslation();
  return (
    <InlineConfirm
      className="mt-2.5"
      label={t('work.delete.title')}
      says={parts > 1 ? t('work.delete.parts', { count: parts }) : t('work.delete.means')}
      meanIt={t('work.act.deleteMeanIt')}
      busy={busy}
      onConfirm={onDelete}
      onClose={onClose}
    />
  );
}
