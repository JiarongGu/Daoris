/**
 * The screen counter (UX6a, docs/2026-10-04-ux6-redesign.md §9.1–§9.3):
 *
 *   node --test tools/ux-count.test.mjs
 *
 * Outside `npm run verify`, whose tests are the CLI package's, as the benches' are. The counting runs in
 * Playwright's chromium, the web's own (`src/Daoris.Web`, installed by its `npm ci`), over the fixed pages in
 * `ux-count-fixtures/`, whose counts are known by hand. Where that browser is not installed here, the cases that
 * need it are skipped and say why. Nothing here starts Storybook or the window: the runner is driven against a
 * stand-in Storybook served from the fixtures, and the window's path is the same expression sent through
 * `cdp.mjs`, the client `desktop.mjs eval` sends it through. Its scratch is a gitignored folder of the repository.
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { dirname, join } from 'node:path';
import { after, before, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { Cdp, freePort, targetsAt } from './cdp.mjs';
import { evalExpression } from './desktop.mjs';
import {
  LANGUAGE_KEY, LIST_ROW, REGION, countExpression, countStories, expandFiles, expandStories, glossaryTerms, loadChromium,
  parseLooks, parseSizes, readAnswer, readTasks, renderRows, storyIndex, writeWindowScript,
} from './ux-count.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const root = dirname(here);
const fixtures = join(here, 'ux-count-fixtures');
const scratch = join(root, 'local', 'scratch', 'ux-count-test');
const web = join(root, 'src', 'Daoris.Web');
const fileUrl = (name) => new URL(`file:///${join(fixtures, name).replaceAll('\\', '/')}`).href;

/** The terms the fixed pages are counted by: each says where it is, or why it is not counted there. */
const TERMS = [
  { term: 'session', match: '\\bsessions?\\b', zh: '会话' },
  { term: 'quest', match: '\\bquests?\\b', zh: '委托' }, // the zh page draws it nowhere
  { term: 'workspace', match: '\\bworkspaces?\\b', zh: '工作区' },
  { term: 'repository', match: '\\brepositor(y|ies)\\b', zh: '仓库' }, // a placeholder's
  { term: 'overview', match: '\\boverview\\b', zh: '总览' }, // only an aria-label: no text
  { term: 'settings', match: '\\bsettings\\b', zh: '设置' },
  { term: 'hold', match: '\\bhold\\b', zh: '暂停' }, // only in a folded section
  { term: 'ghost', match: '\\bghost\\b', zh: '幽灵' }, // only at opacity 0
  { term: 'delete', match: '\\bdelete\\b', zh: '删除' }, // only in a closed menu
];

const none = { concepts: 0, terms: [], controls: 0, words: 0, rows: 0 };

/** page.html, counted by hand: see its comments. */
const PAGE = {
  language: 'en',
  theme: 'light',
  viewport: { width: 1546, height: 1013 },
  place: 'Overview',
  mainFrom: 'region',
  list: { concepts: 1, terms: ['repository'], controls: 4, words: 6, rows: 3 },
  main: {
    concepts: 3, terms: ['quest', 'session', 'workspace'], controls: 8, words: 23, rows: 0,
    screens: 2.5, scroll: { content: 1000, visible: 400 },
  },
  frame: {
    concepts: 1, terms: ['session'], controls: 5, words: 11, rows: 0,
    parts: {
      unmarked: { ...none, controls: 1, words: 2 },
      activity: { ...none, controls: 3, words: 1 },
      side: { ...none, words: 2 },
      status: { concepts: 1, terms: ['session'], controls: 1, words: 6, rows: 0 },
    },
  },
};

/** zh.html, counted by hand: no regions, so the page is the main area. */
const ZH = {
  language: 'zh',
  theme: 'dark',
  viewport: { width: 1546, height: 1013 },
  place: null,
  mainFrom: 'page',
  list: null,
  main: {
    concepts: 2, terms: ['session', 'settings'], controls: 1, words: 4, rows: 0,
    screens: 2, scroll: { content: 2026, visible: 1013 },
  },
  frame: null,
};

