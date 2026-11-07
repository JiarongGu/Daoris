import { type ReactNode, type Ref, type RefObject, useId, useLayoutEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, EmptyState, type IconName, Prose, SectionTitle, SkeletonRows } from '../../design/ui';
import { cn } from '../../lib/cn';
import { type ContextOffer, contextOffer } from '../../menus/press';
import { usePublishedOffer } from '../../menus/mainOffer';

/**
 * A page's header in the main area: its title, with what is beside it (a version, its pills); its
 * id in the mono face; its line; and its acts. A title and a line are content.
 *
 * @remarks
 * Shared by every view's page: a plugin's, a quest's and an ask's wear it. A
 * repository's has no id line: its name is its id, and a title said twice is noise.
 *
 * **The line is shown whole**: content is shown as it is, and cut to one line a plugin's
 * description would end in an ellipsis, its tip the only place it could be read. It wraps at the column's
 * edge, as the page's every block does.
 *
 * **An icon leads it where the record has one**: a plugin's, beside its title, decoration the title
 * names.
 *
 * **A record whose state moves leads with its state**: `lead` is drawn before the
 * title, so the pill stays in one place whatever the title's length; `clamp` gives the title that many lines at most,
 * whole in its tip and on *Show all* where it is cut; and `facts` is the line under it, the facts the state makes
 * matter, said once. Such a head has no id line: the id is its ⋯'s and its *Details*'.
 */
export function PageHead({
  title, version, pills, id, line, acts, fold, more, icon, lead, clamp, facts, fact, wholeElsewhere, actsBeside = false,
  factsOneLine = false, factsTip,
}: {
  title: string; version?: string; pills?: ReactNode; id?: string; line?: string; icon?: ReactNode;
  /**
   * The acts: primary first, then the secondary. They hold still: above
   * 640 px of the main area they sit in a column of their own at the title's first line, so a long title wraps under itself;
   * at 640 px or less they take a right-aligned row under the facts.
   */
  acts?: ReactNode;
  /** An act that folds into the ⋯ at the narrow width: shown only above 640 px, after `acts`; its caller says it again in the ⋯. */
  fold?: ReactNode;
  /** The ⋯, last. */
  more?: ReactNode;
  /** What leads the title: a record's state, on its pill. */
  lead?: ReactNode;
  /** The title's lines at most; absent, it wraps whole. */
  clamp?: 1 | 2;
  /** The facts line under the title, in order; none draws no line. */
  facts?: readonly string[];
  /**
   * A fact that is a door, last on the facts line: where the record's work stands in its
   * workflow, whose door opens the run.
   */
  fact?: ReactNode;
  /**
   * The head offers its own door to the whole text (*View full request*), so the clamped title keeps its tip
   * and drops its *Show all*: one way to the same text.
   */
  wholeElsewhere?: boolean;
  /**
   * The acts stay beside the title at every width: a session's head, whose acts at the narrow width are an icon
   * and the ⋯, so a row of their own under the facts would spend 38 px of the record head's 200.
   */
  actsBeside?: boolean;
  /**
   * The facts line keeps to one line, cut with an ellipsis before `fact`, which it never cuts: a session's head,
   * whose line a running call's whole command would wrap to three.
   */
  factsOneLine?: boolean;
  /** The facts line's tip: what a fact says whole where the line says it short (a running call's title). */
  factsTip?: string;
}) {
  const said = facts && facts.length > 0 ? facts.join(' · ') : null;
  return (
    <header className={cn('mb-4 grid gap-x-4 gap-y-2', actsBeside
      ? 'grid-cols-[minmax(0,1fr)_auto] items-start'
      : '@[40rem]/main:grid-cols-[minmax(0,1fr)_auto] @[40rem]/main:items-start')}
    >
      <div className="flex min-w-0 items-start gap-3">
        {icon && <span className="shrink-0">{icon}</span>}
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
            {lead && <span className="flex shrink-0 items-baseline gap-1.5 self-start pt-1.5">{lead}</span>}
            {clamp
              ? <ClampedTitle title={title} lines={clamp} showAll={!wholeElsewhere} />
              : <h1 className="m-0 min-w-0 text-view font-[650] tracking-[-0.01em] wrap-anywhere">{title}</h1>}
            {version && <span className="font-mono text-small text-ink-faint">{version}</span>}
            {pills}
          </div>
          {(said || fact) && (factsOneLine
            ? (
              <p title={factsTip} className="m-0 mt-0.5 flex min-w-0 items-baseline text-small text-ink-faint">
                {said && <span className="min-w-0 truncate">{said}</span>}
                {said && fact && <span aria-hidden className="shrink-0 whitespace-pre"> · </span>}
                {fact && <span className="shrink-0">{fact}</span>}
              </p>
            )
            : (
              <p title={factsTip} className="m-0 mt-0.5 text-small text-ink-faint wrap-anywhere">
                {said}
                {said && fact && <span aria-hidden> · </span>}
                {fact}
              </p>
            ))}
          {id && <p className="m-0 mt-0.5 font-mono text-meta text-ink-faint">{id}</p>}
          {line && <Prose className="mt-1 wrap-anywhere">{line}</Prose>}
        </div>
      </div>
      {(acts || fold || more) && (
        <div className={cn('flex items-center justify-end gap-2', actsBeside ? 'self-start' : '@[40rem]/main:self-start')}>
          {acts}
          {fold && <div className="hidden @[40rem]/main:contents">{fold}</div>}
          {more}
        </div>
      )}
    </header>
  );
}

