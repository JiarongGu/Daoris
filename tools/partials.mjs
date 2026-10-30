/**
 * PART1: no C# type is declared `partial` in more than one hand-written file (D166's note "no partial type without
 * generated code"; docs/2026-10-14-partial-types-survey.md, "The gate").
 *
 *   node tools/partials.mjs           check src/**\/*.cs against tools/partials.json (exit 0 clean, 1 breach, 2 tool error)
 *   node tools/partials.mjs --write   rewrite tools/partials.json from the tree (the first list, or after a row cut entries)
 *
 * A type is partial where a source generator needs it, and then it is declared in one file. A type split by hand over
 * several files is an offender; the offenders stand on `tools/partials.json` (type -> its files), a list that may only
 * shrink: a type split anywhere else, in a file beyond its listed ones, or a listed entry that is no longer true (the type
 * is whole again, or a listed file no longer declares it) is a breach, so the row that fixes an offender cuts its entry.
 *
 * Types are matched by bare name over the whole of `src/`, nested types included, so two unrelated types of one name in
 * different projects would merge; none do today (the list is generated and the survey checked it). A `partial` in a
 * comment or a string is not a declaration; `bin/`, `obj/`, `node_modules/` and `_fixtures/` are skipped. `partial void M()` is a
 * method, not a type, and is not matched. There is no .NET build.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, writeAtomic } from './fsx.mjs';

// `_fixtures/` is repository scratch, never source: a run beside the check makes and removes it while the walk reads it.
const SKIPPED = new Set(['bin', 'obj', 'node_modules', '_fixtures']);
const DECLARATION = /\bpartial\s+(?:record\s+struct|record\s+class|class|record|struct|interface)\s+([A-Za-z_]\w*)/g;

/** The source with comments, strings and character literals blanked (newlines kept), so a keyword inside one is not read. */
export function blank(src) {
  let out = '';
  let i = 0;
  const n = src.length;
  const gap = (from, to) => src.slice(from, to).replace(/[^\r\n]/g, ' ');
  while (i < n) {
    const c = src[i];
    const d = src[i + 1];
    if (c === '/' && d === '/') {
      let j = src.indexOf('\n', i);
      if (j < 0) j = n;
      out += gap(i, j);
      i = j;
    } else if (c === '/' && d === '*') {
      let j = src.indexOf('*/', i + 2);
      j = j < 0 ? n : j + 2;
      out += gap(i, j);
      i = j;
    } else if (c === '"' || ((c === '$' || c === '@') && /^[$@]*"/.test(src.slice(i, i + 4)))) {
      let j = i;
      let verbatim = false;
      while (src[j] !== '"') {
        if (src[j] === '@') verbatim = true;
        j++;
      }
      let quotes = 0;
      while (src[j + quotes] === '"') quotes++;
      let end;
      if (quotes >= 3) {
        const close = '"'.repeat(quotes);
        end = src.indexOf(close, j + quotes);
        end = end < 0 ? n : end + quotes;
      } else {
        end = j + 1;
        while (end < n) {
          if (src[end] === '"' && verbatim && src[end + 1] === '"') end += 2;
          else if (src[end] === '"') {
            end++;
            break;
          } else if (src[end] === '\\' && !verbatim) end += 2;
          else if (src[end] === '\n' && !verbatim) break;
          else end++;
        }
      }
      out += gap(i, end);
      i = end;
    } else if (c === "'") {
      const end = d === '\\' ? src.indexOf("'", i + 3) + 1 || n : i + 3;
      out += gap(i, end);
      i = end;
    } else {
      out += c;
      i++;
    }
  }
  return out;
}

function* sources(dir) {
  let entries;
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch (error) {
    // A folder another run removed between its listing and this read holds no source of this tree.
    if (error?.code === 'ENOENT') return;
    throw error;
  }
  for (const entry of entries) {
    if (entry.isDirectory()) {
      if (!SKIPPED.has(entry.name)) yield* sources(join(dir, entry.name));
    } else if (entry.name.endsWith('.cs')) {
      yield join(dir, entry.name);
    }
  }
}

/** Every type partial in two or more files: a Map from the type's name to its repository-relative files, sorted. */
export function scanPartials(root) {
  const byType = new Map();
  let dir;
  try {
    dir = join(root, 'src');
    readdirSync(dir);
  } catch {
    return byType;
  }
  for (const file of sources(dir)) {
    const rel = file.slice(root.length + 1).replaceAll('\\', '/');
    const text = blank(readFileSync(file, 'utf8'));
    for (const m of text.matchAll(DECLARATION)) {
      if (!byType.has(m[1])) byType.set(m[1], new Set());
      byType.get(m[1]).add(rel);
    }
  }
  const split = new Map();
  for (const [type, files] of byType) if (files.size > 1) split.set(type, [...files].sort());
  return split;
}

/** The breaches of `list` (type -> files) over the tree at `root`: `{ problems, found }`. */
export function checkPartials(root, list) {
  const found = scanPartials(root);
  const problems = [];
  for (const [type, files] of [...found].sort(([a], [b]) => a.localeCompare(b))) {
    const listed = list[type];
    if (!listed) {
      problems.push(`${type} is declared partial in ${files.length} hand-written files (${files.join(', ')}): partial is for generated code, so make it one class or split it into named classes`);
      continue;
    }
    const extra = files.filter((f) => !listed.includes(f));
    if (extra.length) problems.push(`${type} is split into a file beyond its listed ones (${extra.join(', ')}): a listed offender may not grow`);
    const gone = listed.filter((f) => !files.includes(f));
    if (gone.length) problems.push(`${type} no longer declares partial in ${gone.join(', ')}: cut it from the list (tools/partials.json)`);
  }
  for (const type of Object.keys(list).sort()) {
    if (!found.has(type)) problems.push(`${type} is no longer split across files: cut it from the list (tools/partials.json)`);
  }
  return { problems, found };
}

function main() {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  const listFile = join(root, 'tools', 'partials.json');
  try {
    if (process.argv.includes('--write')) {
      const sorted = Object.fromEntries([...scanPartials(root)].sort(([a], [b]) => a.localeCompare(b)));
      writeAtomic(listFile, JSON.stringify(sorted, null, 2) + '\n');
      console.log(`partials: wrote ${Object.keys(sorted).length} types to tools/partials.json`);
      return 0;
    }
    const list = JSON.parse(readFileSync(listFile, 'utf8'));
    const { problems } = checkPartials(root, list);
    for (const p of problems) console.error(`partials: ${p}`);
    if (problems.length) return 1;
    console.log(`partials: clean (${Object.keys(list).length} listed offenders)`);
    return 0;
  } catch (e) {
    console.error(`partials: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main();
