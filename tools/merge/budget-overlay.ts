/**
 * The home's learned values laid over a declaration's gates, and the other direction: a run's green times taught back.
 * A gate's `timeoutMinutes`, `memoryGb` and `alone` are the home's where it has them (`check-budgets.ts`): a pin, else what the gate's
 * green times teach; where the home has nothing, the member an older declaration still holds is the gate's, and where there is none the
 * lane's default applies (`timeout.ts`). The member an older declaration holds is read until no repository the home knows holds one.
 */
import { spawnSync } from 'node:child_process';
import { basename, dirname, resolve } from 'node:path';
import { daorisHome } from '../../src/Daoris.Cli/src/platform/home.ts';
import { budgetOf, readBudgets, recordGreen, saidRise, type Budget } from './check-budgets.ts';

/** Where a run reads its repository's budgets: the Daoris home, and the repository's name in it. */
export type BudgetSource = { home: string | null; repository: string };

type Budgeted = { name: string; timeoutMinutes?: number; memoryGb?: number; alone?: boolean; budget?: Budget };

/**
 * The home and the repository a run in `root` reads. The repository is the driver's `DAORIS_REPOSITORY` for a session it started, else the
 * folder of the main checkout that `root` is a tree of; the home is `DAORIS_HOME`, none where there is none.
 */
export function budgetSourceOf(root: string, env: Record<string, string | undefined> = process.env): BudgetSource {
  return { home: daorisHome(env, null), repository: env['DAORIS_REPOSITORY']?.trim() || mainCheckoutName(root) };
}

/** The name of the folder holding the repository's main checkout: a linked tree's git folder is inside it. */
function mainCheckoutName(root: string): string {
  const common = spawnSync('git', ['rev-parse', '--path-format=absolute', '--git-common-dir'], { cwd: root, encoding: 'utf8', windowsHide: true });
  const folder = common.status === 0 ? dirname(resolve(common.stdout.trim())) : resolve(root);
  return basename(folder);
}

/** The gates with the home's values laid over: a pin or a learned timeout, and the memory and load settings. Answers the gates themselves where `source` names no home. */
export function overlay<T extends Budgeted>(gates: readonly T[], source: BudgetSource | null): T[] {
  if (!source?.home) return [...gates];
  const kept = readBudgets(source.home).repositories[source.repository]?.gates ?? {};
  return gates.map((gate) => {
    const own = kept[gate.name];
    if (!own) return gate;
    const budget = budgetOf(own, 'merge');
    const next: T = { ...gate };
    if (budget.source !== 'default') { next.timeoutMinutes = budget.minutes; next.budget = budget; }
    if (typeof own.memoryGb === 'number') next.memoryGb = own.memoryGb;
    if (own.alone === true) next.alone = true;
    return next;
  });
}

/** The lines saying each gate whose learned timeout rose past the bound, each said once: it is marked in the home as it is returned. */
export function riseLines(gates: readonly Budgeted[], source: BudgetSource | null): string[] {
  const lines: string[] = [];
  for (const gate of gates) {
    const rise = gate.budget?.rise;
    if (!rise) continue;
    lines.push(`  budget: ${gate.name} runs slower: its learned timeout rose from ${rise.from} to ${rise.to} min (\`daoris settings checks\` pins one)`);
    saidRise(source?.home, source?.repository ?? '', gate.name, rise.to);
  }
  return lines;
}

/**
 * What a gate's run teaches: its wall time, when it ended green, ran whole (not a scoped run's part) and replayed no standing pass.
 * Answers whether it is kept.
 */
export function learnGreen(
  source: BudgetSource | null, gate: string, result: { verdict: string; ms?: number; replayed?: unknown; classReplay?: unknown }, whole: boolean, at: string,
): boolean {
  if (!source?.home || !whole || result.verdict !== 'PASS' || result.replayed || result.classReplay || !(typeof result.ms === 'number' && result.ms > 0)) return false;
  recordGreen(source.home, source.repository, gate, result.ms, at);
  return true;
}

/** The run's budget source, after saying each rise in `gates`: a run's start. */
export function saidBudgets(root: string, gates: readonly Budgeted[], say: (line: string) => void = console.log): BudgetSource {
  const source = budgetSourceOf(root);
  for (const line of riseLines(gates, source)) say(line);
  return source;
}
