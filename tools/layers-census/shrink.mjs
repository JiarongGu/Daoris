import { readFileSync } from 'node:fs';
import { writeAtomic } from '../fsx.mjs';
import { scanAmbient } from './ambient.mjs';
import { scanBoundary } from './boundary.mjs';
import { scanClock } from './clock.mjs';
import { scanGit } from './git.mjs';
import { packagesOutside } from './references.mjs';
import { scanProcesses } from './processes.mjs';

/**
 * The list's allowances lowered to what the tree reads and nothing else (`node tools/layers-census.mjs --shrink`, a `generated` entry of
 * the merge and the commit hook's writer): a count that fell takes the tree's, one on nothing leaves, a package no longer referenced
 * leaves its project's entry. A count the tree is over, a key the list lacks and `slowTests` (this machine's timings) are never written,
 * so a rise stays a breach for a person and `--write` stays the only way to fill the rest.
 */
export function shrunk(list, tree) {
  const lowered = [];
  const out = { ...list };

  const counts = (section, listed, found, label = (key) => `${section} ${key}`) => {
    const kept = {};
    for (const [key, allowed] of Object.entries(listed ?? {})) {
      const reads = found[key] ?? 0;
      if (reads >= allowed) kept[key] = allowed;
      else if (reads === 0) lowered.push(`${label(key)}: ${allowed} to none`);
      else {
        kept[key] = reads;
        lowered.push(`${label(key)}: ${allowed} to ${reads}`);
      }
    }
    return kept;
  };

  if (list.packages) {
    const found = packagesOutside(tree);
    out.packages = {};
    for (const [project, names] of Object.entries(list.packages)) {
      if (!found[project] && !tree.files.some((path) => path.endsWith(`/${project}.csproj`))) {
        lowered.push(`packages ${project}: no longer a project`);
        continue;
      }
      const kept = names.filter((name) => (found[project] ?? []).includes(name));
      for (const name of names.filter((each) => !kept.includes(each))) lowered.push(`packages ${project}: ${name} is no longer referenced`);
      if (kept.length) out.packages[project] = kept;
    }
  }
  if (list.ambient) out.ambient = counts('ambient', list.ambient, scanAmbient(tree));
  if (list.clock) out.clock = counts('clock', list.clock, scanClock(tree));
  if (list.processes) out.processes = counts('processes', list.processes, scanProcesses(tree));
  if (list.git) out.git = counts('git', list.git, scanGit(tree));
  if (list.boundaries) {
    out.boundaries = {};
    for (const [id, held] of Object.entries(list.boundaries)) {
      let found;
      try {
        found = scanBoundary(tree, id);
      } catch {
        out.boundaries[id] = held;
        continue;
      }
      const lane = { ...held };
      for (const way of ['inbound', 'outbound']) {
        if (held[way]) lane[way] = counts('boundaries', held[way], found[way], (name) => `boundaries ${id} ${way} ${name}`);
      }
      out.boundaries[id] = lane;
    }
  }
  return { list: out, lowered };
}

/**
 * The list file at `file` lowered to `tree` and written back where an allowance fell, holding the sections the file already held and
 * their order (a shrink adds no key); the lines that say each, none where nothing fell and the file is untouched.
 */
export function shrinkFile(file, tree, empty = {}) {
  const before = JSON.parse(readFileSync(file, 'utf8'));
  const { list, lowered } = shrunk({ ...empty, ...before }, tree);
  if (lowered.length) writeAtomic(file, `${JSON.stringify(Object.fromEntries(Object.keys(before).map((key) => [key, list[key]])), null, 2)}\n`);
  return lowered;
}
