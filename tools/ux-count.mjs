#!/usr/bin/env node
/**
 * The screen counter (UX6a; docs/2026-10-04-ux6-redesign.md §9.1–§9.3, D150 point 9). One function counts a
 * screen in its regions, the view's list pane and its main area, and the frame apart, and it runs in two places
 * from one source:
 *
 *   node tools/ux-count.mjs --stories <id|prefix*>[,…] [--storybook <url>] [--looks en-light,zh-dark]
 *                           [--sizes 1546x1013,680x1013] [--json]
 *   node tools/ux-count.mjs --window [--out <script.js>]     write the script for `npm run desktop -- eval --file`
 *   node tools/ux-count.mjs --window --read <answer.json>…   read eval's answers, and print the counts
 *   node tools/ux-count.mjs --tasks                           §9.4's tasks: declared, not measured
 *
 * `--terms <file>` counts by a JSON list of `{ term, match, zh }` in place of the glossary's (the test's seam).
 *
 * THE MEASURES (§9.1), each per region:
 * - **Concepts**: the distinct glossary terms named in the region's visible text, read as the names check reads
 *   `glossary.json`: each term without `use`, by its `match` (case aside) in English and its `zh` in Chinese.
 *   `zhForms` is left out: it is the names check's leniency for a label whose English already names the term, and
 *   read forward over a screen its short forms find other words (接 is in 接口).
 * - **Controls**: the visible, innermost interactive elements: a link, a button, a field, a select, a summary, and
 *   what says it is one by its role (checkbox, radio, switch, tab, slider, combobox, menu item, option…). One that
 *   holds another counts as the one inside. A disabled control still shows, and counts.
 * - **Words**: `Intl.Segmenter`'s word-like segments of the visible text, in the screen's language; a number is a
 *   word-like segment. A field's value, else its placeholder, and a select's chosen option are visible text; an
 *   `aria-label` and a tip are not. English and 中文 are counted apart and never summed.
 * - **Screens**: the main area's content height over the part of it the window shows. A page with no main area is
 *   its own main area, and its screens are the page's.
 * - **Presses** are declared in `ux-count-tasks.json` and not measured: a task crosses screens, and its route is
 *   followed on the window.
 *
 * VISIBLE is what the engine draws (`checkVisibility`, opacity and visibility included) and is not clipped away:
 * an element that hides its overflow clips what it holds, so a clamped line's words, a closed section's rows and
 * a screen reader's one-pixel text are not counted. What a scroller holds past its edge is one scroll away and
 * counts, while the scroller itself shows. A closed menu's items are not drawn, so they are not counted.
 *
 * TWINS (`.claude/knowledge/twins.md`). The page's spellings are spelled here too, and the test reads the page's:
 * `data-region` and its `list`, `main` and `activity` (`work/regions.ts`, `ListPane`, `ViewMain`, `frame.tsx`),
 * `data-list-row` (`work/listKeys.ts`), the activity bar's `aria-current="page"`, and the language in
 * `daoris.language` and on `<html lang>` (`i18n.ts`).
 *
 * It starts neither Storybook nor the window. The stories are counted on a Storybook the caller started; the
 * window's script is written for `npm run desktop -- eval --file`, which the caller runs against the install.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { basename, dirname, extname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, writeAtomic } from './fsx.mjs';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const web = join(repoRoot, 'src', 'Daoris.Web');

/** The attribute a region of the window carries, naming it: the page's `REGION` (`work/regions.ts`). */
export const REGION = 'data-region';
/** The attribute a list's row carries: the page's `LIST_ROW` (`work/listKeys.ts`). */
export const LIST_ROW = 'data-list-row';
/** Where the page keeps its language: i18n's `lookupLocalStorage` (`i18n.ts`). */
export const LANGUAGE_KEY = 'daoris.language';

