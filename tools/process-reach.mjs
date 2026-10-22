#!/usr/bin/env node
/**
 * Which test classes of a `Process` half a change reaches (PROCREACH1, D180 point 2), so the CI runs those again and keeps
 * the rest of the half's verdicts.
 *
 * ## Why
 *
 * A release's verification at `c557800` ran the driver's git half (693 tests, 272 minutes of test time in 69 of wall) and its
 * other Process half (666 tests, 130 minutes in 23), and any driver or modules change reached both whole: CI1 skips a gate no
 * changed path reaches, and a gate reached ran every class in it. This goes one level finer, to the class.
 *
 * ## What a class reaches (the census, `censusOf`)
 *
 * Read from the sources, never a hand-kept list: a list goes stale the day a class is written, as the Process category's did
 * (`isProcessGate` in the merge tool). For a half (`halfOf`: the test project and the run settings its gate's command names):
 *
 * - **Its classes** are the test project's classes with a `[Fact]` or `[Theory]` of their own or a base's, whose traits (their
 *   own and their bases', as xUnit reads them, or one test's own with them) hold the run settings' `TestCaseFilter`.
 * - **A class's files** are the files that declare it and its bases, and, read through, every file of the test project that
 *   declares what they name (a type, or an extension method): a helper's code runs in the class, so what it names the class
 *   reaches.
 * - **Its reach** is the source areas its files name: each top-level type and extension method of a project the test project
 *   compiles against (`ProjectReference`, read through) is defined in a file, and its area is that project's first folder
 *   (`Daoris.Desktop.Driver/Sessions`, a driver module, D166). A program the test project builds only to start
 *   (`ReferenceOutputAssembly="false"`) is an area of its own, reached by the classes whose strings name it, with the areas of
 *   the sources it compiles in by link, and every area where it compiles against a source project (the driver's host).
 * - Only what the files name is followed, not what the named types use in turn: the driver's modules name each other nearly
 *   all, so a closure over them would reach every class from every change. What that misses is the sweep's (the merge tool
 *   runs a whole half at least daily, and a release refuses older), and a hand-written `[Reaches]` with its reason.
 * - **A class with no reach** is reached by every path that reaches its half: an unknown runs.
 *
 * ## What a changed path reaches (`pathReach`)
 *
 * Only a path the merge tool's lane table sends to the half's gate is asked here. Then:
 * - a file of a source project, under one of its folders: the classes whose reach holds that area; a program's file, the
 *   classes that start it;
 * - a C# file of the test project: the classes whose files hold it, none where no class of the half reads it;
 * - a file of the test project a test reads (a fixture, a golden): the classes whose strings name it or a folder above it;
 * - another project's file beside them: the classes whose strings name that project, none where none does (nothing compiles
 *   against it);
 * - the whole half for what acts without being named: a test-project file with a `global using`, an assembly or module
 *   attribute or a module initializer, a project file, a project's root, `Directory.Build.props`, the run settings, and any path
 *   this map cannot place (the lane table's every-gate files, a CLI source a test runs, the example plugins).
 *
 * ## How the CI runs a half by reach
 *
 * The merge tool keeps each class's verdict with the tree it ran on, and a class whose newest standing verdict passed is kept.
 * The half then runs with run settings of its own (`reachSettings`): its filter ANDed with `FullyQualifiedName!~<class>.` for
 * each kept class, in a file rather than on the command line (cmd's 8,191 characters: two hundred class names do not fit, and
 * VSTest reads a settings file's filter whole). Keeping a class out rather than naming each class to run means a class this
 * census never saw still runs.
 *
 *   node tools/process-reach.mjs census [<gate>…]              each half's classes and reach
 *   node tools/process-reach.mjs reach <from> <to> [<gate>…]   which classes the paths changed between two commits reach
 *
 * Exit codes: 0 answered · 2 a tool error (no such gate, a commit git cannot read).
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { join, posix } from 'node:path';
import { isMain } from './fsx.mjs';

// ---------------------------------------------------------------------------------------------------
// Reading C#

const KEYWORDS = new Set([
  'abstract', 'as', 'base', 'bool', 'break', 'byte', 'case', 'catch', 'char', 'checked', 'class', 'const', 'continue', 'decimal',
  'default', 'delegate', 'do', 'double', 'else', 'enum', 'event', 'explicit', 'extern', 'false', 'finally', 'fixed', 'float', 'for',
  'foreach', 'goto', 'if', 'implicit', 'in', 'int', 'interface', 'internal', 'is', 'lock', 'long', 'namespace', 'new', 'null',
  'object', 'operator', 'out', 'override', 'params', 'private', 'protected', 'public', 'readonly', 'ref', 'return', 'sbyte',
  'sealed', 'short', 'sizeof', 'stackalloc', 'static', 'string', 'struct', 'switch', 'this', 'throw', 'true', 'try', 'typeof',
  'uint', 'ulong', 'unchecked', 'unsafe', 'ushort', 'using', 'virtual', 'void', 'volatile', 'while', 'where', 'with', 'record',
  'var', 'when', 'and', 'or', 'not', 'select', 'from', 'into', 'let', 'on', 'equals', 'by', 'group', 'join', 'orderby',
  'ascending', 'descending', 'get', 'set', 'init', 'value', 'yield', 'await', 'async', 'file', 'required', 'scoped', 'global',
]);

/**
 * C# source read two ways, each the length of the text with its newlines kept: `code`, with its comments and the contents of its
 * string and character literals blanked, so its braces and words are code alone; and `literal`, with its comments blanked and
 * its literals kept, for an attribute's arguments and the names a test's strings hold. An interpolated string's holes stay code.
 * A raw string's holes are blanked with it: a word missed there only narrows what a class is read to name, and no test here
 * interpolates a type into one.
 */
