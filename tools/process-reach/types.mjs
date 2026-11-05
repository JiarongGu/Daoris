import { join } from 'node:path';
import { KEYWORDS, closing, depths, readCs } from './source.mjs';

/** The text of a file's literals alone: what `literal` keeps that `code` blanks, each literal's run kept apart by a space. */
export const stringsOf = (code, literal) => {
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
    // A fact attribute of the project's own (`GcmFact`: a FactAttribute that skips where a tool is absent) is a test as `Fact` is.
    for (const fact of body.matchAll(/\b\w*(?:Fact|Theory)(?:Attribute)?\b/g)) {
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
export function constantsIn(text, types) {
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