/** Where `npm --prefix src/Daoris.Web run storybook` serves. */
export const DEFAULT_STORYBOOK = 'http://localhost:6006';
/** §9.2: English light and 中文 dark. */
export const DEFAULT_LOOKS = 'en-light,zh-dark';
/** §9.2: 1546 and 680 px, at the install's height. */
export const DEFAULT_SIZES = '1546x1013,680x1013';
/** Where `--window` writes its script, in the repository's gitignored scratch. */
export const DEFAULT_SCRIPT = join('local', 'scratch', 'ux-count', 'window.js');

const GLOSSARY = join(web, 'src', 'locales', 'glossary.json');
const TASKS = join(repoRoot, 'tools', 'ux-count-tasks.json');

/**
 * The counter, page-side (§9.1). It is sent as source text, to Playwright's `page.evaluate` and to the window's
 * `desktop -- eval` alike (`countExpression`), so it reaches nothing outside its own body.
 *
 * Each region's answer is `{ concepts, terms, controls, words, rows }`, `rows` being the list rows it draws (a
 * story's fixture size). `list` is null where no list pane is drawn, and `frame` where nothing outside the list
 * and the main area is; the frame's `parts` are its regions by name, `unmarked` for what carries none. `place` is
 * the activity bar's current place, percent-encoded, so the answer is ASCII whatever console carries it.
 */
