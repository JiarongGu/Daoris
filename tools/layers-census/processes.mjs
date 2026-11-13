import { readProjects } from './projects.mjs';

/**
 * The process starts in the application and the unit-test projects: `Process.Start`, `Process.GetProcessById`,
 * `Process.GetProcesses`, `Process.GetProcessesByName` and a `new Process`, counted per file. A file's count is its allowance and
 * only falls: a start moves behind the machine's ports, which a root's adapter implements (a test's, onto `ScriptedProcesses`), and
 * the list is written again after it. A file the list does not name has no allowance. A `ProcessStartInfo` is data and is not
 * counted. The compiler holds the same symbols in the layered libraries; this holds the projects that have not moved yet.
 */
const STARTS = /\bProcess\.(?:Start|GetProcessById|GetProcesses|GetProcessesByName)\s*\(|\bnew\s+Process\s*(?:\(\s*\)|\{)/g;

/** The starts in one source's text, comments aside. */
export function processSites(text) {
  let count = 0;
  for (const raw of text.split('\n')) {
    const line = raw.trim();
    if (line.startsWith('//') || line.startsWith('*') || line.startsWith('/*')) continue;
    count += (line.match(STARTS) ?? []).length;
  }
  return count;
}

/** Each source of an application or unit-test project with its count, the files with none left out. */
export function scanProcesses(tree) {
  const held = readProjects(tree).filter((project) => project.role === 'application' || project.role === 'unit-tests');
  const found = {};
  for (const path of tree.files) {
    if (!path.endsWith('.cs') || /(^|\/)(bin|obj)\//.test(path)) continue;
    if (!held.some((project) => path.startsWith(`${project.folder}/`))) continue;
    const count = processSites(tree.read(path));
    if (count > 0) found[path] = count;
  }
  return Object.fromEntries(Object.entries(found).sort(([a], [b]) => (a < b ? -1 : 1)));
}

/** The ratchet's breaches: a file over its allowance or with none, and an allowance the file fell under. */
export function checkProcesses(found, list) {
  const problems = [];
  for (const [path, count] of Object.entries(found)) {
    const allowed = list[path] ?? 0;
    if (count > allowed) problems.push(`${path} starts or finds a process ${count} times, over its allowance of ${allowed}: start through the machine's ports (a test, ScriptedProcesses)`);
  }
  for (const [path, allowed] of Object.entries(list)) {
    const count = found[path] ?? 0;
    if (count < allowed) problems.push(`${path} fell to ${count} from its allowance of ${allowed}: write the list again (node tools/layers-census.mjs --write)`);
  }
  return problems;
}
