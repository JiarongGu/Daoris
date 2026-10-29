import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join, relative } from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * AGENTPROVIDER1 row a, part 3 (the agent-provider design §9, D213): the page's twin of the driver's `ProviderCensusTests`. Every site in
 * the page's production sources that names a provider's id, variable, file or wire key is counted over the source with its comments
 * blanked, and each is a named exception in the `page` section of the driver's `fixtures/provider-census.json`, with its exact count,
 * the design's row that moves it (s: the page reads the sheet) and its class. The ids and the vocabulary are read from
 * `fixtures/provider-vocabulary.json`, the one table the four censuses share; nothing here copies a word of it. Tests, the test
 * support in `src/test/` and the translations are no production source.
 */

const root = join(process.cwd(), '..', '..');
const fixtures = join(root, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'fixtures');

/** The design's rows (§11) that can remove a site; `-` is a site no row removes. */
const ROWS = ['-', 'b', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't'];
const CLASSES = ['behaviour', 'permanent', 'words', 'measurement'];

type Row = [file: string, token: string, count: number, row: string, kind: string, why: string];
interface Section { roots: string[]; exceptions: Row[] }
interface Token { name: string; pattern: RegExp }

const census = JSON.parse(readFileSync(join(fixtures, 'provider-census.json'), 'utf8')) as { page: Section };
const vocabulary = JSON.parse(readFileSync(join(fixtures, 'provider-vocabulary.json'), 'utf8')) as {
  ids: [string, string][]; words: [string, string, string][];
};
const section = census.page;

const escape = (text: string): string => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

