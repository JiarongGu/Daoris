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

/** A TypeScript or JavaScript file's imports, its literal dynamic imports, its computed ones, its strings, its globs and its declared reaches. */
export function tsFacts(text) {
  const imports = new Set();
  for (const [, spec] of text.matchAll(/\b(?:import|export)\s+(?:type\s+)?(?:[^'";]*?\sfrom\s*)?['"]([^'"]+)['"]/g)) imports.add(spec);
  for (const [, spec] of text.matchAll(/\bimport\(\s*(?:\/\/[^\n]*\n\s*|\/\*[\s\S]*?\*\/\s*)*['"`]([^'"`$]+)['"`]\s*\)/g)) imports.add(spec);
  for (const [, spec] of text.matchAll(/\b(?:vi\.mock|vi\.importActual|require)\(\s*['"]([^'"]+)['"]/g)) imports.add(spec);
  const computed = /\bimport\(\s*(?!['"`]|\/\/|\/\*)[^)]/.test(text.replace(/import\(\s*(?:\/\/[^\n]*\n\s*|\/\*[\s\S]*?\*\/\s*)*['"`][^'"`$]+['"`]\s*\)/g, ''));
  const strings = new Set(literalsOf(text).filter((value) => value && value.length < 300).map((value) => value.replace(/\\/g, '/')));
  const globs = [];
  for (const [, body] of text.matchAll(/import\.meta\.glob(?:<[^>]*>)?\(\s*(\[[^\]]*\]|['"][^'"]+['"])/g)) {
    for (const [, pattern] of body.matchAll(/['"]([^'"]+)['"]/g)) globs.push(pattern);
  }
  const reaches = [];
  for (const [, target, why] of text.matchAll(/^\s*\/\/\s*reaches:\s*(\S+)\s*(?:—|--|-)\s*(.+)$/gm)) reaches.push({ target, why: why.trim() });
  return { imports: [...imports], computed, strings: [...strings], globs, reaches };
}

/** A relative import resolved to a tracked file: as written, with an extension, `.js` as its `.ts`, or a folder's index. */
export function resolveImport(from, spec, tracked) {
  if (!spec.startsWith('.')) return null;
  const base = posix.normalize(posix.join(posix.dirname(from), spec));
  const tries = [base, `${base}.ts`, `${base}.tsx`, `${base}.mts`, `${base}.mjs`, `${base}.js`, base.replace(/\.js$/, '.ts'), base.replace(/\.js$/, '.tsx'), base.replace(/\.mjs$/, '.mts'), `${base}/index.ts`, `${base}/index.tsx`, `${base}/index.js`];
  return tries.find((path) => tracked.has(path)) ?? null;
}
