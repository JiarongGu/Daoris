/** How many earlier parts a line draws above its newest one, and how many one press draws more. */
export const EARLIER_AT_MOST = 3;

/**
 * The earlier parts drawn, the nearest to the newest one last: the three nearest and, for each press, up to three more, and how
 * many the next press would draw (`more`, which its words say). The parts are the line's by its membership (`lineMembersBefore`).
 */
export function drawnOf(line: readonly string[], pressed: number): { drawn: string[]; more: number } {
  const drawn = line.slice(Math.max(0, line.length - EARLIER_AT_MOST * (pressed + 1)));
  return { drawn, more: Math.min(EARLIER_AT_MOST, line.length - drawn.length) };
}
