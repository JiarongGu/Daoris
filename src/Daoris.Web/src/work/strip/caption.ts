// The DOM contract between the strip that RESERVES the caption room and the hook that reports it
// (SURF7). It lives on its own because the two sides sit in different layers: `frame.tsx` is
// presentational and must import no hook, and `windowChrome.ts` holds the bridge. A shared constant
// in either one would drag the other's dependencies across that line — the strip would reach the
// bridge, or the hook would pull in every other region in the frame.

/** The three, in the order Windows puts them. The page reserves one slot per kind. */
export const CAPTION_SLOTS = ['minimize', 'maximize', 'close'] as const;

export type CaptionSlot = (typeof CAPTION_SLOTS)[number];

/** The attribute a reserved slot carries, so the reader finds them without a second registry. */
export const CAPTION_ATTRIBUTE = 'data-caption';
