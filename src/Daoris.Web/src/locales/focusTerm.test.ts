import { describe, expect, it } from 'vitest';
import { type Catalogue, type Glossary, check, load } from '../../scripts/names-check.mjs';

/**
 * The glossary's *focus* / 聚焦 (ASKSCOPE2a, `docs/2026-11-11-ask-scope-ux-design.md` §0.2): what an Ask Daoris
 * conversation is about. Its pattern leaves the keyboard's focus alone, and a label that calls it 焦点 or *scope* fails.
 */

const { glossary, en } = load() as { glossary: Glossary; en: Catalogue };
const focus = glossary.terms.find((term) => term.term === 'focus');

/** The real glossary with two planted labels, keyed under a kind the glossary holds to its names. */
const planted = (english: string, chinese: string) => {
  const key = 'help.history.pin';
  return check(glossary, { ...en, [key]: english }, { [key]: chinese }).filter((finding) => finding.key === key)
    .map(({ rule, language }) => `${rule}:${language}`);
};

describe('the focus term', () => {
  it('is in the glossary as focus and 聚焦, avoiding scope, 焦点 and 范围', () => {
    expect(focus).toMatchObject({ en: 'focus', zh: '聚焦', avoid: { en: ['scope'], zh: ['焦点', '范围'] } });
  });

  it("does not catch the two sentences that say focus for the keyboard's", () => {
    const pattern = new RegExp(focus!.match!, 'i');
    expect(pattern.test(en['menu.keys.bar']!)).toBe(false);
    expect(pattern.test(en['projects.git.changes.readAt']!)).toBe(false);
    expect(pattern.test("Ask Daoris's focus")).toBe(true);
    expect(pattern.test('Make Work the focus')).toBe(true);
  });

  it('passes a focus label that says 聚焦, and fails one that says 焦点 or scope', () => {
    expect(planted('Change focus…', '更改聚焦…')).toEqual([]);
    expect(planted('Change focus…', '更改焦点…')).toEqual(['glossary:zh']);
    expect(planted('Change scope…', '更改聚焦…')).toEqual(['glossary:en']);
  });
});
