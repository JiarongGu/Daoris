import { type WorkflowSaveAnswer, type WorkflowUseAnswer } from './answers';
import { type WayRowKey } from './graphDraft';

/** How long a change holds still before it is judged: each judge is a planned save, and typing a name is many changes. */
export const JUDGE_MS = 250;

/** The id a draft is judged under before it has one: a plan writes nothing, so it only has to be an id. */
export const DRAFT_ID = 'draft';

/** The editor's region holds the chart and the inspector side by side from 64rem (the flowchart design §2, §8.8); else a drawer. */
/** The editor's width, in rem, from which the inspector stands beside the chart: read in the root's rem, so the window's zoom leaves the chart its room (CANVAS1b). */
export const BESIDE_REM = 64;

/** The id the drawing gives the folded holds, by their first step (`graphFlow`). */
export const HOLDS = 'holds:';

/** The focus asked for the first step that can take the way being chosen. */
export const PICKABLE = ':pickable';

/** The focus asked back for the control the picker was opened from. */
export const RETURN = ':return';

/** The settling press of a proposal: on its way, answered, or refused in the driver's words. */
export type Settling = { state: 'pending' | 'told' } | { state: 'failed'; why: string };

/** A way being chosen on the chart: the step and the row it leaves by. */
export type Choosing = { from: string; row: WayRowKey };

export type Said = { saved: WorkflowSaveAnswer | null; used: WorkflowUseAnswer | null; failed: string | null };
export const QUIET: Said = { saved: null, used: null, failed: null };
