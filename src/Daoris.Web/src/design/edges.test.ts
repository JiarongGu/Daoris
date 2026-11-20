import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { rooted } from '../test/sources';

/**
 * **A 3 px edge is a wait's or a failure's, and the card atom alone draws it** (the window design §7.4: borders are 1, 2 for
 * the focus ring and a selected row's rail, and 3 for a focused wait card's or a failed card's edge, and no other width; cards
 * lose the default left edge they wore). The census found 3 px on every card, on the selected row of every list and on a few
 * dozen notes, 4 px on the session dock's wait card and 1.5 px on the map's cards; so a border of any width but 1 and 2
 * anywhere but `Card`'s two edges fails, by its file and its class. A selected row's rail is 2 px (`border-l-2`), a note says
 * its tone in words, and a card that waits or failed takes `Card`'s `edge` or `cardFrame`.
 */
const THE_ATOM = './design/atoms/Card.tsx';

/**
 * A border width as a class, on any side and behind any variant, but the design's 1 (`border`, `[1px]`) and 2: a number, a length
 * (`[3px]`, `[0.25rem]`), a variable read as a length (`[var(--w)]`, `[length:var(--w)]`, `(length:--w)`), and `divide-x-3` as much as `border-l-3`.
 * The one variable kept is `--frame-rule`, the frame's 1 px hairline scaled by its zoom (`tokens.css`).
 */
const CLASS = new RegExp(
  '(?<![\\w-])(?:border|divide)(?:-[xytrblse])?-(?:'
  + '\\[(?!(?:1|2)px\\])(?:[\\d.]+(?:px|rem|em)|var\\((?!--frame-rule\\))[^\\]]*\\)|length:(?!var\\(--frame-rule\\)\\])[^\\]]*)\\]'
  + '|\\(length:--[\\w-]+\\)'
  + '|[3-9]|[1-9]\\d+)(?![\\w.\\]-])',
  'g',
);
/** A class string's width beside `border-transparent` draws no edge, as the style branch's does: dropped before the scan. */
const TRANSPARENT = /(?<![\w-])border(?:-[xytrblse])?-\[[\d.]+(?:px|rem|em)\](?=[^'"`]*?(?<![\w-])border(?:-[xytrblse])?-transparent(?![\w-]))|(?<=(?<![\w-])border(?:-[xytrblse])?-transparent[^'"`]*?)(?<![\w-])border(?:-[xytrblse])?-\[[\d.]+(?:px|rem|em)\]/g;
/** A numeric width in a style object: `borderLeftWidth: 3` is 3 px. */
const NUMERIC = /border(?:Left|Right|Top|Bottom|Inline|Block)?(?:Start|End)?Width\s*:\s*(?!(?:1|2)\s*[,}\n])\d+(?:\.\d+)?\s*[,}\n]/g;
/**
 * The same as a style or a stylesheet's declaration, in either spelling. A transparent one draws no edge: the scrollbar's
 * thumb is inset by one (`tokens.css`), its padding and not a border anyone sees.
 */
