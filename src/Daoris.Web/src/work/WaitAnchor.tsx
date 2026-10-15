import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Button } from '../design/ui';
import type { Wait } from './waits';

/** A wait's name, as the strip's folded line and a raising block's mark say it. */
export function waitName(t: TFunction, wait: Wait): string {
  switch (wait.kind) {
    case 'question': return t('work.wait.name.question');
    case 'intake': return t('work.wait.name.intake', { ask: wait.ask });
    case 'goAhead': return t('work.wait.name.goAhead', { number: wait.number });
    default: return t(`work.wait.name.${wait.kind}`);
  }
}

/**
 * *In the conversation* (WAITFOOT1c, D168 point 4): on the head's section and the strip's line of a wait whose raising event the
 * conversation holds, or may hold on a page before; the press shows that event, outlined. Props only.
 */
export function InConversation({ onPress }: { onPress: () => void }) {
  const { t } = useTranslation();
  return (
    <Button variant="ghost" className="px-2 py-0.5 text-small" onClick={onPress}>{t('work.wait.inConversation')}</Button>
  );
}

/**
 * The line under a block that raised a wait (WAITFOOT1c, D168 point 4): what waits on the person from here, in the waiting hue,
 * and *To the wait*, which takes them to its line at the foot, where it is answered. Props only.
 */
export function WaitMark({ waits, onPress }: { waits: readonly Wait[]; onPress: () => void }) {
  const { t } = useTranslation();
  return (
    <p className="m-0 mt-1 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 border-l-[3px] border-st-open pl-2 text-small text-ink-open">
      <span className="min-w-0">{t('work.wait.raised', { names: waits.map((wait) => waitName(t, wait)).join(' · ') })}</span>
      <Button variant="ghost" className="px-2 py-0.5 text-small" onClick={onPress}>{t('work.wait.toWait')}</Button>
    </p>
  );
}