export function readCs(text) {
  const n = text.length;
  const code = text.split('');
  const literal = text.split('');
  const blank = (from, to, both) => {
    for (let k = from; k < to && k < n; k += 1) {
      if (code[k] === '\n' || code[k] === '\r') continue;
      code[k] = ' ';
      if (both) literal[k] = ' ';
    }
  };
  const lineEnd = (from) => {
    const at = text.indexOf('\n', from);
    return at < 0 ? n : at;
  };

  // A string from its first prefix character: blanked in `code` but for its holes, which are scanned as code.
  const scanString = (start) => {
    let i = start;
    let dollars = 0;
    let verbatim = false;
    while (text[i] === '$' || text[i] === '@') {
      if (text[i] === '$') dollars += 1;
      else verbatim = true;
      i += 1;
    }
    let quotes = 0;
    while (text[i + quotes] === '"') quotes += 1;
    if (quotes >= 3) {
      // A raw string: its content runs to the same number of quotes.
      const end = text.indexOf('"'.repeat(quotes), i + quotes);
      const stop = end < 0 ? n : end + quotes;
      blank(start, stop, false);
      return stop;
    }
    if (quotes === 2) {
      blank(start, i + 2, false);
      return i + 2;
    }
    let j = i + 1;
    let from = start;
    while (j < n) {
      const c = text[j];
      if (!verbatim && c === '\\') {
        j += 2;
        continue;
      }
      if (c === '"') {
        if (verbatim && text[j + 1] === '"') {
          j += 2;
          continue;
        }
        blank(from, j + 1, false);
        return j + 1;
      }
      if (!verbatim && c === '\n') {
        blank(from, j, false);
        return j;
      }
      if (dollars && c === '{') {
        if (text[j + 1] === '{') {
          j += 2;
          continue;
        }
        blank(from, j + 1, false);
        const close = scanCode(j + 1, true);
        from = close;
        j = close + 1;
        continue;
      }
      j += 1;
    }
    blank(from, n, false);
    return n;
  };

  const scanChar = (start) => {
    let j = start + 1;
    if (text[j] === '\\') j += 2;
    else j += 1;
    while (j < n && text[j] !== '\'' && text[j] !== '\n') j += 1;
    const stop = Math.min(n, j + 1);
    blank(start, stop, false);
    return stop;
  };

  // Code from `start`; in a hole, until the brace that closes it, whose index it answers.
  function scanCode(start, hole) {
    let i = start;
    let depth = 0;
    while (i < n) {
      const c = text[i];
      if (c === '/' && text[i + 1] === '/') {
        const end = lineEnd(i);
        blank(i, end, true);
        i = end;
      } else if (c === '/' && text[i + 1] === '*') {
        const close = text.indexOf('*/', i + 2);
        const end = close < 0 ? n : close + 2;
        blank(i, end, true);
        i = end;
      } else if (c === '\'') {
        i = scanChar(i);
      } else if (c === '"' || ((c === '$' || c === '@') && /^[$@]+"/.test(text.slice(i, i + 8)))) {
        i = scanString(i);
      } else {
        if (hole && c === '{') depth += 1;
        else if (hole && c === '}') {
          if (depth === 0) return i;
          depth -= 1;
        }
        i += 1;
      }
    }
    return n;
  }

  scanCode(0, false);
  return { code: code.join(''), literal: literal.join('') };
}

/** The brace depth before each character of `code`, an array one longer than it. */
function depths(code) {
  const at = new Int32Array(code.length + 1);
  let depth = 0;
  for (let i = 0; i < code.length; i += 1) {
    at[i] = depth;
    if (code[i] === '{') depth += 1;
    else if (code[i] === '}') depth = Math.max(0, depth - 1);
  }
  at[code.length] = depth;
  return at;
}

/** The index of the brace that closes the one at `open`, or the text's end. */
function closing(code, open) {
  let depth = 0;
  for (let i = open; i < code.length; i += 1) {
    if (code[i] === '{') depth += 1;
    else if (code[i] === '}') {
      depth -= 1;
      if (depth === 0) return i;
    }
  }
  return code.length;
}

/** Every identifier the code names, as a set: its keywords left out. */
export function namesIn(code) {
  const found = new Set();
  for (const [word] of code.matchAll(/@?[A-Za-z_][A-Za-z0-9_]*/g)) {
    const name = word.replace(/^@/, '');
    if (!KEYWORDS.has(name)) found.add(name);
  }
  return found;
}

/** The text of a file's literals alone: what `literal` keeps that `code` blanks, each literal's run kept apart by a space. */
const stringsOf = (code, literal) => {
  let out = '';
  for (let i = 0; i < code.length; i += 1) out += code[i] === literal[i] ? (out.endsWith(' ') ? '' : ' ') : literal[i];
  return out;
};

const MODIFIERS = '(?:public|private|internal|protected|static|sealed|abstract|partial|readonly|ref|file|unsafe|new|required)';
const DECLARATION = /\b(class|struct|interface|enum|record)\s+(?:(?:class|struct)\s+)?@?([A-Za-z_][A-Za-z0-9_]*)/g;
const STATEMENT_START = new RegExp(`(?:^|[;{}\\]])\\s*(?:${MODIFIERS}\\s+)*$`);
const EXTENSION = /\bstatic\s+[^;{}()=]*?\b([A-Za-z_]\w*)\s*(?:<[^<>()]*>)?\s*\(\s*this\s/g;

/**
 * The types a C# file declares, in order: each one's name, kind, modifiers, whether it is top-level in its namespace, its
 * outer type's name when nested, its bases (the words of its base list, generic arguments dropped), its attributes' text
 * (comments out, literals kept), its body's span, whether its body holds a `[Fact]` or `[Theory]` of its own, and each such
 * test's own attributes. With them the file's namespace, its `code` and `literal` readings (`readCs`), and its extension
 * methods' names. One namespace per file, file-scoped or a block, as this repository writes them.
 */
export function typesIn(text) {
  const { code, literal } = readCs(text);
  const depth = depths(code);
  const space = /\bnamespace\s+([\w.]+)\s*([;{])/.exec(code);
  const namespace = space ? space[1] : '';
  const top = space && space[2] === '{' ? 1 : 0;
  const found = [];
  for (const match of code.matchAll(DECLARATION)) {
    const at = match.index;
    const name = match[2];
    if (KEYWORDS.has(name)) continue;
    const before = code.slice(Math.max(0, at - 400), at);
    const start = STATEMENT_START.exec(before);
    if (!start) continue;
    // The modifiers and attributes before it: from the statement's start, each attribute list before the modifiers read back.
    let from = at - (before.length - start.index);
    for (;;) {
      let p = from;
      while (p < at && /\s/.test(code[p])) p += 1;
      if (code[p] !== ']') break;
      let bracket = 0;
      let q = p;
      for (; q >= 0; q -= 1) {
        if (code[q] === ']') bracket += 1;
        else if (code[q] === '[') {
          bracket -= 1;
          if (bracket === 0) break;
        }
      }
      if (q < 0) break;
      let r = q - 1;
      while (r >= 0 && /\s/.test(code[r])) r -= 1;
      from = r >= 0 && code[r] === ']' ? r : q;
      if (code[from] !== ']') break;
    }
    let open = -1;
    let paren = 0;
    let end = at + match[0].length;
    for (let i = end; i < code.length; i += 1) {
      const c = code[i];
      if (c === '(') paren += 1;
      else if (c === ')') paren -= 1;
      else if (paren === 0 && c === ';') {
        end = i;
        break;
      } else if (paren === 0 && c === '{') {
        open = i;
        break;
      }
    }
    const close = open >= 0 ? closing(code, open) : end;
    const header = code.slice(at + match[0].length, open >= 0 ? open : end).replace(/\bwhere\b[\s\S]*$/, '');
    const bases = [];
    const colon = header.replace(/\([^)]*\)/g, (part) => ' '.repeat(part.length)).indexOf(':');
    if (colon >= 0) {
      let angle = 0;
      let part = '';
      for (const c of `${header.slice(colon + 1)},`) {
        if (c === '<') angle += 1;
        else if (c === '>') angle -= 1;
        if (c === ',' && angle === 0) {
          const word = /^\s*(?:global::)?([A-Za-z_][\w.]*)/.exec(part.replace(/\([^)]*\)/g, ''));
          if (word) bases.push(word[1].split('.').at(-1));
          part = '';
        } else part += c;
      }
    }
    // Attributes, from the text of the statement's start (literals kept): Trait and Reaches arguments are read from it.
    const attributes = literal.slice(from, at);
    const modifiers = [...code.slice(from, at).matchAll(new RegExp(`\\b${MODIFIERS}\\b`, 'g'))].map((m) => m[0]);
    found.push({
      name, kind: match[1] === 'record' && /\brecord\s+struct\b/.test(match[0]) ? 'struct' : match[1], modifiers, bases, attributes,
      depth: depth[at], topLevel: depth[at] === top, at, open, close,
    });
  }
  // Each nested type's outer type, and whether a body holds a test of its own (at its own depth, not a nested type's).
  for (const type of found) {
    const outer = found.filter((other) => other !== type && other.open >= 0 && other.open < type.at && type.at < other.close)
      .sort((a, b) => b.open - a.open)[0];
    type.outer = outer ? outer.name : null;
    type.tests = false;
    // Each test's own attributes (literals kept), from the member before it to its parameters: a Trait may sit on a test alone.
    type.methods = [];
    if (type.open < 0) continue;
    const body = code.slice(type.open, type.close);
    for (const fact of body.matchAll(/\b(?:Fact|Theory)(?:Attribute)?\b/g)) {
      const at = type.open + fact.index;
      const prior = code.slice(0, at).trimEnd().at(-1);
      if ((prior !== '[' && prior !== ',') || depth[at] !== depth[type.open] + 1) continue;
      type.tests = true;
      let from = at;
      while (from > type.open && !';{}'.includes(code[from - 1])) from -= 1;
      let to = at;
      for (let bracket = 1; to < type.close; to += 1) {
        if (code[to] === '[') bracket += 1;
        else if (code[to] === ']') bracket -= 1;
        else if (code[to] === '(' && bracket === 0) break;
      }
      type.methods.push(literal.slice(from, to));
    }
  }
  const extensions = [...new Set([...code.matchAll(EXTENSION)].map((m) => m[1]))];
  return { namespace, types: found, code, literal, extensions };
}

/** Each `const string Name = "value";` a C# file declares, by its class, for a trait named by a constant (`Category.Process`). */
function constantsIn(text, types) {
  const out = new Map();
  for (const type of types) {
    if (type.open < 0) continue;
    const body = text.slice(type.open, type.close);
    for (const [, name, value] of body.matchAll(/\bconst\s+string\s+([A-Za-z_]\w*)\s*=\s*"([^"\\]*)"\s*;/g)) out.set(`${type.name}.${name}`, value);
  }
  return out;
}

/** The `[Trait(name, value)]` pairs in an attribute text, each part a string literal or a constant `constants` names. */
export function traitsIn(attributes, constants = new Map()) {
  const value = (part) => {
    const trimmed = part.trim();
    const quoted = /^"([^"]*)"$/.exec(trimmed);
    if (quoted) return quoted[1];
    const name = trimmed.replace(/^global::/, '').split('.').slice(-2).join('.');
    return constants.get(name) ?? null;
  };
  const out = [];
  for (const [, name, val] of attributes.matchAll(/\bTrait(?:Attribute)?\s*\(\s*([^,()]+?)\s*,\s*([^,()]+?)\s*\)/g)) {
    const pair = [value(name), value(val)];
    if (pair[0] !== null && pair[1] !== null) out.push(pair);
  }
  return out;
}

