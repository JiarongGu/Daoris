import type { Box } from './viewMath';

// Where a mark's chip stands (CANVAS1b; D224, the canvas design §2.3): beside its node, where it clears every box, and dropped where it
// clears none. A mark never adds a line or a border's width to a node, so a judgement landing after an edit moves nothing below it.

/** A chip to place: its node's box and its own size, in world units. */
export type ChipAsk = { id: string; node: Box; w: number; h: number };
/** Where it stands (its top left), and whether it is shown at all. */
export type ChipPlaced = { id: string; x: number; y: number; shown: boolean };

/** The air a chip keeps from the box it stands beside, and from what it must clear. */
const GAP = 2;

const clear = (box: Box, others: readonly Box[]) =>
  others.every((other) => box.x + box.w + GAP <= other.x || other.x + other.w + GAP <= box.x || box.y + box.h + GAP <= other.y || other.y + other.h + GAP <= box.y);

/**
 * Each chip above its node's right end, else its left end, else below the right or left end, else straddling the node's top edge at
 * either end like a tag (clear of every box but its own node): the first place that clears every
 * obstacle (nodes, labels, ＋s, dividers), every chip placed before it and every point of ink. A chip that clears none is not shown; its words are in the
 * node's description and the feature's list.
 */
export function placeChips(asks: readonly ChipAsk[], obstacles: readonly Box[], ink: readonly { x: number; y: number }[] = []): ChipPlaced[] {
  const standing: Box[] = [];
  return asks.map(({ id, node, w, h }) => {
    const above = node.y - h - GAP;
    const below = node.y + node.h + GAP;
    const right = node.x + node.w - w;
    // Last, straddling its own node's top edge like a tag: 40% of the chip over the node's border, clear of everything else.
    const tag = node.y - h * 0.6;
    const candidates: Box[] = [
      { x: right, y: above, w, h }, { x: node.x, y: above, w, h }, { x: right, y: below, w, h }, { x: node.x, y: below, w, h },
      { x: right - 4, y: tag, w, h }, { x: node.x + 4, y: tag, w, h },
    ];
    const others = obstacles.filter((other) => other.x !== node.x || other.y !== node.y || other.w !== node.w || other.h !== node.h);
    const found = candidates.find((box, at) => clear(box, [...(at < 4 ? obstacles : others), ...standing])
      && ink.every((point) => point.x < box.x || point.x > box.x + box.w || point.y < box.y || point.y > box.y + box.h));
    if (!found) return { id, x: node.x, y: above, shown: false };
    standing.push(found);
    return { id, x: found.x, y: found.y, shown: true };
  });
}
