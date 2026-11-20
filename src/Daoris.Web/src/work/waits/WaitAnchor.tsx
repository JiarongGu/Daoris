import { type FocusEvent, type RefObject, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Button, Icon, Tip } from '../../design/ui';
import type { AnchorWait } from './anchors';
import type { Wait } from './waits';

/** A press that leads to a wait's line, counted so the same one pressed again leads there again. */
export type Seek = { wait: AnchorWait; n: number };

/** The outline a line wears once a raising block's *To the wait* led to it, as the conversation outlines a revealed block. */
export const SOUGHT = 'rounded-control outline-2 outline-offset-2 outline-accent/60';

/** Whether the focus left `event`'s element for somewhere outside it. */
export function leftFor(event: FocusEvent<HTMLElement>): boolean {
  return !event.currentTarget.contains(event.relatedTarget as Node | null);
}

/** A wait's line inside `box`: the dock's card by its key (an opinion's is the work's), else a park box's line by its own mark. */
function lineOf(box: HTMLElement | null, wait: AnchorWait): HTMLElement | null | undefined {
  const key = wait === 'opinion' ? 'work' : wait;
  const card = [...(box?.querySelectorAll<HTMLElement>('[data-wait]') ?? [])].find((each) => each.dataset.wait === key);
  if (card) return card;
  if (wait.startsWith('rule:')) {
    const id = wait.slice('rule:'.length);
    return [...(box?.querySelectorAll<HTMLElement>('[data-rule]') ?? [])].find((each) => each.dataset.rule === id);
  }
  if (wait.startsWith('helper:')) {
    const id = wait.slice('helper:'.length);
    return [...(box?.querySelectorAll<HTMLElement>('[data-helper]') ?? [])].find((each) => each.dataset.helper === id);
  }
  if (wait.startsWith('permission:')) {
    const id = wait.slice('permission:'.length);
    return [...(box?.querySelectorAll<HTMLElement>('[data-permission]') ?? [])].find((each) => each.dataset.permission === id);
  }
  return wait.startsWith('goAhead:') ? box?.querySelector<HTMLElement>(`[data-go-ahead="${wait.slice('goAhead:'.length)}"]`) : null;
}

/**
 * The wait a press led to, in the dock or the park's box: each new `seek` outlines its wait, `onSeek` first opens what hides it, and
 * its line takes the focus once drawn, looked for again as `drawn` (what the place holds) changes. `letGo` puts the outline down.
 */
export function useSought(box: RefObject<HTMLElement | null>, seek: Seek | null | undefined, onSeek?: () => void, drawn?: unknown) {
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
    const line = lineOf(box.current, seek.wait);
    if (!line) return;
    line.focus();
    // A line in a place standing aside takes no focus until the place is drawn again for it.
    if (line.ownerDocument.activeElement === line) focusing.current = false;
    // eslint-disable-next-line react-hooks/exhaustive-deps -- `drawn` says when the place holds something new to look in
  }, drawn === undefined ? undefined : [seek?.n, sought, drawn]);
  return { sought, letGo: () => setSought(null) };
}

/** A wait's name, as the strip's folded line and a raising block's mark say it. */
export function waitName(t: TFunction, wait: Wait): string {
  switch (wait.kind) {
    case 'question': return wait.signIn ? t('work.wait.name.signIn') : t('work.wait.name.question');
    case 'intake': return t('work.wait.name.intake', { ask: wait.ask });
    case 'goAhead': return t('work.wait.name.goAhead', { number: wait.number });
    default: return t(`work.wait.name.${wait.kind}`);
  }
}

/**
 * *In the conversation*: on the head's section and the strip's line of a wait whose raising event the
 * conversation holds, or may hold on a page before; the press shows that event, outlined. Props only.
 */
export function InConversation({ onPress }: { onPress: () => void }) {
  const { t } = useTranslation();
  return (
    <Button variant="ghost" className="px-2 py-0.5 text-small" onClick={onPress}>{t('work.wait.inConversation')}</Button>
  );
}

/**
 * The mark under a block that raised waits: for each, a quiet glyph in the waiting hue, *To the wait* and its name, and what
 * waits on the person from here its tip (a line of words under the block would be one more copy of the wait on the screen). Each
 * press takes the person to its own wait, in the dock, where it is answered. Props only.
 */
export function WaitMark({ waits, onPress }: { waits: readonly Wait[]; onPress: (wait: Wait) => void }) {
  const { t } = useTranslation();
  return (
    <div className="mt-1 flex gap-1">
      {waits.map((wait) => {
        const names = waitName(t, wait);
        return (
          <Tip key={names} content={t('work.wait.raised', { names })}>
            <Button variant="ghost" aria-label={t('work.wait.toWait', { names })} onClick={() => onPress(wait)} className="h-6 w-6 justify-center px-0 text-ink-open">
              <Icon name="answer" size={13} />
            </Button>
          </Tip>
        );
      })}
    </div>
  );
}