export function countScreen({ terms = [], language = null, root = null, region = 'data-region', listRow = 'data-list-row' } = {}) {
  const html = document.documentElement;
  const top = root ? document.querySelector(root) : document.body;
  if (!top) return { refused: `nothing on the page matches ${root}` };
  if (typeof top.checkVisibility !== 'function') return { refused: 'this engine cannot say what it draws (checkVisibility)' };

  const lang = /^zh/i.test(language ?? html.lang ?? '') ? 'zh' : 'en';
  const forced = html.dataset.theme;
  const theme = forced === 'light' || forced === 'dark' ? forced
    : matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';

  const styles = new Map();
  const style = (el) => {
    if (!styles.has(el)) styles.set(el, getComputedStyle(el));
    return styles.get(el);
  };
  const drawnOnes = new Map();
  const drawn = (el) => {
    if (!drawnOnes.has(el)) drawnOnes.set(el, el.checkVisibility({ opacityProperty: true, visibilityProperty: true }));
    return drawnOnes.get(el);
  };
  // A box with no box of its own (`display: contents`) is drawn where its parent is.
  const boxed = (el) => {
    while (el && el !== html && style(el).display === 'contents') el = el.parentElement;
    return el;
  };

  // More than a pixel of `a` inside `b`, both ways.
  const meets = (a, b) => Math.min(a.right, b.right) - Math.max(a.left, b.left) > 1
    && Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top) > 1;
  const within = (a, b) => a.left >= b.left - 0.5 && a.right <= b.right + 0.5 && a.top >= b.top - 0.5 && a.bottom <= b.bottom + 0.5;

  // What the page can be scrolled to show.
  const page = document.scrollingElement ?? html;
  const reach = { left: -scrollX, top: -scrollY, right: page.scrollWidth - scrollX, bottom: page.scrollHeight - scrollY };
  const SCROLLS = /^(auto|scroll|overlay)$/;
  const CLIPS = /^(hidden|clip)$/;
  const clips = new Map();
  // The rectangle an element's content is seen through, in the window's coordinates, or null where none is seen.
  const clipOf = (el) => {
    if (!el || el.nodeType !== 1 || el === html) return reach;
    if (clips.has(el)) return clips.get(el);
    const outer = clipOf(el.parentElement);
    const s = style(el);
    let box = outer;
    if (outer && s.display !== 'contents' && s.display !== 'inline') {
      const scrollsX = SCROLLS.test(s.overflowX);
      const scrollsY = SCROLLS.test(s.overflowY);
      const clipX = scrollsX || CLIPS.test(s.overflowX);
      const clipY = scrollsY || CLIPS.test(s.overflowY);
      if (clipX || clipY) {
        const r = el.getBoundingClientRect();
        const own = el.clientWidth || el.clientHeight
          ? {
            left: r.left + el.clientLeft, top: r.top + el.clientTop,
            right: r.left + el.clientLeft + el.clientWidth, bottom: r.top + el.clientTop + el.clientHeight,
          }
          : r;
        // A scroller shows what it holds along its axis while it shows itself; a clip cuts to its own box.
        box = !meets(own, outer) ? null : {
          left: scrollsX ? own.left - el.scrollLeft : clipX ? Math.max(outer.left, own.left) : outer.left,
          right: scrollsX ? own.left - el.scrollLeft + el.scrollWidth : clipX ? Math.min(outer.right, own.right) : outer.right,
          top: scrollsY ? own.top - el.scrollTop : clipY ? Math.max(outer.top, own.top) : outer.top,
          bottom: scrollsY ? own.top - el.scrollTop + el.scrollHeight : clipY ? Math.min(outer.bottom, own.bottom) : outer.bottom,
        };
        if (box && !(box.right - box.left > 1 && box.bottom - box.top > 1)) box = null;
      }
    }
    clips.set(el, box);
    return box;
  };
  const shown = (el) => {
    if (!drawn(el)) return false;
    const clip = clipOf(el.parentElement);
    return !!clip && [...el.getClientRects()].some((r) => meets(r, clip));
  };

  // Which region a node is in: the list, the main area, or a part of the frame.
  const mains = [...document.querySelectorAll(`[${region}="main"]`)]
    .filter((el) => (top.contains(el) || el.contains(top)) && drawn(el));
  const main = mains[0] ?? null;
  const tallies = new Map();
  const tally = (el) => {
    const name = el.closest(`[${region}]`)?.getAttribute(region) ?? (main ? 'unmarked' : 'main');
    if (!tallies.has(name)) tallies.set(name, { text: [], words: 0, controls: 0, rows: 0 });
    return tallies.get(name);
  };

  const segmenter = new Intl.Segmenter(lang, { granularity: 'word' });
  const wordsOf = (text) => [...segmenter.segment(text)].filter((s) => s.isWordLike).length;
  const range = document.createRange();

  // Words: each text node's segments, those its clip lets show.
  const walker = document.createTreeWalker(top, NodeFilter.SHOW_TEXT);
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    const text = node.data;
    if (!/\S/.test(text)) continue;
    const parent = boxed(node.parentElement);
    if (!parent || parent.closest('select, textarea') || !drawn(parent)) continue;
    const clip = clipOf(parent);
    if (!clip) continue;
    range.selectNodeContents(node);
    const rects = [...range.getClientRects()].filter((r) => r.width > 0 && r.height > 0);
    if (!rects.some((r) => meets(r, clip))) continue;
    const whole = rects.every((r) => within(r, clip));
    const into = tally(parent);
    let seen = '';
    for (const { segment, index, isWordLike } of segmenter.segment(text)) {
      if (!isWordLike) {
        seen += segment;
        continue;
      }
      let visible = whole;
      if (!visible) {
        range.setStart(node, index);
        range.setEnd(node, index + segment.length);
        visible = [...range.getClientRects()].some((r) => meets(r, clip));
      }
      if (visible) into.words += 1;
      seen += visible ? segment : ' ';
    }
    into.text.push(seen);
  }

  // A field's own text: its value, else its placeholder; a select's chosen option.
  const TEXTUAL = /^(text|search|email|url|tel|number)$/;
  for (const field of top.querySelectorAll('input, textarea, select')) {
    if (!shown(field)) continue;
    const type = (field.getAttribute('type') || 'text').toLowerCase();
    const says = field.tagName === 'SELECT' ? field.selectedOptions[0]?.text ?? ''
      : field.tagName === 'TEXTAREA' || TEXTUAL.test(type) ? field.value || field.placeholder || ''
        : /^(button|submit|reset)$/.test(type) ? field.value : '';
    if (!/\S/.test(says)) continue;
    const into = tally(field);
    into.words += wordsOf(says);
    into.text.push(says);
  }

  // Controls: the visible interactive elements, each counted unless a visible one is inside it.
  const CONTROL = [
    'a[href]', 'button', 'input:not([type="hidden"])', 'select', 'textarea', 'summary',
    '[contenteditable=""]', '[contenteditable="true"]',
    ...['button', 'link', 'checkbox', 'radio', 'switch', 'tab', 'slider', 'spinbutton', 'combobox', 'textbox',
      'searchbox', 'menuitem', 'menuitemcheckbox', 'menuitemradio', 'option', 'treeitem'].map((role) => `[role="${role}"]`),
  ].join(', ');
  const controls = [...top.querySelectorAll(CONTROL)].filter(shown);
  const holding = new Set(controls);
  const outerOnes = new Set();
  for (const el of controls) {
    for (let a = el.parentElement; a && a !== top.parentElement; a = a.parentElement) if (holding.has(a)) outerOnes.add(a);
  }
  for (const el of controls) if (!outerOnes.has(el)) tally(el).controls += 1;

  for (const row of top.querySelectorAll(`[${listRow}]`)) if (shown(row)) tally(row).rows += 1;

  // Concepts: the terms each region's visible text names.
  const namers = terms.map(({ term, match, zh }) => {
    if (lang === 'zh') return { term, says: (text) => !!zh && text.includes(zh) };
    let pattern = null;
    try {
      pattern = new RegExp(match, 'i');
    } catch {
      pattern = null; // the names check refuses a glossary whose match does not compile
    }
    return { term, says: (text) => !!pattern && pattern.test(text) };
  });
  const measured = (t) => {
    const text = t.text.join('\n');
    const said = namers.filter(({ says }) => says(text)).map(({ term }) => term).sort();
    return { concepts: said.length, terms: said, controls: t.controls, words: t.words, rows: t.rows };
  };
  const nothing = { text: [], words: 0, controls: 0, rows: 0 };

  const listShown = [...document.querySelectorAll(`[${region}="list"]`)]
    .some((el) => (top.contains(el) || el.contains(top)) && drawn(el));
  const parts = [...tallies.keys()].filter((name) => name !== 'list' && name !== 'main').sort();
  // The frame is its parts summed, its concepts those any part names.
  let frame = null;
  if (parts.length) {
    const each = Object.fromEntries(parts.map((name) => [name, measured(tallies.get(name))]));
    const of = Object.values(each);
    const union = [...new Set(of.flatMap((p) => p.terms))].sort();
    const sum = (key) => of.reduce((total, p) => total + p[key], 0);
    frame = {
      concepts: union.length, terms: union, controls: sum('controls'), words: sum('words'), rows: sum('rows'), parts: each,
    };
  }

  // Screens: the main area's content over the part of it the window shows. With none in what is counted, what is
  // counted is the main area: the page, or the root named.
  const scrollOf = (el) => {
    const r = el.getBoundingClientRect();
    const inView = Math.max(0, Math.min(r.bottom, innerHeight) - Math.max(r.top, 0));
    return { content: el.scrollHeight, visible: Math.min(el.clientHeight, inView) };
  };
  const scroll = main ? scrollOf(main)
    : top === document.body ? { content: page.scrollHeight, visible: innerHeight } : scrollOf(top);
  const screens = scroll.visible > 0 ? Math.round((scroll.content / scroll.visible) * 100) / 100 : null;

  const place = document.querySelector(`[${region}="activity"] [aria-current="page"]`)?.getAttribute('aria-label') ?? null;
  return {
    language: lang,
    theme,
    viewport: { width: innerWidth, height: innerHeight },
    place: place === null ? null : encodeURIComponent(place),
    mainFrom: main ? 'region' : top === document.body ? 'page' : 'root',
    list: listShown ? measured(tallies.get('list') ?? nothing) : null,
    main: { ...measured(tallies.get('main') ?? nothing), screens, scroll },
    frame,
  };
}

