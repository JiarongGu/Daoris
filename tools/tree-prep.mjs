/**
 * What a repository declares a prepared tree runs, read from its `daoris.gates.json`: the `regenerate` command of each gate (each
 * distinct one, in the gates' order) and the top-level `treeBaseline`, one fast gate run whose verdicts are the tree's base. A
 * builder's worktree (`tools/worktree-setup.mjs`) runs it; the driver hands a session's tree without it (`TreePreparation`).
 */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { budgetSourceOf, keptOf } from './merge/budget-overlay.ts';

/** The declaration at `root`, its tree baseline the home's where the home keeps one; empty where the file is absent or does not read. */
export function declaredPrep(root, source = budgetSourceOf(root)) {
  let parsed;
  try {
    parsed = JSON.parse(readFileSync(join(root, 'daoris.gates.json'), 'utf8'));
  } catch {
    return { regenerate: [], baseline: null };
  }
  const kept = keptOf(source).treeBaseline;
  const regenerate = [];
  for (const gate of Array.isArray(parsed?.gates) ? parsed.gates : []) {
    if (typeof gate?.regenerate === 'string' && gate.regenerate.trim() && !regenerate.includes(gate.regenerate)) regenerate.push(gate.regenerate);
  }
  const named = typeof kept === 'string' && kept.trim() ? kept : parsed?.treeBaseline;
  return { regenerate, baseline: typeof named === 'string' && named.trim() ? named : null };
}
