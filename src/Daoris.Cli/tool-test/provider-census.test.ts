import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * AGENTPROVIDER1 row a, part 3 (the agent-provider design §9, D213): the tools' twin of the driver's `ProviderCensusTests`. Every site
 * in the repository's tools (`tools/`) and in the CLI's doctrine code (`src/Daoris.Cli/src/`) that names a provider's id, variable, file
 * or wire key is counted over the source with its comments blanked, and each is a named exception in the `tools` section of the driver's
 * `fixtures/provider-census.json` with its exact count, the design's row that moves it and its class. The ids and the vocabulary are read
 * from `fixtures/provider-vocabulary.json`, the one table the four censuses share; nothing here copies a word of it. Run by the `tool-tests`
 * gate (the merge tool's `REACH` rows send both fixtures here).
 */

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const fixtures = join(root, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'fixtures');

/** The design's rows (§11) that can remove a site; `-` is a site no row removes. */
const ROWS = ['-', 'b', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't'];
const CLASSES = ['behaviour', 'permanent', 'words', 'measurement'];

type Row = [file: string, token: string, count: number, row: string, kind: string, why: string];
interface Section { roots: string[]; exceptions: Row[] }
interface Token { name: string; pattern: RegExp }

const census = JSON.parse(readFileSync(join(fixtures, 'provider-census.json'), 'utf8')) as { tools: Section };
const vocabulary = JSON.parse(readFileSync(join(fixtures, 'provider-vocabulary.json'), 'utf8')) as {
  ids: [string, string][]; words: [string, string, string][];
};
const section = census.tools;

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

const SKIPPED = new Set(['node_modules', 'bin', 'obj', 'dist', 'out', 'coverage']);

/** Every production code file under the section's roots: tests, fixtures and recorded data are no source. */
function sources(): [string, string][] {
  const files: [string, string][] = [];
  const walk = (folder: string): void => {
    for (const entry of readdirSync(folder, { withFileTypes: true })) {
      const path = join(folder, entry.name);
      if (entry.isDirectory()) {
        if (!SKIPPED.has(entry.name) && !entry.name.endsWith('-fixtures')) walk(path);
      } else if (/\.(mjs|cjs|js|ts)$/.test(entry.name) && !/\.test\.(mjs|js|ts)$/.test(entry.name)) {
        files.push([relative(root, path).replaceAll('\\', '/'), readFileSync(path, 'utf8').replaceAll('\r\n', '\n')]);
      }
    }
  };
  for (const each of section.roots) walk(join(root, each));
  return files;
}

const baseline = count(sources());

test('every site in a tool that knows a provider is a named exception with the row that moves it', () => {
  const rows = [...baseline.entries()].sort(([a], [b]) => (a < b ? -1 : 1)).flatMap(([file, sites]) =>
    [...sites.entries()].sort(([a], [b]) => (a < b ? -1 : 1)).map(([token, n]) => `["${file}", ${JSON.stringify(token)}, ${n}]`));
  const found = problems(baseline, section.exceptions);
  assert.equal(found.length, 0, `${found.length} problem(s):\n${found.join('\n')}\n\nthe sites found, as fixture rows:\n${rows.join('\n')}`);
});

test("the fixture's tools section names each exception once, in order, with its row, class and reason", () => {
  const known = new Set(tokens.map((token) => token.name));
  const keys = section.exceptions.map((row) => `${row[0]}\n${row[1]}`);
  assert.deepEqual(keys, [...keys].sort((a, b) => (a < b ? -1 : 1)), 'rows are in ordinal order of file, then token');
  assert.equal(new Set(keys).size, keys.length, 'a file and token once');
  for (const [file, token, n, row, kind, why] of section.exceptions) {
    assert.ok(ROWS.includes(row), `${file}: ${token} names row ${row}, which the design has none of`);
    assert.ok(CLASSES.includes(kind), `${file}: ${token} has class ${kind}`);
    assert.ok(n > 0 && why.length > 0, `${file}: ${token} says no reason`);
    assert.ok(known.has(token), `${file}: ${token} is no token of the census`);
    assert.ok(existsSync(join(root, file)), `${file} is no file`);
    // Only a behaviour is removed by a row; a permanent site, an id in words and a measurement stay.
    assert.equal(row === '-', kind !== 'behaviour', `${file}: ${token} is ${kind} under row ${row}`);
  }
});

test('the census reads its ids and its vocabulary from the shared table, a word for each', () => {
  assert.ok(vocabulary.ids.length >= 9 && vocabulary.words.length >= 15);
  for (const [word] of vocabulary.words) assert.ok(tokens.some((token) => token.name === word), `${word} is no token`);
  for (const [id] of vocabulary.ids) assert.ok(tokens.some((token) => token.name === `"${id}"`), `${id} is no token`);
});

for (const [file, source, token] of [
  ['tools/Planted.mjs', "process.env.CLAUDE_CONFIG_DIR = home;", 'CLAUDE_CONFIG_DIR'],
  ['tools/Planted.mjs', "if (adapter === 'codex-acp') return;", '"codex-acp"'],
  ['tools/fabric-probe/Planted.mjs', 'if (agent.startsWith("claude")) return;', 'StartsWith(agent)'],
  ['tools/Planted.mjs', 'const wire = { key: `thread_active_writer` };', 'thread_active_writer'],
  ['src/Daoris.Cli/src/layout/Planted.ts', "const home = process.env['CODEX_HOME'];", 'CODEX_HOME'],
] as const) {
  test(`a site planted in ${file} is named by the census: ${token}`, () => {
    const found = problems(count([[file, source]]), section.exceptions, false);
    assert.ok(found.length > 0 && found.every((each) => each.startsWith(`${file}:`)) && found.some((each) => each.includes(token)),
      `expected ${token} named in ${file}, got: ${found.join(' | ')}`);
  });
}

test('a second site in a file that holds a named one, and a named one gone, are both named: the counts are exact', () => {
  const [file, token, n] = section.exceptions[0]!;
  const more = new Map([...baseline].map(([name, sites]) => [name, new Map(sites)]));
  more.get(file)!.set(token, n + 1);
  const fewer = new Map([...baseline].map(([name, sites]) => [name, new Map(sites)]));
  fewer.get(file)!.delete(token);
  assert.ok(problems(more, section.exceptions).includes(`${file}: ${token} found ${n + 1}, the fixture names ${n}`));
  assert.ok(problems(fewer, section.exceptions).includes(`${file}: ${token} is no site now: take it off the fixture`));
});

test('a sentence in a comment, a URL in a string and a regular expression naming a provider are read rightly', () => {
  const found = count([['tools/Words.mjs',
    "// CLAUDE_CONFIG_DIR and 'codex-acp'\nconst url = 'https://example.test/a'; /* CODEX_HOME */\nconst pattern = /\\/\\/ [/]codex/g; // thread_active_writer"]]);
  assert.equal(found.size, 0, JSON.stringify([...found]));
});

test('a template literal keeps its text and its nested expressions, and a comment after it is blanked', () => {
  const found = count([['tools/Template.mjs', "const a = `x ${y('CODEX_HOME')} z`; // DSH_HOME\nconst b = 1; /* ANTHROPIC_MODEL */"]]);
  assert.deepEqual([...(found.get('tools/Template.mjs') ?? [])], [['CODEX_HOME', 1]]);
});