/**
 * A hand-written reach (`[Reaches("Daoris.Desktop.Driver/Sessions", Why = "…")]`): the areas a class runs that its files do not
 * name, each a project's folder as the census names areas (`<project>/<folder>`, or a program's `<project>`), or `*` for every
 * one. Its reason is required where it is written: a census without one says so.
 */
export function reachesIn(attributes) {
  const out = [];
  for (const [, args] of attributes.matchAll(/\bReaches(?:Attribute)?\s*\(((?:[^()"]|"[^"]*")*)\)/g)) {
    const why = /\bWhy\s*=\s*"([^"]*)"/.exec(args)?.[1] ?? null;
    const areas = [...args.replace(/\bWhy\s*=\s*"[^"]*"/, '').matchAll(/"([^"]*)"/g)].map((m) => m[1]);
    out.push({ areas, why });
  }
  return out;
}

// ---------------------------------------------------------------------------------------------------
// VSTest's filter, as a run settings file holds it

/** A run settings file's `TestCaseFilter`, its entities read, or null where it holds none. */
export function settingsFilter(xml) {
  const found = /<TestCaseFilter>([\s\S]*?)<\/TestCaseFilter>/.exec(xml.replace(/<!--[\s\S]*?-->/g, ''));
  if (!found) return null;
  return found[1].trim().replace(/&(amp|lt|gt|quot|apos);/g, (_, e) => ({ amp: '&', lt: '<', gt: '>', quot: '"', apos: "'" })[e]);
}

/**
 * Whether a class whose traits are `traits` (a map of name to its values) can hold a test VSTest's `filter` selects: `&`, `|`,
 * `!`, parentheses, and `=`, `!=`, `~`, `!~` on a trait. A condition on a test's name holds: a class read into a half it is not
 * in runs nothing more there, since the half's own filter still chooses its tests.
 */
export function filterHolds(filter, traits) {
  const tokens = filter.match(/\\.|!=|!~|[()&|!=~]|[^()&|!=~\\]+/g) ?? [];
  let at = 0;
  const peek = () => tokens[at];
  const condition = () => {
    let text = '';
    while (at < tokens.length && !['(', ')', '&', '|'].includes(peek())) text += tokens[at++];
    const parts = /^\s*([^!=~]+?)\s*(!=|!~|=|~)\s*(.*?)\s*$/.exec(text);
    if (!parts) return true;
    const [, name, op, value] = parts;
    if (/^(FullyQualifiedName|Name|ClassName|DisplayName)$/.test(name)) return true;
    const has = [...(traits.get(name) ?? [])].some((each) => (op.endsWith('~') ? each.includes(value) : each === value));
    return op.startsWith('!') ? !has : has;
  };
  const factor = () => {
    if (peek() === '!') {
      at += 1;
      return !factor();
    }
    if (peek() === '(') {
      at += 1;
      const value = expression();
      if (peek() === ')') at += 1;
      return value;
    }
    return condition();
  };
  const term = () => {
    let value = factor();
    while (peek() === '&') {
      at += 1;
      value = factor() && value;
    }
    return value;
  };
  function expression() {
    let value = term();
    while (peek() === '|') {
      at += 1;
      value = term() || value;
    }
    return value;
  }
  return expression();
}

/** A filter value with VSTest's special characters escaped. */
const filterValue = (text) => text.replace(/[\\()&|=!~]/g, (c) => `\\${c}`);

/**
 * The run settings a half runs by reach with (PROCREACH1): the half's own, its filter ANDed with one `FullyQualifiedName!~`
 * condition per kept class, the class's full name and a dot, so a class whose name another's begins with is not kept with it.
 * In a file, not on the command line: VSTest ANDs the file's filter with one a re-run alone adds (the merge tool's FLAKE1),
 * and two hundred names pass cmd's 8,191 characters. A class kept out is the only one that does not run: one this census never
 * saw still runs.
 */
export function reachSettings(xml, kept) {
  const filter = settingsFilter(xml);
  if (filter === null) throw new Error('the run settings hold no TestCaseFilter to narrow');
  if (!kept.length) return xml;
  const narrowed = `(${filter})${kept.map((name) => `&FullyQualifiedName!~${filterValue(`${name}.`)}`).join('')}`;
  const escaped = narrowed.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  return xml.replace(/<TestCaseFilter>[\s\S]*?<\/TestCaseFilter>/, () => `<TestCaseFilter>${escaped}</TestCaseFilter>`);
}

// ---------------------------------------------------------------------------------------------------
// The census

const posixPath = (path) => path.split('\\').join('/');
const folderOf = (path) => posix.dirname(path);
const inFolder = (path, folder) => path.startsWith(`${folder}/`) && !/\/(bin|obj)\//.test(path.slice(folder.length));
const isProjectFile = (path) => /\.(csproj|props|targets)$/i.test(path);

/** What a file of a test project holds that acts on every test without being named: a global using, an assembly or module attribute, a module initializer. */
const UNNAMED = /\bglobal\s+using\b|\[\s*(?:assembly|module)\s*:|\bModuleInitializer\b/;

/**
 * The project and run settings a Process half's command names (`dotnet test <project> --settings <file>`), `/`-separated as the
 * repository's paths are, or null for a command that names either not: a half this cannot read runs whole.
 */
export function halfOf(run) {
  const words = run.trim().match(/"[^"]*"|'[^']*'|\S+/g)?.map((word) => word.replace(/^["']|["']$/g, '')) ?? [];
  if (words[0] !== 'dotnet' || words[1] !== 'test' || !words[2] || words[2].startsWith('-')) return null;
  const at = words.findIndex((word) => word === '--settings' || word === '-s');
  const settings = at >= 0 ? words[at + 1] : words.find((word) => /^--settings[=:]/.test(word))?.replace(/^--settings[=:]/, '');
  if (!settings) return null;
  const clean = (path) => posixPath(path).split('/').filter((part) => part && part !== '.').join('/');
  const project = clean(words[2]).replace(/\/[^/]+\.csproj$/i, '');
  return { project, settings: clean(settings) };
}

