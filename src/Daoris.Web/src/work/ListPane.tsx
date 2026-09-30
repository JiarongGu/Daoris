import { type KeyboardEvent, type ReactNode, useEffect, useRef } from 'react';
import * as Menu from '@radix-ui/react-dropdown-menu';
import { Button, EmptyState, Icon, SkeletonRows, Tip } from '../ui';
import { cn } from '../lib/cn';
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
  name, labels, layout, bounds, make, more, strip, loading = false, empty, onOpen, onClose, onDismiss, onResize, children,
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
    if (event.key !== 'Escape' || event.defaultPrevented) return;
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
          ? (
            <EmptyState
              icon="inbox"
              headline={empty.headline}
              body={empty.body}
              action={make && <Button onClick={() => make.onMake(make.kinds?.[0]?.id)}>{make.label}</Button>}
            />
          )
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

  if (layout.mode === 'open') {
    return (
      <aside ref={aside} data-region="list" data-list-mode="open" className="relative flex shrink-0 flex-col border-r border-line" style={{ width: layout.beside }}>
        {header}
        {body}
        {edge}
      </aside>
    );
  }

  return (
    <aside ref={aside} data-region="list" data-list-mode={layout.mode} className="relative flex shrink-0 flex-col border-r border-line" style={{ width: LIST_STRIP }}>
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
          {body}
          {edge}
        </div>
      )}
    </aside>
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
    <Menu.Root modal={false}>
      <Tip content={make.label} side={side}>
        <Menu.Trigger asChild>
          <Button variant="ghost" aria-label={make.label} className={className}>{icon}</Button>
        </Menu.Trigger>
      </Tip>
      <Menu.Portal>
        <Menu.Content
          side={side}
          align="start"
          sideOffset={4}
          collisionPadding={8}
          className="z-30 min-w-44 rounded-control border border-line bg-overlay p-1 text-small shadow-lg"
        >
          {make.kinds.map((kind) => (
            <Menu.Item
              key={kind.id}
              onSelect={() => make.onMake(kind.id)}
              className="flex cursor-default items-center gap-2 rounded-control px-2 py-1.5 text-ink outline-none data-[highlighted]:bg-accent-soft"
            >
              {kind.label}
            </Menu.Item>
          ))}
        </Menu.Content>
      </Menu.Portal>
    </Menu.Root>
  );
}
