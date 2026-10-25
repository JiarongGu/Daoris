import { describe, expect, it } from 'vitest';
import { en, zh } from '.';

/**
 * The glossary (NAME1a, D116): the authority names are checked against, `docs/2026-10-01-naming-design.md`
 * §5. These hold its shape, so the check that reads it (`scripts/names-check.mjs`) never reports against
 * a glossary that has stopped saying anything.
 */
type Term = {
  term: string;
  en?: string;
  zh?: string;
  zhForms?: string[];
  means: string;
  why?: string;
  match?: string;
  pair?: boolean;
  use?: string;
  avoid: { en: string[]; zh: string[] };
};
type Kind = {
  what: string;
  room: string;
  budget: { en: number; zh: number } | null;
  case: 'sentence' | 'lower' | 'none';
  keys: string[];
};
type Glossary = {
  properNouns: string[];
  kinds: Record<string, Kind>;
  doors: { door: string; opens: string }[];
  terms: Term[];
};

const loaded = Object.values(import.meta.glob<Glossary>('./glossary.json', { eager: true, import: 'default' }));
const glossary = loaded[0] as Glossary;

/** The design's fourteen kinds, in its order (§3). */
const KINDS = [
  'nav', 'title', 'tab', 'section', 'field', 'choice', 'button', 'status', 'menu', 'command', 'headline',
  'placeholder', 'toast', 'sentence',
];

/** A pattern's reach: its own key, or every key under its prefix. */
const holds = (pattern: string, key: string): boolean =>
  pattern.endsWith('.*') ? key.startsWith(pattern.slice(0, -1)) : key === pattern;

