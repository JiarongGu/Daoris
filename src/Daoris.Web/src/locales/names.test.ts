import { spawnSync } from 'node:child_process';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import {
  type Catalogue, type Finding, type Glossary, check, kindOf, load, measure, report, validate,
} from '../../scripts/names-check.mjs';

/**
 * The names check (NAME1a, D116): `scripts/names-check.mjs`, beside the parity check. It reports and
 * never fails today; NAME1b turns its facts' half to a gate with `--strict`
 * (`docs/2026-10-01-naming-design.md` §6).
 */

const MEASURE: Glossary['measure'] = {
  latinUnit: 0.5, numericPlaceholders: ['count'], numericLength: 2, otherLength: 10,
};

const kind = (budget: { en: number; zh: number } | null, casing: 'sentence' | 'lower' | 'none', keys: string[]) =>
  ({ what: 'w', room: 'r', budget, case: casing, keys });

/** A glossary of a few terms and every kind, small enough to read beside each assertion. */
const fixture = (): Glossary => ({
  measure: MEASURE,
  properNouns: ['Daoris'],
  kinds: {
    nav: kind({ en: 16, zh: 5 }, 'sentence', ['nav.*']),
    title: kind({ en: 40, zh: 16 }, 'sentence', []),
    tab: kind({ en: 10, zh: 4 }, 'sentence', []),
    section: kind({ en: 32, zh: 12 }, 'sentence', ['area.title']),
    field: kind({ en: 36, zh: 14 }, 'sentence', []),
    choice: kind({ en: 16, zh: 6 }, 'sentence', []),
    button: kind({ en: 20, zh: 8 }, 'sentence', ['area.*', 'menu.go']),
    status: kind({ en: 16, zh: 5 }, 'lower', ['area.state.*']),
    menu: kind({ en: 24, zh: 10 }, 'sentence', ['menu.*']),
    command: kind({ en: 40, zh: 16 }, 'sentence', ['palette.*']),
    headline: kind({ en: 40, zh: 16 }, 'sentence', []),
    placeholder: kind({ en: 40, zh: 16 }, 'lower', []),
    toast: kind({ en: 110, zh: 55 }, 'none', []),
    sentence: kind(null, 'none', []),
  },
  doors: [{ door: 'menu.go', opens: 'nav.quests' }],
  terms: [
    { term: 'quest', en: 'quest', zh: '委托', means: 'A request.', match: '\\bquests?\\b', avoid: { en: [], zh: ['任务'] } },
    { term: 'pack', en: 'pack', zh: '规范包', means: 'A set.', match: '\\bpacks?\\b', avoid: { en: [], zh: ['包'] } },
    { term: 'account', en: 'account', zh: '账户', means: 'Who.', match: '\\baccounts?\\b', avoid: { en: [], zh: [] } },
    { term: 'profile', use: 'account', means: 'The code word.', avoid: { en: ['profile'], zh: [] } },
    {
      term: 'waiting on you', en: 'waiting on you', zh: '等你处理', zhForms: ['等你'], means: 'For the person.',
      match: '\\bwaiting on you\\b', avoid: { en: [], zh: ['等你决定'] },
    },
  ],
});

const catalogues = (entries: Record<string, [string, string]>): { en: Catalogue; zh: Catalogue } => ({
  en: Object.fromEntries(Object.entries(entries).map(([key, [english]]) => [key, english])),
  zh: Object.fromEntries(Object.entries(entries).map(([key, [, chinese]]) => [key, chinese])),
});

const rules = (findings: Finding[], key: string) =>
  findings.filter((finding) => finding.key === key).map(({ rule, language }) => `${rule}:${language}`).sort();

describe('which kind a key is', () => {
  const kindFor = kindOf(fixture());

  it('is its own entry before any prefix, and the longest prefix before a shorter one', () => {
    expect(kindFor('menu.go')).toBe('button');
    expect(kindFor('menu.other')).toBe('menu');
    expect(kindFor('area.state.open')).toBe('status');
    expect(kindFor('area.take')).toBe('button');
    expect(kindFor('area.title')).toBe('section');
  });

  it("is its stem's for a plural form, and a sentence where no kind names it", () => {
    expect(kindFor('area.state.open_other')).toBe('status');
    expect(kindFor('elsewhere.body')).toBe('sentence');
  });
});