/** The expression both places evaluate: the counter's own source, called with what it counts by. */
export function countExpression({ terms = [], language = null, root = null } = {}) {
  return `(${countScreen})(${JSON.stringify({ terms, language, root, region: REGION, listRow: LIST_ROW })})`;
}

/** The glossary's terms as the names check reads them (`names-check.mjs`): each without `use`, its match and zh. */
export function glossaryTerms(glossary) {
  return glossary.terms
    .filter((term) => !term.use)
    .map(({ term, match, zh }) => ({ term, match, zh }));
}

/** The terms to count by: the glossary's, or a file's list of `{ term, match, zh }`. */
function readTerms(file) {
  if (file) return JSON.parse(readFileSync(file, 'utf8'));
  return glossaryTerms(JSON.parse(readFileSync(GLOSSARY, 'utf8')));
}

/** A refusal: said whole, and exits 2. */
function refusal(message) {
  const error = new Error(message);
  error.refusal = true;
  return error;
}

/** `en-light,zh-dark` as looks. */
export function parseLooks(text) {
  return String(text).split(',').map((name) => {
    const found = /^(en|zh)-(light|dark)$/.exec(name.trim());
    if (!found) throw refusal(`a look is <en|zh>-<light|dark>: ${name}`);
    return { name: name.trim(), language: found[1], theme: found[2] };
  });
}

