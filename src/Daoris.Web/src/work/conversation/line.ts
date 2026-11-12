/** How many earlier parts a line draws above its newest one, and how many one press draws more. */
export const EARLIER_AT_MOST = 3;

/** The earlier parts drawn, the nearest last: three, and up to three more a press, with how many the next press would draw. */
export function drawnOf(line: readonly string[], pressed: number): { drawn: string[]; more: number } {
  const drawn = line.slice(Math.max(0, line.length - EARLIER_AT_MOST * (pressed + 1)));
  return { drawn, more: Math.min(EARLIER_AT_MOST, line.length - drawn.length) };
}
