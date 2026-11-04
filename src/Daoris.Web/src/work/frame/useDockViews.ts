import { type ReactNode, useCallback, useEffect, useRef, useState } from 'react';
import { logEvent } from '../../bridge/shell';
import type { Session } from '../../data/api';
import type { FileOpen } from '../review/preview';
import type { FrameClosings } from './closings';
import { type Place, type Placements, type ViewId, viewsIn } from './placements';
import { dockOpensOn, type DockTab, followed } from './RightDock';

/**
 * Which view each region shows (DOCK1b, FRAME6): the side bar's tab per followed session, the panel's view, a file's preview,
 * a view moved or dragged, and the doors that ask for Ask Daoris or *Notifications* where it stands.
 */
export function useDockViews({
  attended, elsewhere, here, ask, notifications, terminal, placed, closed, collapsed, dockClosed, closeDock, askFocus,
  notificationsShow, notificationsToggle,
}: {
  attended: Session | null;
  elsewhere: boolean;
  here: boolean;
  ask?: ReactNode;
  notifications?: ReactNode;
  terminal: boolean;
  placed: Placements;
  closed: FrameClosings;
  collapsed: boolean;
  dockClosed: boolean;
  closeDock: (shut: boolean) => void;
  askFocus: number;
  notificationsShow: number;
  notificationsToggle: number;
}) {
  // Which dock surface each session has up (FRAME6: tabs per session). Not remembered across launches:
  // unlike the attended session, this one is answered by what the person is doing in the next ten seconds.
  const [docked, setDocked] = useState<Record<string, DockTab>>({});
  // The session a session's views follow (UX6b, D150 §8): on Sessions the attended one; off Sessions only while it runs or
  // waits on the person, so an ended one's note no longer runs down every other view's side bar.
  const following = followed(attended, elsewhere);
  // The followed session's dock surface: the one it had, or the tab it opens on — the timeline (FRAME6), or Ask Daoris
  // off Sessions with none followed.
  const dockKey = following?.id ?? '';
  // What stands where (DOCK1b): Ask Daoris only where a shell handed it in, and the terminal likewise.
  // *Notifications* likewise (NOTIFY2b): where the application handed it in.
  const present = (view: ViewId) => (view === 'ask' ? Boolean(ask) : view === 'terminal' ? terminal
    : view === 'notifications' ? Boolean(notifications) : true);
  const rightViews = viewsIn(placed.places, 'right', present);
  const panelViews = viewsIn(placed.places, 'panel', present);
  // Ask Daoris and *Notifications* are the machine's, not the attended session's: their tab stays whichever session is attended.
  const [machineTab, setMachineTab] = useState<'ask' | 'notifications' | null>(null);
  // A file's preview per session (PREVIEW1, D111): what a tool card or the review opened, and the tab it
  // covered, which its × goes back to. Kept while the window is open, like the dock's tab.
  const [previews, setPreviews] = useState<Record<string, FileOpen & { back: DockTab }>>({});
  const previewing = following && here ? previews[following.id] ?? null : null;
  const sessionTab = docked[dockKey] ?? dockOpensOn(elsewhere, following !== null);
  const dock: DockTab | undefined = machineTab && rightViews.includes(machineTab)
    ? machineTab
    : sessionTab === 'preview'
      ? (previewing ? 'preview' : rightViews[0])
      : rightViews.includes(sessionTab) ? sessionTab : rightViews[0];
  const setDock = (tab: DockTab) => {
    const machine = tab === 'ask' || tab === 'notifications' ? tab : null;
    setMachineTab(machine);
    if (!machine) setDocked((was) => ({ ...was, [dockKey]: tab }));
  };
  // The panel's shown view: the machine's, like its height, not the attended session's.
  const [panelPick, setPanelPick] = useState<ViewId>('console');
  // The view being dragged (DOCK1e), so a region with nothing in it is drawn to be dropped on.
  const [dragging, setDragging] = useState<ViewId | null>(null);
  // 🔴 Told a beat after the drag starts, never inside it: Chromium drops a drag whose page changes
  // under the pointer in the same task as `dragstart`, and drawing an emptied region is such a change.
  const onDrag = (view: ViewId | null) => { window.setTimeout(() => setDragging(view), 0); };
  const panelView = panelViews.includes(panelPick) ? panelPick : panelViews[0];
  // The person opening the dock on a surface — from its strip.
  const openDock = (tab: DockTab) => {
    setDock(tab);
    closeDock(false);
  };
  // A door opening a file's preview (PREVIEW1): the side bar opens on it, over the tab it covers. Handed to
  // the conversation's cards through a context, so one stable function is handed and the latest state read.
  const previewNow = useRef<(file: FileOpen) => void>(() => {});
  previewNow.current = (file: FileOpen) => {
    if (!following) return;
    const covered = dock && dock !== 'preview' ? dock : previews[following.id]?.back ?? 'timeline';
    setPreviews((was) => ({ ...was, [following.id]: { path: file.path, lines: file.lines ?? null, back: covered } }));
    openDock('preview');
  };
  const openPreview = useCallback((file: FileOpen) => previewNow.current(file), []);
  const closePreview = () => {
    if (!following) return;
    const back = previews[following.id]?.back ?? 'timeline';
    setPreviews(({ [following.id]: _closed, ...rest }) => rest);
    if (dock === 'preview') setDock(back);
    // A side bar the preview leaves with nothing in it closes, as one a moved view leaves does (DOCK1b).
    if (rightViews.length === 0) closeDock(true);
  };
  // The person asking for a view by a door that does not know where it stands — the palette's review,
  // Ask Daoris's `F1` — opens whichever region holds it, on it.
  const openView = (view: ViewId) => {
    if (placed.places[view] === 'panel') {
      setPanelPick(view);
      closed.setPanel(false);
    } else {
      openDock(view);
    }
  };
  // A view moved (DOCK1b): shown where it went, as VS Code opens the container a view lands in, and a
  // region it leaves with nothing closes, as VS Code hides an empty one.
  const moveView = (view: ViewId, to: Place) => {
    const from = placed.places[view];
    // A drop ends the drag here: the tab it started from is gone by the time `dragend` fires on it.
    setDragging(null);
    if (from === to) return;
    placed.move(view, to);
    logEvent('panel.moved', { view, region: to });
    // Not `openView`: this render's places still say where the view was.
    if (to === 'panel') {
      setPanelPick(view);
      closed.setPanel(false);
    } else {
      openDock(view);
    }
    if (viewsIn({ ...placed.places, [view]: to }, from, present).length === 0) {
      if (from === 'panel') closed.setPanel(true);
      else closeDock(true);
    }
  };

  // The person asked for Ask Daoris — the right side bar's toggle, `F1`, the palette: its region opens on it.
  useEffect(() => {
    if (askFocus <= 0) return;
    openView('ask');
    // Only a new ask for it; where it stands and the closings are read, not watched.
  }, [askFocus]);

  // NOTIFY2b (the notification design §2.1): *Notifications* shown where it stands, by a door that asks for it; and what its region
  // showed before the bell opened it, so the bell's next press gives the region back: the view it held, or closed where it was.
  const notificationsBack = useRef<{ place: Place; view: ViewId | DockTab | undefined; closed: boolean } | null>(null);
  const notificationsShown = placed.places.notifications === 'panel'
    ? !collapsed && panelView === 'notifications'
    : !dockClosed && dock === 'notifications';
  useEffect(() => {
    if (notificationsShow <= 0) return;
    if (!notificationsShown) {
      const place = placed.places.notifications;
      notificationsBack.current = { place, view: place === 'panel' ? panelView : dock, closed: place === 'panel' ? collapsed : dockClosed };
    }
    openView('notifications');
    // Only a new ask for it, as Ask Daoris's.
  }, [notificationsShow]);
  useEffect(() => {
    if (notificationsToggle <= 0) return;
    const place = placed.places.notifications;
    if (!notificationsShown) {
      notificationsBack.current = { place, view: place === 'panel' ? panelView : dock, closed: place === 'panel' ? collapsed : dockClosed };
      openView('notifications');
      return;
    }
    const back = notificationsBack.current?.place === place ? notificationsBack.current : null;
    notificationsBack.current = null;
    const others: (ViewId | DockTab)[] = (place === 'panel' ? panelViews : rightViews).filter((view) => view !== 'notifications');
    if (back?.closed) {
      // Opened from a closed region: the region closes, as it was.
      if (place === 'panel') closed.setPanel(true);
      else closeDock(true);
      return;
    }
    // The view the region held, where it still stands there (or the file's preview it covered); else the region's first other view.
    const held = back?.view && back.view !== 'notifications' && (others.includes(back.view) || back.view === 'preview') ? back.view : others[0];
    if (!held) {
      if (place === 'panel') closed.setPanel(true);
      else closeDock(true);
    } else if (place === 'panel') {
      setPanelPick(held as ViewId);
    } else {
      setDock(held);
    }
    // Only a new press of the bell; where it stands and what it showed are read, not watched.
  }, [notificationsToggle]);

  return {
    setDocked, following, rightViews, panelViews, previewing, dock, setDock, setPanelPick, dragging, onDrag, panelView, openDock,
    openPreview, closePreview, openView, moveView,
  };
}