/** `1546x1013,680x1013` as sizes. */
export function parseSizes(text) {
  return String(text).split(',').map((size) => {
    const found = /^(\d+)[x×](\d+)$/.exec(size.trim());
    if (!found) throw refusal(`a size is <width>x<height>: ${size}`);
    return { width: Number(found[1]), height: Number(found[2]) };
  });
}

/** Playwright's chromium, the web's own (its e2e's); null where the web's packages are not installed. */
export function loadChromium() {
  const require = createRequire(join(web, 'package.json'));
  for (const name of ['@playwright/test', 'playwright']) {
    try {
      return require(name).chromium;
    } catch {
      // the next, or none
    }
  }
  return null;
}

/** The stories a Storybook serves, from its `index.json`; a docs entry is no story. */
export async function storyIndex(storybook) {
  const url = new URL('index.json', storybook.endsWith('/') ? storybook : `${storybook}/`).href;
  let response;
  try {
    response = await fetch(url, { signal: AbortSignal.timeout(10_000) });
  } catch {
    throw refusal(`nothing answers at ${url}: start Storybook first (npm --prefix src/Daoris.Web run storybook), `
      + 'or name where it runs with --storybook <url>.');
  }
  if (!response.ok) throw refusal(`${url} answered ${response.status}: is a Storybook there?`);
  const index = await response.json();
  return {
    url,
    stories: Object.values(index.entries ?? index.stories ?? {})
      .filter((entry) => (entry.type ?? 'story') === 'story')
      .map(({ id, title, name }) => ({ id, title, name })),
  };
}

/** The ids asked for, held to the index: an id it lists, or a prefix ending `*` naming every story under it. */
export function expandStories(asked, stories, where) {
  const ids = [];
  for (const one of asked) {
    if (one.endsWith('*')) {
      const prefix = one.slice(0, -1);
      const under = stories.filter((story) => story.id.startsWith(prefix)).map((story) => story.id);
      if (!under.length) throw refusal(`no story starts ${prefix} in ${where}.`);
      ids.push(...under);
    } else if (stories.some((story) => story.id === one)) {
      ids.push(one);
    } else {
      const component = one.split('--')[0];
      const near = stories.filter((story) => story.id.startsWith(`${component}--`)).map((story) => story.id.split('--')[1]);
      throw refusal(`no story ${one} in ${where}${near.length ? `; under ${component}: ${near.join(', ')}` : ''}.`);
    }
  }
  return [...new Set(ids)];
}

/**
 * Count each story in each look at each size, on a Storybook the caller started (§9.2). A look's language is put
 * where the page reads it, its theme in the scheme the page follows; the page's own answer says what it took, and
 * a look it did not take is a warning on its row. A story that did not draw is a refusal on its row.
 */
