import { type ReactNode, type RefObject, useEffect, useRef, useState } from 'react';
import { LIST_BOUNDS, type ListChoice, type ListLayout, type ListMode, type ListView, listLayout, listToggled } from './layout';
import { type ListMake, ListPane } from './ListPane';
import type { ListPanes } from './listPanes';

/**
 * A view's list, as it hands it to the frame (D118 §2, §5): what `ListPane` draws, less where it stands,
 * which the frame decides from the room and the view's memory.
 */
export type ListSpec = {
  /** Which view's list: its bounds (`LIST_BOUNDS`) and its memory (`listPanes.ts`). */
  view: ListView;
  name: string;
  labels: { open: string; close: string; resize: string };
  make?: ListMake;
  more?: ReactNode;
  strip?: ReactNode;
  loading?: boolean;
  empty?: { headline: string; body: string };
  /** The item the list has chosen: a change of it is a choice, which closes a list laid over the main area. */
  chosen?: string | null;
  /** The list itself: its rows, each carrying `data-list-row`. */
  body: ReactNode;
};

/**
 * What a view puts in the frame (D118 §1): its list pane where it has one, and its main area. The frame
 * owns the rest — the activity bar, the strip, the status bar, the side bar and the panel — and they are the
 * same on every view.
 */
export type ViewLayout = { list?: ListSpec; main: ReactNode };

/**
 * How wide the window is and how wide the frame is (FRAME6): the window decides the one threshold left, a
 * full side bar under 768 px, and the frame decides the room, which is what makes the list a strip (D118).
 * Where nothing measures the frame (a unit test's DOM), it is the window less the 48px activity bar.
 */
export function useFrameWidth(frame: RefObject<HTMLElement | null>) {
  const [viewport, setViewport] = useState(() => window.innerWidth);
  const [measured, setMeasured] = useState(0);

  useEffect(() => {
    const onResize = () => setViewport(window.innerWidth);
    window.addEventListener('resize', onResize);
    const element = frame.current;
    if (element) setMeasured(element.getBoundingClientRect().width);
    const observer = element && typeof ResizeObserver !== 'undefined'
      ? new ResizeObserver(([entry]) => { if (entry) setMeasured(entry.contentRect.width); })
      : null;
    if (element) observer?.observe(element);
    return () => {
      window.removeEventListener('resize', onResize);
      observer?.disconnect();
    };
  }, [frame]);

  return { viewport, frame: measured > 0 ? measured : Math.max(0, viewport - 48) };
}

/** What the person chose for a view's list: its memory, and whether it lies over the main area now. */
export function listChoice(view: ListView, lists: ListPanes, over: boolean): ListChoice {
  const kept = lists.pane(view);
  return { bounds: LIST_BOUNDS[view], width: kept.width, closed: kept.closed, over };
}

/**
 * The list's mode told to the application, whose doors toggle it and say whether it is shown; and a list
 * laid over the main area let go once the room makes it anything else (D118 §3f: it is never kept).
 */
export function useListMode(
  mode: ListMode | null, onListMode: ((mode: ListMode | null) => void) | undefined, over: boolean, setOver: (over: boolean) => void,
) {
  useEffect(() => { onListMode?.(mode); }, [mode, onListMode]);
  useEffect(() => { if (over && mode !== 'over') setOver(false); }, [over, mode, setOver]);
}

/**
 * A view's list on its pane (D118 §3a): the spec it handed in, where the room put it, and its memory.
 *
 * @remarks
 * Its closing and its width are the view's own (`listPanes.ts`); laying it over the main area is the
 * frame's, and never kept. **A choice closes a list laid over**: a change of the chosen item, and the `＋`,
 * which opens a form.
 */
export function ViewListPane({ spec, layout, lists, onOver }: {
  spec: ListSpec;
  layout: ListLayout;
  lists: ListPanes;
  /** The list laid over the main area, or let go. */
  onOver: (over: boolean) => void;
}) {
  const chosen = spec.chosen ?? null;
  const was = useRef(chosen);
  const laid = layout.mode === 'over';
  useEffect(() => {
    if (was.current !== chosen && laid) onOver(false);
    was.current = chosen;
  }, [chosen, laid, onOver]);

  const make = spec.make;
  return (
    <ListPane
      name={spec.name}
      labels={spec.labels}
      layout={layout}
      bounds={LIST_BOUNDS[spec.view]}
      make={make && { ...make, onMake: (kind) => { if (laid) onOver(false); make.onMake(kind); } }}
      more={spec.more}
      strip={spec.strip}
      loading={spec.loading}
      empty={spec.empty}
      // The strip's open: beside where there is room, over the main area where the window drew the strip.
      onOpen={() => {
        const next = listToggled(layout);
        lists.setClosed(spec.view, next.closed);
        onOver(next.over);
      }}
      onClose={() => lists.setClosed(spec.view, true)}
      onDismiss={() => onOver(false)}
      onResize={(width) => lists.setWidth(spec.view, width)}
    >
      {spec.body}
    </ListPane>
  );
}

/**
 * **A browser's frame** (D118 §4, amending DOCK1a): the view's list and its main area, which hold nothing
 * machine-local, and never the side bar or the panel, which hold this machine's sessions (D47 §4). The list
 * has the frame's whole width for room, since no side bar stands beside it.
 *
 * **The monitor window's too** (FRAME1h): its rail and its tiles, under its native frame, with no side bar
 * beside them either (D55 §b).
 */
export function ViewFrame({ layout, lists, over, onOver, onListMode }: {
  layout: ViewLayout;
  lists: ListPanes;
  /** The list laid over the main area: the application's, so its doors reach it. */
  over: boolean;
  onOver: (over: boolean) => void;
  onListMode?: (mode: ListMode | null) => void;
}) {
  const root = useRef<HTMLDivElement>(null);
  const width = useFrameWidth(root);
  const spec = layout.list;
  const list = spec ? listLayout(width.frame, 0, listChoice(spec.view, lists, over)) : null;
  useListMode(list?.mode ?? null, onListMode, over, onOver);

  return (
    // Its own stacking context, as the Work frame's is, so an overlay drawn at the page's root lies above it.
    <div ref={root} className="relative isolate flex min-h-0 min-w-0 flex-1">
      {spec && list && <ViewListPane spec={spec} layout={list} lists={lists} onOver={onOver} />}
      <div className="flex min-w-0 flex-1 flex-col">{layout.main}</div>
    </div>
  );
}
