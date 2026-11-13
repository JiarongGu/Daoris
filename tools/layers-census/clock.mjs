import { readProjects } from './projects.mjs';

/**
 * The wall clock and the real wait in the application and the unit-test projects: `DateTime.Now`, `DateTime.UtcNow`,
 * `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`, `TimeProvider.System`, `Thread.Sleep` and a `Task.Delay` that names no clock and no
 * `Timeout.Infinite`, counted per file. A file's count is its allowance and only falls: a site moves to a `TimeProvider` the root
 * hands (the application) or to `MovedClock` (a test), and the list is written again after it. A file the list does not name has no
 * allowance. The compiler holds the same symbols in the layered libraries; this holds the projects that have not moved yet.
 */
const WALL = /\b(?:DateTime(?:Offset)?\.(?:Utc)?Now|TimeProvider\.System|Thread\.Sleep\s*\()/g;
const DELAY = /\bTask\.Delay\s*\(([^\n]*)/g;
const HANDED = /Timeout\.Infinite|[Cc]lock|[Tt]ime[Pp]rovider|\bprovider\b|\btime\b|MovedClock|_time\b/;

/** The occurrences in one source's text, comments aside. */
export function clockSites(text) {
  let count = 0;
  for (const raw of text.split('\n')) {
    const line = raw.trim();
    if (line.startsWith('//') || line.startsWith('*') || line.startsWith('/*')) continue;
    count += (line.match(WALL) ?? []).length;
    for (const delay of line.matchAll(DELAY)) if (!HANDED.test(delay[1])) count += 1;
  }
  return count;
}

/** Each source of an application or unit-test project with its count, the files with none left out. */
export function scanClock(tree) {
  const held = readProjects(tree).filter((project) => project.role === 'application' || project.role === 'unit-tests');
  const found = {};
  for (const path of tree.files) {
    if (!path.endsWith('.cs') || /(^|\/)(bin|obj)\//.test(path)) continue;
    if (!held.some((project) => path.startsWith(`${project.folder}/`))) continue;
    const count = clockSites(tree.read(path));
    if (count > 0) found[path] = count;
  }
  return Object.fromEntries(Object.entries(found).sort(([a], [b]) => (a < b ? -1 : 1)));
}

/** The ratchet's breaches: a file over its allowance or with none, and an allowance the file fell under. */
export function checkClock(found, list) {
  const problems = [];
  for (const [path, count] of Object.entries(found)) {
    const allowed = list[path] ?? 0;
    if (count > allowed) problems.push(`${path} reads the wall clock or waits for real ${count} times, over its allowance of ${allowed}: take a TimeProvider the root hands (a test, MovedClock)`);
  }
  for (const [path, allowed] of Object.entries(list)) {
    const count = found[path] ?? 0;
    if (count < allowed) problems.push(`${path} fell to ${count} from its allowance of ${allowed}: write the list again (node tools/layers-census.mjs --write)`);
  }
  return problems;
}
