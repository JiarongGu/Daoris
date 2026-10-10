import { Fragment, type KeyboardEvent, type ReactNode, useEffect, useRef } from 'react';
import { Button, EmptyState, Icon, Menu, SkeletonRows, Tip } from '../ui';
import { cn } from '../lib/cn';
import { isComposing } from '../lib/composing';
import { Splitter } from './frame';
import { LIST_STRIP, type ListBounds, type ListLayout } from './layout';
import { LIST_DOOR, useListKeys } from './listKeys';

/**
 * What a view makes from its list's `＋` (D118 §3a): one control, and where the view makes two kinds of
 * thing, a menu of both with its primary first.
 */
export type ListMake = {
  /** The `＋`'s name: the view's act, *Start a session*. */
  label: string;
  kinds?: { id: string; label: string }[];
  /** Told which kind, where there are kinds. */
  onMake: (kind?: string) => void;
};

/** The attribute the strip's open carries, so a list laid over hands the focus back to it. */
const OPEN_ATTRIBUTE = 'data-list-open';

/**
 * A press that lands in a menu or a dialog the list opened is the list's, though drawn elsewhere; and one
 * on a door to the list is the door's, which toggles it.
 */
const OPENED_ELSEWHERE = `[data-radix-popper-content-wrapper], [role="menu"], [role="dialog"], [${LIST_DOOR}]`;

/**
 * A view's **list pane** (D118 §3a, §5): the header with the list's name, its `＋`, the view's ⋯ and its
 * close; the list; the edge it is resized by; its 56px strip; and the list laid over the main area.
 *
 * @remarks
 * **A molecule: every state is reached by its props** and every press goes out. Where it stands is
 * `layout.ts`'s answer, handed in as `layout`, since the room rule lives there and nowhere else.
 *
 * - **Open**, it sits beside the main area at its width, resized by `Splitter` within the view's bounds.
 * - **A strip**, it keeps its doors: the open, the `＋`, and the view's marks. A strip the window drew
 *   has an open too, which lays the list over the main area (amending FRAME6, whose window strip offered
 *   none and so left an ended session and the rail's search out of reach below 1024 px).
 * - **Laid over**, the list lies beside its strip, over the main area, and takes the focus. It closes on
 *   Escape, on a press outside it, from its header, and from the strip's open pressed again; the caller
 *   closes it on a choice. Escape a search inside it answered first is the search's.
 *
 * The rows carry `data-list-row`, and ↑, ↓, Home and End move between them (`listKeys`).
 */