export async function countStories({
  storybook = DEFAULT_STORYBOOK, stories, looks = parseLooks(DEFAULT_LOOKS), sizes = parseSizes(DEFAULT_SIZES),
  terms, chromium = loadChromium(), settleMs = 300,
}) {
  const index = await storyIndex(storybook);
  const ids = expandStories(stories, index.stories, index.url);
  if (!chromium) {
    throw refusal('Playwright is the web\'s, and is not installed: npm --prefix src/Daoris.Web ci installs it.');
  }
  const base = index.url.replace(/index\.json$/, '');
  const expression = countExpression({ terms });
  const browser = await chromium.launch();
  const rows = [];
  try {
    for (const look of looks) {
      for (const { width, height } of sizes) {
        const context = await browser.newContext({
          viewport: { width, height },
          colorScheme: look.theme,
          locale: look.language === 'zh' ? 'zh-CN' : 'en-US',
        });
        await context.addInitScript(([key, value]) => {
          try {
            localStorage.setItem(key, value);
          } catch {
            // a page with no store reads the browser's language, which the context set too
          }
        }, [LANGUAGE_KEY, look.language]);
        const page = await context.newPage();
        for (const id of ids) {
          const row = { screen: id, look: look.name, size: `${width}x${height}` };
          await page.goto(`${base}iframe.html?id=${encodeURIComponent(id)}&viewMode=story`);
          await page.waitForFunction(
            () => /\bsb-show-(main|errordisplay|nopreview)\b/.test(document.body.className), null, { timeout: 60_000 });
          const failed = await page.evaluate(() => (document.body.classList.contains('sb-show-main') ? null
            : (document.querySelector('#error-message')?.textContent || 'Storybook shows no preview').trim()));
          if (failed) {
            rows.push({ ...row, refused: `the story did not draw: ${failed}` });
            continue;
          }
          await page.waitForLoadState('networkidle').catch(() => {});
          await page.evaluate(() => document.fonts?.ready.then(() => true));
          if (settleMs) await page.waitForTimeout(settleMs);
          const counts = await page.evaluate(expression);
          if (counts.refused) {
            rows.push({ ...row, refused: counts.refused });
            continue;
          }
          counts.place = counts.place === null ? null : decodeURIComponent(counts.place);
          const warnings = [];
          if (counts.language !== look.language) warnings.push(`the page spoke ${counts.language} where the look asked ${look.language}`);
          if (counts.theme !== look.theme) warnings.push(`the page was ${counts.theme} where the look asked ${look.theme}`);
          rows.push({ ...row, counts, warnings });
        }
        await context.close();
      }
    }
  } finally {
    await browser.close();
  }
  return rows;
}

/**
 * Write the window's script, atomically, and say the two commands that take a screen's counts with it. The window
 * is never started here: the caller runs `eval` against the install `npm run desktop -- run --install` started.
 * `renameOptions` are `writeAtomic`'s, for a test to hold the file.
 */
export function writeWindowScript({ out = DEFAULT_SCRIPT, terms }, renameOptions = {}) {
  const script = resolve(repoRoot, out);
  const text = countExpression({ terms });
  writeAtomic(script, text, renameOptions);
  const shown = (path) => relative(repoRoot, path).replaceAll('\\', '/');
  const answer = join(dirname(script), '<screen>.json');
  return [
    `The counter's script is written: ${shown(script)} (${(Buffer.byteLength(text) / 1024).toFixed(1)} KB).`,
    'It counts the screen the window shows: in the window\'s language and theme, at its size.',
    'With the install started by `npm run desktop -- run --install <dir>`, open a screen, then:',
    `  npm run --silent desktop -- eval --file ${shown(script)} > ${shown(answer)}`,
    `  node tools/ux-count.mjs --window --read ${shown(answer)}`,
  ].join('\n');
}

/**
 * Eval's answer, as the shell between wrote it: UTF-16 or UTF-8, a byte-order mark or none, and npm's banner above
 * the JSON where `--silent` was not said. Throws, with the file's first line, where it holds no answer.
 */
