/**
 * The facts the census of test units reads of one TypeScript source (`tools/test-map.mjs`): its string literals, its imports, its globs and its declared reaches, and an import resolved to a tracked file.
 */
import { posix } from 'node:path';

/**
 * The string literals of a TypeScript or JavaScript source, in order: quoted strings, and each literal stretch of a template
 * (its holes scanned as code). Comments are skipped. A quote never runs past its line, so a quote inside a regular expression
 * costs at most that line.
 */
export function literalsOf(text) {
  const out = [];
  const holes = [];
  const n = text.length;
  let i = 0;
  const quoted = (j, close, template) => {
    let value = '';
    for (; j < n && text[j] !== close && (template || text[j] !== '\n'); j += 1) {
      if (text[j] === '\\') {
        j += 1;
        value += text[j] ?? '';
      } else if (template && text[j] === '$' && text[j + 1] === '{') break;
      else value += text[j];
    }
    return { value, end: j };
  };
  while (i < n) {
    const c = text[i];
    if (c === '/' && text[i + 1] === '/') {
      const end = text.indexOf('\n', i);
      i = end < 0 ? n : end;
    } else if (c === '/' && text[i + 1] === '*') {
      const end = text.indexOf('*/', i + 2);
      i = end < 0 ? n : end + 2;
    } else if (c === "'" || c === '"') {
      const { value, end } = quoted(i + 1, c, false);
      if (text[end] === c) out.push(value);
      i = end + 1;
    } else if (c === '`' || (c === '}' && holes.length && holes.at(-1) === 0)) {
      if (c === '}') holes.pop();
      const { value, end } = quoted(i + 1, '`', true);
      out.push(value);
      if (text[end] === '$') {
        holes.push(0);
        i = end + 2;
      } else i = end + 1;
    } else {
      if (holes.length && c === '{') holes[holes.length - 1] += 1;
      else if (holes.length && c === '}') holes[holes.length - 1] -= 1;
      i += 1;
    }
  }
  return out;
}

/** The names an import or a re-export's braces take, each as its module exports it (`a as b` takes `a`), a type's left out. */
const namesIn = (braces) => braces.split(',').map((each) => each.trim()).filter((each) => each && !/^type\s/.test(each)).map((each) => each.split(/\s+as\s+/)[0]);

/**
 * Each import of a source: its specifier, the names it takes (null where it takes the module whole: a namespace, a side effect,
 * `export *`, a literal dynamic import, a mock, a require), whether the runtime erases it (`import type`, `export type … from`:
 * the page and the tools compile with `verbatimModuleSyntax` or strip types, so an erased import loads nothing), and whether it
 * re-exports (`export … from`).
 */
export function edgesOf(text) {
  const edges = [];
  for (const [, kind, type, clause, spec] of text.matchAll(/\b(import|export)\s+(type\s+)?(?:([^'";]*?)\sfrom\s*)?['"]([^'"]+)['"]/g)) {
    const taken = (clause ?? '').trim();
    let names = null;
    if (taken && !taken.includes('*')) {
      const braces = /\{([^}]*)\}/.exec(taken);
      names = [...(kind === 'import' && /^[\w$]+\s*(?:,|$)/.test(taken) ? ['default'] : []), ...(braces ? namesIn(braces[1]) : [])];
    }
    edges.push({ spec, names, typeOnly: Boolean(type), reexport: kind === 'export' });
  }
  for (const [, spec] of text.matchAll(/\bimport\(\s*(?:\/\/[^\n]*\n\s*|\/\*[\s\S]*?\*\/\s*)*['"`]([^'"`$]+)['"`]\s*\)/g)) edges.push({ spec, names: null, typeOnly: false });
  for (const [, spec] of text.matchAll(/\b(?:vi\.mock|vi\.importActual|require)\(\s*['"]([^'"]+)['"]/g)) edges.push({ spec, names: null, typeOnly: false });
  return edges;
}

/** A TypeScript or JavaScript file's imports (`edgesOf`), whether it computes one, its strings, its globs and its declared reaches. */
export function tsFacts(text) {
  const edges = edgesOf(text);
  const computed = /\bimport\(\s*(?!['"`]|\/\/|\/\*)[^)]/.test(text.replace(/import\(\s*(?:\/\/[^\n]*\n\s*|\/\*[\s\S]*?\*\/\s*)*['"`][^'"`$]+['"`]\s*\)/g, ''));
  const strings = new Set(literalsOf(text).filter((value) => value && value.length < 300).map((value) => value.replace(/\\/g, '/')));
  const globs = [];
  for (const [, body] of text.matchAll(/import\.meta\.glob(?:<[^>]*>)?\(\s*(\[[^\]]*\]|['"][^'"]+['"])/g)) {
    for (const [, pattern] of body.matchAll(/['"]([^'"]+)['"]/g)) globs.push(pattern);
  }
  const reaches = [];
  for (const [, target, why] of text.matchAll(/^\s*\/\/\s*reaches:\s*(\S+)\s*(?:—|--|-)\s*(.+)$/gm)) reaches.push({ target, why: why.trim() });
  return { edges, computed, strings: [...strings], globs, reaches };
}

/**
 * What a module exports: the names it declares and lists, and the modules it re-exports from with the names each hands it
 * (`names` null: `export *`, every name of that module).
 */
export function exportsOf(text) {
  const own = new Set();
  for (const [, name] of text.matchAll(/\bexport\s+(?:declare\s+)?(?:default\s+)?(?:async\s+)?(?:abstract\s+)?(?:function\*?|class|const|let|var|enum|interface|type|namespace)\s+([\w$]+)/g)) own.add(name);
  if (/\bexport\s+default\b/.test(text)) own.add('default');
  for (const [, braces] of text.matchAll(/\bexport\s+(?:type\s+)?\{([^}]*)\}(?!\s*from)/g)) {
    for (const each of braces.split(',').map((part) => part.trim()).filter(Boolean)) own.add(each.replace(/^type\s+/, '').split(/\s+as\s+/).at(-1));
  }
  const from = [];
  for (const [, type, what, spec] of text.matchAll(/\bexport\s+(type\s+)?(\*(?:\s*as\s+[\w$]+)?|\{[^}]*\})\s*from\s*['"]([^'"]+)['"]/g)) {
    if (type) continue;
    const star = /^\*\s*as\s+([\w$]+)/.exec(what);
    if (star) own.add(star[1]);
    // Each name the module re-exports under, with the name its source exports it by.
    const renames = what.startsWith('{') ? what.slice(1, -1).split(',').map((part) => part.trim()).filter((part) => part && !/^type\s/.test(part)).map((part) => part.split(/\s+as\s+/)) : null;
    from.push({ spec, names: star || !renames ? null : renames.map(([source, name = source]) => ({ source, name })) });
  }
  return { own: [...own], from };
}

/** A relative import resolved to a tracked file: as written, with an extension, `.js` as its `.ts`, or a folder's index. */
export function resolveImport(from, spec, tracked) {
  if (!spec.startsWith('.')) return null;
  const base = posix.normalize(posix.join(posix.dirname(from), spec));
  const tries = [base, `${base}.ts`, `${base}.tsx`, `${base}.mts`, `${base}.mjs`, `${base}.js`, base.replace(/\.js$/, '.ts'), base.replace(/\.js$/, '.tsx'), base.replace(/\.mjs$/, '.mts'), `${base}/index.ts`, `${base}/index.tsx`, `${base}/index.js`];
  return tries.find((path) => tracked.has(path)) ?? null;
}