export function ListPane({
  name, labels, layout, bounds, make, more, head, strip, loading = false, empty, onOpen, onClose, onDismiss, onResize, children,
}: {
  /** The list's name, as its header says it. */
  name: string;
  /** The names of its own controls, in the view's words: *Show the session list* and the rest. */
  labels: { open: string; close: string; resize: string };
  layout: ListLayout;
  bounds: ListBounds;
  make?: ListMake;
  /** The view's ⋯: its list's own filters and menu. */
  more?: ReactNode;
  /**
   * What heads the list under its header, outside its rows' scroll, so it stays in reach as they scroll: Knowledge's
   * two-way choice (UX6i, D150 §2.2). Open and laid over; a strip's marks stand for it.
   */
  head?: ReactNode;
  /** The view's marks on its strip (`StripMark`s), where it has them. */
  strip?: ReactNode;
  /** The list's first load: skeleton rows, and the strip its controls alone. */
  loading?: boolean;
  /** The list holds nothing: the empty state, with the `＋`'s act. */
  empty?: { headline: string; body: string };
  /** The strip's open, whether the person or the window drew the strip. */
  onOpen: () => void;
  /** The person's close of a list open beside the main area. */
  onClose: () => void;
  /** A list laid over the main area closes. */
  onDismiss: () => void;
  /** A width dragged to, or null for the view's own again. */
  onResize: (width: number | null) => void;
  children: ReactNode;
}) {
  const keys = useListKeys();
  const aside = useRef<HTMLElement>(null);
  const over = useRef<HTMLDivElement>(null);
  const laid = layout.mode === 'over';
  // The latest close, for a listener that is added once per laying over.
  const dismiss = useRef(onDismiss);
  dismiss.current = onDismiss;

  // Laid over, the list takes the focus, so its keys work at once; and a press outside it closes it.
  useEffect(() => {
    if (!laid) return undefined;
    over.current?.focus();
    const outside = (event: PointerEvent) => {
      const target = event.target as Element | null;
      if (!target || aside.current?.contains(target) || target.closest?.(OPENED_ELSEWHERE)) return;
      dismiss.current();
    };
    document.addEventListener('pointerdown', outside, true);
    return () => document.removeEventListener('pointerdown', outside, true);
  }, [laid]);

  const onOverKey = (event: KeyboardEvent<HTMLDivElement>) => {
    // An Escape that drops an input method's composition in the list's search leaves the list where it is (IME1).
    if (isComposing(event) || event.key !== 'Escape' || event.defaultPrevented) return;
    event.preventDefault();
    onDismiss();
    aside.current?.querySelector<HTMLElement>(`[${OPEN_ATTRIBUTE}]`)?.focus();
  };

  const control = 'h-6 w-6 justify-center px-0';
  const header = (
    <header className="flex h-8 shrink-0 items-center gap-1 border-b border-line pl-3 pr-1.5">
      <span className="mr-auto min-w-0 truncate text-meta uppercase tracking-[0.06em] text-ink-faint">{name}</span>
      {make && <MakeButton make={make} className={control} />}
      {more}
      <Tip content={labels.close}>
        <Button variant="ghost" aria-label={labels.close} onClick={laid ? onDismiss : onClose} className={control}>
          <Icon name="railClose" size={14} />
        </Button>
      </Tip>
    </header>
  );

  const body = (
    <div className="min-h-0 flex-1 overflow-y-auto" onKeyDown={keys}>
      {loading
        ? <div className="px-2.5 py-2"><SkeletonRows rows={5} /></div>
        : empty
          ? <EmptyState icon="inbox" headline={empty.headline} body={empty.body} action={make && <EmptyActs make={make} />} />
          : children}
    </div>
  );

  const edge = (
    <Splitter
      label={labels.resize}
      value={layout.width}
      min={bounds.min}
      max={bounds.max}
      edge="right"
      onChange={onResize}
      onReset={() => onResize(null)}
    />
  );

  // Named by the list's name, open or a strip (FRAME1d): a landmark a reader, and a test, finds by its name.
  if (layout.mode === 'open') {
    return (
      <aside ref={aside} aria-label={name} data-region="list" data-list-mode="open" className="relative flex shrink-0 flex-col border-r border-line" style={{ width: layout.beside }}>
        {header}
        {head}
        {body}
        {edge}
      </aside>
    );
  }

  return (
    <aside ref={aside} aria-label={name} data-region="list" data-list-mode={layout.mode} className="relative flex shrink-0 flex-col border-r border-line" style={{ width: LIST_STRIP }}>
      {/* The strip's controls, stacked, since 56px holds one across. */}
      <header className="flex shrink-0 flex-col items-center gap-0.5 border-b border-line py-1">
        <Tip content={labels.open} side="right">
          <Button
            variant="ghost"
            aria-label={labels.open}
            aria-expanded={laid}
            {...{ [OPEN_ATTRIBUTE]: '' }}
            onClick={laid ? onDismiss : onOpen}
            className={control}
          >
            <Icon name="railOpen" size={14} />
          </Button>
        </Tip>
        {make && <MakeButton make={make} side="right" className={control} />}
      </header>

      {strip && !loading && <div className="min-h-0 flex-1 overflow-y-auto" onKeyDown={keys}>{strip}</div>}

      {laid && (
        // Beside the strip and over the main area, above a full side bar (D118 §3d).
        <div
          ref={over}
          role="region"
          aria-label={name}
          tabIndex={-1}
          onKeyDown={onOverKey}
          className={cn(
            'absolute inset-y-0 left-full z-30 flex flex-col border-r border-line bg-page outline-none',
            'shadow-[6px_0_24px_rgb(15_12_8/0.12)]',
          )}
          style={{ width: layout.width }}
        >
          {header}
          {head}
          {body}
          {edge}
        </div>
      )}
    </aside>
  );
}