/** A half's command told other run settings (`file`, quoted): what `reachSettings` wrote, in place of the half's own. */
export function withSettings(run, file) {
  const words = run.trim().match(/"[^"]*"|'[^']*'|\S+/g) ?? [];
  const at = words.findIndex((word) => word === '--settings' || word === '-s');
  if (at < 0 || at + 1 >= words.length) throw new Error(`the command names no settings to replace: ${run}`);
  words[at + 1] = `"${file}"`;
  return words.join(' ');
}

/** A project's file, and each `ProjectReference` in it as `{ folder, compiles }`: `compiles` false for one built only to be started. */
function projectOf(folder, files, read) {
  const csproj = files.find((file) => folderOf(file) === folder && file.endsWith('.csproj'));
  if (!csproj) return null;
  const xml = read(csproj);
  const references = [];
  for (const [tag] of xml.matchAll(/<ProjectReference\b[^>]*?(?:\/>|>[\s\S]*?<\/ProjectReference>)/g)) {
    const include = /Include\s*=\s*"([^"]+)"/.exec(tag)?.[1];
    if (!include) continue;
    const compiles = !/ReferenceOutputAssembly\s*=\s*"false"|<ReferenceOutputAssembly>\s*false\s*</i.test(tag);
    references.push({ folder: folderOf(posix.normalize(posix.join(folder, posixPath(include)))), compiles });
  }
  const links = [...xml.matchAll(/<Compile\b[^>]*\bInclude\s*=\s*"([^"]+)"/g)].map((m) => posix.normalize(posix.join(folder, posixPath(m[1]))));
  const assembly = /<AssemblyName>\s*([^<\s]+)\s*<\/AssemblyName>/.exec(xml)?.[1] ?? null;
  return { csproj, references, links, assembly };
}

