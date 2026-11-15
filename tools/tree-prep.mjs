/**
 * What a repository declares a prepared tree runs, read from its `daoris.gates.json`: the `regenerate` command of each gate (each
 * distinct one, in the gates' order) and the top-level `treeBaseline`, one fast gate run whose verdicts are the tree's base. A
 * builder's worktree (`tools/worktree-setup.mjs`) runs it; the driver hands a session's tree without it (`TreePreparation`).
 */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

/** The declaration at `root`; empty where the file is absent or does not read. */
export function declaredPrep(root) {
  let parsed;
  try {
    parsed = JSON.parse(readFileSync(join(root, 'daoris.gates.json'), 'utf8'));
  } catch {
    return { regenerate: [], baseline: null };
  }
  const regenerate = [];
  for (const gate of Array.isArray(parsed?.gates) ? parsed.gates : []) {
    if (typeof gate?.regenerate === 'string' && gate.regenerate.trim() && !regenerate.includes(gate.regenerate)) regenerate.push(gate.regenerate);
  }
  return { regenerate, baseline: typeof parsed?.treeBaseline === 'string' && parsed.treeBaseline.trim() ? parsed.treeBaseline : null };
}
