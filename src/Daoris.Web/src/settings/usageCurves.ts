// The usage curve on the page (SWITCH1d; D165 point 2; the switch design §2 and §5): per window, the most of it an account
// should have used by each point of the window's time, so the Accounts screen draws each window against it and the said line
// marks a window over it, as the terminal and the start's line do.
//
// It is the page's twin of the driver's `UsageCurves` (`UsageCurves.cs`) and the CLI's `usagecurves.ts`: all three read one
// table, the CLI's `test/fixtures/usage-curves.json`, row for row (`usageCurves.test.ts`, `usagecurves.test.ts`,
// `UsageCurvesTwinTests.cs`). The rules all keep:
//
//   1. A window's length is five hours for `session` and seven days for `weekly`, by those names exactly; any other window
//      has no curve here, so its number passes nothing.
//   2. The share gone is `1 - (reset - now) / length`, clamped to 0..1, as a percent.
//   3. A curve's value joins its points with straight lines, flat before the first and after the last.
//   4. The smart curve: `session` flat at 90; `weekly` (0, 20), (75, 95), (100, 100), its pace plus 20 up to 95, then rising
//      evenly to the whole week at its reset, never in a jump (USAGERESET1). Under `curve` a window the person set takes
//      their points and the rest the smart curve's; under `smart` and `off` the smart curve, since a start under `off` still
//      passes an account over it (the switch design §3).
//   5. Room is the curve now less the used percent; the tightest window is the one said with the least room among those with a
//      number, the first said on a tie; an account is over its curve where that room is below zero.
//
// Pure: the moment comes in as a value, and nothing is asked. The walk that ranks by it is the driver's alone. A curve's rules
// (`curveProblem`) are the same three's, held by the CLI's `rotation-use.json` `refusal` rows, so the point editor says what is
// wrong in the sentence the door would refuse it in.

import { sentence } from '../locales/format';

/** A point of a curve, `[elapsed, used]`, each a whole percent: as `harnesses.json` keeps it. */
export type CurvePoint = [elapsed: number, used: number];

/** When to switch (D165 point 2): Daoris's curve, the person's own, or no switch while a session runs. */
export type Switch = 'smart' | 'curve' | 'off';

/** The windows a curve names, as `windows.json` names them, in the order a person reads and the files write them. */
export const CURVE_WINDOWS = ['session', 'weekly'] as const;

/**
 * A scope's switch and curve, as the accounts' answer gives them: all the curve reads of how a list is used. A shell older than
 * SWITCH1d answers neither, read as the smart switch with no curve of the person's own.
 */
export type CurveScope = { switch?: Switch; curve?: Record<string, CurvePoint[]> };

// The smart curve's five-hour window, and the smart week's lead over its pace, the knee its tail rises from and the end it
// reaches at its reset, in percent. 🔴 Duplicated deliberately: the driver's copies are `RotationUse.SmartSession`,
// `UsageCurves.WeeklyLead`, `WeeklyKnee` and `WeeklyEnd`, the CLI's `SMART_SESSION`, `WEEKLY_LEAD`, `WEEKLY_KNEE` and
// `WEEKLY_END`; the shared table holds all three to the same numbers.
export const SMART_SESSION = 90;
const WEEKLY_LEAD = 20;
const WEEKLY_KNEE = 95;
const WEEKLY_END = 100;

const HOUR = 3_600_000;

/** Rule 1: a window's length in milliseconds, or null for a window this build does not know. */
export function windowLength(window: string): number | null {
  return window === 'session' ? 5 * HOUR : window === 'weekly' ? 7 * 24 * HOUR : null;
}

/** Rule 2: how much of the window has passed at `now`, from its reset: 0 to 100, or null for an unknown window. */
export function elapsed(window: string, reset: string, now: Date): number | null {
  const length = windowLength(window);
  return length === null ? null : Math.min(Math.max(1 - (new Date(reset).getTime() - now.getTime()) / length, 0), 1) * 100;
}

/** Rule 3: a curve's value at a share gone, its points joined by straight lines and flat beyond them. */
export function curveValue(points: readonly CurvePoint[], gone: number): number {
  if (gone <= points[0]![0]) return points[0]![1];
  for (let i = 1; i < points.length; i++) {
    const [from, to] = [points[i - 1]!, points[i]!];
    if (gone > to[0]) continue;
    return to[0] === from[0] ? to[1] : from[1] + (to[1] - from[1]) * (gone - from[0]) / (to[0] - from[0]);
  }
  return points[points.length - 1]![1];
}

/** Rule 4: Daoris's own curve for a window, or null for a window this build does not know. */
export function smartCurve(window: string): CurvePoint[] | null {
  if (window === 'session') return [[0, SMART_SESSION]];
  if (window === 'weekly') return [[0, WEEKLY_LEAD], [WEEKLY_KNEE - WEEKLY_LEAD, WEEKLY_KNEE], [100, WEEKLY_END]];
  return null;
}

