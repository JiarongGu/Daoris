import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { rooted } from '../test/sources';
import ALIAS_SITES from './amberAliases.json';

/**
 * **Amber is the owner's alone** (the window design §7.1): `you`, `you-ink`, `you-soft` and `on-you` mark only what waits on
 * the owner (◆, the waits pill, a wait card's edge and field, its *yes*), so this is a census over the sources. A file that
 * draws the role is one of the owner's waits, named here with its reason. Today's `st-open`, `ink-open` and `warn` draw the
 * same amber as aliases until each site is split into `you` or neutral by its screen's row; `amberAliases.json` counts them
 * per file, a list that may only shrink and goes with the aliases.
 */
const OWNERS_WAITS: Record<string, string> = {
  './design/atoms/Card.tsx': "the card atom's wait edge, the one a card that waits on the owner wears (`Card`'s `edge`)",
  './work/session/WaitCard.tsx': "a wait card in the session's dock: its ◆ and its edge",
  './work/session/SessionPanel.tsx': "the dock's count of what waits on the owner",
  './work/session/PanelHead.tsx': "the head's press to the oldest wait on the owner",
};

const UTILITY = String.raw`(?:text|bg|border(?:-[xytrblse])?|fill|stroke|ring|outline|decoration|divide|caret|accent|shadow|placeholder|from|via|to)`;
const OPACITY = String.raw`(?:\/(?:\d+|\[[^\]\s]+\]))?`;
const ROLE = new RegExp(String.raw`(?<![\w-])(?:${UTILITY}-(?:you|you-ink|you-soft|on-you)${OPACITY}|var\(--(?:you|you-ink|you-soft|on-you)\))(?![\w-])`, 'g');
const ALIAS = new RegExp(String.raw`(?<![\w-])(?:${UTILITY}-(?:st-open|ink-open|warn)${OPACITY}|var\(--(?:st-open|ink-open|warn)\))(?![\w-])`, 'g');

/** Each file that draws the owner's role, less the owner's waits. */
export function amberOutsideWaits(files: [path: string, source: string][], waits: Record<string, string> = OWNERS_WAITS): string[] {
  return files.filter(([path]) => !(path in waits))
    .flatMap(([path, source]) => (source.match(ROLE) ?? []).map((hit) => `${path} draws the owner's amber: ${hit}`));
}

/** Each file's count of alias sites, where it has any. */
export function aliasCounts(files: [path: string, source: string][]): Record<string, number> {
  return Object.fromEntries(files
    .map(([path, source]) => [path, (source.match(ALIAS) ?? []).length] as const)
    .filter(([, count]) => count > 0));
}

const sources = Object.entries(rooted(import.meta.glob(['/src/**/*.tsx', '/src/**/*.ts'], {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>)).filter(([path]) => !/\.test\.tsx?$/.test(path));
const stylesheets = Object.keys(rooted(import.meta.glob('/src/**/*.css')))
  .filter((path) => path !== './tokens.css')
  .map((path) => [path, readFileSync(join(process.cwd(), 'src', path), 'utf8')] as [string, string]);
const all = [...sources, ...stylesheets];

describe("the owner's amber", () => {
  it('catches the role outside a wait, behind a variant, with an opacity and in a stylesheet, and leaves a wait its own', () => {
    expect(amberOutsideWaits([['./quests/QuestRow.tsx', '<span className="text-you-ink hover:bg-you-soft/60" />']]))
      .toEqual(["./quests/QuestRow.tsx draws the owner's amber: text-you-ink", "./quests/QuestRow.tsx draws the owner's amber: bg-you-soft/60"]);
    expect(amberOutsideWaits([['./x.css', '.mark { color: var(--you); }']])).toHaveLength(1);
    expect(amberOutsideWaits([['./waits/WaitCard.tsx', "'border-l-you bg-you-soft'"]], { './waits/WaitCard.tsx': 'a wait card' })).toEqual([]);
    // A word that holds the name is no use of it.
    expect(amberOutsideWaits([['./x.tsx', "t('ask.you') 'bg-young' data-you-ink"]])).toEqual([]);
  });

  it('is reading the sources and the stylesheets', () => {
    expect(sources.length).toBeGreaterThan(500);
    expect(stylesheets.map(([path]) => path)).toContain('./work/review/code.css');
  });

  it("holds: the owner's amber is drawn only by the owner's waits", () => {
    expect(amberOutsideWaits(all)).toEqual([]);
    for (const path of Object.keys(OWNERS_WAITS)) {
      expect(all.find(([each]) => each === path)?.[1].match(ROLE), `${path} is a wait that draws the role, or leaves the list`).toBeTruthy();
    }
  });

  it('counts the aliases, in a class, a side and a variable', () => {
    expect(aliasCounts([['./a.tsx', "'border-l-st-open text-ink-open hover:text-warn bg-warn/10 text-warning' var(--st-open)"], ['./b.tsx', 'text-ink']]))
      .toEqual({ './a.tsx': 5 });
  });

  it("holds: each file's alias sites are the count the list names, which only shrinks as sites are split", () => {
    expect(aliasCounts(all)).toEqual(ALIAS_SITES);
  });
});
