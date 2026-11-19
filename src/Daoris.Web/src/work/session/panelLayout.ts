/**
 * The session panel's own thresholds, by its own width (the frame contract, design §3.2): its facts rail stands beside the story
 * from 68rem, where the story keeps 72 characters beside an 18rem rail; below 40rem the head folds, carrying the changes as one
 * fact, and the dock draws one card whole.
 */
export const RAIL_FROM_REM = 68;
export const NARROW_BELOW_REM = 40;

export type PanelLayout = { rail: boolean; narrow: boolean };

/** The panel's layout at `width` px with a root em of `rem` px; unmeasured, the middle layout: no rail and no fold. */
export function panelLayoutOf(width: number | undefined, rem: number): PanelLayout {
  if (width === undefined) return { rail: false, narrow: false };
  return { rail: width >= RAIL_FROM_REM * rem, narrow: width < NARROW_BELOW_REM * rem };
}