/**
 * An empty list's acts: the `＋`'s, or where it makes two kinds, each by its own name with its primary first (PLUGUI1b,
 * D119 §3.1), since a menu behind one button is a door too many on a list with nothing in it.
 */
function EmptyActs({ make }: { make: ListMake }) {
  if (!make.kinds || make.kinds.length < 2) {
    return <Button onClick={() => make.onMake(make.kinds?.[0]?.id)}>{make.label}</Button>;
  }
  return (
    <div className="flex flex-wrap justify-center gap-2">
      {make.kinds.slice(0, 2).map((kind) => <Button key={kind.id} onClick={() => make.onMake(kind.id)}>{kind.label}</Button>)}
    </div>
  );
}

/**
 * A group of a list's rows under its name and count, as the session rail draws a repository's (`RepositoryGroup`) and
 * the Plugins view a state's. Its rows are `<li>`s, each a row of its list (`data-list-row`). A group a door may bring into
 * view (ENTRY1b) has its heading's `id` (`groupHeading`), and the heading takes the focus there, out of the Tab order.
 */
export function ListGroup({ title, id, children }: { title: string; id?: string; children: ReactNode }) {
  return (
    <section className="border-t border-line first:border-t-0">
      <h3 id={id} tabIndex={id ? -1 : undefined} className="m-0 truncate px-2.5 pb-1 pt-2.5 text-small font-semibold text-ink">
        {title}
      </h3>
      <ul className="m-0 list-none p-0">{children}</ul>
    </section>
  );
}

/** The id of a list's group heading, the one a door brings into view (ENTRY1b): the view's, then the group's own name. */
export const groupHeading = (view: string, group: string) => `${view}-group-${group}`;

/**
 * A group a door names, brought into view once its list has answered (ENTRY1b, D161's ENTRY1 note): its heading scrolled to
 * the list's top and focused, then the door told, so the next visit opens as the list was left.
 *
 * @remarks
 * **An event, let go once the list has answered**, as Settings' anchor and an agent's part are told once brought. A list
 * that draws no such group then (nothing waits in it, it is arranged by repository or searched, it is a strip) tells the
 * door all the same: a group that appeared later would take the focus from wherever the person had gone since.
 */
export function useBringGroup(heading: string | null, answered: boolean, onBrought?: () => void) {
  const told = useRef(onBrought);
  told.current = onBrought;
  useEffect(() => {
    if (!heading || !answered) return;
    const found = document.getElementById(heading);
    found?.scrollIntoView?.({ block: 'start' });
    found?.focus({ preventScroll: true });
    told.current?.();
  }, [heading, answered]);
}

/**
 * A row's door: the whole row chooses it, wearing the list's selection as the rail's rows do, and a closed record
 * read as finished, dimmed (FRAME1d). `aria-current`, as the rail's: the row is a button, not a listbox option.
 */
export function ListRowDoor({ chosen, dimmed = false, onPress, children }: {
  chosen: boolean;
  dimmed?: boolean;
  onPress: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      aria-current={chosen || undefined}
      onClick={onPress}
      className={cn(
        'block w-full min-w-0 border-l-[3px] px-2.5 py-1.5 text-left transition-colors duration-(--speed)',
        'hover:bg-accent-soft/50',
        chosen ? 'border-l-accent bg-accent-soft' : 'border-l-transparent',
        dimmed && 'opacity-75',
      )}
    >
      {children}
    </button>
  );
}

/**
 * One of the ⋯'s items: an act, or a toggle where it says whether it is on (a filter: *Include closed*). `rule` draws a
 * rule above it, setting an act apart from the filters before it (Sessions' *Archive what ended…*, D126 §4.1).
 */
