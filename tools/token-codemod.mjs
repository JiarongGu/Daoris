/**
 * The token codemod (UIDS1, D330): the web sources move from the old tokens' names to the role set's, by whole class.
 *
 *   node tools/token-codemod.mjs [--check] [--aliases]
 *
 * Re-runnable, and kept until UIDS18 deletes the aliases: a web branch that lands after UIDS1 runs it after merging main, and
 * a second run changes nothing. It reads `src/Daoris.Web/src`, `.storybook` and `measured` (`.ts`, `.tsx`, `.css`, `.mdx`),
 * leaves `tokens.css`, which declares the names, and rewrites in place:
 *
 * - a status colour to its role (`ink-danger` → `fail-ink`, `st-declined` → `fail`, `ink-done` → `ok-ink`, `st-done` → `ok`,
 *   `ink-taken` → `live-ink`, `st-taken` → `live`), in any utility, behind any variant, with its opacity and in a variable;
 *   a light field of a hue (`bg-st-done/10`, up to 20%) to its soft token, and `bg-accent/10` to `bg-accent-soft`; a token
 *   read by its bare name (`token('st-done')`, the terminal's theme) to its role's;
 * - a word in the accent (`text-accent`, a stylesheet's `color: var(--accent)`) to `accent-ink`, and a word on the solid
 *   accent (`text-accent` or the old `text-accent-ink` in a class string that holds `bg-accent`) to `on-accent`; a mark,
 *   border, ring or outline keeps the accent;
 * - a type step to its new name (`meta` → `caption`, `title` → `heading`, `view`, `wordmark` and `value` → `display`), the
 *   serif to the sans, and `--speed` to `--duration-quick`.
 *
 * `st-open`, `ink-open` and `warn` are aliases of the owner's amber, split site by site into `you` or neutral as each screen's
 * row lands; they are listed (`--aliases`), never moved. `--check` writes nothing and exits 1 when a file would change.
 * Exit 0 clean, 1 a file would change under `--check`, 2 a tool error.
 */
import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';

/** The status colours and the role each becomes. */
export const COLOURS = {
  'ink-danger': 'fail-ink', 'st-declined': 'fail',
  'ink-done': 'ok-ink', 'st-done': 'ok',
  'ink-taken': 'live-ink', 'st-taken': 'live',
};

/** The type steps and the step each becomes; `small` and `body` keep their names. */
export const STEPS = { meta: 'caption', title: 'heading', view: 'display', wordmark: 'display', value: 'display' };

/** The amber aliases, listed until UIDS18. */
export const ALIASES = ['st-open', 'ink-open', 'warn'];

/** Where the codemod reads, under the web package. */
export const FOLDERS = ['src', '.storybook', 'measured'];

const START = String.raw`(?<![\w-])`;
const END = String.raw`(?![\w-])`;
const UTILITY = String.raw`(?:text|bg|border(?:-(?:x|y|t|r|b|l|s|e))?|fill|stroke|ring-offset|ring|outline|decoration|divide|caret|accent|shadow|placeholder|from|via|to)`;
const OPACITY = String.raw`(?:\/(?:\d+|\[[^\]\s]+\]))?`;
const SOFT_HUES = { 'st-declined': 'fail', 'st-done': 'ok', 'st-taken': 'live' };

