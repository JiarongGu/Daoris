import type { Session } from '../src/data/api';
import { DOCK_BODY } from '../src/work/frame/RightDock';
import { SessionTimeline } from '../src/work/head/SessionTimeline';

// A session's Timeline in the side bar, drawn at the width the dock gives it: its states chart's edges
// stay inside the pane. The page's own gutters wrap the view, as the dock's pane has them.

const SESSION: Session = {
  id: 's1a2b3c4', repository: 'engine', door: 'claude-code', state: 'completed', kind: 'chat',
  created: '2026-10-20T09:00:00Z', updated: '2026-10-20T11:00:00Z',
};

export const SIDE_BAR_WIDTHS = [372] as const;

/** Tall enough that the list fits it and the drawing does not: the bar comes and goes with the placement. */
const DOCK_HEIGHT = 900;

/** The view at one width of the side bar's content, inside the dock's padding. */
export const sideBarTimeline = Object.fromEntries(SIDE_BAR_WIDTHS.map((width) => [`At${width}`, () => (
  <div data-frame={width} style={{ width, height: DOCK_HEIGHT }} className={`${DOCK_BODY} box-border px-3`}>
    <SessionTimeline session={SESSION} titled={false} />
  </div>
)]));
