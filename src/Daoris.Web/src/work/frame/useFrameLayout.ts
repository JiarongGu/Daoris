import { useRef, useState } from 'react';
import type { OpenGroup } from '../../app/opener';
import { store } from '../../lib/stored';
import { type FrameClosings, useFrameClosings } from './closings';
import { frameLayout, type ListMode } from './layout';
import { DOCK_SHARE, PANEL_HEIGHT, remembered, rememberedShare } from './layoutMemory';
import { type ListPanes, useListPanes } from './listPanes';
import { type Placements, usePlacements } from './placements';
import { listChoice, useFrameWidth, useGoOver, useListMode, type ViewLayout } from './ViewFrame';

/**
 * The Work frame's columns and regions: the list, the panel's height and the dock's share as the person left
 * them and as the window leaves room for, and what the person closed and moved, the application's where it holds them.
 */
export function useFrameLayout({ lists, closings, placements, viewLayout, group, onGroupBrought, onListMode }: {
  lists?: ListPanes;
  closings?: FrameClosings;
  placements?: Placements;
  viewLayout?: ViewLayout;
  group: OpenGroup | null;
  onGroupBrought?: () => void;
  onListMode?: (mode: ListMode | null) => void;
}) {
  const [height, setHeight] = useState(() => remembered(PANEL_HEIGHT, 200));

  // The frame's columns: what the person chose, and what the window leaves room for.
  const root = useRef<HTMLDivElement>(null);
  const width = useFrameWidth(root);
  const [dockShare, setDockShare] = useState(rememberedShare);
  const ownLists = useListPanes();
  const listed = lists ?? ownLists;
  // What the person closed: the application's where it holds them, so the strip's toggles and
  // the View menu reach them from every view; this frame's own where it is rendered alone.
  const own = useFrameClosings();
  const closed = closings ?? own;
  // Where each view stands: the application's where it holds them, this frame's own alone.
  const ownPlaces = usePlacements();
  const placed = placements ?? ownPlaces;
  const collapsed = closed.panel;
  const dockClosed = closed.dock;
  const [dockFull, setDockFull] = useState(false);
  // Another view in the frame: its own list or none, and its main area. Absent, it is Sessions.
  const listView = viewLayout !== undefined ? viewLayout.list?.view ?? null : 'sessions';
  // A go's group lays the view's strip over the main area while it is brought into view.
  const { listOver, setListOver } = closed;
  const go = useGoOver(group, listOver, setListOver, onGroupBrought);
  const layout = frameLayout(width.viewport, width.frame, {
    list: listView ? listChoice(listView, listed, go.over) : null,
    dockShare, dockClosed, dockFull,
  });
  const list = layout.list;

  // What the list is now, told to the application, whose doors toggle it and say whether it is shown; and
  // a list laid over the main area let go once the room makes it anything else.
  useListMode(list?.mode ?? null, onListMode, listOver, setListOver, go.bringing);

  /** A choice in a list laid over closes it, and lets a go still on its way go. */
  const chosen = () => { if (list?.mode === 'over') go.setOver(false); };
  /** A drag lands as the dock's share of the window it is dragged in; null is the default again. */
  const resizeDock = (next: number | null) => {
    const share = next === null || width.viewport <= 0 ? null : next / width.viewport;
    setDockShare(share);
    store(DOCK_SHARE, share === null ? null : String(share));
  };
  const closeDock = (shut: boolean) => {
    closed.setDock(shut);
    if (shut) setDockFull(false);
  };

  const resize = (next: number) => {
    setHeight(next);
    store(PANEL_HEIGHT, String(next));
  };

  const toggle = () => closed.setPanel(!collapsed);

  return {
    root, width, listed, closed, placed, collapsed, dockClosed, dockFull, setDockFull, go, layout, list, chosen, resizeDock, closeDock,
    height, resize, toggle,
  };
}
