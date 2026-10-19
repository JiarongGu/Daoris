// The usage curve (D165 point 2; the switch design §2, §3 and §5; SWITCH1b-iii): per window, the most of it an account
// should have used by each point of the window's time, and whether what its agent said is over that, so `profile use`'s
// said line marks an account over its curve as the start's line does (`opening.said-over`).
//
// It is the CLI's twin of the driver's `UsageCurves` (`UsageCurves.cs`): both read one table, `test/fixtures/usage-curves.json`,
// row for row (`usagecurves.test.ts`, `UsageCurvesTwinTests.cs`). The rules both keep:
//
//   1. A window's length is five hours for `session` and seven days for `weekly`, by those names exactly; any other window
//      has no curve here, so its number passes nothing.
//   2. The share gone is `1 - (reset - now) / length`, clamped to 0..1, as a percent.
//   3. A curve's value joins its points with straight lines, flat before the first and after the last.
//   4. The smart curve: `session` flat at `SMART_SESSION` (90); `weekly` (0, 20), (75, 95), (100, 100), its pace plus 20 up
//      to 95, then rising evenly to the whole week at its reset, never in a jump (USAGERESET1). Under `curve` a window the
//      person set takes their points and the rest the smart curve's; under `smart` and `off` the smart curve, since a start
//      under `off` still passes an account over it (the switch design §3).
//   5. Room is the curve now less the used percent; the tightest window is the one said with the least room among those with
//      a number, the first said on a tie; an account is over its curve where that room is below zero, so one exactly on its
//      curve is not.
//
// Pure: the moment comes in as a value, and nothing is read. The walk that ranks by it is the driver's alone.

import { SMART_SESSION } from './rotation.ts';
import type { CurvePoint, RotationUse } from './rotation.ts';
import type { WindowSaid } from './windows.ts';

/** A scope's switch and curve: all the curve reads of how a list is used. */
export type CurveScope = Pick<RotationUse, 'switch' | 'curve'>;

// The smart week's lead over its pace, the knee its tail rises from and the end it reaches at its reset, in percent.
// 🔴 Duplicated deliberately: the driver's copy is `UsageCurves.WeeklyLead`, `WeeklyKnee` and `WeeklyEnd`
// (`UsageCurves.cs`); `usagecurves.test.ts` holds the two to the same numbers.
const WEEKLY_LEAD = 20;
const WEEKLY_KNEE = 95;
const WEEKLY_END = 100;

const HOUR = 3_600_000;

/** Rule 1: a window's length in milliseconds, or null for a window this build does not know (the driver's `Length`). */
export function windowLength(window: string): number | null {
  return window === 'session' ? 5 * HOUR : window === 'weekly' ? 7 * 24 * HOUR : null;
}

/** Rule 2: how much of the window has passed at `now`, from its reset: 0 to 100, or null for an unknown window. */
export function elapsed(window: string, reset: Date, now: Date): number | null {
  const length = windowLength(window);
  return length === null ? null : Math.min(Math.max(1 - (reset.getTime() - now.getTime()) / length, 0), 1) * 100;
}

/** Rule 3: a curve's value at a share gone, its points joined by straight lines and flat beyond them (the driver's `Value`). */
export function curveValue(points: readonly CurvePoint[], gone: number): number {
  if (gone <= points[0]![0]) return points[0]![1];
  for (let i = 1; i < points.length; i++) {
    const [from, to] = [points[i - 1]!, points[i]!];
    if (gone > to[0]) continue;
    return to[0] === from[0] ? to[1] : from[1] + (to[1] - from[1]) * (gone - from[0]) / (to[0] - from[0]);
  }
  return points[points.length - 1]![1];
}

/** Rule 4: Daoris's own curve for a window, or null for a window this build does not know (the driver's `Smart`). */
export function smartCurve(window: string): CurvePoint[] | null {
  if (window === 'session') return [[0, SMART_SESSION]];
  if (window === 'weekly') return [[0, WEEKLY_LEAD], [WEEKLY_KNEE - WEEKLY_LEAD, WEEKLY_KNEE], [100, WEEKLY_END]];
  return null;
}

/** Rule 4: a scope's curve for a window: the person's points under `curve` where they set the window, else the smart curve. */
export function curveFor(use: CurveScope, window: string): CurvePoint[] | null {
  // Own keys only: a window named like an object's own property is no window of the person's curve.
  const own = Object.hasOwn(use.curve, window) ? (use.curve as Record<string, CurvePoint[] | undefined>)[window] : undefined;
  return use.switch === 'curve' && own !== undefined && own.length > 0 ? own : smartCurve(window);
}

/** The scope's curve on a window now, in percent; null for a window this build does not know (the driver's `Value`). */
export function curveNow(use: CurveScope, window: string, reset: Date, now: Date): number | null {
  const points = curveFor(use, window);
  const gone = elapsed(window, reset, now);
  return points === null || gone === null ? null : curveValue(points, gone);
}

/**
 * Rule 5: the window an account's agent said with the least room under its curve now, the curve's value there and that room;
 * null where no window it said is measured (the driver's `TightestSaid`). Over its curve is that room below zero.
 */
export function tightestSaid(
  use: CurveScope, windows: readonly WindowSaid[] | null, now: Date,
): { window: WindowSaid; curve: number; room: number } | null {
  let least: { window: WindowSaid; curve: number; room: number } | null = null;
  for (const window of windows ?? []) {
    const curve = window.used === null ? null : curveNow(use, window.window, window.reset, now);
    if (window.used === null || curve === null) continue;
    const room = curve - window.used * 100;
    if (least === null || room < least.room) least = { window, curve, room };
  }
  return least;
}

/** A number of points as a whole number, a half away from zero, as the driver's `AccountRotation.Points` says one. */
export function wholePoints(points: number): number {
  return Math.sign(points) * Math.round(Math.abs(points));
}
