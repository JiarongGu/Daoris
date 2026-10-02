import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import {
  type Catalogue, type Finding, type Glossary, check, kindOf, load, measure, report, validate, verdict,
} from '../../scripts/names-check.mjs';

/**
 * The names check (NAME1a, D116): `scripts/names-check.mjs`, beside the parity check. NAME1b turned its
 * facts' half to a gate: `--strict` runs in the web's build and fails on a glossary, form or door finding;
 * the budgets report and never fail (`docs/2026-10-01-naming-design.md` §6).
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

  /**
   * NAME1b: *like a git remote* says 远程仓库, git remote's own name, which holds 远程, a word the workspace's
   * remote must not be called. A term the same English names is its own name, never a stray word.
   */
  it("does not take another term's name, where the English names that term too, for a stray word", () => {
    const glossary = fixture();
    glossary.terms.push(
      { term: 'remote', en: 'remote', zh: '远端', means: 'A deployment.', match: '\\bremotes?\\b', avoid: { en: [], zh: ['远程'] } },
      { term: 'git remote', en: 'git remote', zh: '远程仓库', means: 'Git\'s.', match: '\\bgit remotes?\\b', avoid: { en: [], zh: [] } },
    );
    const { en, zh } = catalogues({
      'elsewhere.like': ['Wiring, like a git remote.', '接线，如同 git 的远程仓库。'],
      'elsewhere.stray': ['Wired to a remote.', '已接到远程。'],
    });
    const all = check(glossary, en, zh, { all: true });
    expect(rules(all, 'elsewhere.like')).toEqual([]);
    expect(rules(all, 'elsewhere.stray')).toEqual(['glossary:zh']);
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

  /**
   * NAME2: a judgement looked at and kept is recorded in the glossary with its reason, and the report stops
   * repeating it; the acceptance holds the name it accepted, so a rename is judged again.
   */
  describe('accepted in the glossary', () => {
    const accepting = (names: NonNullable<Glossary['accepted']>['names']): Glossary => ({ ...fixture(), accepted: { names } });
    const budgets = (glossary: Glossary, entries: Record<string, [string, string]>) => {
      const { en, zh } = catalogues(entries);
      return check(glossary, en, zh).filter(({ rule }) => rule === 'budget');
    };

    it('is not reported, in the language it was accepted in, while the name is the one accepted', () => {
      const glossary = accepting([{ key: 'nav.long', en: 'Agents and accounts', why: 'The list wraps it.' }]);
      expect(budgets(glossary, { 'nav.long': ['Agents and accounts', 'Daoris 自身的 AI'] })
        .map(({ key, language }) => `${key}:${language}`)).toEqual(['nav.long:zh']);
    });

    it('covers a plural form that says the name it accepted', () => {
      const glossary = accepting([{ key: 'nav.long', en: 'Agents and accounts', why: 'The list wraps it.' }]);
      expect(budgets(glossary, {
        'nav.long': ['Agents and accounts', '智能体'],
        'nav.long_other': ['Agents and accounts', '智能体'],
      })).toEqual([]);
    });

    it('lapses once the name changes, and says what it had accepted', () => {
      const glossary = accepting([{ key: 'nav.long', en: 'Agents and accounts', why: 'The list wraps it.' }]);
      const found = budgets(glossary, { 'nav.long': ['Agents and their accounts', '智能体'] });
      expect(found.map(({ key, language }) => `${key}:${language}`)).toEqual(['nav.long:en']);
      expect(found[0]!.message).toMatch(/over the nav budget of 16/);
      expect(found[0]!.message).toMatch(/accepted as "Agents and accounts"/);
    });

    it('says an accepted name back within its budget can go, still as a report', () => {
      const glossary = { ...accepting([{ key: 'nav.long', en: 'Agents and accounts', why: 'The list wraps it.' }]) };
      glossary.kinds = { ...glossary.kinds, nav: kind({ en: 24, zh: 5 }, 'sentence', ['nav.*']) };
      const found = budgets(glossary, { 'nav.long': ['Agents and accounts', '智能体'] });
      expect(found.map(({ key, language }) => `${key}:${language}`)).toEqual(['nav.long:en']);
      expect(found[0]!.message).toMatch(/within/);
      expect(verdict(found, { strict: true })).toBe(0);
    });

    it('is malformed naming a key the catalogues lack, a name with no budget, no language, or no reason', () => {
      const glossary = accepting([
        { key: 'nav.gone', en: 'Gone', why: 'r' },
        { key: 'elsewhere.body', en: 'A sentence.', why: 'r' },
        { key: 'nav.long', why: 'r' },
        { key: 'nav.wide', zh: 'Daoris 自身的 AI', why: '' },
      ]);
      const { en, zh } = catalogues({
        'nav.long': ['Agents and accounts', '智能体'], 'nav.wide': ['Agents', 'Daoris 自身的 AI'], 'elsewhere.body': ['A sentence.', '一句话。'],
        'menu.go': ['Open quests', '打开委托'], 'nav.quests': ['Quests', '委托'],
      });
      const problems = validate(glossary, en, zh).join('\n');
      expect(problems).toMatch(/nav\.gone/);
      expect(problems).toMatch(/elsewhere\.body.*sentence/);
      expect(problems).toMatch(/nav\.long.*neither/);
      expect(problems).toMatch(/nav\.wide.*reason/);
    });

    it('is counted in the report, apart from what is still reported', () => {
      expect(report([], { labels: 3, accepted: 2 })).toMatch(/2 budget judgements accepted in the glossary/);
    });
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
      'command.go.convergence': ['Convergence', '同归'],
      // The owner's two calls: the view of repositories is named for them, and an ask is 需求.
      'nav.projects': ['Repositories', '仓库'],
      'asks.group': ['Asks ({{count}})', '需求（{{count}}）'],
    };
    for (const [key, [english, chinese]] of Object.entries(approved)) {
      expect([en[key], zh[key]], key).toEqual([english, chinese]);
      expect(rules(found, key), key).toEqual([]);
    }
  });

  /**
   * NAME2: DEV4 put a quest's lanes on the window (D115 §2.2) after NAME1's glossary was written, so the
   * concept had no term and the drawer's *Lanes* no kind. A lane is 泳道 wherever either language names it.
   */
  it('name a lane 泳道 wherever it is said, and read the drawer\'s Lanes as a field', () => {
    const lane = glossary.terms.find((term) => term.term === 'lane');
    expect([lane?.en, lane?.zh]).toEqual(['lane', '泳道']);
    expect(kindOf(glossary)('quests.detail.lanes')).toBe('field');
    const found = check(glossary, en, zh, { all: true });
    for (const key of ['quests.detail.lanes', 'quests.card.lanes', 'quests.card.lanes_other', 'quests.card.lanesHint']) {
      expect(zh[key], key).toMatch(/泳道/);
      expect(rules(found, key), key).toEqual([]);
    }
  });

  /**
   * NAME2, seen on the install: a plugin's switch read 关闭, close's word, so *Turn off* said *close* as much
   * as *turn off*, and an off plugin wore a closed quest's 已关闭. The glossary settles the pair, and the
   * check holds every label whose English turns a plugin on or off to it.
   */
  it("name a plugin's switch 启用 and 停用, never close's 关闭", () => {
    const byTerm = new Map(glossary.terms.map((term) => [term.term, term]));
    expect([byTerm.get('turn on')?.en, byTerm.get('turn on')?.zh]).toEqual(['turn on', '启用']);
    expect([byTerm.get('turn off')?.en, byTerm.get('turn off')?.zh]).toEqual(['turn off', '停用']);
    expect(byTerm.get('turn off')?.avoid.zh).toContain('关闭');
    const named: Record<string, [string, string]> = {
      'plugin.enable': ['Turn on', '启用'],
      'plugin.disable': ['Turn off', '停用'],
      'plugin.off': ['off', '已停用'],
      'plugin.group.on': ['On ({{count}})', '已启用（{{count}}）'],
      'plugin.group.off': ['Off ({{count}})', '已停用（{{count}}）'],
    };
    for (const [key, [english, chinese]] of Object.entries(named)) expect([en[key], zh[key]], key).toEqual([english, chinese]);
    expect(zh['plugin.enabled']).toMatch(/^\{\{id\}\} 已启用。/);
    expect(zh['plugin.disabled']).toMatch(/^\{\{id\}\} 已停用。/);
    const found = check(glossary, en, zh, { all: true });
    for (const key of [...Object.keys(named), 'plugin.enabled', 'plugin.disabled']) {
      expect(rules(found, key).filter((rule) => !rule.startsWith('budget')), key).toEqual([]);
    }
  });

  /**
   * NAME2 went through the budget judgements NAME1b left by kind: a shorter name where it says the same as
   * well, an acceptance with its reason where the length is the name, and the rest left for the window.
   */
  it('name the budget judgements NAME2 renamed by the shorter names that say the same', () => {
    const renamed: Record<string, [string, string]> = {
      'help.proposal.titleHand': ['Ask Daoris proposes a hand-off', '问道衍提议交接一个分支'],
      'help.proposal.titleSync': ['Ask Daoris proposes updates', '问道衍提议更新到最新'],
      'quests.groups.open': ['Open — waiting to be taken ({{count}})', '待接——等人接下（{{count}}）'],
      'work.intakeRunning.title': ['This intake is reading ask #{{ask}}', '受理会话正在读需求 #{{ask}}'],
      'scope.none': ['no workspace yet', '尚无工作区'],
      'harness.pin.missing': ['pinned {{version}} — not installed', '已固定 {{version}}——未安装'],
      'asks.record.anotherPlaceholder': ['any repository in workspace {{circle}}', '工作区 {{circle}} 中的任一仓库'],
    };
    for (const [key, [english, chinese]] of Object.entries(renamed)) expect([en[key], zh[key]], key).toEqual([english, chinese]);
    // A key nothing renders is no name to judge, so it went rather than being shortened.
    expect('harness.machineDefault' in en || 'harness.machineDefault' in zh).toBe(false);
  });

  it('leave on the report only the budget judgements the window must make', () => {
    const left = check(glossary, en, zh).filter(({ rule }) => rule === 'budget').map(({ key, language }) => `${key}:${language}`).sort();
    expect(left).toEqual([
      'help.setup:en', 'help.setup:zh',
      'scope.every:en', 'scope.every:zh',
      'signin.titleNew:en', 'signin.titleNew:zh',
      'work.group.noCheckout:en',
    ]);
  });

  /**
   * NAME1b turned the facts' half to a gate: the build runs `--strict` beside the parity check, and it
   * passes, because no label breaks the glossary, its form or its door. The budgets still report and never
   * gate (D54): a character count estimates a width, and the window is where a width is a fact.
   */
  it('find no glossary, form or door finding, so --strict passes, while the budgets still report', () => {
    const found = check(glossary, en, zh);
    expect(found.filter((finding) => finding.rule !== 'budget').map(({ key, rule, language }) => `${key} ${rule}:${language}`))
      .toEqual([]);
    expect(report(found)).toMatch(/budget/);

    const script = join(process.cwd(), 'scripts', 'names-check.mjs');
    const reported = spawnSync(process.execPath, [script], { encoding: 'utf8' });
    expect(reported.status, reported.stderr).toBe(0);
    expect(reported.stdout).toMatch(/names-check: \d+ label keys/);
    const strict = spawnSync(process.execPath, [script, '--strict'], { encoding: 'utf8' });
    expect(strict.status, strict.stdout).toBe(0);
  });

  it('are gated by the build: --strict runs beside the parity check, before the bundle is built', () => {
    const scripts = (JSON.parse(readFileSync(join(process.cwd(), 'package.json'), 'utf8')) as { scripts: Record<string, string> }).scripts;
    const build = scripts.build!.split('&&').map((step) => step.trim());
    expect(build).toContain('node scripts/names-check.mjs --strict');
    expect(build.indexOf('node scripts/names-check.mjs --strict')).toBeLessThan(build.findIndex((step) => step.startsWith('vite build')));
  });

  /**
   * A sentence's words are held only under `--all`, which reports; the eight it still finds are words a
   * sentence rightly says of something else (the audit's §5 lists them): a browser's or Windows' profile,
   * git's credential helper, a request's value no application accepts, a process that ended.
   */
  it("say in sentences only what the audit found to be another thing's word", () => {
    const stray = check(glossary, en, zh, { all: true })
      .filter((finding) => finding.rule === 'glossary' && finding.kind === 'sentence')
      .map(({ key, language }) => `${key}:${language}`).sort();
    expect(stray).toEqual([
      'errors.HARNESS_ACTION_IDLE:zh', 'errors.INVALID_PAYLOAD_VALUE:zh',
      'settings.browser.which.hintDaoris:en', 'settings.browser.which.hintEdge:en',
      'settings.home.hint:en', 'settings.home.hintOverridden:en', 'settings.home.hintThisStart:en',
      'settings.sync.notFetched.https:en',
    ]);
  });
});

describe('what the check answers', () => {
  const finding = (rule: Finding['rule']): Finding =>
    ({ key: 'k', kind: 'button', rule, language: 'en', message: 'm', value: 'v' });

  it('fails under --strict on a fact, and never on a budget, and never without --strict', () => {
    expect(verdict([finding('budget')], { strict: true })).toBe(0);
    for (const rule of ['glossary', 'form', 'door'] as const) {
      expect(verdict([finding('budget'), finding(rule)], { strict: true }), rule).toBe(1);
      expect(verdict([finding(rule)], { strict: false }), rule).toBe(0);
    }
    expect(verdict([], { strict: true })).toBe(0);
  });
});