export function readAnswer(bytes) {
  const text = bytes[0] === 0xff && bytes[1] === 0xfe ? bytes.subarray(2).toString('utf16le')
    : bytes.length > 1 && bytes[0] !== 0 && bytes[1] === 0 ? bytes.toString('utf16le')
      : bytes.toString('utf8').replace(/^﻿/, '');
  const start = text.search(/^\s*\{/m);
  if (start === -1) throw refusal(`holds no answer: ${text.trim().split(/\r?\n/)[0] ?? ''}`);
  const answer = JSON.parse(text.slice(start));
  if (typeof answer.place === 'string') answer.place = decodeURIComponent(answer.place);
  return answer;
}

/**
 * The answer files named, a `*` in a file's name matching any run of characters in its folder, sorted. Windows
 * PowerShell hands a native command its wildcard as typed, so the tool expands it. One that matches nothing is
 * refused.
 */
export function expandFiles(paths) {
  return paths.flatMap((path) => {
    const name = basename(path);
    if (!name.includes('*')) return [path];
    const folder = dirname(path);
    const pattern = new RegExp(`^${name.split('*').map((part) => part.replace(/[.+?^${}()|[\]\\]/g, '\\$&')).join('.*')}$`, 'i');
    let found = [];
    try {
      found = readdirSync(folder).filter((entry) => pattern.test(entry)).sort();
    } catch {
      found = [];
    }
    if (!found.length) throw refusal(`no file matches ${path}.`);
    return found.map((entry) => join(folder, entry));
  });
}

/** The counts as a table: a line for each region of each screen, the frame's parts beneath it. */
export function renderRows(rows) {
  const head = ['screen', 'look', 'size', 'region', 'concepts', 'controls', 'words', 'screens', 'rows'];
  const lines = [];
  for (const row of rows) {
    const lead = [row.screen, row.look, row.size];
    if (row.refused) {
      lines.push([...lead, `refused: ${row.refused}`]);
      continue;
    }
    const { list, main, frame } = row.counts;
    const line = (name, counts, screens = '') =>
      [...lead, name, counts.concepts, counts.controls, counts.words, screens, counts.rows].map(String);
    if (list) lines.push(line('list', list));
    lines.push(line('main', main, main.screens ?? '—'));
    if (frame) {
      lines.push(line('frame', frame));
      for (const [part, counts] of Object.entries(frame.parts)) lines.push(line(`  ${part}`, counts));
    }
  }
  // A refusal's sentence is its row's last cell, and widens no column; its screen, look and size still align.
  const widths = head.map((name, at) => Math.max(name.length,
    ...lines.filter((cells) => at < 3 || cells.length === head.length).map((cells) => cells[at].length)));
  const numeric = new Set([4, 5, 6, 7, 8]);
  const align = (cells) => cells.map((cell, at) => (at === cells.length - 1 ? cell
    : numeric.has(at) ? cell.padStart(widths[at]) : cell.padEnd(widths[at]))).join('  ');
  return [align(head), ...lines.map(align)].join('\n');
}

/** What each count's region named and what the page said beside it, under the table. */
function renderNotes(rows) {
  const notes = [];
  for (const row of rows) {
    if (row.refused) continue;
    const at = `${row.screen} ${row.look} ${row.size}`;
    const { counts } = row;
    const said = [`${counts.language}, ${counts.theme}`, `${counts.viewport.width}x${counts.viewport.height}`];
    if (counts.place) said.push(`place ${counts.place}`);
    if (counts.mainFrom === 'page') said.push('no main area: the page counted as one');
    notes.push(`${at}: ${said.join('; ')}`);
    for (const [name, region] of [['list', counts.list], ['main', counts.main], ['frame', counts.frame]]) {
      if (region?.terms.length) notes.push(`  ${name} names ${region.terms.join(', ')}`);
    }
    for (const warning of row.warnings ?? []) notes.push(`  ⚠ ${warning}`);
  }
  return notes.join('\n');
}

/** §9.4's tasks, as declared. */
export function readTasks(file = TASKS) {
  return JSON.parse(readFileSync(file, 'utf8')).tasks;
}

function renderTasks(tasks) {
  const lines = ['§9.4\'s tasks. Presses are counted on the window by following each route; none is measured here.'];
  for (const { task, before, after, measured } of tasks) {
    lines.push('', task, `  before: ${before}`, `  after:  ${after}`,
      `  ${measured ? `measured: ${measured.presses} presses, ${measured.scanned} rows scanned, ${measured.on}` : 'not measured'}`);
  }
  return lines.join('\n');
}

function usage() {
  return readFileSync(fileURLToPath(import.meta.url), 'utf8')
    .split('\n')
    .slice(6, 13)
    .map((line) => line.replace(/^ \*\/?/, '').replace(/^ /, ''))
    .join('\n');
}

/** The value after a flag, or null; a flag with nothing after it is refused. */
function flag(args, name) {
  const at = args.indexOf(name);
  if (at === -1) return null;
  const value = args[at + 1];
  if (value === undefined || value.startsWith('--')) throw refusal(`${name} needs a value.`);
  args.splice(at, 2);
  return value;
}

/** Every value after a flag, up to the next flag. */
function values(args, name) {
  const at = args.indexOf(name);
  if (at === -1) return null;
  let end = at + 1;
  while (end < args.length && !args[end].startsWith('--')) end += 1;
  const taken = args.slice(at + 1, end);
  args.splice(at, end - at);
  return taken;
}

export async function main(argv) {
  const args = [...argv];
  const json = args.includes('--json');
  if (json) args.splice(args.indexOf('--json'), 1);
  const terms = readTerms(flag(args, '--terms'));

  if (args.includes('--tasks')) {
    console.log(renderTasks(readTasks()));
    return 0;
  }

  if (args.includes('--window')) {
    args.splice(args.indexOf('--window'), 1);
    const read = values(args, '--read');
    if (read) {
      if (!read.length) throw refusal('--read needs the answers eval wrote.');
      const rows = expandFiles(read).map((file) => {
        const row = { screen: basename(file, extname(file)) };
        let answer;
        try {
          answer = readAnswer(readFileSync(file));
        } catch (error) {
          return { ...row, look: '', size: '', refused: `${file} ${error.message}` };
        }
        if (answer.refused) return { ...row, look: '', size: '', refused: answer.refused };
        return {
          ...row, look: `${answer.language}-${answer.theme}`, size: `${answer.viewport.width}x${answer.viewport.height}`,
          counts: answer, warnings: [],
        };
      });
      console.log(json ? JSON.stringify(rows, null, 2) : `${renderRows(rows)}\n\n${renderNotes(rows)}`);
      return rows.some((row) => row.refused) ? 1 : 0;
    }
    console.log(writeWindowScript({ out: flag(args, '--out') ?? DEFAULT_SCRIPT, terms }));
    return 0;
  }

  const stories = values(args, '--stories');
  if (stories) {
    const storybook = flag(args, '--storybook') ?? DEFAULT_STORYBOOK;
    const looks = parseLooks(flag(args, '--looks') ?? DEFAULT_LOOKS);
    const sizes = parseSizes(flag(args, '--sizes') ?? DEFAULT_SIZES);
    const ids = stories.flatMap((one) => one.split(',')).map((one) => one.trim()).filter(Boolean);
    if (!ids.length) throw refusal('--stories needs a story id, or a prefix ending *.');
    const rows = await countStories({ storybook, stories: ids, looks, sizes, terms });
    console.log(json ? JSON.stringify(rows, null, 2) : `${renderRows(rows)}\n\n${renderNotes(rows)}`);
    return rows.some((row) => row.refused) ? 1 : 0;
  }

  console.log(usage());
  return args.includes('--help') ? 0 : 2;
}

if (isMain(import.meta.url)) {
  try {
    process.exitCode = await main(process.argv.slice(2));
  } catch (error) {
    console.error(error.refusal ? error.message : error.stack);
    process.exitCode = 2;
  }
}
