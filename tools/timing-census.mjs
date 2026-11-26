/**
 * No test asserts a wall-clock duration. A time is a budget, which a gate judges (the time ratchet, the layers' unit budget), not
 * an assertion: it fails on a loaded machine and passes on a free one, and what it guards is nothing a plausible change breaks.
 * A test asserts a relation (inside, wider, changed, before, at most), moves a clock it holds, or leaves the time to the ratchet.
 *
 *   node tools/timing-census.mjs     check every test (exit 0 clean, 1 a problem, 2 tool error)
 *
 * An assertion that reads a clock (`Date.now()`, `performance.now()`, a `Stopwatch`) or a value taken from one in the same test is
 * named at its line. A kept one is listed in `tools/timing-census.json` by file as `{ count, why }`: the reason a clock really is
 * what the test judges (a clock's own contract, a bound a moved clock cannot check). The list only shrinks: a file with more than
 * its count, an entry with no reason and an entry its file has fewer than are named. A net over what a clock read and an assertion
 * say in one statement, strings and comments blanked: a value kept in a field, or handed back by a helper, passes it.
 */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { listFiles } from './test-sets.mjs';
import { blanked } from './unit-pure/reads.mjs';

const HELD = [{ files: ['tools/**', 'src/**', 'examples/**'] }];
const SCRIPT_TEST = /\.(?:test\.(?:mjs|ts|tsx)|e2e\.ts|spec\.ts)$/;
const CSHARP_TEST = /(?:^|\/)(?:[\w.]+\.Tests|Daoris\.Testing)\/.*\.cs$/;

const ASSERTION = {
  script: /(?<![.\w$])(?:assert(?:\.\w+)?|expect)\s*\(/g,
  csharp: /(?<![.\w$])Assert\.\w+\s*[(<]/g,
};
/** What reads a clock as an elapsed time: a clock read less a name (less a number is a stamp for a fixture), or a stopwatch's reading. */
const ELAPSED = {
  script: /\b(?:Date|performance)\.now\(\)\s*-\s*(?![\d_.]+\b|process\.uptime)[A-Za-z_$(]|\bprocess\.hrtime\b/,
  csharp: /\bElapsedMilliseconds\b|\.Elapsed\b(?!\s*\()|\bStopwatch\.GetElapsedTime\b|\bDateTime(?:Offset)?\.(?:Utc)?Now\s*-\s*(?![\d_.]+\b|TimeSpan\.From)[A-Za-z_$(]|\bEnvironment\.TickCount\b/,
};
/** Where a test or a method begins: a value a line kept lives until the next. */
const SCOPE = {
  script: /^\s*(?:test|it)(?:\.\w+)*\s*\(/,
  csharp: /^\s*(?:\[(?:Fact|Theory)\b|(?:public|private|internal|protected)\b[^=;]*\)\s*(?:=>.*)?$)/,
};

/** The language of a test file by its path, or null for a file that is no test. */
export function languageOf(path) {
  if (SCRIPT_TEST.test(path)) return 'script';
  return CSHARP_TEST.test(path) ? 'csharp' : null;
}

/** The names a line keeps an elapsed value in: what it is assigned to, and what it is added to (`times.Add(clock.Elapsed…)`). */
function keptBy(line, elapsed) {
  const names = [];
  for (const match of line.matchAll(/([A-Za-z_$][\w$]*)\s*(?<![=!<>])=(?![=>])(?=([^;{]*))/g)) if (elapsed.test(match[2])) names.push(match[1]);
  for (const match of line.matchAll(/([A-Za-z_$][\w$]*)\.(?:Add|Enqueue|push)\s*\((?=(.*))/g)) if (elapsed.test(match[2])) names.push(match[1]);
  return names;
}

/** `text` with each template literal blanked, its lines kept, so a message that reads a clock is no assertion on one. */
const withoutTemplates = (text) => text.replace(/`(?:[^`\\]|\\.)*`/g, (each) => each.replace(/[^\n]/g, ' '));

/** Each assertion of the test `text` that reads a clock, as `{ line, text }`: `language` is `script` or `csharp`. */
export function timingAsserts(text, language) {
  const code = blanked(withoutTemplates(text));
  const elapsed = ELAPSED[language];
  const rows = code.split('\n');
  const said = text.split('\n');
  let scope = 0;
  const scopeAt = rows.map((row) => (SCOPE[language].test(row) ? ++scope : scope));
  const kept = new Map();
  rows.forEach((row, at) => {
    for (const name of keptBy(row, elapsed)) kept.set(scopeAt[at], [...(kept.get(scopeAt[at]) ?? []), name]);
  });
  const found = [];
  for (const match of code.matchAll(ASSERTION[language])) {
    const end = code.indexOf(';', match.index);
    const statement = code.slice(match.index, end < 0 ? code.length : end);
    const line = code.slice(0, match.index).split('\n').length;
    const names = kept.get(scopeAt[line - 1]) ?? [];
    const named = names.length > 0 && new RegExp(`(?<![.\\w$])(?:${names.map((name) => name.replace(/\$/g, '\\$')).join('|')})(?![\\w$])`).test(statement);
    if (elapsed.test(statement) || named) found.push({ line, text: said[line - 1].trim() });
  }
  return found;
}

const readKept = (root) => {
  try {
    return JSON.parse(readFileSync(join(root, 'tools', 'timing-census.json'), 'utf8')).kept ?? {};
  } catch {
    return {};
  }
};

const ADVICE = 'a duration is a budget the time ratchet judges: assert a relation (inside, wider, changed, before, at most) or move a clock the test holds, or list the file in tools/timing-census.json with the reason a real clock is what it judges';

/** The census over the tree at `root`: `{ problems, count }`, a problem for each timing assertion a file has beyond its kept count, and for each kept entry at fault. */
export function checkTimingTests(root) {
  const kept = readKept(root);
  const problems = [];
  let count = 0;
  const seen = new Set();
  for (const file of listFiles(root, HELD).filter((path) => languageOf(path))) {
    const found = timingAsserts(readFileSync(join(root, file), 'utf8'), languageOf(file));
    count += found.length;
    const allowed = kept[file]?.count ?? 0;
    if (kept[file]) seen.add(file);
    if (found.length === allowed) continue;
    if (found.length < allowed) {
      problems.push(`${file}: ${found.length} timing assertions, ${allowed} kept; lower the entry in tools/timing-census.json (remove it at none)`);
    } else if (allowed === 0) {
      for (const each of found) problems.push(`${file}:${each.line}: asserts a wall-clock duration (${each.text}); ${ADVICE}`);
    } else {
      problems.push(`${file}: ${found.length} timing assertions, ${allowed} kept; ${ADVICE}`);
    }
  }
  for (const [file, entry] of Object.entries(kept)) {
    if (!String(entry.why ?? '').trim()) problems.push(`tools/timing-census.json: ${file} is kept with no reason`);
    if (!seen.has(file)) problems.push(`tools/timing-census.json: ${file} is kept, and no test file of that path holds a timing assertion`);
  }
  return { problems, count };
}

function main() {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  try {
    const { problems, count } = checkTimingTests(root);
    for (const problem of problems) console.error(`timing-census: ${problem}`);
    if (problems.length) return 1;
    console.log(`timing-census: clean (${count} timing assertions, each kept with its reason)`);
    return 0;
  } catch (e) {
    console.error(`timing-census: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main();