/** The folders of the projects a project compiles against, read through; never one built only to be started. */
export function referencedProjects(project, files, read) {
  const found = [];
  const visit = (folder) => {
    for (const reference of projectOf(folder, files, read)?.references ?? []) {
      if (!reference.compiles || found.includes(reference.folder)) continue;
      found.push(reference.folder);
      visit(reference.folder);
    }
  };
  visit(project);
  return found;
}

/** The source area a path is in: its source project's first folder (`…/Daoris.Desktop.Driver/Sessions`), or null at a project's root or outside. */
export function areaOf(path, sources) {
  for (const source of sources) {
    if (!path.startsWith(`${source}/`)) continue;
    const rest = path.slice(source.length + 1).split('/');
    return rest.length > 1 ? `${source}/${rest[0]}` : null;
  }
  return null;
}

/** Whether a file's strings name `name`: as a word of its own, a path's part or a file name, not inside another word. */
const names = (strings, name) => new RegExp(`(?:^|[^A-Za-z0-9_.-])${name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?:$|[^A-Za-z0-9_-])`).test(strings);

/**
 * The census of one Process half (`half`: its project and settings, `halfOf`), read from the repository's `files` (paths
 * `/`-separated from its root) through `read`. It answers the half's `classes` (each with its full name, the files it is read
 * from, its traits, its `reach` and any hand-written `reaches`), the `sources` it compiles against, the `programs` it starts,
 * what its project's files hold (`files`), and anything it could not read (`problems`).
 */
