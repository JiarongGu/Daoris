import type { UxTrailPress } from './generated/routes';
import { call } from './call';

const answered = (answer: unknown): boolean => (answer as { on?: boolean } | null)?.on === true;

/** Whether the person's UX trail is on, as the driver keeps it. */
export const readUxTrail = async (): Promise<boolean> => answered(await call('UX_TRAIL', {}));

/** Turns the trail on or off; answers what the driver now holds. */
export const setUxTrail = async (on: boolean): Promise<boolean> => answered(await call('UX_TRAIL', { on }));

/** Sends the trail's entries the window recorded; the driver keeps them on this machine only. */
export const sendUxTrail = (entries: NonNullable<UxTrailPress['entries']>): Promise<unknown> => call('UX_TRAIL', { entries });