// The browser is the web's (its e2e runs on it); this file adds none. Without it the counting cases are skipped,
// each saying so, and the rest still run.
const chromium = loadChromium();
const noBrowser = chromium ? false
  : 'Playwright is not installed under src/Daoris.Web (npm --prefix src/Daoris.Web ci installs it)';
let browser = null;

before(async () => {
  rmSync(scratch, { recursive: true, force: true });
  mkdirSync(scratch, { recursive: true });
  if (chromium) browser = await chromium.launch();
});
after(async () => {
  await browser?.close();
  rmSync(scratch, { recursive: true, force: true });
});

/** The answer for a fixed page, at a size and in a scheme, through Playwright's evaluate. */
async function counted(name, { width = 1546, height = 1013, colorScheme = 'light', options = {} } = {}) {
  const context = await browser.newContext({ viewport: { width, height }, colorScheme });
  try {
    const page = await context.newPage();
    await page.goto(fileUrl(name));
    return await page.evaluate(countExpression({ terms: TERMS, ...options }));
  } finally {
    await context.close();
  }
}

test('the glossary is read as the names check reads it: each term without `use`, its match and its zh', () => {
  const glossary = JSON.parse(readFileSync(join(web, 'src', 'locales', 'glossary.json'), 'utf8'));
  const terms = glossaryTerms(glossary);
  const named = glossary.terms.filter((term) => !term.use);
  assert.equal(terms.length, named.length);
  assert.ok(terms.length > 100, 'the real glossary names over a hundred terms');
  assert.deepEqual(terms.map((term) => term.term), named.map((term) => term.term));
  for (const term of terms) {
    assert.deepEqual(Object.keys(term), ['term', 'match', 'zh'], `${term.term} carries what the count reads`);
    new RegExp(term.match, 'i');
  }
  // A short Chinese form (zhForms: 接 for take) is the names check's leniency for a label that already names the
  // term in English. Read forward over a screen it would find 接口, so the counter reads `zh` alone.
  const take = terms.find((term) => term.term === 'take');
  assert.deepEqual(take, { term: 'take', match: named.find((term) => term.term === 'take').match, zh: '接下' });
  // The window's answer names terms by their ids, ASCII, so no console's code page can garble them.
  assert.ok(terms.every((term) => /^[\x20-\x7e]+$/.test(term.term)));
});

test('the spellings the counter reads are the page’s own', () => {
  const read = (path) => readFileSync(join(web, 'src', ...path.split('/')), 'utf8');
  assert.equal(REGION, 'data-region');
  assert.match(read('work/regions.ts'), new RegExp(`export const REGION = '${REGION}';`));
  assert.match(read('work/ListPane.tsx'), new RegExp(`${REGION}="list"`));
  assert.match(read('work/ViewMain.tsx'), new RegExp(`${REGION}="main"`));
  assert.match(read('work/frame.tsx'), new RegExp(`${REGION}="activity"`));
  assert.match(read('work/frame.tsx'), /aria-current=\{active === tab \? 'page' : undefined\}/);
  assert.equal(LIST_ROW, 'data-list-row');
  assert.match(read('work/listKeys.ts'), new RegExp(`export const LIST_ROW = '${LIST_ROW}';`));
  assert.equal(LANGUAGE_KEY, 'daoris.language');
  assert.match(read('i18n.ts'), new RegExp(`lookupLocalStorage: '${LANGUAGE_KEY.replace('.', '\\.')}'`));
  assert.match(read('i18n.ts'), /document\.documentElement\.lang = i18n\.language;/);
});

test('a window in miniature counts as it was counted by hand, region by region', { skip: noBrowser }, async () => {
  assert.deepEqual(await counted('page.html'), PAGE);
});

test('a page with no main area is the main area whole, and its screens are the page’s', { skip: noBrowser }, async () => {
  assert.deepEqual(await counted('zh.html', { colorScheme: 'dark' }), ZH);
});

