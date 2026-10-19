import { type FocusEvent, type RefObject, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Button, Icon, Tip } from '../../design/ui';
import type { AnchorWait } from './anchors';
import type { Wait } from './waits';

/** A press that leads to a wait's line (WAITFOOT1c), counted so the same one pressed again leads there again. */
export type Seek = { wait: AnchorWait; n: number };

/** The outline a line wears once a raising block's *To the wait* led to it, as the conversation outlines a revealed block. */
export const SOUGHT = 'rounded-control outline-2 outline-offset-2 outline-accent/60';

/** Whether the focus left `event`'s element for somewhere outside it. */
export function leftFor(event: FocusEvent<HTMLElement>): boolean {
  return !event.currentTarget.contains(event.relatedTarget as Node | null);
}

/** A wait's line inside `box`: a go-ahead's and a rule's by their own, the question's and the work's by their place. */
function lineOf(box: HTMLElement | null, wait: AnchorWait): HTMLElement | null | undefined {
  // A proposal's id is the driver's, found by its value rather than spelled into a selector.
  if (wait.startsWith('rule:')) {
    const id = wait.slice('rule:'.length);
    return [...(box?.querySelectorAll<HTMLElement>('[data-rule]') ?? [])].find((each) => each.dataset.rule === id);
  }
  if (wait.startsWith('helper:')) {
    const id = wait.slice('helper:'.length);
    return [...(box?.querySelectorAll<HTMLElement>('[data-helper]') ?? [])].find((each) => each.dataset.helper === id);
  }
  return box?.querySelector<HTMLElement>(wait.startsWith('goAhead:')
    ? `[data-go-ahead="${wait.slice('goAhead:'.length)}"]`
    : `[data-wait="${wait === 'opinion' ? 'work' : wait}"]`);
}

/**
 * The wait a press led to, in the strip at the foot or the record's head (WAITFOOT1c; WAITFOOT2b, D168's WAITFOOT2 note): each
 * new `seek` makes its wait the one outlined, `onSeek` first opens whatever hides its line, and the line in `box` takes the focus
 * once it is drawn. `letGo` puts the outline down, once the person moves on from the place it led to.
 */
export function useSought(box: RefObject<HTMLElement | null>, seek: Seek | null | undefined, onSeek?: () => void) {
  const [sought, setSought] = useState<AnchorWait | null>(null);
  // A press owes its line the focus until the line is drawn: in the strip only once the fold opens.
  const focusing = useRef(false);
  useEffect(() => {
    if (!seek) return;
    onSeek?.();
    setSought(seek.wait);
    focusing.current = true;
  }, [seek?.n]);
  useEffect(() => {
    if (!focusing.current || !seek) return;
    const drawn = lineOf(box.current, seek.wait);
    if (!drawn) return;
    drawn.focus();
    // A line in a place standing aside takes no focus until the place is drawn again for it.
    if (drawn.ownerDocument.activeElement === drawn) focusing.current = false;
  });
  return { sought, letGo: () => setSought(null) };
}

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
 * The mark under a block that raised a wait (WAITFOOT1c, D168 point 4): a quiet glyph in the waiting hue, *To the wait* and what
 * waits its name, and what waits on the person from here its tip (WAITFOOT2b: a line of words under the block was one more copy
 * of the wait on the screen). Its press takes the person to the wait's line, in the head or at the foot, where it is answered.
 * Props only.
 */
export function WaitMark({ waits, onPress }: { waits: readonly Wait[]; onPress: () => void }) {
  const { t } = useTranslation();
  const names = waits.map((wait) => waitName(t, wait)).join(' · ');
  return (
    <div className="mt-1 flex">
      <Tip content={t('work.wait.raised', { names })}>
        <Button variant="ghost" aria-label={t('work.wait.toWait', { names })} onClick={onPress} className="h-6 w-6 justify-center px-0 text-ink-open">
          <Icon name="answer" size={13} />
        </Button>
      </Tip>
    </div>
  );
}
