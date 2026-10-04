import { type ReactNode, type Ref, useId } from 'react';
import { EmptyState, type IconName, Prose, SectionTitle, SkeletonRows } from '../ui';
import { cn } from '../lib/cn';
import { type ContextOffer, contextOffer } from '../menus/press';

/**
 * A page's header in the main area (D118 §3b, D119 §3.2): its title, with what is beside it (a version, its pills); its
 * id in the mono face; its line; and its acts. A title and a line are content.
 *
 * @remarks
 * Shared by every view's page since FRAME1d: PLUGUI1b drew it for a plugin, and a quest's and an ask's wear it. A
 * repository's has no id line (FRAME1e): its name is its id, and a title said twice is noise.
 *
 * **The line is shown whole** (NAME2): content is shown as it is (platform language §4), and cut to one line a plugin's
 * description ended in an ellipsis on the install, its tip the only place it could be read. It wraps at the column's
 * edge, as the page's every block does (D141): a plugin's page asked for that alone (D140 §2), and every page has it.
 *
 * **An icon leads it where the record has one** (PLUGUI2, D140 §2): a plugin's, beside its title, decoration the title
 * names.
 */
export function PageHead({ title, version, pills, id, line, acts, icon }: {
  title: string; version?: string; pills?: ReactNode; id?: string; line?: string; acts?: ReactNode; icon?: ReactNode;
}) {
  return (
    <header className="mb-4 flex flex-wrap items-start justify-between gap-x-4 gap-y-2">
      <div className="flex min-w-0 flex-1 basis-64 items-start gap-3">
        {icon && <span className="shrink-0">{icon}</span>}
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
            <h1 className="m-0 min-w-0 text-view font-[650] tracking-[-0.01em] wrap-anywhere">{title}</h1>
            {version && <span className="font-mono text-small text-ink-faint">{version}</span>}
            {pills}
          </div>
          {id && <p className="m-0 mt-0.5 font-mono text-meta text-ink-faint">{id}</p>}
          {line && <Prose className="mt-1 wrap-anywhere">{line}</Prose>}
        </div>
      </div>
      {acts && <div className="flex flex-wrap items-center gap-2">{acts}</div>}
    </header>
  );
}

/**
 * **A page's tabs** (UX6f, D150 §4.2): several pages of one record under one header, each a tab, and the chosen one's
 * panel beneath, named by its tab. A repository's page has Details and Setup.
 *
 * @remarks
 * **The chosen tab is its holder's**, remembered per view (§4.2), so a door can open the page at a tab.
 *
 * **One stop for the keys**: the chosen tab is where Tab lands, and ←, →, Home and End choose along the row, as every
 * tab list's do. **A tab is its whole name** (TABS1): the names are short words in both languages, and a row too narrow
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

/** A section of a page: its name as a heading, and the region it names (D119 §3.2). */
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
 * **It is the page's one column, and the column is its pane** (D141, LAYOUT11). Every block of a page
 * takes its width, so a page wraps at one edge in every language; it is the one place a line's length
 * could be capped, and it caps none. A cap, if the window ever shows one is wanted, goes here once, in
 * `em` (D141 §3), never on a paragraph.
 *
 * **It scrolls by itself**, and is the block anything positioned inside it is placed in: an `sr-only` label
 * far down a long page otherwise took the viewport as its block and grew a second scrollbar (PERM1).
 *
 * **A molecule**: every state is reached by its props. A first load is skeleton rows, never the empty
 * state, which read as *Nothing attended* while the list that held the attended session was still on its
 * way (audit SE11).
 *
 * **A right-click on the page offers what is done to its record** (CTX1, D138 §2): `menu`, the page's header's acts and
 * its body's, from the owner each already has. A field, a link, a selection and a code span on it add their own.
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
