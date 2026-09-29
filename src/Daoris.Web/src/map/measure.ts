/** A rectangle in the map's units: what a drawing's `viewBox` frames. */
export type Frame = { x: number; y: number; width: number; height: number };

/** Characters drawn a whole em wide: CJK ideographs and radicals, their compatibility forms, full-width forms, CJK punctuation. */
const WIDE = /[⺀-鿿豈-﫿＀-￯　-〿]/;

/**
 * How wide a line of text is drawn, estimated before it is drawn: a Chinese character is a whole em,
 * anything else about two thirds of one, which is a semibold Latin name's width with room to spare
 * (*game* measured 7.62 units a letter at 12px on the window). The frame holds what this says.
 */
export function textWidth(text: string, px: number): number {
  let width = 0;
  for (const char of text) width += WIDE.test(char) ? px : px * 0.64;
  return width;
}
