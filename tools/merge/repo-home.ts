/**
 * What this repository's own tests read of the machine's check budgets: a home of their own holding the load settings the repository's
 * plan is tested against, so a tree whose home keeps no record of it (a workflow's, a fresh machine's) reads the same plan as the owner's.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import type { BudgetSource } from './budget-overlay.ts';
import { budgetsFile, type Budgets } from './check-budgets.ts';
import { scratchFolder } from './fixture-home.ts';

const kept: Budgets = {
  schema: 1,
  repositories: {
    Daoris: {
      gates: { censuses: { alone: true }, reached: { memoryGb: 6 }, 'tool-e2e': { memoryGb: 6 } },
      sets: { 'merge-e2e': { timeoutMs: 120_000 }, 'merge-unit': { timeoutMs: 10_000 } },
      treeBaseline: 'node tools/quick.mjs --record-base',
    },
  },
};

let source: BudgetSource | null = null;

/** The home and repository name the repository's own tests read; the home is made on first use and removed when the process ends. */
export function repoBudgetSource(): BudgetSource {
  if (source) return source;
  const home = scratchFolder('repo-home');
  const file = budgetsFile(home);
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, `${JSON.stringify(kept, null, 2)}\n`);
  source = { home, repository: 'Daoris' };
  return source;
}