describe('how long a name is', () => {
  it('counts English in characters, a number placeholder as two and any other as ten', () => {
    expect(measure('Clean up {{count}} branches', 'en', MEASURE)).toBe('Clean up 00 branches'.length);
    expect(measure('Open {{name}}', 'en', MEASURE)).toBe(5 + 10);
  });

  it('counts Chinese in units: a character or full-width mark 1, a Latin letter, digit or space a half', () => {
    expect(measure('智能体', 'zh', MEASURE)).toBe(3);
    expect(measure('AI 功能', 'zh', MEASURE)).toBe(3.5);
    expect(measure('需求（{{count}}）', 'zh', MEASURE)).toBe(5);
  });
});

describe('the glossary rules', () => {
  it('reports a label whose English names a term and whose Chinese says neither its name nor its forms', () => {
    const { en, zh } = catalogues({ 'area.new': ['New quest', '新建任务'] });
    const found = check(fixture(), en, zh);
    // One finding a rule and a language, naming every problem: here the missing name and the word it avoids.
    expect(rules(found, 'area.new')).toEqual(['glossary:zh']);
    expect(found[0]!.message).toMatch(/委托/);
    expect(found[0]!.message).toMatch(/任务/);
  });

  it("does not take a term's own name for a word it must not be called", () => {
    const { en, zh } = catalogues({ 'area.packs': ['Packs', '规范包'] });
    expect(rules(check(fixture(), en, zh), 'area.packs')).toEqual([]);
  });

  it('reports a code word in English and names the word the window says instead', () => {
    const { en, zh } = catalogues({ 'area.use': ['Use profile', '使用账户'] });
    const found = check(fixture(), en, zh).filter(({ key }) => key === 'area.use');
    expect(found.map(({ rule, language }) => `${rule}:${language}`)).toEqual(['glossary:en']);
    expect(found[0]!.message).toMatch(/account/);
  });

  it("still reports a word it must not be called when that word holds one of the term's short forms", () => {
    const { en, zh } = catalogues({
      'area.state.waiting': ['waiting on you', '等你决定'],
      'area.state.parked': ['waiting on you', '在等你'],
    });
    const found = check(fixture(), en, zh);
    expect(rules(found, 'area.state.waiting')).toEqual(['glossary:zh']);
    expect(rules(found, 'area.state.parked')).toEqual([]);
  });

  it('checks a sentence only when asked for every key, and then only for the words a term must not be called', () => {
    const { en, zh } = catalogues({
      'elsewhere.body': ['A quest waits.', '一个任务在等。'],
      'elsewhere.rephrased': ['Each account is listed.', '每个登录身份都列在这里。'],
    });
    expect(check(fixture(), en, zh)).toEqual([]);
    const all = check(fixture(), en, zh, { all: true });
    expect(rules(all, 'elsewhere.body')).toEqual(['glossary:zh']);
    expect(all[0]!.message).toMatch(/任务/);
    expect(all[0]!.message).not.toMatch(/nowhere/);
    // A sentence may say a thing its own way: no 账户 here, and nothing it must not say.
    expect(rules(all, 'elsewhere.rephrased')).toEqual([]);
  });
});

describe('the budgets', () => {
  it("reports a name over its kind's budget, in the language it is over in", () => {
    const { en, zh } = catalogues({
      'nav.long': ['Agents and accounts', '智能体'],
      'nav.wide': ['Agents', 'Daoris 自身的 AI'],
      'nav.fits': ['Quests', '委托'],
    });
    const found = check(fixture(), en, zh).filter(({ rule }) => rule === 'budget');
    expect(found.map(({ key, language }) => `${key}:${language}`).sort()).toEqual(['nav.long:en', 'nav.wide:zh']);
  });

  it('measures a palette row by its name, since the gloss after its dash is a sentence', () => {
    const { en, zh } = catalogues({
      'palette.browser': ["Open Daoris's browser — sign in where sessions will look", '打开 Daoris 浏览器——在会话会看的地方登录'],
      'palette.long': ['Open this session in a window of its very own — at once', '在一个完全属于它自己的单独窗口中打开该会话——立即'],
    });
    const found = check(fixture(), en, zh).filter(({ rule }) => rule === 'budget');
    expect(found.map(({ key, language }) => `${key}:${language}`).sort()).toEqual(['palette.long:en', 'palette.long:zh']);
  });
});