export type MoreItem = { id: string; label: string; checked?: boolean; rule?: boolean };

/** One value among several, a filter's (*Receiver*): named above its options, the chosen one ticked. */
export type MoreChoice = {
  label: string;
  value: string;
  options: { value: string; label: string }[];
  onChoose: (value: string) => void;
};

/**
 * The list's own **⋯** (D118 §3a): its filters and its menu, in its header before its close. An arrow is a direction,
 * never a menu, so a region's own menu is "⋯", as VS Code's *Views and More Actions* is (platform language §4).
 *
 * @remarks
 * **A filter is a ticked item** (FRAME1d): one value among several is a group named for what it filters, its chosen
 * value ticked, and a toggle is ticked while it is on. The view keeps the values (`listPanes.ts`); the menu only says
 * them and tells it what was chosen.
 */
export function ListMore({ label, items = [], choice, onChoose }: {
  /** Its name and its tip. */
  label: string;
  items?: MoreItem[];
  /** One value among several, above the items. */
  choice?: MoreChoice;
  /** Told the item's id: an act chosen, or a toggle pressed. */
  onChoose?: (id: string) => void;
}) {
  // The tick's column is reserved on every row where anything may be ticked, so the labels line up.
  const ticks = choice !== undefined || items.some((item) => item.checked !== undefined);
  return (
    <Menu.Root>
      <Tip content={label}>
        <Menu.Trigger asChild>
          <Button variant="ghost" aria-label={label} className="h-6 w-6 justify-center px-0">
            <Icon name="more" size={15} />
          </Button>
        </Menu.Trigger>
      </Tip>
      <Menu.Content side="bottom" align="end" highlight="accent" className="min-w-44">
        {choice && (
          <>
            <Menu.Label className="pt-1.5">{choice.label}</Menu.Label>
            <Menu.RadioGroup aria-label={choice.label} value={choice.value} onValueChange={choice.onChoose}>
              {choice.options.map((option) => (
                <Menu.RadioItem key={option.value} value={option.value}>
                  <span className="truncate">{option.label}</span>
                </Menu.RadioItem>
              ))}
            </Menu.RadioGroup>
            {items.length > 0 && <Menu.Separator />}
          </>
        )}
        {items.map((item) => (
          <Fragment key={item.id}>
            {item.rule && <Menu.Separator />}
            {item.checked === undefined
              ? (
                <Menu.Item tick={ticks ? false : undefined} onSelect={() => onChoose?.(item.id)}>
                  {item.label}
                </Menu.Item>
              )
              : (
                <Menu.CheckboxItem checked={item.checked} onCheckedChange={() => onChoose?.(item.id)}>
                  {item.label}
                </Menu.CheckboxItem>
              )}
          </Fragment>
        ))}
      </Menu.Content>
    </Menu.Root>
  );
}

/** The `＋`: the act itself, or a menu of the view's two kinds with its primary first. */
function MakeButton({ make, side = 'bottom', className }: { make: ListMake; side?: 'bottom' | 'right'; className?: string }) {
  const icon = <Icon name="plus" size={15} />;
  if (!make.kinds || make.kinds.length < 2) {
    return (
      <Tip content={make.label} side={side}>
        <Button variant="ghost" aria-label={make.label} onClick={() => make.onMake(make.kinds?.[0]?.id)} className={className}>{icon}</Button>
      </Tip>
    );
  }
  return (
    <Menu.Root>
      <Tip content={make.label} side={side}>
        <Menu.Trigger asChild>
          <Button variant="ghost" aria-label={make.label} className={className}>{icon}</Button>
        </Menu.Trigger>
      </Tip>
      <Menu.Content side={side} align="start" highlight="accent" className="min-w-44">
        {make.kinds.map((kind) => (
          <Menu.Item key={kind.id} onSelect={() => make.onMake(kind.id)}>{kind.label}</Menu.Item>
        ))}
      </Menu.Content>
    </Menu.Root>
  );
}