export function censusOf(half, files, read) {
  const problems = [];
  const xml = files.includes(half.settings) ? read(half.settings) : null;
  const filter = xml === null ? null : settingsFilter(xml);
  if (filter === null) problems.push(`${half.settings} holds no TestCaseFilter`);
  const sources = referencedProjects(half.project, files, read);
  const project = projectOf(half.project, files, read);
  const projectFiles = files.filter((file) => inFolder(file, half.project) && file.endsWith('.cs'));

  // The test project: each file's types, names, strings and extension methods, read once.
  const parsed = new Map();
  const constants = new Map();
  for (const file of projectFiles) {
    const text = read(file);
    const { namespace, types, code, literal, extensions } = typesIn(text);
    parsed.set(file, { namespace, types, names: namesIn(code), strings: stringsOf(code, literal), extensions, unnamed: UNNAMED.test(code) });
    for (const [key, value] of constantsIn(text, types)) constants.set(key, value);
  }
  // Each type by its full name (a nested one `Outer+Inner`), its partial declarations merged.
  const byFull = new Map();
  const bySimple = new Map();
  for (const [file, { namespace, types }] of parsed) {
    for (const type of types) {
      const chain = [];
      for (let t = type; t; t = t.outer ? types.find((other) => other.name === t.outer && other.open < t.at && t.at < other.close) : null) chain.unshift(t.name);
      const full = `${namespace ? `${namespace}.` : ''}${chain.join('+')}`;
      const entry = byFull.get(full) ?? { full, name: type.name, files: new Set(), decls: [], topLevel: type.topLevel };
      entry.files.add(file);
      entry.decls.push(type);
      byFull.set(full, entry);
      if (!bySimple.has(type.name)) bySimple.set(type.name, []);
      if (!bySimple.get(type.name).includes(entry)) bySimple.get(type.name).push(entry);
    }
  }
  const baseOf = (entry) => {
    for (const decl of entry.decls) {
      for (const base of decl.bases) {
        const found = (bySimple.get(base) ?? []).find((other) => other !== entry && other.decls.some((d) => d.kind === 'class'));
        if (found) return found;
      }
    }
    return null;
  };
  const chainOf = (entry) => {
    const out = [];
    for (let at = entry; at && !out.includes(at); at = baseOf(at)) out.push(at);
    return out;
  };
  const modified = (entry, word) => entry.decls.some((d) => d.modifiers.includes(word));
  const testClasses = [...byFull.values()].filter((entry) => entry.decls.some((d) => d.kind === 'class') && !modified(entry, 'abstract')
    && !modified(entry, 'static') && chainOf(entry).some((each) => each.decls.some((d) => d.tests)));

  // What each test-project file gives the files that name it: its top-level types and its extension methods.
  const givenBy = new Map();
  const give = (name, file) => {
    if (!givenBy.has(name)) givenBy.set(name, new Set());
    givenBy.get(name).add(file);
  };
  for (const entry of byFull.values()) if (entry.topLevel) for (const file of entry.files) give(entry.name, file);
  for (const [file, { extensions }] of parsed) for (const name of extensions) give(name, file);

  // The source projects' top-level types and extension methods, each by the files that declare it.
  const defined = new Map();
  for (const source of sources) {
    for (const file of files) {
      if (!inFolder(file, source) || !file.endsWith('.cs')) continue;
      const { types, extensions } = typesIn(read(file));
      for (const name of [...types.filter((type) => type.topLevel).map((type) => type.name), ...extensions]) {
        if (!defined.has(name)) defined.set(name, new Set());
        defined.get(name).add(file);
      }
    }
  }
  // The programs the test project builds only to start: each an area of its own, with what it compiles in.
  const programs = (project?.references ?? []).filter((reference) => !reference.compiles).map(({ folder }) => {
    const program = projectOf(folder, files, read);
    const linked = [...new Set((program?.links ?? []).map((path) => areaOf(path, sources)).filter(Boolean))];
    const everything = (program?.references ?? []).some((reference) => reference.compiles && sources.includes(reference.folder));
    // Named by its project's folder, or a file of its build (`daoris-driver.dll`): its assembly's name alone is a word strings
    // hold for other reasons (the launcher's is `Daoris`, the host's a lease's reason).
    const built = program?.assembly ? [`${program.assembly}.dll`, `${program.assembly}.exe`] : [];
    return { folder, names: [posix.basename(folder), ...built], linked, everything };
  });

  const traitMap = (pairs) => {
    const map = new Map();
    for (const [name, value] of pairs) {
      if (!map.has(name)) map.set(name, new Set());
      map.get(name).add(value);
    }
    return map;
  };
  const classes = [];
  for (const entry of testClasses) {
    const chain = chainOf(entry);
    const classPairs = chain.flatMap((each) => each.decls.flatMap((d) => traitsIn(d.attributes, constants)));
    const traits = traitMap(classPairs);
    // In the half when the class's traits hold its filter, or one test's own with them do (a class whose git cases alone are Process).
    const tests = chain.flatMap((each) => each.decls.flatMap((d) => d.methods));
    const inHalf = filter === null || filterHolds(filter, traits)
      || tests.some((attributes) => filterHolds(filter, traitMap([...classPairs, ...traitsIn(attributes, constants)])));
    if (!inHalf) continue;
    for (const [name, value] of tests.flatMap((attributes) => traitsIn(attributes, constants))) {
      if (!traits.has(name)) traits.set(name, new Set());
      traits.get(name).add(value);
    }
    // Its files: its own and its bases', then each test-project file giving what they name, read through.
    const reading = new Set(chain.flatMap((each) => [...each.files]));
    const queue = [...reading];
    while (queue.length) {
      const file = queue.shift();
      for (const name of parsed.get(file)?.names ?? []) {
        for (const other of givenBy.get(name) ?? []) {
          if (!reading.has(other)) {
            reading.add(other);
            queue.push(other);
          }
        }
      }
    }
    const reach = new Set();
    const strings = [...reading].map((file) => parsed.get(file)?.strings ?? '').join(' ');
    for (const file of reading) {
      for (const name of parsed.get(file)?.names ?? []) {
        for (const source of defined.get(name) ?? []) {
          const area = areaOf(source, sources);
          if (area) reach.add(area);
        }
      }
    }
    for (const program of programs) {
      if (!program.names.some((name) => names(strings, name))) continue;
      reach.add(program.folder);
      for (const area of program.linked) reach.add(area);
      if (program.everything) reach.add('*');
    }
    const reaches = chain.flatMap((each) => each.decls.flatMap((d) => reachesIn(d.attributes)));
    for (const hand of reaches) {
      if (!hand.why) problems.push(`${entry.full}: a [Reaches] without its reason (Why = "…")`);
      for (const area of hand.areas) {
        const full = area === '*' ? '*'
          : [...sources, ...programs.map((program) => program.folder)].map((folder) => {
            const base = posix.basename(folder);
            if (area === base) return programs.some((program) => program.folder === folder) ? folder : null;
            return area.startsWith(`${base}/`) ? `${folderOf(folder)}/${area}` : null;
          }).find(Boolean);
        if (full) reach.add(full);
        else problems.push(`${entry.full}: [Reaches("${area}")] names no folder this half compiles against or starts`);
      }
    }
    classes.push({
      name: entry.full.split('.').at(-1), full: entry.full, files: [...reading].sort(),
      traits: Object.fromEntries([...traits].map(([name, values]) => [name, [...values].sort()])),
      reach: [...reach].sort(), reaches, strings,
    });
  }
  classes.sort((a, b) => a.full.localeCompare(b.full));
  return {
    half, filter, sources, programs, classes, problems,
    // Each test-project file: whether it acts unnamed, and whether it declares test classes alone (a class's own file).
    files: Object.fromEntries([...parsed].map(([file, { unnamed }]) => {
      const tops = [...byFull.values()].filter((entry) => entry.topLevel && entry.files.has(file));
      return [file, { unnamed, own: tops.length > 0 && tops.every((entry) => testClasses.includes(entry)) }];
    })),
  };
}