describe('the form rules', () => {
  it("holds English to its kind's case", () => {
    const { en, zh } = catalogues({
      'area.take': ['take', '接下'],
      'area.state.open': ['Open', '待接'],
      'area.state.daoris': ['Daoris runs it', 'Daoris 运行'],
      'area.title': ['Show The Whole Map', '显示整张地图'],
      'menu.proper': ['Open Daoris settings', '打开 Daoris 设置'],
    });
    const found = check(fixture(), en, zh).filter(({ rule }) => rule === 'form');
    expect(found.map(({ key }) => key).sort()).toEqual(['area.state.open', 'area.take', 'area.title']);
  });

  it('holds the marks: no closing stop, one ellipsis, no pronoun on a Chinese button, Latin set apart', () => {
    const { en, zh } = catalogues({
      'area.stop': ['Stop it.', '停止会话'],
      'area.dots': ['Choose...', '选择……'],
      'area.it': ['Register', '注册它'],
      'area.this': ['Trust this folder', '信任这个文件夹'],
      'area.tight': ['Add key', '添加API密钥'],
      'area.fine': ['Add API key', '添加 API 密钥'],
    });
    const found = check(fixture(), en, zh).filter(({ rule }) => rule === 'form');
    expect(found.map(({ key, language }) => `${key}:${language}`).sort()).toEqual([
      'area.dots:en', 'area.dots:zh', 'area.it:zh', 'area.stop:en', 'area.tight:zh',
    ]);
  });
});

describe('the doors', () => {
  it("reports a door that does not say its destination's name, in either language", () => {
    const named = catalogues({ 'menu.go': ['Open quests', '打开委托'], 'nav.quests': ['Quests', '委托'] });
    expect(rules(check(fixture(), named.en, named.zh), 'menu.go')).toEqual([]);
    const unnamed = catalogues({ 'menu.go': ['Open tasks', '打开任务'], 'nav.quests': ['Quests', '委托'] });
    expect(rules(check(fixture(), unnamed.en, unnamed.zh), 'menu.go')).toContain('door:en');
    expect(rules(check(fixture(), unnamed.en, unnamed.zh), 'menu.go')).toContain('door:zh');
  });
});

describe('a malformed glossary', () => {
  it('is a list of problems, since a glossary that cannot be read has stopped checking anything', () => {
    const broken = fixture();
    broken.kinds.menu!.keys.push('menu.gone');
    broken.kinds.status!.keys.push('area.*');
    broken.terms.push({ term: 'twin', en: 'Quest', zh: '委托', means: 'Again.', match: '(', avoid: { en: [], zh: [] } });
    broken.terms.push({ term: 'orphan', use: 'nothing', means: 'Points nowhere.', avoid: { en: [], zh: [] } });
    const { en, zh } = catalogues({ 'menu.go': ['Open quests', '打开委托'], 'nav.quests': ['Quests', '委托'] });
    const problems = validate(broken, en, zh).join('\n');
    expect(problems).toMatch(/menu\.gone/);
    expect(problems).toMatch(/area\.\*.*button.*status|area\.\*.*status.*button/);
    expect(problems).toMatch(/twin.*quest|quest.*twin/i);
    expect(problems).toMatch(/twin.*match/i);
    expect(problems).toMatch(/orphan.*nothing/);
  });
});

describe('the real glossary and catalogues', () => {
  const { glossary, en, zh } = load();

  it('read without a problem', () => {
    expect(validate(glossary, en, zh)).toEqual([]);
  });

  /**
   * NAME1b: the owner's examples, named as the owner approved them (2026-10-01): Settings' names were
   * translated, a door had a third name, and a heading named another act than the press under it.
   */
  it("name the owner's examples as approved, and the check finds nothing in them", () => {
    const found = check(glossary, en, zh);
    const approved: Record<string, [string, string]> = {
      'settings.domain.ai': ['AI features', 'AI 功能'],
      'settings.domain.agents': ['Agents', '智能体'],
      'menu.agents.tools': ['Agent settings', '智能体设置'],
      'settings.sync.title': ['Updates', '更新'],
      'settings.sync.look': ['Look for updates', '检查更新'],
    };
    for (const [key, [english, chinese]] of Object.entries(approved)) {
      expect([en[key], zh[key]], key).toEqual([english, chinese]);
      expect(rules(found, key), key).toEqual([]);
    }
    expect(rules(found, 'command.go.convergence')).toContain('door:zh');
  });

  it('report what they find and exit 0, and exit 1 under --strict until the renames land', () => {
    const script = join(process.cwd(), 'scripts', 'names-check.mjs');
    const reported = spawnSync(process.execPath, [script], { encoding: 'utf8' });
    expect(reported.status, reported.stderr).toBe(0);
    expect(reported.stdout).toMatch(/names-check: \d+ label keys/);
    const strict = spawnSync(process.execPath, [script, '--strict'], { encoding: 'utf8' });
    expect(strict.status).toBe(1);
    expect(report(check(glossary, en, zh))).toMatch(/budget/);
  });
});