test('the language is the one asked, else the page’s, and each is counted in its own words', { skip: noBrowser }, async () => {
  // Asked to read the Chinese page as English: no term's match is in it, and its words are still its words.
  const asEnglish = await counted('zh.html', { options: { language: 'en' } });
  assert.equal(asEnglish.language, 'en');
  assert.deepEqual(asEnglish.main.terms, []);
  assert.equal(asEnglish.main.words, 4);
  // And the English page read as Chinese names none of the Chinese terms.
  const asChinese = await counted('page.html', { options: { language: 'zh' } });
  assert.equal(asChinese.language, 'zh');
  assert.deepEqual([asChinese.list.terms, asChinese.main.terms, asChinese.frame.terms], [[], [], []]);
});

test('a narrower window holds the same words and controls', { skip: noBrowser }, async () => {
  const narrow = await counted('page.html', { width: 680 });
  assert.deepEqual(narrow.viewport, { width: 680, height: 1013 });
  assert.deepEqual(
    [narrow.list, narrow.main.words, narrow.main.controls, narrow.frame.words],
    [PAGE.list, PAGE.main.words, PAGE.main.controls, PAGE.frame.words]);
});

test('a root names the part of the page counted, and one that matches nothing is refused', { skip: noBrowser }, async () => {
  // The list alone: no main area in it, so the root is the main area, holding nothing of its own, its screens its own.
  const listed = await counted('page.html', { options: { root: 'aside[data-region="list"]' } });
  assert.deepEqual(listed.list, PAGE.list);
  assert.equal(listed.mainFrom, 'root');
  assert.deepEqual(listed.main, { ...none, screens: 1, scroll: { content: 400, visible: 400 } });
  assert.equal(listed.frame, null);
  assert.deepEqual(await counted('page.html', { options: { root: '#nothing' } }), { refused: 'nothing on the page matches #nothing' });
});

test('the window’s path gives the same answer: the same expression, through the client eval uses', { skip: noBrowser }, async () => {
  const port = await freePort(9480);
  const debuggable = await chromium.launch({ args: [`--remote-debugging-port=${port}`] });
  try {
    const context = await debuggable.newContext({ viewport: { width: 1546, height: 1013 }, colorScheme: 'light' });
    const page = await context.newPage();
    await page.goto(fileUrl('page.html'));

    // `--window` writes the script, here by the test's terms so the counts by hand hold; `desktop.mjs eval --file`
    // reads it back whole.
    const script = join(scratch, 'window.js');
    const terms = join(scratch, 'terms.json');
    writeFileSync(terms, JSON.stringify(TERMS));
    const written = spawnSync(process.execPath, [join(here, 'ux-count.mjs'), '--window', '--out', script, '--terms', terms],
      { encoding: 'utf8' });
    assert.equal(written.status, 0, written.stderr);
    assert.match(written.stdout, /npm run --silent desktop -- eval --file .*window\.js > .*<screen>\.json/);
    assert.match(written.stdout, /node tools\/ux-count\.mjs --window --read .*<screen>\.json/);
    const expression = evalExpression(['--file', script]);
    assert.equal(expression, readFileSync(script, 'utf8'));

    const target = (await targetsAt(port)).find((t) => t.type === 'page' && t.url.endsWith('page.html'));
    assert.ok(target, 'the fixed page is a target on the debug port');
    const cdp = await new Cdp(target.webSocketDebuggerUrl).open();
    try {
      const overTheWire = await cdp.evaluate(expression);
      assert.deepEqual({ ...overTheWire, place: decodeURIComponent(overTheWire.place) }, PAGE);
    } finally {
      cdp.close();
    }
  } finally {
    await debuggable.close();
  }
});

