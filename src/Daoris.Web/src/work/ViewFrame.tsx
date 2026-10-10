import { type ReactNode, type RefObject, useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { OpenGroup } from '../opener';
import { LIST_BOUNDS, type ListChoice, type ListLayout, type ListMode, type ListView, listLayout, listToggled } from './layout';
import { type ListMake, ListPane } from './ListPane';
import type { ChoiceStanding, ListPanes } from './listPanes';

/**
 * A view's list, as it hands it to the frame (D118 §2, §5): what `ListPane` draws, less where it stands,
 * which the frame decides from the room and the view's memory.
 */
export type ListSpec = {
  /** Which view's list: its bounds (`LIST_BOUNDS`) and its memory (`listPanes.ts`). */
  view: ListView;
  /**
   * The list whose memory holds the chosen item, where it is not the pane's own: Knowledge's mode (UX6i, D150 §2.2), whose
   * chosen item and filters are Search's or Convergence's while the pane's closing and width are Knowledge's.
   */
  chosenIn?: ListView;
  name: string;
  labels: { open: string; close: string; resize: string };
  make?: ListMake;
  more?: ReactNode;
  /** What heads the list above its rows, outside their scroll: Knowledge's two-way choice (UX6i). */
  head?: ReactNode;
  strip?: ReactNode;
  loading?: boolean;
  empty?: { headline: string; body: string };
  /** The item the list has chosen: a change of it is a choice, which closes a list laid over the main area. */
  chosen?: string | null;
  /**
   * What the chosen item is now, as the view reads it (UX6b, D150 §8): a remembered choice whose item closed or went is
   * let go, so the view opens with nothing chosen. Absent with nothing chosen, and for a view whose items never close
   * or go (Settings' domains, Sessions' attended session).
   */
  standing?: ChoiceStanding;
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
 *
 * `over` and `setOver` are the application's own; `bringing`, a go's group still on its way (`useGoOver`), whose list
 * laid over stays laid over once the group is brought.
 */
export function useListMode(
  mode: ListMode | null, onListMode: ((mode: ListMode | null) => void) | undefined, over: boolean, setOver: (over: boolean) => void,
  bringing = false,
) {
  useEffect(() => { onListMode?.(mode); }, [mode, onListMode]);
  useEffect(() => { if (over && mode !== 'over') setOver(false); }, [over, mode, setOver]);
  useEffect(() => { if (bringing && mode === 'over' && !over) setOver(true); }, [bringing, mode, over, setOver]);
}

/**
 * A go to a group of the view's list (ENTRY1g, D161's ENTRY1b note): a list drawn as a strip, whoever drew it, is laid over
 * the main area while the group is brought into view, and `useListMode` keeps it laid over once it is, as a strip the
 * person opened. Never remembered, so the person's closing stands once it goes.
 *
 * @remarks
 * **Laid over in the render the go arrives in**, not by an effect after it: the view looks for the group's heading once its
 * list has answered, and a list still a strip then would let the go go with nothing brought.
 *
 * **Let go with the list.** The list's own doors set `setOver`, so a list let go before its group is brought (Escape, a
 * press outside, a choice) lets the go go too: a group appearing later would take the focus from where the person went. A
 * group is held only for the view whose list holds it (`opener.ts`), so the frame need not ask whose it is.
 */
export function useGoOver(group: OpenGroup | null, over: boolean, onOver: (over: boolean) => void, onGroupBrought?: () => void) {
  const bringing = group !== null;
  return {
    bringing,
    over: over || bringing,
    setOver: (next: boolean) => {
      if (!next && bringing) onGroupBrought?.();
      onOver(next);
    },
  };
}

/**
 * A view's list on its pane (D118 §3a): the spec it handed in, where the room put it, and its memory.
 *
 * @remarks
 * Its closing and its width are the view's own (`listPanes.ts`); laying it over the main area is the
 * frame's, and never kept. **A choice closes a list laid over**: a change of the chosen item, and the `＋`,
 * which opens a form. Not a change that is no choice (ENTRY1g): the next view's item as a go opens it, the
 * frame drawing every view's list on this one pane, nor a remembered item let go as the view opens (UX6b).
 *
 * **A remembered choice ends with what it chose** (UX6b, design §1 rule 6): what the view reads of its chosen item is
 * told to the list's memory, which lets go of a remembered one that closed or went. Before paint, so a done quest's page
 * is never drawn on the way to nothing chosen.
 */
export function ViewListPane({ spec, layout, lists, onOver }: {
  spec: ListSpec;
  layout: ListLayout;
  lists: ListPanes;
  /** The list laid over the main area, or let go. */
  onOver: (over: boolean) => void;
}) {
  const chosen = spec.chosen ?? null;
  const was = useRef({ view: spec.view, chosen });
  const laid = layout.mode === 'over';
  useEffect(() => {
    const before = was.current;
    if (laid && before.view === spec.view && before.chosen !== chosen && chosen !== null) onOver(false);
    was.current = { view: spec.view, chosen };
  }, [spec.view, chosen, laid, onOver]);
  const { settle } = lists;
  const standing = chosen ? spec.standing : undefined;
  const memory = spec.chosenIn ?? spec.view;
  useLayoutEffect(() => {
    if (standing) settle(memory, standing);
  }, [settle, memory, standing]);

  const make = spec.make;
  return (
    <ListPane
      name={spec.name}
      labels={spec.labels}
      layout={layout}
      bounds={LIST_BOUNDS[spec.view]}
      make={make && { ...make, onMake: (kind) => { if (laid) onOver(false); make.onMake(kind); } }}
      more={spec.more}
      head={spec.head}
      strip={spec.strip}
      loading={spec.loading}
      empty={spec.empty}
      // The strip's open: beside where there is room, over the main area where the window drew the strip.
      onOpen={() => {
        const next = listToggled(layout);
        if (next.closed !== undefined) lists.setClosed(spec.view, next.closed);
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
export function ViewFrame({ layout, lists, over, onOver, onListMode, group = null, onGroupBrought }: {
  layout: ViewLayout;
  lists: ListPanes;
  /** The list laid over the main area: the application's, so its doors reach it. */
  over: boolean;
  onOver: (over: boolean) => void;
  onListMode?: (mode: ListMode | null) => void;
  /** The group of the view's list a go brings into view (ENTRY1g), laid over where the list is a strip; and its letting go. */
  group?: OpenGroup | null;
  onGroupBrought?: () => void;
}) {
  const root = useRef<HTMLDivElement>(null);
  const width = useFrameWidth(root);
  const spec = layout.list;
  const go = useGoOver(group, over, onOver, onGroupBrought);
  const list = spec ? listLayout(width.frame, 0, listChoice(spec.view, lists, go.over)) : null;
  useListMode(list?.mode ?? null, onListMode, over, onOver, go.bringing);

  return (
    // Its own stacking context, as the Work frame's is, so an overlay drawn at the page's root lies above it.
    <div ref={root} className="relative isolate flex min-h-0 min-w-0 flex-1">
      {spec && list && <ViewListPane spec={spec} layout={list} lists={lists} onOver={go.setOver} />}
      <div className="flex min-w-0 flex-1 flex-col">{layout.main}</div>
    </div>
  );
}
