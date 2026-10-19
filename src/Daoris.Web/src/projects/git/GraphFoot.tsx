import { type RefObject, useLayoutEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { Refused } from '../../work/InlineConfirm';

/** What a graph's holder holds of its paging: the foot's press, and the foot's place for the focus to be told from. */
export type GraphPaging = {
  /** *Show more* pressed: asks the next page, once while one is on its way. */
  more: () => void;
  foot: RefObject<HTMLDivElement | null>;
};

/**
 * **The paging a graph's holder keeps** (UX6h3a, UXLOADMORE1): the press, and the focus carried to the first row it brought.
 * The focus goes only where it was on the body or in the foot; a reader who went elsewhere keeps their place, as Ask Daoris's
 * history keeps it. It is the holder's, not the foot's, because the last page takes the foot away with it and the focus is
 * still owed to the row that page brought.
 */
export function useGraphPaging({ shown, loadingMore, onMore, rowAt }: {
  /** The commits listed so far. */
  shown: number;
  loadingMore: boolean;
  onMore: () => void;
  /** The row at an index of the list, which takes the focus when the next page arrives; rows are focusable by script. */
  rowAt: (index: number) => HTMLElement | null;
}): GraphPaging {
  const foot = useRef<HTMLDivElement>(null);
  // How many rows there were when *Show more* was pressed, so the first row the next page brings takes the focus.
  const pressed = useRef<number | null>(null);

  useLayoutEffect(() => {
    const was = pressed.current;
    if (was === null || shown <= was) return;
    pressed.current = null;
    const focused = document.activeElement;
    // The press leaves with the page: the focus may be on the body by then (the foot went), or still on the foot.
    if (focused && focused !== document.body && !foot.current?.contains(focused)) return;
    rowAt(was)?.focus();
  }, [shown, rowAt]);

  const more = () => {
    // Pressed again while it reads, it asks nothing more, and is not a new press, so the focus is never taken from it.
    if (loadingMore) return;
    pressed.current = shown;
    onMore();
  };
  return { more, foot };
}

/**
 * **The foot of a paged graph** (UX6h3a, UXLOADMORE1): the count of what is listed, with no total, since the graph answers
 * none; *Show more*; a status that is there from the first, so a reader hears the wait begin; and the next page's refusal
 * said in its words. The History tab's, and the branch page's (UX6h3b).
 *
 * @remarks
 * **A molecule**: its holder draws it only while a next page exists, and holds the pages and their paging.
 */
export function GraphFoot({ shown, loadingMore, error, paging }: {
  shown: number;
  loadingMore: boolean;
  /** Why the next page did not come, as a sentence. */
  error?: string | null;
  paging: GraphPaging;
}) {
  const { t } = useTranslation();
  return (
    <div ref={paging.foot} className="grid grid-cols-[minmax(0,1fr)] justify-items-start gap-1.5 pt-1">
      <p className="m-0 text-meta text-ink-faint">{t('projects.git.history.listed', { count: shown })}</p>
      {error && <Refused sentence={error} />}
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <Button className="px-2.5 text-small" onClick={paging.more}>{t('projects.git.history.more')}</Button>
        {/* There from the first, so a reader hears it speak when the wait begins. */}
        <span role="status" className="text-small text-ink-soft">{loadingMore ? t('projects.git.history.loadingMore') : ''}</span>
      </div>
    </div>
  );
}