/** The person's own points for a window, whatever the switch says; none where they set none. */
export function ownPoints(use: CurveScope, window: string): CurvePoint[] | null {
  const curve = use.curve ?? {};
  // Own keys only: a window named like an object's own property is no window of the person's curve.
  const own = Object.hasOwn(curve, window) ? curve[window] : undefined;
  return Array.isArray(own) && own.length > 0 ? own : null;
}

/** Rule 4: a scope's curve for a window: the person's points under `curve` where they set the window, else the smart curve. */
export function curveFor(use: CurveScope, window: string): CurvePoint[] | null {
  const own = use.switch === 'curve' ? ownPoints(use, window) : null;
  return own ?? smartCurve(window);
}

/** The scope's curve on a window now, in percent; null for a window this build does not know. */
export function curveNow(use: CurveScope, window: string, reset: string, now: Date): number | null {
  const points = curveFor(use, window);
  const gone = elapsed(window, reset, now);
  return points === null || gone === null ? null : curveValue(points, gone);
}

/** A window as its agent said it, as much as the curve reads: its name, its share used from 0 to 1, and its reset. */
export type CurveSaid = { window: string; used?: number | null; reset: string };

/**
 * Rule 5: the window an account's agent said with the least room under its curve now, the curve's value there and that room;
 * null where no window it said is measured. Over its curve is that room below zero.
 */
export function tightestSaid<W extends CurveSaid>(
  use: CurveScope, windows: readonly W[] | null | undefined, now: Date,
): { window: W; curve: number; room: number } | null {
  let least: { window: W; curve: number; room: number } | null = null;
  for (const window of windows ?? []) {
    if (typeof window.used !== 'number') continue;
    const curve = curveNow(use, window.window, window.reset, now);
    if (curve === null) continue;
    const room = curve - window.used * 100;
    if (least === null || room < least.room) least = { window, curve, room };
  }
  return least;
}

/** A number of points as a whole number, a half away from zero, as the driver and the terminal say one. */
export function wholePoints(points: number): number {
  return Math.sign(points) * Math.round(Math.abs(points));
}

// ---------------------------------------------------------------- a curve's rules (the switch design §2, §6)

/** A window's curve has one point to this many (the switch design §6): the driver's `UseCurve.MostPoints`. */
export const MOST_POINTS = 12;

/**
 * What is wrong with a curve, as the modules' `ACCOUNT_USE_CURVE` refusal names it (SWITCH1d): the rule (the catalogue's form)
 * and the window, point and numbers it names. The driver's `CurveProblem`, the CLI's `curveProblem`; the CLI's
 * `rotation-use.json` `refusal` rows hold the three.
 */
export type CurveProblem = {
  kind: 'shape' | 'window' | 'list' | 'count' | 'point' | 'elapsed' | 'used' | 'rising' | 'falling';
  window?: string;
  point?: number;
  value?: number;
  before?: number;
  count?: number;
};

const isWhole = (value: unknown): value is number => typeof value === 'number' && Number.isInteger(value);

/**
 * The first problem with a curve, or null. Window by window in the order written, then point by point: its shape, elapsed from
 * 0 to 100, used from 0 to 100, elapsed rising, used never falling — the twins' order, so the editor says before saving what
 * the door would refuse.
 */
export function curveProblem(curve: unknown): CurveProblem | null {
  if (typeof curve !== 'object' || curve === null || Array.isArray(curve)) return { kind: 'shape' };
  for (const [window, points] of Object.entries(curve)) {
    if (!(CURVE_WINDOWS as readonly string[]).includes(window)) return { kind: 'window', window };
    if (!Array.isArray(points)) return { kind: 'list', window };
    if (points.length < 1 || points.length > MOST_POINTS) return { kind: 'count', window, count: points.length };
    let before: CurvePoint | null = null;
    for (const [index, point] of (points as unknown[]).entries()) {
      const at = index + 1;
      if (!Array.isArray(point) || point.length !== 2 || !point.every(isWhole)) return { kind: 'point', window, point: at };
      const [elapsed, used] = point as CurvePoint;
      if (elapsed < 0 || elapsed > 100) return { kind: 'elapsed', window, point: at, value: elapsed };
      if (used < 0 || used > 100) return { kind: 'used', window, point: at, value: used };
      if (before !== null && elapsed <= before[0]) return { kind: 'rising', window, point: at, value: elapsed, before: before[0] };
      if (before !== null && used < before[1]) return { kind: 'falling', window, point: at, value: used, before: before[1] };
      before = [elapsed, used];
    }
  }
  return null;
}

/** A curve's problem as the reader reads it: the modules' refusal's sentence, by its rule, in the reader's language. */
export function curveSentence(problem: CurveProblem): string {
  // The refusal's values as the modules send them: a point with the one before it, and the count as `size`, since i18next reads
  // a `count` as a plural's.
  const { kind, point, count, ...named } = problem;
  const values = Object.fromEntries(Object.entries({
    ...named, ...(point !== undefined ? { point, previous: point - 1 } : {}), ...(count !== undefined ? { size: count } : {}),
  }).map(([key, value]) => [key, String(value)]));
  return sentence({ code: 'ACCOUNT_USE_CURVE', parameters: { ...values, context: kind } });
}
