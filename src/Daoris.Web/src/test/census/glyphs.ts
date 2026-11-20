import { TYPE_STEPS } from '../../lib/cn';

/**
 * What the glyph census reads (`design/glyphInk.test.tsx`, and every story's render in `test/composeStories.ts`): the colour a
 * glyph is drawn in. A glyph (a lucide icon, the mark) draws in `currentColor`, so it takes the nearest colour a class sets on it
 * or above it, and an icon inside a link or a word in the accent took the accent's blue with it (FIX-LOG 2026-11-20). Glyphs wear
 * the neutral inks (the window design §7.1): a word that acts keeps its blue, the glyph beside it does not, and a place that is
 * chosen says so by its rail or its field.
 */

/** The accent's colours, and `live`, its second name: a glyph wears none of them, by its own class or its container's. */
export const ACCENT_INKS = ['accent', 'accent-ink', 'live', 'live-ink'] as const;

/** `text-` utilities that set no colour: the type steps, alignment, wrapping and overflow. */
const NOT_A_COLOUR = new Set<string>([
  ...TYPE_STEPS, 'left', 'center', 'right', 'justify', 'start', 'end', 'wrap', 'nowrap', 'balance', 'pretty', 'ellipsis', 'clip',
]);

/** Colours that pass the container's on: the glyph is read further up. */
const PASSES_ON = new Set(['current', 'inherit']);

/** The colour an unprefixed `text-` class sets, its opacity aside, or null where it sets none (a variant's state is not the rest). */
function textColour(name: string): string | null {
  const match = /^text-([a-z][a-z0-9-]*)(?:\/\S+)?$/.exec(name);
  if (!match || NOT_A_COLOUR.has(match[1]!) || match[1]!.startsWith('[')) return null;
  return match[1]!;
}

/** The glyph's name, said in a finding: its lucide name, or the mark. */
function glyphName(glyph: Element): string {
  const lucide = [...glyph.classList].find((name) => name.startsWith('lucide-'));
  return lucide ? lucide.slice('lucide-'.length) : 'the mark';
}

/** The glyphs drawn under `root`: every lucide icon, and the mark (`design/Mark.tsx`, found by its mask). */
export function glyphsUnder(root: ParentNode): Element[] {
  const marks = [...root.querySelectorAll('mask#daoris-mark')].map((mask) => mask.closest('svg')).filter((svg): svg is SVGSVGElement => svg !== null);
  return [...new Set([...root.querySelectorAll('svg.lucide'), ...marks])];
}

/** An element's classes as written, an SVG's included (its `className` is no string). */
const classesOf = (element: Element) => (element.getAttribute('class') ?? '').split(/\s+/).filter(Boolean);

/**
 * Each glyph under `root` drawn in the accent's colours at rest: by its own `stroke-`/`fill-` class, or by the nearest unprefixed
 * `text-` colour on it or an ancestor. A hover or a pressed state's colour is the state's, and is not read.
 */
export function glyphsInAccent(root: ParentNode): string[] {
  const accent = new Set<string>(ACCENT_INKS);
  return glyphsUnder(root).flatMap((glyph) => {
    const own = classesOf(glyph).find((name) => {
      const paint = /^(?:stroke|fill)-([a-z][a-z0-9-]*)(?:\/\S+)?$/.exec(name);
      return paint !== null && accent.has(paint[1]!);
    });
    if (own) return [`${glyphName(glyph)} wears ${own}`];
    for (let at: Element | null = glyph; at; at = at.parentElement) {
      const colour = classesOf(at).map(textColour).filter((each): each is string => each !== null && !PASSES_ON.has(each)).at(-1);
      if (colour === undefined) continue;
      return accent.has(colour)
        ? [`${glyphName(glyph)} wears text-${colour}${at === glyph ? '' : ` from its <${at.tagName.toLowerCase()}>`}`]
        : [];
    }
    return [];
  });
}