/**
 * What one changed path reaches of a half, by its census: `{ whole: why }`, every class; `{ areas }`, the classes whose reach
 * holds one; `{ classes }`, those named; or `{ none: why }`. Asked only of a path the lane table sends to the half's gate.
 */
export function pathReach(path, census) {
  const { half, sources, programs } = census;
  const among = (test) => census.classes.filter(test).map((each) => each.full);
  if (inFolder(path, half.project)) {
    if (isProjectFile(path)) return { whole: `${path} is the test project's own project file` };
    if (path.endsWith('.cs')) {
      const file = census.files[path];
      if (!file) return { whole: `${path} is a test source this tree does not hold` };
      if (file.unnamed) return { whole: `${path} acts on every test unnamed (a global using, an assembly attribute, a module initializer)` };
      const classes = among((each) => each.files.includes(path));
      return classes.length ? { classes } : { none: `no class of the half reads ${path}` };
    }
    // A fixture or a golden: the classes whose strings name it, or a folder of the test project above it.
    const parts = path.slice(half.project.length + 1).split('/');
    const words = [parts.at(-1), parts.at(-1).replace(/\.[^.]+$/, ''), ...parts.slice(0, -1)];
    const classes = among((each) => words.some((word) => names(each.strings, word)));
    return classes.length ? { classes } : { none: `no class of the half names ${path}` };
  }
  const area = areaOf(path, sources);
  if (area) return { areas: [area] };
  const source = sources.find((folder) => path.startsWith(`${folder}/`));
  if (source) return { whole: `${path} is at ${source}'s root (its project file)` };
  const program = programs.find((each) => path.startsWith(`${each.folder}/`));
  if (program) return { areas: [program.folder] };
  // Another project beside the test project's: nothing compiles against it, so only a test naming it reads it.
  const beside = folderOf(half.project);
  const rest = path.startsWith(`${beside}/`) ? path.slice(beside.length + 1).split('/') : [];
  if (rest.length > 1 && !isProjectFile(path)) {
    const classes = among((each) => names(each.strings, rest[0]));
    return classes.length ? { classes } : { none: `no class of the half compiles against, starts or names ${rest[0]}` };
  }
  return { whole: `no class's reach places ${path}` };
}

/**
 * Which of a half's classes `paths` reach, each with the first path that reaches it, and the paths that reach the whole half.
 * A class with no reach is reached by any path; `*` in its reach is every area.
 */
export function classesReached(paths, census) {
  const whole = [];
  const by = new Map();
  for (const path of paths) {
    const placed = pathReach(path, census);
    if (placed.whole) {
      whole.push({ path, why: placed.whole });
      continue;
    }
    // A path no class reads, by what every class's files name and say, reaches none: one with no reach was read too.
    if (placed.none) continue;
    for (const each of census.classes) {
      if (by.has(each.full)) continue;
      const hit = !each.reach.length
        || (placed.areas && (each.reach.includes('*') || placed.areas.some((area) => each.reach.includes(area))))
        || (placed.classes && placed.classes.includes(each.full));
      if (hit) by.set(each.full, path);
    }
  }
  if (whole.length) return { whole, classes: census.classes.map((each) => each.full), by: new Map(census.classes.map((each) => [each.full, whole[0].path])) };
  return { whole, classes: census.classes.filter((each) => by.has(each.full)).map((each) => each.full), by };
}

// ---------------------------------------------------------------------------------------------------
// The tool

/** What the census reads of a commit: the desktop's sources, projects and run settings, and the declared gates. */
const READ = (path) => path === 'daoris.gates.json' || (path.startsWith('src/Daoris.Desktop/') && /\.(cs|csproj|runsettings|props|targets)$/.test(path));

