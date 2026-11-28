import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { rooted } from '../test/sources';
import { glyphsInAccent, glyphsUnder } from '../test/census/glyphs';
import { Icon } from './atoms/Icon';
import { Mark } from './Mark';

/**
 * 🔴 **A glyph wears the neutral inks, never the accent** (FIX-LOG 2026-11-20). An icon draws in `currentColor`, so it takes its
 * container's colour; the role set's codemod moved every word in the accent to the blue `accent-ink`, and every icon inside a link, an
 * accent word, the chosen place of the activity bar and the strip's mark turned blue with it. A word that acts keeps the blue; the
 * glyph beside it wears `ink-soft`, and a chosen place says so by its rail and its field. Every story's render is held to it
 * (`test/composeStories.ts`); this file holds the reader, and the sources for a glyph given the accent by its own class.
 */

const DRAWN_IN = String.raw`(?<![\w:/-])(?:text|stroke|fill)-(?:accent|accent-ink|live|live-ink)(?:\/\S+)?(?![\w-])`;
const GLYPH_WITH_CLASS = /<(?:Icon|Mark)\b[^>]*?className=(?:"[^"]*"|'[^']*'|\{[^}]*\})/g;

/** Every `<Icon>` or `<Mark>` a source gives the accent by its own class, at rest. */
export function glyphsGivenAccent(files: [path: string, source: string][]): string[] {
  const drawn = new RegExp(DRAWN_IN);
  return files.flatMap(([path, source]) => (source.match(GLYPH_WITH_CLASS) ?? [])
    .filter((glyph) => drawn.test(glyph.slice(glyph.indexOf('className='))))
    .map((glyph) => `${path} gives a glyph the accent: ${glyph.replace(/\s+/g, ' ')}`));
}

const sources = Object.entries(rooted(import.meta.glob('/src/**/*.tsx', {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>)).filter(([path]) => !/\.test\.tsx$/.test(path));

describe('a glyph wears the neutral inks', () => {
  it('reads an icon in a link, an icon given the accent, the mark, and a paint class, in the shapes the regression took', () => {
    const { container } = render(
      <>
        <a href="#x" className="inline-flex text-body text-accent-ink"><Icon name="link" size={12} />a link</a>
        <button type="button" className="bg-accent-soft text-accent-ink"><Icon name="settings" /></button>
        <Icon name="check" size={12} className="text-accent-ink" />
        <Mark size={18} className="text-accent-ink" />
        <span className="text-live-ink/80"><Icon name="external" /></span>
        <Icon name="x" className="stroke-accent" />
      </>,
    );
    expect(glyphsUnder(container)).toHaveLength(6);
    expect(glyphsInAccent(container)).toEqual([
      'link wears text-accent-ink from its <a>',
      'settings wears text-accent-ink from its <button>',
      'check wears text-accent-ink',
      'square-arrow-out-up-right wears text-live-ink from its <span>',
      'x wears stroke-accent',
      'the mark wears text-accent-ink',
    ]);
  });

  it('leaves a glyph in the inks, on the solid accent, beside a word that acts, and in a state the rest is not', () => {
    const { container } = render(
      <>
        <a href="#x" className="text-accent-ink"><Icon name="link" className="text-ink-soft" />a link</a>
        <button type="button" className="bg-accent text-on-accent"><Icon name="check" /></button>
        <button type="button" className="bg-accent-soft text-ink"><span aria-hidden className="bg-accent" /><Icon name="settings" /></button>
        <span className="text-small text-fail-ink"><Icon name="failure" className="text-current" /></span>
        <button type="button" className="text-ink-faint hover:text-accent-ink aria-pressed:text-accent-ink"><Icon name="x" /></button>
        <Mark size={18} className="text-ink" />
        <Icon name="bell" />
      </>,
    );
    expect(glyphsInAccent(container)).toEqual([]);
  });

  it('catches a glyph the source gives the accent, behind none of the variants, and leaves a hover and the inks', () => {
    expect(glyphsGivenAccent([['./help/focus/FocusPicker.tsx', '<Icon name="check" size={12} className="text-accent-ink" />']]))
      .toEqual(['./help/focus/FocusPicker.tsx gives a glyph the accent: <Icon name="check" size={12} className="text-accent-ink"']);
    expect(glyphsGivenAccent([['./work/frame/frame.tsx', '<Mark size={18} className="text-accent-ink" />']])).toHaveLength(1);
    expect(glyphsGivenAccent([['./x.tsx', "<Icon name=\"x\" className={cn('shrink-0', on && 'text-live')} />"]])).toHaveLength(1);
    expect(glyphsGivenAccent([['./x.tsx', [
      '<Icon name="x" className="text-ink-soft hover:text-accent-ink" />',
      '<Icon name="bell" />',
      '<Mark size={18} className="text-ink" />',
      '<Icon name="check" className="text-on-accent" />',
    ].join('\n')]])).toEqual([]);
  });

  it('is reading the sources', () => {
    expect(sources.length).toBeGreaterThan(500);
    expect(sources.map(([path]) => path)).toContain('./work/frame/frame.tsx');
  });

  it('holds: no source gives a glyph the accent', () => {
    expect(glyphsGivenAccent(sources)).toEqual([]);
  });
});