const STYLE = /border(?:-?(?:left|right|top|bottom|inline|block)(?:-?(?:start|end))?)?(?:-?width)?\s*:\s*['"`]?(?!(?:1|2)px)[\d.]+px(?![^;'"`}]*transparent)/gi;

/** Each border of a width the design does not have that a file draws, as a class or a style, outside the card atom. */
export function thickEdges(files: [path: string, source: string][], atom = THE_ATOM): string[] {
  return files.filter(([path]) => path !== atom)
    .flatMap(([path, source]) => [
      ...source.replace(TRANSPARENT, '').match(CLASS) ?? [], ...source.match(STYLE) ?? [], ...source.match(NUMERIC) ?? [],
    ]
      .map((hit) => `${path} draws a border the design has no width for: ${hit}`));
}

const sources = Object.entries(rooted(import.meta.glob(['/src/**/*.tsx', '/src/**/*.ts'], {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>)).filter(([path]) => !/\.test\.tsx?$/.test(path));
const stylesheets = Object.keys(rooted(import.meta.glob('/src/**/*.css')))
  .map((path) => [path, readFileSync(join(process.cwd(), 'src', path), 'utf8')] as [string, string]);
const all = [...sources, ...stylesheets];

describe('the 3 px edge', () => {
  it("catches it on a card, a selected row, a note, a side, behind a variant and in a style — the shapes the census found", () => {
    expect(thickEdges([['./quests/Conflicts.tsx', 'className="rounded-card border border-line border-l-[3px] border-l-st-open"']]))
      .toEqual(['./quests/Conflicts.tsx draws a border the design has no width for: border-l-[3px]']);
    expect(thickEdges([['./x.tsx', "'border-l-3 aria-[current]:border-l-[3px] border-[3px] border-t-3'"]])).toHaveLength(4);
    // The dock's wait card's 4 px, the map's 1.5 px, and a width in rem.
    expect(thickEdges([['./work/session/WaitCard.tsx', "'border-l-4 border-l-you'"], ['./map/MapCards.tsx', "'border-[1.5px] border-[0.25rem] border-12'"]]))
      .toHaveLength(4);
    expect(thickEdges([['./x.css', '.note { border-left: 3px solid var(--warn); } .b { border-width: 1.5px }']])).toHaveLength(2);
    expect(thickEdges([['./x.tsx', "style={{ borderLeftWidth: '3px' }}"]])).toHaveLength(1);
    expect(thickEdges([['./x.stories.tsx', '<div className="border-l-[3px] border-l-accent" />']])).toHaveLength(1);
    // Sabotage: each hole the review found is caught, by the shape that slipped through.
    expect(thickEdges([['./x.tsx', "'divide-x-3 divide-y-[3px] md:divide-x-4'"]])).toHaveLength(3);
    expect(thickEdges([['./x.tsx', "'border-l-[var(--w)] border-[length:var(--w)] border-(length:--w)'"]])).toHaveLength(3);
    expect(thickEdges([['./x.tsx', 'style={{ borderLeftWidth: 3 }} style={{ borderWidth: 4.5, color: 1 }}']])).toHaveLength(2);
    // The atom's own two edges are its alone.
    expect(thickEdges([[THE_ATOM, "wait: 'border-l-[3px] border-l-you'"]])).toEqual([]);
  });

  it('leaves the widths the design keeps, and what is no width', () => {
    expect(thickEdges([['./x.tsx', "'border border-0 border-2 border-l-2 border-x border-t-0 border-[1px] border-[2px] border-line-strong border-[#cfcabe]'"]]))
      .toEqual([]);
    expect(thickEdges([['./x.tsx', "'py-3 gap-3 rounded-[3px] outline-3 ring-3 border-spacing-3'"]])).toEqual([]);
    expect(thickEdges([['./x.css', '.pill { border-radius: 999px; border-width: 2px; border: 1px solid; }']])).toEqual([]);
    // A width beside border-transparent in a class string is an inset, in either order, as the style branch's is.
    expect(thickEdges([['./x.tsx', "'border-l-[3px] border-l-transparent px-2' 'border-transparent border-[4px]'"]])).toEqual([]);
    expect(thickEdges([['./x.tsx', "style={{ borderLeftWidth: 2 }} style={{ borderWidth: 1 }} 'divide-y divide-line divide-x-2'"]])).toEqual([]);
    // The scrollbar thumb's inset: transparent, so padding, never an edge.
    expect(thickEdges([['./tokens.css', '::-webkit-scrollbar-thumb { border: 3px solid transparent; }']])).toEqual([]);
  });

  it('is reading the sources and the stylesheets', () => {
    expect(sources.length).toBeGreaterThan(500);
    expect(stylesheets.map(([path]) => path)).toContain('./tokens.css');
    expect(all.find(([path]) => path === THE_ATOM)?.[1].match(CLASS), "the atom's two edges").toEqual(['border-l-[3px]', 'border-l-[3px]']);
  });

  it("holds: no source draws a 3 px border but the card atom's wait and failure edges", () => {
    expect(thickEdges(all)).toEqual([]);
  });
});