const SOFT_FIELD = new RegExp(String.raw`${START}bg-(${Object.keys(SOFT_HUES).join('|')})\/(?:5|10|15|20)${END}`, 'g');
const ACCENT_FIELD = new RegExp(String.raw`${START}bg-accent\/10${END}`, 'g');
const COLOUR = new RegExp(String.raw`${START}(${UTILITY})-(${Object.keys(COLOURS).join('|')})(${OPACITY})${END}`, 'g');
const BARE_NAME = new RegExp(String.raw`(['"\x60])(--)?(${Object.keys(COLOURS).join('|')})\1`, 'g');
const LITERAL = /'[^'\n]*'|"[^"\n]*"|`[^`]*`/g;
const SOLID_ACCENT = new RegExp(String.raw`${START}bg-accent(?![\w/-])`);
const WORD_ON_ACCENT = new RegExp(String.raw`${START}text-accent(?:-ink)?(${OPACITY})${END}`, 'g');
const WORD_IN_ACCENT = new RegExp(String.raw`${START}text-accent(${OPACITY})${END}`, 'g');
const STEP = new RegExp(String.raw`${START}text-(${Object.keys(STEPS).join('|')})${END}`, 'g');
// A selector (`.font-serif`) is a test's question whether the serif is drawn, and keeps asking it.
const SERIF = new RegExp(String.raw`(?<![\w.-])font-serif${END}`, 'g');
const VARIABLES = { ...COLOURS, speed: 'duration-quick', ...Object.fromEntries(Object.entries(STEPS).map(([from, to]) => [`text-${from}`, `text-${to}`])) };
const VARIABLE = new RegExp(String.raw`(?<=\(\s*)--(${Object.keys(VARIABLES).join('|')})(?=\s*[),])`, 'g');
const ACCENT_COLOUR = /(?<![\w-])(color\s*:\s*['"]?)var\(--accent\)/g;
const ALIAS_CLASS = new RegExp(String.raw`${START}${UTILITY}-(?:${ALIASES.join('|')})${OPACITY}${END}`, 'g');
const ALIAS_VARIABLE = new RegExp(String.raw`var\(--(?:${ALIASES.join('|')})\)`, 'g');

/** One file's text moved to the role set's names, with the count of names moved. `tokens.css` is left whole. */
export function moveSource(text, path) {
  if (/(?:^|\/)src\/tokens\.css$/.test(path.replaceAll('\\', '/'))) return { text, moved: 0 };
  let moved = 0;
  const swap = (source, pattern, to) => source.replace(pattern, (...match) => {
    moved += 1;
    return typeof to === 'function' ? to(...match) : to;
  });
  let out = text;
  out = swap(out, SOFT_FIELD, (_, hue) => `bg-${SOFT_HUES[hue]}-soft`);
  out = swap(out, ACCENT_FIELD, 'bg-accent-soft');
  out = swap(out, COLOUR, (_, utility, colour, opacity) => `${utility}-${COLOURS[colour]}${opacity}`);
  out = swap(out, BARE_NAME, (_, quote, dashes = '', colour) => `${quote}${dashes}${COLOURS[colour]}${quote}`);
  out = out.replace(LITERAL, (literal) => SOLID_ACCENT.test(literal)
    ? swap(literal, WORD_ON_ACCENT, (_, opacity) => `text-on-accent${opacity}`)
    : literal);
  out = swap(out, WORD_IN_ACCENT, (_, opacity) => `text-accent-ink${opacity}`);
  out = swap(out, STEP, (_, step) => `text-${STEPS[step]}`);
  out = swap(out, SERIF, 'font-sans');
  out = swap(out, VARIABLE, (_, name) => `--${VARIABLES[name]}`);
  out = swap(out, ACCENT_COLOUR, (_, lead) => `${lead}var(--accent-ink)`);
  return { text: out, moved };
}

/** Each use of an amber alias in a text, as written, in order. */
export function aliasSites(text) {
  return [...text.matchAll(ALIAS_CLASS), ...text.matchAll(ALIAS_VARIABLE)]
    .sort((a, b) => a.index - b.index)
    .map((match) => match[0]);
}

/** Every source file the codemod reads under the web package, by walking (an in-flight branch's new file is untracked). */
function sources(web) {
  const found = [];
  const walk = (dir) => {
    let names;
    try { names = readdirSync(dir); } catch { return; }
    for (const name of names) {
      if (name === 'node_modules' || name.startsWith('.') && dir !== web) continue;
      const path = join(dir, name);
      if (statSync(path).isDirectory()) walk(path);
      else if (/\.(?:tsx?|css|mdx)$/.test(name)) found.push(path);
    }
  };
  for (const folder of FOLDERS) walk(join(web, folder));
  return found;
}

/** The web package's sources moved (or, without `write`, what would move), and each file's alias sites. */
export function moveTree(root, { write = false } = {}) {
  const web = join(root, 'src', 'Daoris.Web');
  const changed = [];
  const aliases = [];
  const files = sources(web);
  for (const file of files) {
    const path = relative(root, file).replaceAll('\\', '/');
    const text = readFileSync(file, 'utf8');
    const result = moveSource(text, path);
    if (result.text !== text) {
      changed.push({ path, moved: result.moved });
      if (write) writeFileSync(file, result.text);
    }
    const sites = aliasSites(result.text);
    if (sites.length > 0 && !path.endsWith('src/tokens.css')) aliases.push({ path, sites });
  }
  return { read: files.length, changed, aliases };
}

if (isMain(import.meta.url)) {
  const args = process.argv.slice(2);
  const unknown = args.filter((arg) => !['--check', '--aliases'].includes(arg));
  if (unknown.length > 0) {
    console.error(`usage: node tools/token-codemod.mjs [--check] [--aliases] (unknown: ${unknown.join(' ')})`);
    process.exit(2);
  }
  const check = args.includes('--check');
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  const run = moveTree(root, { write: !check });
  for (const { path, moved } of run.changed) console.log(`${check ? 'would move' : 'moved'} ${moved} in ${path}`);
  if (args.includes('--aliases')) {
    for (const { path, sites } of run.aliases) console.log(`alias ${sites.length} in ${path}: ${[...new Set(sites)].join(' ')}`);
  }
  const sites = run.aliases.reduce((sum, each) => sum + each.sites.length, 0);
  console.log(`${run.read} files read; ${run.changed.length} ${check ? 'would change' : 'changed'}; ${sites} alias sites in ${run.aliases.length} files`);
  process.exit(check && run.changed.length > 0 ? 1 : 0);
}
