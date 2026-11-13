import type { FrameIntent } from '../../menus/commands';
import type { SessionGrouping } from '../groups';

/** The run whose review waits for the person's Accept in a session's placed row; null where the row names none. */
export const acceptRunOf = (placed: Pick<SessionGrouping, 'waits'> | null | undefined): string | null =>
  placed?.waits?.find((wait) => wait.why === 'accept')?.run ?? null;

/** What the frame opens for a review row: the run's outputs where the row carries it, else the session's own. */
export const outputsIntent = (item: { run?: string | null }): FrameIntent => (item.run ? `outputs/${item.run}` : 'review');
