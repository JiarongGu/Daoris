import { describe, expect, it } from 'vitest';
import { en, zh } from '.';

/**
 * The glossary's `pairs`: two keys that read as one pair, an act and its counterpart on one object (open and
 * close a tree, show and hide a list). Each language declares its two verbs and the object they share, and the
 * check holds that both keys exist in both catalogues, the first begins with the first verb (English) or holds it
 * (Chinese), the second the second, and both name the object. A pair that reads as an activity and an object,
 * or as two verb families, fails here.
 */
type Side = { verbs: [string, string]; object?: string };
type Pair = { pair: string; keys: [string, string]; en: Side; zh: Side };

const loaded = Object.values(import.meta.glob<{ pairs: Pair[] }>('./glossary.json', { eager: true, import: 'default' }));
const declared = loaded[0].pairs;

const speaks = (value: string | undefined, verb: string, object: string | undefined, language: 'en' | 'zh'): boolean => {
  if (value === undefined) return false;
  const text = language === 'en' ? value.toLowerCase() : value;
  const word = language === 'en' ? verb.toLowerCase() : verb;
  const said = language === 'en' ? text.startsWith(word) : text.includes(word);
  return said && (object === undefined || text.includes(language === 'en' ? object.toLowerCase() : object));
};

/** The pairs that do not read as one: the declared pair's name and the language and key that broke it. */
export const unevenPairs = (pairs: Pair[], english: Record<string, string>, chinese: Record<string, string>): string[] =>
  pairs.flatMap(({ pair, keys, en: sideEn, zh: sideZh }) => {
    const broken: string[] = [];
    for (const [language, side, catalogue] of [['en', sideEn, english], ['zh', sideZh, chinese]] as const) {
      keys.forEach((key, at) => {
        if (!speaks(catalogue[key], side.verbs[at], side.object, language)) broken.push(`${pair}: ${language} ${key}`);
      });
    }
    return broken;
  });

describe('the glossary pairs', () => {
  it('are declared, each with two distinct keys and two verbs in each language', () => {
    expect(declared.length).toBeGreaterThan(0);
    for (const { pair, keys, en: sideEn, zh: sideZh } of declared) {
      expect(keys[0], pair).not.toBe(keys[1]);
      expect(sideEn.verbs[0], pair).not.toBe(sideEn.verbs[1]);
      expect(sideZh.verbs[0], pair).not.toBe(sideZh.verbs[1]);
    }
    expect(new Set(declared.map(({ pair }) => pair)).size).toBe(declared.length);
  });

  it('read as one pair in both catalogues: each key exists, says its verb, and names the shared object', () => {
    expect(unevenPairs(declared, en, zh)).toEqual([]);
  });

  it('catches a pair whose start is an activity and whose end is an object', () => {
    const tree: Pair = {
      pair: 'tree', keys: ['a', 'b'],
      en: { verbs: ['Open', 'Close'], object: 'tree' }, zh: { verbs: ['打开', '关闭'], object: '工作树' },
    };
    expect(unevenPairs([tree], { a: 'Open a tree', b: 'Close the tree' }, { a: '打开工作树', b: '关闭工作树' })).toEqual([]);
    expect(unevenPairs([tree], { a: 'Work apart', b: 'End the tree' }, { a: '分开工作', b: '结束工作树' }))
      .toEqual(['tree: en a', 'tree: en b', 'tree: zh a', 'tree: zh b']);
    expect(unevenPairs([tree], { a: 'Open a tree' }, { a: '打开工作树', b: '关闭工作树' })).toEqual(['tree: en b']);
  });
});
