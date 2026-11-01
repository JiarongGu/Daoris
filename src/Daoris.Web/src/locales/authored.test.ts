import { describe, expect, it } from 'vitest';
import { authoredEntries, authoredText, authoredTexts, composeAuthored, readAuthored } from './authored';

describe('authored text (D222)', () => {
  it('shows a string as written in any language', () => {
    expect(authoredText('Reviewer', 'fr')).toBe('Reviewer');
  });

  it("shows the viewer's language, else its base language, else the first entry as written", () => {
    const name = { fr: 'Relecteur', zh: '审阅', en: 'Reviewer' };
    expect(authoredText(name, 'fr')).toBe('Relecteur');
    expect(authoredText(name, 'zh-TW')).toBe('审阅');
    expect(authoredText(name, 'de')).toBe('Relecteur');
  });

  it('reads an old { en, zh } pair as it was', () => {
    const pair = { en: 'Build', zh: '开发' };
    expect([authoredText(pair, 'zh'), authoredText(pair, 'en')]).toEqual(['开发', 'Build']);
  });

  it('is empty where there is nothing to show', () => {
    expect(authoredText(null, 'en')).toBe('');
    expect(authoredText(undefined, 'en')).toBe('');
  });

  it('reads only a string or a map of tags to non-blank text', () => {
    expect(readAuthored('x')).toBe('x');
    expect(readAuthored({ fr: 'Relecteur' })).toEqual({ fr: 'Relecteur' });
    for (const bad of ['  ', {}, { en: '' }, { en: 3 }, 3, null, [], { 'not a tag': 'x' }]) expect(readAuthored(bad)).toBeNull();
  });

  it('lists every text for a search and every entry for an editor', () => {
    expect(authoredTexts({ fr: 'Relecteur', zh: '审阅' })).toEqual(['Relecteur', '审阅']);
    expect(authoredTexts('A')).toEqual(['A']);
    expect(authoredEntries('A')).toEqual([]);
    expect(authoredEntries({ fr: 'R' })).toEqual([['fr', 'R']]);
  });

  it('writes one text as a plain string and several as a map, the text kept as en beside the others', () => {
    expect(composeAuthored('Reviewer', [])).toBe('Reviewer');
    expect(composeAuthored('Reviewer', [['zh', '审阅'], ['fr', ' ']])).toEqual({ en: 'Reviewer', zh: '审阅' });
    expect(composeAuthored('', [['fr', 'Relecteur']])).toEqual({ fr: 'Relecteur' });
    expect(composeAuthored('  ', [])).toBeNull();
  });
});
