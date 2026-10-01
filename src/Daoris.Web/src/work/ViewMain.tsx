import type { ReactNode, Ref } from 'react';
import { EmptyState, type IconName, SkeletonRows } from '../ui';
import { cn } from '../lib/cn';

/**
 * - `chosen`: the item the list chose, or the view's page where the view has no list.
 * - `none`: nothing chosen, so it says how to choose.
 * - `loading`: the chosen item is still on its way.
 * - `gone`: the chosen item is no longer there to show.
 */
export type MainState = 'chosen' | 'none' | 'loading' | 'gone';

/** An empty state's words: its headline, its body, and for nothing chosen the list's `＋`. */
export type MainNotice = { icon?: IconName; headline: string; body: string; action?: ReactNode };

/**
 * The view's gutters: a page's (24 px, 12 below 768 px) or the session's tighter column, which is a flex
 * column so its conversation follows its tail.
 */
const GUTTERS = {
  page: 'px-6 pb-12 pt-5 max-md:px-3 max-md:pb-8 max-md:pt-4',
  session: 'flex flex-col px-4 py-3',
} as const;

/**
 * A view's **main area** (D118 §3b, §5): the scroll box beside its list, its page header's slot, and its
 * four states.
 *
 * @remarks
 * **It lays out by its own width, never the viewport's.** It is the container named `main`, and a split
 * inside it follows it (`@4xl/main:grid-cols-2`), since the list and the side bar narrow it while the
 * window stays as wide (audit A4). USE1's container query on Overview's repository rows was the rule's
 * first instance.
 *
 * **It scrolls by itself**, and is the block anything positioned inside it is placed in: an `sr-only` label
 * far down a long page otherwise took the viewport as its block and grew a second scrollbar (PERM1).
 *
 * **A molecule**: every state is reached by its props. A first load is skeleton rows, never the empty
 * state, which read as *Nothing attended* while the list that held the attended session was still on its
 * way (audit SE11).
 */
export function ViewMain({ header, state = 'chosen', none, gone, gutters = 'page', ref, children }: {
  /** The page header: the chosen item's title, its line and its acts, or the view's own where it has no list. */
  header?: ReactNode;
  state?: MainState;
  /** What nothing chosen says, and the list's `＋`. */
  none?: MainNotice;
  /** What an item that has gone says. */
  gone?: MainNotice;
  gutters?: keyof typeof GUTTERS;
  /** The scroll box, for a page that follows its own tail. */
  ref?: Ref<HTMLElement>;
  children?: ReactNode;
}) {
  const notice = state === 'none' ? none : state === 'gone' ? gone : undefined;
  return (
    <main
      ref={ref}
      data-region="main"
      data-main-state={state}
      aria-busy={state === 'loading' || undefined}
      className={cn('@container/main relative min-h-0 min-w-0 flex-1 overflow-y-auto', GUTTERS[gutters])}
    >
      {header}
      {state === 'loading'
        ? <SkeletonRows rows={5} />
        : notice
          ? <EmptyState icon={notice.icon ?? 'inbox'} headline={notice.headline} body={notice.body} action={notice.action} />
          : children}
    </main>
  );
}