/**
 * The repository's tracked files at `commit` (the working tree's when null), `/`-separated, and a reader of each. A commit's
 * files are read by one `git cat-file --batch`: a `git show` a file took half a second to start on this machine (DEVDRIVE1),
 * and the census reads about two thousand.
 */
export function repository(root, commit = null) {
  const listed = spawnSync('git', commit ? ['ls-tree', '-r', '-z', commit] : ['ls-files', '-z'], { cwd: root, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024 });
  if (listed.status !== 0) throw new Error(`git could not list ${commit ?? 'the tree'}: ${String(listed.stderr).trim()}`);
  if (!commit) {
    const files = listed.stdout.split('\0').filter(Boolean);
    return { files, read: (file) => (existsSync(join(root, file)) ? readFileSync(join(root, file), 'utf8') : '') };
  }
  const blobs = new Map();
  for (const entry of listed.stdout.split('\0').filter(Boolean)) {
    const parts = /^\d+ (\w+) ([0-9a-f]+)\t(.*)$/s.exec(entry);
    if (parts && parts[1] === 'blob') blobs.set(parts[3], parts[2]);
  }
  const wanted = [...blobs].filter(([path]) => READ(path));
  const batch = spawnSync('git', ['cat-file', '--batch'], { cwd: root, input: `${wanted.map(([, sha]) => sha).join('\n')}\n`, maxBuffer: 1024 * 1024 * 1024 });
  if (batch.status !== 0) throw new Error(`git could not read ${commit}'s files: ${String(batch.stderr).trim()}`);
  const texts = new Map();
  let at = 0;
  for (const [path] of wanted) {
    const eol = batch.stdout.indexOf(10, at);
    const size = Number(/ (\d+)$/.exec(batch.stdout.subarray(at, eol).toString('utf8'))?.[1] ?? 0);
    texts.set(path, batch.stdout.subarray(eol + 1, eol + 1 + size).toString('utf8'));
    at = eol + 1 + size + 1;
  }
  return { files: [...blobs.keys()], read: (file) => texts.get(file) ?? '' };
}

/** The declared Process halves, all or those named. */
function halvesOf(declared, names) {
  const halves = declared.filter((gate) => /process\.runsettings/.test(gate.run));
  const unknown = names.filter((name) => !halves.some((gate) => gate.name === name));
  if (unknown.length) throw new Error(`no Process half named ${unknown.join(', ')}: ${halves.map((gate) => gate.name).join(', ')}`);
  return names.length ? halves.filter((gate) => names.includes(gate.name)) : halves;
}

async function main(argv) {
  const [verb, ...rest] = argv;
  const root = process.cwd();
  if (verb === 'census') {
    const { files, read } = repository(root);
    for (const gate of halvesOf(JSON.parse(read('daoris.gates.json')).gates ?? [], rest)) {
      const half = halfOf(gate.run);
      const census = censusOf(half, files, read);
      console.log(`${gate.name}: ${census.classes.length} classes (${half.project}, ${half.settings}: ${census.filter})`);
      console.log(`  compiles against ${census.sources.join(', ')}; starts ${census.programs.map((program) => program.folder).join(', ') || 'nothing'}`);
      for (const each of census.classes) {
        console.log(`  ${each.name}: ${each.reach.length ? each.reach.map((area) => posix.basename(area)).join(', ') : '(no reach: every change reaches it)'}`);
      }
      for (const problem of census.problems) console.log(`  problem: ${problem}`);
    }
    return 0;
  }
  if (verb === 'reach') {
    const [from, to, ...wanted] = rest;
    if (!from || !to) throw new Error('usage: node tools/process-reach.mjs reach <from> <to> [<gate>…]');
    const diff = spawnSync('git', ['diff', '--name-only', '--no-renames', '-z', from, to], { cwd: root, encoding: 'utf8' });
    if (diff.status !== 0) throw new Error(`git could not compare ${from} and ${to}: ${String(diff.stderr).trim()}`);
    const paths = diff.stdout.split('\0').filter(Boolean);
    const { files, read } = repository(root, to);
    // The lane table's answer first: a path that cannot reach a half's gate reaches none of its classes.
    const { pathsReaching, readLanes } = await import('./merge-branch.mjs');
    const lanes = readLanes(root)?.lanes ?? [];
    for (const gate of halvesOf(JSON.parse(read('daoris.gates.json')).gates ?? [], wanted)) {
      const reaching = pathsReaching(gate.name, paths, { lanes });
      if (!reaching.length) {
        console.log(`${gate.name}: no changed path reaches it`);
        continue;
      }
      const census = censusOf(halfOf(gate.run), files, read);
      const reached = classesReached(reaching, census);
      const why = reached.whole.length ? `the whole half: ${reached.whole[0].why}` : `${reached.classes.length} of ${census.classes.length} classes`;
      console.log(`${gate.name}: ${why}`);
      if (!reached.whole.length) for (const name of reached.classes) console.log(`  ${name.split('.').at(-1)} (${reached.by.get(name)})`);
    }
    return 0;
  }
  console.error('usage: node tools/process-reach.mjs census [<gate>…] | reach <from> <to> [<gate>…]');
  return 2;
}

if (isMain(import.meta.url)) {
  main(process.argv.slice(2)).then(
    (code) => { process.exitCode = code; },
    (error) => {
      console.error(`process-reach: ${error.message}`);
      process.exitCode = 2;
    },
  );
}
