/** A gate's budget, five minutes (the run's is `verification.ts`'s): one over is said and recorded, never refused; slow tests become rows. */

import { readFileSync } from 'node:fs';
import { stepsLine, type Step } from './reached-steps.ts';

const { development } = JSON.parse(readFileSync(new URL('../dev-metrics/budgets.json', import.meta.url), 'utf8')) as {
  development: { gateMs: number };
};
export const GATE_BUDGET_MS = development.gateMs;

export type Slow = { name: string; ms: number; tests: number };
export type Timed = { name: string; ms: number; slowest?: Slow[]; steps?: readonly Step[] };

export function overBudget(gates: Timed[]): { gates: string[] } {
  return { gates: gates.filter((each) => each.ms > GATE_BUDGET_MS).map((each) => each.name) };
}

const minutes = (ms: number): string => `${Number((ms / 60_000).toFixed(1))}`;

export function budgetLines(gates: Timed[]): string[] {
  const over = overBudget(gates);
  const lines: string[] = [];
  for (const gate of gates.filter((each) => over.gates.includes(each.name))) {
    const slow = (gate.slowest ?? []).slice(0, 3).map((each) => `${each.name} ${minutes(each.ms)} min`).join(', ');
    const steps = gate.steps?.length ? `; its steps: ${stepsLine(gate.steps)}` : '';
    lines.push(`  time: ${gate.name} took ${Math.round(gate.ms / 60_000)} min, over its ${GATE_BUDGET_MS / 60_000} min budget${steps}${slow ? `; slowest: ${slow}` : ''}`);
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
