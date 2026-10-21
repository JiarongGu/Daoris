// The usage curve on the page (SWITCH1d; D165 point 2; the switch design §2 and §5): per window, the most of it an account should
// have used by each point of the window's time, which the Accounts screen draws each window against and the said line marks a window
// over.
//
// Judged in .NET alone since PUSH1e (D182 point 6): the driver's `UsageCurves` measures, and the accounts' answer carries what the
// page shows, each reading's window its start, its curve now in whole points and whether it is over it, and each scope's curve in
// effect, its list's accounts measured by it and those over it (`AccountsRoutes`). The page words those and draws them; it holds no
// window's length, no smart curve and no curve's rule of its own. The CLI's `usagecurves.ts`, the terminal's twin, left with
// `daoris agent list` (PUSH1f2e4); the shared table, the driver's `fixtures/usage-curves.json`, holds the driver, and the
// page's words of the verdicts (`usageCurves.test.ts`).

/** A point of a curve, `[elapsed, used]`, each a whole percent: as `harnesses.json` keeps it. */
export type CurvePoint = [elapsed: number, used: number];

/** When to switch (D165 point 2): Daoris's curve, the person's own, or no switch while a session runs. */
export type Switch = 'smart' | 'curve' | 'off';

/** The windows a curve names, as `windows.json` names them, in the order a person reads and the files write them. */
export const CURVE_WINDOWS = ['session', 'weekly'] as const;

/**
 * A scope's switch and curve, as the accounts' answer gives them: how a list is used, the person's own points among it. A shell
 * older than SWITCH1d answers neither, read as the smart switch with no curve of the person's own.
 */
export type CurveScope = { switch?: Switch; curve?: Record<string, CurvePoint[]> };

/** The person's own points for a window, whatever the switch says; none where they set none. What the point editor edits. */
export function ownPoints(use: CurveScope, window: string): CurvePoint[] | null {
  const curve = use.curve ?? {};
  // Own keys only: a window named like an object's own property is no window of the person's curve.
  const own = Object.hasOwn(curve, window) ? curve[window] : undefined;
  return Array.isArray(own) && own.length > 0 ? own : null;
}

/**
 * A window's curve has one point to this many (the switch design §6): the point editor offers no row past it. The driver's
 * `UseCurve.MostPoints`, whose door refuses a curve past it (`ACCOUNT_USE_CURVE`, its `count` rule); a bound on a control, held
 * here so the editor never offers a row the door would refuse.
 */
export const MOST_POINTS = 12;

/**
 * Where an account stands on a window now, as its chart places it: how much of the window's time has gone, 0 to 100, from when it
 * began and when it resets, both as .NET answered them (`from`, `reset`). Geometry, not a rule: null where no start was answered.
 */
export function goneOf(from: string | null | undefined, reset: string, now: Date): number | null {
  if (!from) return null;
  const start = Date.parse(from);
  const end = Date.parse(reset);
  if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start) return null;
  return Math.min(Math.max((now.getTime() - start) / (end - start), 0), 1) * 100;
}
