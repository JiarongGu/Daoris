import { readProjects } from './projects.mjs';

/**
 * Git reached around its port in the application and the unit-test projects: a git's start composed by hand (`GitStart(`), a read
 * of git's own files (`RefFiles.`, a `".git"` path) and the git library (`LibGit2Sharp`), counted per file. A file's count is its
 * allowance and only falls: a start or a read moves behind the git port (`IGit`, the application's runners and a test's
 * `ScriptedGit`), and the list is written again after it. A file the list does not name has no allowance. The runners' calls through
 * the port are not counted.
 */
const SITES = /\bGitStart\s*\(|\bRefFiles\.\w+|"\.git"|\bLibGit2Sharp\b/g;

/** The sites in one source's text, comments aside. */
export function gitSites(text) {
  let count = 0;
  for (const raw of text.split('\n')) {
    const line = raw.trim();
    if (line.startsWith('//') || line.startsWith('*') || line.startsWith('/*')) continue;
    count += (line.match(SITES) ?? []).length;
  }
  return count;
}

/** Each source of an application or unit-test project with its count, the files with none left out. */
export function scanGit(tree) {
  const held = readProjects(tree).filter((project) => project.role === 'application' || project.role === 'unit-tests');
  const found = {};
  for (const path of tree.files) {
    if (!path.endsWith('.cs') || /(^|\/)(bin|obj)\//.test(path)) continue;
    if (!held.some((project) => path.startsWith(`${project.folder}/`))) continue;
    const count = gitSites(tree.read(path));
    if (count > 0) found[path] = count;
  }
  return Object.fromEntries(Object.entries(found).sort(([a], [b]) => (a < b ? -1 : 1)));
}

/** The ratchet's breaches: a file over its allowance or with none, and an allowance the file fell under. */
export function checkGit(found, list) {
  const problems = [];
  for (const [path, count] of Object.entries(found)) {
    const allowed = list[path] ?? 0;
    if (count > allowed) problems.push(`${path} starts git or reads its files ${count} times, over its allowance of ${allowed}: ask git through its port (a test, ScriptedGit)`);
  }
  for (const [path, allowed] of Object.entries(list)) {
    const count = found[path] ?? 0;
    if (count < allowed) problems.push(`${path} fell to ${count} from its allowance of ${allowed}: write the list again (node tools/layers-census.mjs --write)`);
  }
  return problems;
}
