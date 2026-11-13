/**
 * The time budget a merge is read against: a gate under five minutes, the merge under ten. A gate over it is said in the
 * summary and recorded with its slowest classes, never refused: the slow tests become rows.
 */

import { readFileSync } from 'node:fs';

const { development } = JSON.parse(readFileSync(new URL('../dev-metrics/budgets.json', import.meta.url), 'utf8')) as {
  development: { gateMs: number; mergeWallMs: number };
};
export const GATE_BUDGET_MS = development.gateMs;
export const MERGE_BUDGET_MS = development.mergeWallMs;

export type Slow = { name: string; ms: number; tests: number };
export type Timed = { name: string; ms: number; slowest?: Slow[] };

export function overBudget(gates: Timed[], mergeMs: number): { gates: string[]; merge: boolean } {
  return { gates: gates.filter((each) => each.ms > GATE_BUDGET_MS).map((each) => each.name), merge: mergeMs > MERGE_BUDGET_MS };
}

const minutes = (ms: number): string => `${Number((ms / 60_000).toFixed(1))}`;

export function budgetLines(gates: Timed[], mergeMs: number): string[] {
  const over = overBudget(gates, mergeMs);
  const lines: string[] = [];
  if (over.merge) lines.push(`  time: the merge took ${Math.round(mergeMs / 60_000)} min, over its ${MERGE_BUDGET_MS / 60_000} min budget; the merge stands, and its slow gates are rows`);
  for (const gate of gates.filter((each) => over.gates.includes(each.name))) {
    const slow = (gate.slowest ?? []).slice(0, 3).map((each) => `${each.name} ${minutes(each.ms)} min`).join(', ');
    lines.push(`  time: ${gate.name} took ${Math.round(gate.ms / 60_000)} min, over its ${GATE_BUDGET_MS / 60_000} min budget${slow ? `; slowest: ${slow}` : ''}`);
  }
  return lines;
}

/**
 * The command with a trx logger on each `dotnet test` segment, so the gate's classes' times are read once it ends. A command
 * that logs already, or runs no dotnet test, is left as it is.
 */
export function loggedRun(run: string, dir: string, gate: string): { run: string; files: string[] } {
  if (/--logger\b/.test(run)) return { run, files: [] };
  const files: string[] = [];
  const segments = run.split(' && ').map((segment) => {
    if (!/^\s*dotnet test\b/.test(segment)) return segment;
    const file = `${dir}/${gate}.logged-${files.length + 1}.trx`;
    files.push(file);
    return `${segment.trim()} --logger "trx;LogFileName=${file}"`;
  });
  return { run: segments.join(' && '), files };
}
