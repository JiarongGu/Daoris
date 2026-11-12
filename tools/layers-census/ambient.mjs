import { posix } from 'node:path';
import { readProjects } from './projects.mjs';

/**
 * The reads of the ambient ports in each application assembly's sources, counted per subdomain (its project's first folder). The
 * locator is a bridge for the static funnels, so the count is a ratchet that only tightens and no new code reads it.
 */
export function scanAmbient(tree) {
  const applications = readProjects(tree).filter((project) => project.role === 'application');
  const found = {};
  for (const path of tree.files) {
    if (!path.endsWith('.cs') || /(^|\/)(bin|obj)\//.test(path)) continue;
    const project = applications.find((each) => path.startsWith(`${each.folder}/`));
    if (!project) continue;
    const reads = (tree.read(path).match(/\bAmbientPorts\b/g) ?? []).length;
    if (reads === 0) continue;
    const parts = posix.relative(project.folder, path).split('/');
    const key = `${project.name}/${parts.length > 1 ? parts[0] : '.'}`;
    found[key] = (found[key] ?? 0) + reads;
  }
  return Object.fromEntries(Object.entries(found).sort(([a], [b]) => (a < b ? -1 : 1)));
}

/** The ratchet's breaches: a subdomain over its count, or a count the tree fell under and the list still holds. */
export function checkAmbient(found, list) {
  const problems = [];
  for (const [key, reads] of Object.entries(found)) {
    const listed = list[key] ?? 0;
    if (reads > listed) problems.push(`${key} reads the ambient ports ${reads} times, over the ratchet's ${listed}: carry the port through the part's wiring instead`);
  }
  for (const [key, listed] of Object.entries(list)) {
    const reads = found[key] ?? 0;
    if (reads < listed) problems.push(`${key} fell to ${reads} reads from the ratchet's ${listed}: write the list again (node tools/layers-census.mjs --write)`);
  }
  return problems;
}