describe('the glossary', () => {
  it('is there, beside the language folders and in neither', () => {
    expect(loaded).toHaveLength(1);
  });

  it('holds the fourteen kinds of the naming design, each with its budget in both languages', () => {
    expect(Object.keys(glossary.kinds)).toEqual(KINDS);
    for (const [name, kind] of Object.entries(glossary.kinds)) {
      expect(kind.what, name).toMatch(/\S/);
      expect(kind.room, name).toMatch(/\S/);
      if (name === 'sentence') {
        expect(kind.budget, 'a sentence wraps, so it has no budget').toBeNull();
        expect(kind.keys, 'a key no kind names is a sentence').toEqual([]);
      } else {
        expect(kind.budget!.en, name).toBeGreaterThan(0);
        expect(kind.budget!.zh, name).toBeGreaterThan(0);
      }
    }
  });

  it('covers every concept the NAME1 row names, and Ask Daoris itself', () => {
    const named = new Set(glossary.terms.map(({ term }) => term));
    const brief = [
      'session', 'conversation', 'quest', 'request', 'intake', 'line', 'landing', 'branch', 'tree', 'workspace',
      'repository', 'circle', 'agent', 'harness', 'account', 'profile', 'plugin', 'point', 'server', 'driver',
      'loop', 'hold', 'strike', 'park', 'review', 'accept', 'send back', 'discard', 'hand off',
      'bring up to date', 'clean up', 'doctrine', 'canon', 'rule', 'knowledge', 'skill', 'pack', 'adopt', 'sync',
      'drift', 'Ask Daoris',
    ];
    expect(brief.filter((term) => !named.has(term))).toEqual([]);
  });

  it('gives each term its names in both languages, a one-line definition, and what it must not be called', () => {
    const byId = new Map(glossary.terms.map((term) => [term.term, term]));
    for (const term of glossary.terms) {
      expect(term.means, term.term).toMatch(/^[^\n]+\.$/);
      expect(Array.isArray(term.avoid.en) && Array.isArray(term.avoid.zh), term.term).toBe(true);
      if (term.use) {
        // A code word never shown on the window points at the term that is.
        expect(byId.get(term.use)?.en, `${term.term} → ${term.use}`).toBeTruthy();
        expect(term.en ?? term.zh, `${term.term} is a code word, so it has no name of its own`).toBeUndefined();
        continue;
      }
      expect(term.en, term.term).toMatch(/\S/);
      expect(term.zh, term.term).toMatch(/\S/);
      expect(() => new RegExp(term.match!, 'i'), term.term).not.toThrow();
      expect(term.avoid.zh, `${term.term} avoids its own name`).not.toContain(term.zh);
      expect(term.avoid.en.map((word) => word.toLowerCase()), term.term).not.toContain(term.en!.toLowerCase());
    }
  });

  it('names each concept once in each language: no two terms share a name', () => {
    const named = glossary.terms.filter((term) => !term.use);
    const twice = (names: string[]) => names.filter((name, at) => names.indexOf(name) !== at);
    expect(twice(named.map((term) => term.en!.toLowerCase()))).toEqual([]);
    expect(twice(named.map((term) => term.zh!))).toEqual([]);
  });

  it('names only keys the catalogues hold, in both languages', () => {
    const keys = Object.keys(en);
    const stray = Object.entries(glossary.kinds).flatMap(([name, kind]) => kind.keys
      .filter((pattern) => !keys.some((key) => holds(pattern, key)) || (!pattern.endsWith('.*') && !(pattern in zh)))
      .map((pattern) => `${name}: ${pattern}`));
    expect(stray).toEqual([]);
    const doors = glossary.doors.flatMap(({ door, opens }) => [door, opens]).filter((key) => !(key in en) || !(key in zh));
    expect(doors).toEqual([]);
  });

  /**
   * FOUNDATION1u (D212, the design's §6): a role is one concept in two shapes, a working role (D205's actor) and
   * an appointed one (D208, D209), and the page says 角色 for it in both. *actor* is a code and prose word that
   * points at it, since `names:check` refuses two terms sharing one Chinese name.
   */
  describe('the role', () => {
    const byId = new Map(glossary.terms.map((term) => [term.term, term]));
    const role = byId.get('role')!;

    it('is 角色, with the words it must not be called, 职责 among them', () => {
      expect(role.en).toBe('role');
      expect(role.zh).toBe('角色');
      expect(role.means).toMatch(/working role/);
      expect(role.means).toMatch(/任命的角色/);
      expect(role.avoid.en).toEqual([]);
      expect(role.avoid.zh).toEqual(['职务', '岗位', '演员', '人设', '执行者', '职责']);
    });

    it('matches the Actor, Actors and Roles labels', () => {
      const pattern = new RegExp(role.match!, 'i');
      const labels = ['work.actor.title', 'settings.ai.actors.title', 'settings.daoris.label.roles'];
      expect(labels.map((key) => [en[key], pattern.test(en[key]!)])).toEqual([
        ['Actor', true], ['Actors', true], ['Roles', true],
      ]);
      expect(labels.map((key) => zh[key])).toEqual(['角色', '角色', '角色']);
    });

    it('leaves actor a code and prose word that points at it, with no name of its own', () => {
      const actor = byId.get('actor')!;
      expect(actor.use).toBe('role');
      expect(actor.en).toBeUndefined();
      expect(actor.zh).toBeUndefined();
      expect(actor.means).toMatch(/staffing/);
      expect(actor.avoid).toEqual({ en: [], zh: [] });
    });

    it('is the one word the page says for it: no Chinese string says 职责', () => {
      expect(Object.entries(zh).filter(([, value]) => value.includes('职责')).map(([key]) => key)).toEqual([]);
    });

    it('says 角色 in the ten strings of the appointing flow, and 任命的角色 where the hint names the appointed shape', () => {
      const ten = [
        'projects.supervisor.ask.holder', 'projects.supervisor.ask.share.legend', 'projects.supervisor.ask.share.means',
        'projects.supervisor.refused.window-only', 'projects.supervisor.refused.unknown-role',
        'projects.supervisor.refused.choose-terminal', 'projects.supervisor.refused.no-holder',
        'projects.supervisor.refused.revoked-holder', 'projects.supervisor.refused.holder-harness',
        'settings.daoris.steward.ask.share.legend',
      ];
      expect(ten.filter((key) => !zh[key]!.includes('角色'))).toEqual([]);
      expect(zh['settings.daoris.roles.hint']).toContain('任命的角色');
    });
  });

  it('gives a key one kind: its own entry first, then its longest prefix, and never two at once', () => {
    const specificity = (pattern: string) => (pattern.endsWith('.*') ? pattern.length - 2 : Number.MAX_SAFE_INTEGER);
    const ambiguous: string[] = [];
    for (const key of Object.keys(en)) {
      const stem = key.replace(/_(zero|one|two|few|many|other)$/, '');
      const claims = Object.entries(glossary.kinds).flatMap(([name, kind]) =>
        kind.keys.filter((pattern) => holds(pattern, stem)).map((pattern) => ({ name, rank: specificity(pattern) })));
      const best = Math.max(-1, ...claims.map(({ rank }) => rank));
      const winners = new Set(claims.filter(({ rank }) => rank === best).map(({ name }) => name));
      if (winners.size > 1) ambiguous.push(`${key}: ${[...winners].join(', ')}`);
    }
    expect(ambiguous).toEqual([]);
  });
});