const tokens: Token[] = [
  ...vocabulary.ids.map(([id]) => ({ name: `"${id}"`, pattern: new RegExp(`["'\`]${escape(id)}["'\`]`, 'g') })),
  { name: 'StartsWith(agent)', pattern: /\.startsWith\(\s*["'`](?:claude|codex|dsh)/g },
  // An id as an example or a name in a sentence (help, a refusal): words, but still a site that names a provider.
  { name: 'id in a sentence', pattern: /(?<![\w@/.-])(?:claude-code-acp|claude-code|codex-acp|acp-stub|alt-stub)(?![\w"'`-])/g },
  ...vocabulary.words.map(([word]) => {
    const plain = /^[A-Za-z0-9_]+$/.test(word) && !word.startsWith('_') && !word.endsWith('_');
    return { name: word, pattern: new RegExp(plain ? `(?<![A-Za-z0-9_])${escape(word)}(?![A-Za-z0-9_])` : escape(word), 'g') };
  }),
];

/** A source's text with its comments blanked and its strings kept, every offset and line kept. */
function stripComments(text: string): string {
  let out = '';
  let i = 0;
  const copy = (count = 1): void => { out += text.slice(i, i + count); i += count; };
  const blank = (to: number): void => { for (; i < to; i++) out += text[i] === '\n' ? '\n' : ' '; };
  const code = (stopAtBrace: boolean): void => {
    let depth = 0;
    let last = '(';
    let word = '';
    while (i < text.length) {
      const c = text[i]!;
      const next = text[i + 1];
      if (c === '/' && next === '/') {
        const end = text.indexOf('\n', i);
        blank(end < 0 ? text.length : end);
      } else if (c === '/' && next === '*') {
        const end = text.indexOf('*/', i + 2);
        blank(end < 0 ? text.length : end + 2);
      } else if (c === '"' || c === "'") {
        copy();
        while (i < text.length && text[i] !== c && text[i] !== '\n') copy(text[i] === '\\' ? 2 : 1);
        if (text[i] === c) copy();
        last = '"';
        word = '';
      } else if (c === '`') {
        copy();
        while (i < text.length && text[i] !== '`') {
          if (text[i] === '\\') copy(2);
          else if (text[i] === '$' && text[i + 1] === '{') { copy(2); code(true); if (text[i] === '}') copy(); } else copy();
        }
        if (text[i] === '`') copy();
        last = '"';
        word = '';
      } else if (c === '/' && ('(,=:[!&|?{};+-*%~^'.includes(last) || word === 'return' || word === 'typeof')) {
        // A regular expression literal: one line, a class's slash not its end.
        copy();
        let inClass = false;
        while (i < text.length && text[i] !== '\n' && (inClass || text[i] !== '/')) {
          if (text[i] === '\\') { copy(2); continue; }
          if (text[i] === '[') inClass = true;
          else if (text[i] === ']') inClass = false;
          copy();
        }
        if (text[i] === '/') copy();
        last = '"';
        word = '';
      } else {
        if (c === '{') depth++;
        if (c === '}') {
          if (stopAtBrace && depth === 0) return;
          depth--;
        }
        if (/[\w$]/.test(c)) word += c;
        else if (!/\s/.test(c)) word = '';
        if (!/\s/.test(c)) last = c;
        copy();
      }
    }
  };
  code(false);
  return out;
}

/** Every site of every source: file, then token, then how many times. */
function count(sources: Iterable<[file: string, source: string]>): Map<string, Map<string, number>> {
  const found = new Map<string, Map<string, number>>();
  for (const [file, source] of sources) {
    const text = stripComments(source);
    for (const token of tokens) {
      const sites = text.match(token.pattern)?.length ?? 0;
      if (sites === 0) continue;
      if (!found.has(file)) found.set(file, new Map());
      found.get(file)!.set(token.name, sites);
    }
  }
  return found;
}

/** The problems a census holds against the fixture, in order; none is a passing census. */
function problems(found: Map<string, Map<string, number>>, rows: Row[], exact = true): string[] {
  const named = new Map(rows.map((row) => [`${row[0]}\n${row[1]}`, row[2]]));
  const out: string[] = [];
  for (const file of [...found.keys()].sort()) {
    const sites = found.get(file)!;
    for (const token of [...sites.keys()].sort()) {
      const at = named.get(`${file}\n${token}`);
      if (at === undefined) out.push(`${file}: unnamed site ${token} x${sites.get(token)}`);
      else if (at !== sites.get(token)) out.push(`${file}: ${token} found ${sites.get(token)}, the fixture names ${at}`);
    }
  }

  if (exact) {
    // An allowance nothing needs is taken back, so the table stays the sites that are true.
    for (const [file, token] of rows) {
      if (!found.get(file!)?.has(token!)) out.push(`${file}: ${token} is no site now: take it off the fixture`);
    }
  }

  return out;
}

const SKIPPED = new Set(['node_modules', 'dist', 'coverage', 'locales', 'test']);

/** Every production source of the page: tests, the test support and the translations are none. */
function sources(): [string, string][] {
  const files: [string, string][] = [];
  const walk = (folder: string): void => {
    for (const entry of readdirSync(folder, { withFileTypes: true })) {
      const path = join(folder, entry.name);
      if (entry.isDirectory()) {
        if (!SKIPPED.has(entry.name)) walk(path);
      } else if (/\.(ts|tsx)$/.test(entry.name) && !/\.test\.(ts|tsx)$/.test(entry.name)) {
        files.push([relative(root, path).replaceAll('\\', '/'), readFileSync(path, 'utf8').replaceAll('\r\n', '\n')]);
      }
    }
  };
  for (const each of section.roots) walk(join(root, each));
  return files;
}

const baseline = count(sources());

describe('the page names no provider but in a named exception', () => {
  it('has every site that knows a provider as a named exception with the row that moves it', () => {
    const rows = [...baseline.entries()].sort(([a], [b]) => (a < b ? -1 : 1)).flatMap(([file, sites]) =>
      [...sites.entries()].sort(([a], [b]) => (a < b ? -1 : 1)).map(([token, n]) => `["${file}", ${JSON.stringify(token)}, ${n}]`));
    const found = problems(baseline, section.exceptions);
    expect(found, `${found.length} problem(s):\n${found.join('\n')}\n\nthe sites found, as fixture rows:\n${rows.join('\n')}`).toEqual([]);
  });

  it("holds the fixture's page section to one row per file and token, in order, with its row, class and reason", () => {
    const known = new Set(tokens.map((token) => token.name));
    const keys = section.exceptions.map((row) => `${row[0]}\n${row[1]}`);
    expect(keys).toEqual([...keys].sort((a, b) => (a < b ? -1 : 1)));
    expect(new Set(keys).size).toBe(keys.length);
    for (const [file, token, n, row, kind, why] of section.exceptions) {
      expect(ROWS, `${file}: ${token} names row ${row}`).toContain(row);
      expect(CLASSES, `${file}: ${token} has class ${kind}`).toContain(kind);
      expect(n > 0 && why.length > 0, `${file}: ${token} says no reason`).toBe(true);
      expect(known.has(token), `${file}: ${token} is no token of the census`).toBe(true);
      expect(existsSync(join(root, file)), `${file} is no file`).toBe(true);
      // Only a behaviour is removed by a row; a permanent site, an id in words and a measurement stay.
      expect(row === '-', `${file}: ${token} is ${kind} under row ${row}`).toBe(kind !== 'behaviour');
    }
  });

  it('reads its ids and its vocabulary from the shared table, a word for each', () => {
    expect(vocabulary.ids.length).toBeGreaterThanOrEqual(9);
    expect(vocabulary.words.length).toBeGreaterThanOrEqual(15);
    for (const [word] of vocabulary.words) expect(tokens.some((token) => token.name === word), `${word} is no token`).toBe(true);
    for (const [id] of vocabulary.ids) expect(tokens.some((token) => token.name === `"${id}"`), `${id} is no token`).toBe(true);
  });

  it.each([
    ['src/Daoris.Web/src/work/Planted.tsx', "if (agent === 'claude-code') return null;", '"claude-code"'],
    ['src/Daoris.Web/src/work/Planted.tsx', 'const home = env.CLAUDE_CONFIG_DIR;', 'CLAUDE_CONFIG_DIR'],
    ['src/Daoris.Web/src/work/Planted.tsx', 'const key = `thread_active_writer`;', 'thread_active_writer'],
    ['src/Daoris.Web/src/agents/Planted.ts', 'return adapter.startsWith("codex");', 'StartsWith(agent)'],
    ['src/Daoris.Web/src/settings/Planted.tsx', "if (door === 'codex-acp') return 1;", '"codex-acp"'],
  ])('names a site planted in %s: %s', (file, source, token) => {
    const found = problems(count([[file, source]]), section.exceptions, false);
    expect(found.length > 0 && found.every((each) => each.startsWith(`${file}:`)) && found.some((each) => each.includes(token)),
      `expected ${token} named in ${file}, got: ${found.join(' | ')}`).toBe(true);
  });

  it('names a second site in a named file and a named one gone: the counts are exact', () => {
    const [file, token, n] = section.exceptions[0]!;
    const more = new Map([...baseline].map(([name, sites]) => [name, new Map(sites)]));
    more.get(file)!.set(token, n + 1);
    const fewer = new Map([...baseline].map(([name, sites]) => [name, new Map(sites)]));
    fewer.get(file)!.delete(token);
    expect(problems(more, section.exceptions)).toContain(`${file}: ${token} found ${n + 1}, the fixture names ${n}`);
    expect(problems(fewer, section.exceptions)).toContain(`${file}: ${token} is no site now: take it off the fixture`);
  });

  it('reads a comment, a URL in a string, a regular expression and a template literal rightly', () => {
    expect(count([['src/Daoris.Web/src/Words.ts',
      "// CLAUDE_CONFIG_DIR and 'codex-acp'\nconst url = 'https://example.test/a'; /* CODEX_HOME */\nconst pattern = /\\/\\/ [/]codex/g; // thread_active_writer"]]).size).toBe(0);
    const found = count([['src/Daoris.Web/src/Template.ts', "const a = `x ${y('CODEX_HOME')} z`; // DSH_HOME\nconst b = 1; /* ANTHROPIC_MODEL */"]]);
    expect([...(found.get('src/Daoris.Web/src/Template.ts') ?? [])]).toEqual([['CODEX_HOME', 1]]);
  });
});
