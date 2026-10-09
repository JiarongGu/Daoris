import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../ui';
import { type Answered, InlineConfirm } from './InlineConfirm';
import type { DiscardOffer } from './groups';

/**
 * The discard a tree offers where the line, or a branch of the person's, holds its commits by content (SQUASHTIDY1b, D102's
 * note): the session's head offers it where *Accept…* would be, and so does its review (SQUASHTIDY1f), from these pieces, so
 * the two say it alike and press it alike.
 *
 * @remarks
 * **A molecule**: the press arrives as `onDiscard`, told back to its ask (UXFIX2), and the parent holds whether it is asking,
 * so each lays the button out among its own. The word is *Discard branch…*, the head's since LAND3b, since the branch goes
 * with the tree; the ask names the branch and says the driver's sentence naming the ref the commits stay at, shown as it is,
 * as the review's own sentence is: no catalogue words the driver's sentences again.
 */

/** Whether its discard is offered: something can press it, and an unforced discard would go now, keeping the ref named. */
export const heldDiscardable = (discards: DiscardOffer, onDiscard?: (answered: Answered) => void): boolean =>
  Boolean(onDiscard && discards.keptAt);

/** *Discard branch…*: the first press, which opens the ask. */
export function HeldDiscardButton({ onAsk, className }: { onAsk: () => void; className?: string }) {
  const { t } = useTranslation();
  return <Button variant="danger" className={className} onClick={onAsk}>{t('settings.sweep.discard')}</Button>;
}

/** The ask under it: the branch named, the driver's sentence saying where its commits stay, and the press, unforced. */
export function HeldDiscardAsk({ branch, keptAt, onDiscard, onClose }: {
  branch: string;
  /** The driver's `keptAt`: the ref an unforced discard keeps the commits at, and how to have the branch back. */
  keptAt: string;
  onDiscard: (answered: Answered) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  return (
    <InlineConfirm
      block
      label={t('settings.sweep.discardTitle', { branch })}
      says={<p className="m-0 text-small text-ink-soft"><Inline text={keptAt} /></p>}
      meanIt={t('settings.sweep.discardMeanIt')}
      onConfirm={onDiscard}
      onClose={onClose}
    />
  );
}