/** Whether an element's text runs past the lines it is clamped to, read again whenever its box changes. */
export function useCut(element: RefObject<HTMLElement | null>, when: unknown): boolean {
  const [cut, setCut] = useState(false);
  useLayoutEffect(() => {
    const node = element.current;
    if (!node) return undefined;
    const read = () => setCut(node.scrollHeight > node.clientHeight + 1);
    read();
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(read);
    observer?.observe(node);
    return () => observer?.disconnect();
  }, [element, when]);
  return cut;
}

/**
 * A title clamped to its lines: whole in its tip, and whole on *Show all* where the clamp cuts
 * it. Content is shown as it is: nothing is rewritten to fit.
 */
function ClampedTitle({ title, lines, showAll }: { title: string; lines: 1 | 2; showAll: boolean }) {
  const { t } = useTranslation();
  const own = useRef<HTMLHeadingElement>(null);
  const [whole, setWhole] = useState(false);
  const cut = useCut(own, title);
  return (
    <span className="grid min-w-0 flex-1 basis-48 justify-items-start">
      <h1
        ref={own}
        title={title}
        className={cn(
          'm-0 min-w-0 text-view font-[650] tracking-[-0.01em] wrap-anywhere',
          !whole && (lines === 1 ? 'line-clamp-1' : 'line-clamp-2'),
        )}
      >
        {title}
      </h1>
      {showAll && (cut || whole) && (
        <Button variant="ghost" className="px-0 py-0 text-small" onClick={() => setWhole((was) => !was)}>
          {t(whole ? 'work.head.noteLess' : 'work.head.noteMore')}
        </Button>
      )}
    </span>
  );
}

/**
 * **A page's tabs**: several pages of one record under one header, each a tab, and the chosen one's
 * panel beneath, named by its tab, as a repository's page and a workspace's hold theirs (`projects/tabs.ts`).
 *
 * @remarks
 * **The chosen tab is its holder's**, remembered per view, so a door can open the page at a tab.
 *
 * **One stop for the keys**: the chosen tab is where Tab lands, and ←, →, Home and End choose along the row, as every
 * tab list's do. **A tab is its whole name**: the names are short words in both languages, and a row too narrow
 * for them scrolls rather than cutting one.
 *
 * A molecule: every state is reached by its props, and every press goes out.
 */
