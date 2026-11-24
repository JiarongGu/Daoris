/**
 * The home's learned values laid over a declaration's gates, and the other direction: a run's green times taught back.
 * A gate's `timeoutMinutes`, `memoryGb` and `alone` are the home's where it has them (`check-budgets.ts`): a pin, else what the gate's
 * green times teach; where the home has nothing, the member an older declaration still holds is the gate's, and where there is none the
 * lane's default applies (`timeout.ts`). The member an older declaration holds is read until no repository the home knows holds one.
 */
import { spawnSync } from 'node:child_process';
import { basename, dirname, resolve } from 'node:path';
import { daorisHome } from '../../src/Daoris.Cli/src/platform/home.ts';
import { budgetOf, readBudgets, recordGreen, saidRise, type Budget, type RepositoryKept } from './check-budgets.ts';

/** Where a run reads its repository's budgets: the Daoris home, and the repository's name in it. */
export type BudgetSource = { home: string | null; repository: string };

type Budgeted = { name: string; timeoutMinutes?: number; memoryGb?: number; alone?: boolean; budget?: Budget };

/**
 * The home and the repository a run in `root` reads: the Daoris home (`DAORIS_HOME`, none where there is none), and the name of the folder of the
 * main checkout `root` is a tree of. A folder that is no repository's own top (a fixture inside another repository) is named by itself, so what
 * a test builds there never reads the record of the repository around it.
 */
export function budgetSourceOf(root: string, env: Record<string, string | undefined> = process.env): BudgetSource {
  return { home: daorisHome(env, null), repository: mainCheckoutName(root) };
}

const gitAt = (root: string, args: string[]): string | null => {
  const run = spawnSync('git', args, { cwd: root, encoding: 'utf8', windowsHide: true });
  return run.status === 0 ? run.stdout.trim() : null;
};

/** The name of the folder holding the repository's main checkout: a linked tree's git folder is inside it. */
function mainCheckoutName(root: string): string {
  const top = gitAt(root, ['rev-parse', '--show-toplevel']);
  const common = gitAt(root, ['rev-parse', '--path-format=absolute', '--git-common-dir']);
  if (top === null || common === null || resolve(top) !== resolve(root)) return basename(resolve(root));
  return basename(dirname(resolve(common)));
}

/** What the home keeps of the repository, nothing where it names no home or keeps no record. */
export function keptOf(source: BudgetSource | null): RepositoryKept {
  return source?.home ? readBudgets(source.home).repositories[source.repository] ?? {} : {};
}

/** The gates with the home's values laid over: a pin or a learned timeout, and the memory and load settings. Answers the gates themselves where `source` names no home. */
export function overlay<T extends { name: string }>(gates: readonly T[], source: BudgetSource | null): (T & Budgeted)[] {
  if (!source?.home) return [...gates];
  const kept = readBudgets(source.home).repositories[source.repository]?.gates ?? {};
  return gates.map((gate) => {
    const own = kept[gate.name];
    if (!own) return gate;
    const budget = budgetOf(own, 'merge');
    const next: T & Budgeted = { ...gate };
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