// REFAC2: the script was renamed into place bare, so a scanner holding the last one failed `--window` outright.
test('the window’s script is written whole through a held rename, and a refused one leaves the last script as it was', () => {
  const out = join(scratch, 'held', 'window.js');
  const calls = [];
  const said = writeWindowScript({ out, terms: TERMS }, {
    waitMs: 1,
    rename: (from, to) => {
      calls.push(from);
      if (calls.length <= 2) throw Object.assign(new Error('EPERM: operation not permitted, rename'), { code: 'EPERM' });
      renameSync(from, to);
    },
  });
  assert.equal(calls.length, 3, 'tried again while it was held');
  assert.equal(readFileSync(out, 'utf8'), countExpression({ terms: TERMS }));
  assert.deepEqual(readdirSync(dirname(out)), ['window.js'], 'nothing left beside it');
  assert.match(said, /The counter's script is written: /);

  const refused = () => { throw Object.assign(new Error('EXDEV: cross-device link not permitted, rename'), { code: 'EXDEV' }); };
  assert.throws(() => writeWindowScript({ out, terms: [] }, { rename: refused }), /EXDEV/);
  assert.equal(readFileSync(out, 'utf8'), countExpression({ terms: TERMS }), 'the last script stands');
  assert.deepEqual(readdirSync(dirname(out)), ['window.js']);
});

test('eval takes a script from a file, whole, or its words as before', () => {
  const file = join(scratch, 'plain.js');
  writeFileSync(file, '﻿(() => "a \\"quoted\\" 100% & | < > ^ answer")()\n');
  assert.equal(evalExpression(['--file', file]), '(() => "a \\"quoted\\" 100% & | < > ^ answer")()\n');
  assert.equal(evalExpression(['document', '.title']), 'document .title');
  assert.equal(evalExpression([]), null);
  assert.equal(evalExpression(['--file']), null);
  assert.equal(evalExpression(['--file', file, 'more']), null);
  assert.throws(() => evalExpression(['--file', join(scratch, 'absent.js')]), /cannot read .*absent\.js/);
});

test('eval’s answer is read through npm’s banner and whichever encoding the shell wrote', () => {
  const answer = { ...PAGE, place: encodeURIComponent('总览') };
  const json = JSON.stringify(answer, null, 2);
  const banner = '\n> daoris-workspace@0.0.0 desktop\n> node tools/desktop.mjs eval --file x.js\n\n';
  const forms = [
    Buffer.from(json, 'utf8'),
    Buffer.concat([Buffer.from([0xef, 0xbb, 0xbf]), Buffer.from(banner + json, 'utf8')]),
    Buffer.concat([Buffer.from([0xff, 0xfe]), Buffer.from(banner + json, 'utf16le')]),
    Buffer.from(json + '\r\n', 'utf16le'),
  ];
  for (const bytes of forms) assert.deepEqual(readAnswer(bytes), { ...PAGE, place: '总览' });
  assert.deepEqual(readAnswer(Buffer.from('{ "refused": "nothing on the page matches #x" }')), { refused: 'nothing on the page matches #x' });
  assert.throws(() => readAnswer(Buffer.from('the expression threw: boom\n')), /holds no answer: the expression threw: boom/);
});

test('--window --read prints each answer as a screen named by its file, and one with no answer as refused', () => {
  // As Windows PowerShell's `>` writes eval's output: UTF-16 with its mark, npm's banner above the JSON.
  const overview = join(scratch, 'overview.json');
  const banner = '\n> daoris-workspace@0.0.0 desktop\n> node tools/desktop.mjs eval --file x.js\n\n';
  writeFileSync(overview, Buffer.concat([Buffer.from([0xff, 0xfe]), Buffer.from(banner + JSON.stringify(PAGE, null, 2), 'utf16le')]));
  const read = spawnSync(process.execPath, [join(here, 'ux-count.mjs'), '--window', '--read', overview], { encoding: 'utf8' });
  assert.equal(read.status, 0, read.stderr);
  assert.match(read.stdout, /overview\s+en-light\s+1546x1013\s+main\s+3\s+8\s+23\s+2\.5\s+0/);
  assert.match(read.stdout, /overview en-light 1546x1013: en, light; 1546x1013; place Overview/);
  assert.match(read.stdout, /main names quest, session, workspace/);

  const threw = join(scratch, 'threw.json');
  writeFileSync(threw, 'the expression threw: boom\n');
  const refused = spawnSync(process.execPath, [join(here, 'ux-count.mjs'), '--window', '--read', overview, threw], { encoding: 'utf8' });
  assert.equal(refused.status, 1);
  assert.match(refused.stdout, /threw\s+refused: .*threw\.json holds no answer: the expression threw: boom/);
});

test('a * in an answer’s file name is expanded by the tool, since Windows PowerShell hands it over as typed', () => {
  const folder = join(scratch, 'answers');
  mkdirSync(folder, { recursive: true });
  for (const name of ['settings.json', 'overview.json', 'notes.txt']) writeFileSync(join(folder, name), '{}');
  assert.deepEqual(expandFiles([join(folder, '*.json')]), [join(folder, 'overview.json'), join(folder, 'settings.json')]);
  assert.deepEqual(expandFiles([join(folder, 'notes.txt')]), [join(folder, 'notes.txt')]);
  assert.throws(() => expandFiles([join(folder, '*.png')]), /no file matches .*\*\.png/);
});

test('the looks and the sizes are read as written, and anything else is refused', () => {
  assert.deepEqual(parseLooks('en-light,zh-dark'), [
    { name: 'en-light', language: 'en', theme: 'light' },
    { name: 'zh-dark', language: 'zh', theme: 'dark' },
  ]);
  assert.throws(() => parseLooks('fr-light'), /a look is <en\|zh>-<light\|dark>: fr-light/);
  assert.deepEqual(parseSizes('1546x1013,680×1013'), [{ width: 1546, height: 1013 }, { width: 680, height: 1013 }]);
  assert.throws(() => parseSizes('1546'), /a size is <width>x<height>: 1546/);
});

test('story ids are held to the index, and a prefix with * names every story under it', () => {
  const index = [
    { id: 'repositories-projectpage--driven' }, { id: 'repositories-projectpage--held' },
    { id: 'repositories-workspacepage--setup' },
  ];
  assert.deepEqual(expandStories(['repositories-projectpage--*', 'repositories-workspacepage--setup'], index, 'X'), [
    'repositories-projectpage--driven', 'repositories-projectpage--held', 'repositories-workspacepage--setup',
  ]);
  assert.throws(() => expandStories(['repositories-projectpage--gone'], index, 'X/index.json'),
    /no story repositories-projectpage--gone in X\/index\.json; under repositories-projectpage: driven, held/);
  assert.throws(() => expandStories(['agents-*'], index, 'X/index.json'), /no story starts agents- in X\/index\.json/);
});

test('the tasks are declared, and none is measured', () => {
  const tasks = readTasks();
  assert.equal(tasks.length, 6);
  assert.ok(tasks.every((task) => task.task && task.before && task.after && task.measured === null));
  const printed = spawnSync(process.execPath, [join(here, 'ux-count.mjs'), '--tasks'], { encoding: 'utf8' });
  assert.equal(printed.status, 0, printed.stderr);
  assert.match(printed.stdout, /Publish an ask its declarations proposed/);
  assert.match(printed.stdout, /not measured/);
});

test('the table prints each region of each screen, the frame’s parts beneath it, and a refusal whole', () => {
  const text = renderRows([
    { screen: 'fixture', look: 'en-light', size: '1546x1013', counts: PAGE },
    { screen: 'broken', look: 'en-light', size: '1546x1013', refused: 'the story did not draw: The story threw.' },
  ]);
  const lines = text.split('\n');
  assert.match(lines[0], /^screen\s+look\s+size\s+region\s+concepts\s+controls\s+words\s+screens\s+rows$/);
  assert.match(text, /fixture\s+en-light\s+1546x1013\s+list\s+1\s+4\s+6\s+3/);
  assert.match(text, /fixture\s+en-light\s+1546x1013\s+main\s+3\s+8\s+23\s+2\.5\s+0/);
  assert.match(text, /fixture\s+en-light\s+1546x1013\s+frame\s+1\s+5\s+11\s+0/);
  assert.match(text, /fixture\s+en-light\s+1546x1013\s+ {2}activity\s+0\s+3\s+1\s+0/);
  assert.match(text, /broken\s+en-light\s+1546x1013\s+refused: the story did not draw: The story threw\./);
});

/** A stand-in Storybook: its index, and its iframe drawing the stand-in story by id. */
async function standIn() {
  const story = readFileSync(join(fixtures, 'story.html'));
  const index = {
    v: 5,
    entries: {
      'fixture--counted': { type: 'story', id: 'fixture--counted', title: 'Fixture', name: 'Counted' },
      'fixture--broken': { type: 'story', id: 'fixture--broken', title: 'Fixture', name: 'Broken' },
      'fixture--docs': { type: 'docs', id: 'fixture--docs', title: 'Fixture', name: 'Docs' },
    },
  };
  const server = createServer((request, response) => {
    const { pathname } = new URL(request.url, 'http://x');
    if (pathname === '/index.json') {
      response.writeHead(200, { 'content-type': 'application/json' }).end(JSON.stringify(index));
    } else if (pathname === '/iframe.html') {
      response.writeHead(200, { 'content-type': 'text/html; charset=utf-8' }).end(story);
    } else {
      response.writeHead(404).end();
    }
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  return { url: `http://127.0.0.1:${server.address().port}`, close: () => new Promise((resolve) => server.close(resolve)) };
}

test('the runner counts each story in each look at each size, against a Storybook the caller started', { skip: noBrowser }, async () => {
  const storybook = await standIn();
  try {
    // A docs entry is no story.
    const index = await storyIndex(storybook.url);
    assert.equal(index.url, `${storybook.url}/index.json`);
    assert.deepEqual(index.stories.map((story) => story.id), ['fixture--counted', 'fixture--broken']);

    const rows = await countStories({
      storybook: storybook.url,
      stories: ['fixture--counted', 'fixture--broken'],
      looks: parseLooks('en-light,zh-dark'),
      sizes: parseSizes('1546x1013,680x1013'),
      terms: TERMS,
      chromium,
      settleMs: 0,
    });
    assert.deepEqual(rows.map(({ screen, look, size }) => `${screen} ${look} ${size}`), [
      'fixture--counted en-light 1546x1013', 'fixture--broken en-light 1546x1013',
      'fixture--counted en-light 680x1013', 'fixture--broken en-light 680x1013',
      'fixture--counted zh-dark 1546x1013', 'fixture--broken zh-dark 1546x1013',
      'fixture--counted zh-dark 680x1013', 'fixture--broken zh-dark 680x1013',
    ]);
    for (const row of rows.filter((r) => r.screen === 'fixture--broken')) {
      assert.equal(row.refused, 'the story did not draw: The story threw.');
    }
    for (const row of rows.filter((r) => r.screen === 'fixture--counted')) {
      const [language, theme] = row.look.split('-');
      assert.deepEqual(row.warnings, []);
      assert.equal(row.counts.language, language);
      assert.equal(row.counts.theme, theme);
      assert.equal(`${row.counts.viewport.width}x${row.counts.viewport.height}`, row.size);
      assert.deepEqual(row.counts.list, null);
      assert.deepEqual(row.counts.frame, null);
      assert.deepEqual(
        { ...row.counts.main, scroll: undefined },
        { concepts: 2, terms: ['session', 'settings'], controls: 1, words: 2, rows: 0, screens: 1, scroll: undefined });
    }

    await assert.rejects(
      countStories({ storybook: storybook.url, stories: ['fixture--gone'], terms: TERMS, chromium }),
      /no story fixture--gone in http:\/\/127\.0\.0\.1:\d+\/index\.json; under fixture: counted, broken/);
  } finally {
    await storybook.close();
  }
});

test('a Storybook nobody started is said, with how to start one', async () => {
  const port = await freePort(9560);
  await assert.rejects(
    countStories({ storybook: `http://127.0.0.1:${port}`, stories: ['x--y'], terms: TERMS, chromium: null }),
    new RegExp(`nothing answers at http://127\\.0\\.0\\.1:${port}/index\\.json: start Storybook first `
      + '\\(npm --prefix src/Daoris\\.Web run storybook\\), or name where it runs with --storybook <url>'));
});