export function PageTabs<T extends string>({ label, tabs, chosen, onChoose, children }: {
  /** The list's accessible name: the record whose pages these are. */
  label: string;
  tabs: readonly { id: T; label: string }[];
  chosen: T;
  onChoose: (id: T) => void;
  /** The chosen tab's panel. */
  children: ReactNode;
}) {
  const base = useId();
  const tabId = (id: T) => `${base}-tab-${id}`;
  const choose = (id: T) => {
    onChoose(id);
    document.getElementById(tabId(id))?.focus();
  };
  const step = (by: number) => {
    const at = Math.max(0, tabs.findIndex((tab) => tab.id === chosen));
    choose(tabs[(at + by + tabs.length) % tabs.length]!.id);
  };
  return (
    <>
      <div
        role="tablist"
        aria-label={label}
        onKeyDown={(event) => {
          if (event.key === 'ArrowRight') step(1);
          else if (event.key === 'ArrowLeft') step(-1);
          else if (event.key === 'Home') choose(tabs[0]!.id);
          else if (event.key === 'End') choose(tabs[tabs.length - 1]!.id);
          else return;
          event.preventDefault();
        }}
        className="-mt-1 mb-4 flex min-w-0 overflow-x-auto border-b border-line [scrollbar-width:none]"
      >
        {tabs.map((tab) => (
          <button
            key={tab.id}
            id={tabId(tab.id)}
            type="button"
            role="tab"
            aria-selected={tab.id === chosen}
            aria-controls={`${base}-panel`}
            tabIndex={tab.id === chosen ? 0 : -1}
            onClick={() => onChoose(tab.id)}
            className={cn(
              '-mb-px shrink-0 whitespace-nowrap border-b-2 px-3 py-1.5 text-body transition-colors duration-(--speed) first:pl-0',
              tab.id === chosen ? 'border-b-accent text-ink' : 'border-b-transparent text-ink-faint hover:text-ink',
            )}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <div role="tabpanel" id={`${base}-panel`} aria-labelledby={tabId(chosen)} className="min-w-0">
        {children}
      </div>
    </>
  );
}

/** A section of a page: its name as a heading, and the region it names. */
export function PageSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section aria-label={title} className="mt-6 first:mt-0">
      <SectionTitle>{title}</SectionTitle>
      {children}
    </section>
  );
}

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
export const GUTTERS = {
  page: 'px-6 pb-12 pt-5 max-md:px-3 max-md:pb-8 max-md:pt-4',
  session: 'flex flex-col px-4 py-3',
} as const;

/**
 * A view's **main area**: the scroll box beside its list, its page header's slot, and its
 * four states.
 *
 * @remarks
 * **It lays out by its own width, never the viewport's.** It is the container named `main`, and a split
 * inside it follows it (`@4xl/main:grid-cols-2`), since the list and the side bar narrow it while the
 * window stays as wide. Overview's repository rows' container query is the rule's
 * first instance.
 *
 * **It is the page's one column, and the column is its pane.** Every block of a page
 * takes its width, so a page wraps at one edge in every language; it is the one place a line's length
 * could be capped, and it caps none. A cap, if the window ever shows one is wanted, goes here once, in
 * `em`, never on a paragraph.
 *
 * **It scrolls by itself**, and is the block anything positioned inside it is placed in: an `sr-only` label
 * far down a long page otherwise takes the viewport as its block and grows a second scrollbar.
 *
 * **A molecule**: every state is reached by its props. A first load is skeleton rows, never the empty
 * state, which would read as *Nothing attended* while the list that holds the attended session is still on its
 * way.
 *
 * **A right-click on the page offers what is done to its record**: `menu`, the page's header's acts and
 * its body's, from the owner each already has. A field, a link, a selection and a code span on it add their own.
 *
 * **The menu bar reads the same offer**: Run's record group and Edit's *Copy ID* run what the page offers,
 * by the page's own presses, so a menu row applies exactly when the page offers it.
 */
export function ViewMain({ header, state = 'chosen', none, gone, gutters = 'page', menu, ref, children }: {
  /** The page header: the chosen item's title, its line and its acts, or the view's own where it has no list. */
  header?: ReactNode;
  /** What a right-click on the page offers, while it shows its record. */
  menu?: ContextOffer | null;
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
  usePublishedOffer(state === 'chosen' ? menu : null);
  return (
    <main
      ref={ref}
      data-region="main"
      data-main-state={state}
      aria-busy={state === 'loading' || undefined}
      className={cn('@container/main relative min-h-0 min-w-0 flex-1 overflow-y-auto', GUTTERS[gutters])}
      {...contextOffer(state === 'chosen' ? menu : null)}
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
